using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== AI Combat ====================

    public async Task<AICombatSuggestion> GetAITacticalSuggestionsAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var suggestions = new AICombatSuggestion();

        // Analyze combat state
        var livingParticipants = combat.Participants.Where(p => p.CurrentHP > 0).ToList();
        var deadParticipants = combat.Participants.Where(p => p.CurrentHP <= 0).ToList();
        var lowHPParticipants = livingParticipants.Where(p => p.CurrentHP < p.MaxHP * 0.3).ToList();
        var highThreatNPCs = combat.Participants.Where(p =>
            p.ParticipantType == "NPC" && p.CurrentHP > p.MaxHP * 0.5).ToList();

        // Determine threat level
        int npcCount = combat.Participants.Count(p => p.ParticipantType == "NPC" && p.CurrentHP > 0);
        int playerCount = combat.Participants.Count(p => p.ParticipantType == "Player" && p.CurrentHP > 0);

        if (npcCount >= playerCount * 2)
            suggestions.ThreatLevel = "Extreme";
        else if (npcCount >= playerCount)
            suggestions.ThreatLevel = "High";
        else if (npcCount > playerCount / 2)
            suggestions.ThreatLevel = "Medium";
        else
            suggestions.ThreatLevel = "Low";

        // Generate tactical suggestions for living players
        foreach (var player in livingParticipants.Where(p => p.ParticipantType == "Player"))
        {
            // Suggest attacking lowest HP enemy
            var lowestHPEnemy = highThreatNPCs.OrderBy(p => p.CurrentHP).FirstOrDefault();
            if (lowestHPEnemy != null)
            {
                suggestions.Suggestions.Add(new AITacticalAction
                {
                    Actor = player.DisplayName,
                    Action = "Attack",
                    Target = lowestHPEnemy.DisplayName,
                    Reason = $"Lowest HP target ({lowestHPEnemy.CurrentHP}/{lowestHPEnemy.MaxHP} HP)",
                    Priority = 1
                });
            }

            // Suggest healing if low HP
            if (lowHPParticipants.Contains(player))
            {
                suggestions.Suggestions.Add(new AITacticalAction
                {
                    Actor = player.DisplayName,
                    Action = "Use Healing",
                    Target = player.DisplayName,
                    Reason = $"Low HP ({player.CurrentHP}/{player.MaxHP}) - needs healing",
                    Priority = 2
                });
            }

            // Suggest using abilities/spells
            suggestions.Suggestions.Add(new AITacticalAction
            {
                Actor = player.DisplayName,
                Action = "Use Ability/Spell",
                Target = "",
                Reason = "Consider using class abilities or spells",
                Priority = 3
            });
        }

        // Generate NPC behavior suggestions
        foreach (var npc in highThreatNPCs)
        {
            // Find closest living player
            var closestPlayer = livingParticipants
                .Where(p => p.ParticipantType == "Player")
                .FirstOrDefault();

            if (closestPlayer != null)
            {
                suggestions.NPCActions.Add(new AINPCAction
                {
                    NPCName = npc.DisplayName,
                    Behavior = npc.CurrentHP < npc.MaxHP * 0.3 ? "Fleeing" : "Aggressive",
                    Target = closestPlayer.DisplayName,
                    Action = npc.CurrentHP < npc.MaxHP * 0.3 ? "Retreat" : "Attack",
                    Reason = npc.CurrentHP < npc.MaxHP * 0.3
                        ? "Low HP - should flee"
                        : "Primary target available"
                });
            }
        }

        // Generate warnings
        if (lowHPParticipants.Any())
        {
            foreach (var lowHP in lowHPParticipants)
            {
                if (lowHP.CurrentHP < lowHP.MaxHP * 0.2)
                {
                    suggestions.Warnings.Add(new AICombatWarning
                    {
                        Message = $"{lowHP.DisplayName} is critically low on HP ({lowHP.CurrentHP}/{lowHP.MaxHP})",
                        Severity = "High",
                        AffectedParticipant = lowHP.DisplayName
                    });
                }
                else
                {
                    suggestions.Warnings.Add(new AICombatWarning
                    {
                        Message = $"{lowHP.DisplayName} is low on HP ({lowHP.CurrentHP}/{lowHP.MaxHP})",
                        Severity = "Medium",
                        AffectedParticipant = lowHP.DisplayName
                    });
                }
            }
        }

        if (deadParticipants.Any(p => p.ParticipantType == "Player"))
        {
            foreach (var dead in deadParticipants.Where(p => p.ParticipantType == "Player"))
            {
                suggestions.Warnings.Add(new AICombatWarning
                {
                    Message = $"{dead.DisplayName} is down and needs attention!",
                    Severity = "High",
                    AffectedParticipant = dead.DisplayName
                });
            }
        }

        // Recommended strategy
        if (suggestions.ThreatLevel == "Extreme")
            suggestions.RecommendedStrategy = "Consider retreating or using powerful abilities. Focus on healing and defense.";
        else if (suggestions.ThreatLevel == "High")
            suggestions.RecommendedStrategy = "Stay alert. Prioritize healing low HP allies and focus fire on weak enemies.";
        else if (suggestions.ThreatLevel == "Medium")
            suggestions.RecommendedStrategy = "Balanced approach. Use abilities wisely and maintain formation.";
        else
            suggestions.RecommendedStrategy = "Victory seems likely. Push forward but stay cautious.";

        return suggestions;
    }

    public async Task<AICombatSuggestion> GetAINPCBehaviorAsync(Guid combatId, Guid npcId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var npc = combat.Participants.FirstOrDefault(p => p.Id == npcId)
            ?? throw new KeyNotFoundException($"NPC {npcId} not found in combat.");

        var suggestions = new AICombatSuggestion();

        // Determine NPC behavior based on HP and situation
        string behavior;
        string targetAction;
        string reason;

        if (npc.CurrentHP <= 0)
        {
            behavior = "Dead";
            targetAction = "None";
            reason = "NPC is downed";
        }
        else if (npc.CurrentHP < npc.MaxHP * 0.3)
        {
            behavior = "Fleeing";
            targetAction = "Retreat";
            reason = "Low HP - should flee from combat";
        }
        else if (npc.CurrentHP < npc.MaxHP * 0.6)
        {
            behavior = "Cautious";
            targetAction = "Defensive";
            reason = "Moderate damage - use caution";
        }
        else
        {
            behavior = "Aggressive";
            targetAction = "Attack";
            reason = "Full HP - aggressive stance";
        }

        // Find target
        var livingPlayers = combat.Participants
            .Where(p => p.ParticipantType == "Player" && p.CurrentHP > 0)
            .ToList();

        string target = livingPlayers.Any() ? livingPlayers.First().DisplayName : "None";

        suggestions.NPCActions.Add(new AINPCAction
        {
            NPCName = npc.DisplayName,
            Behavior = behavior,
            Target = target,
            Action = targetAction,
            Reason = reason
        });

        suggestions.ThreatLevel = npc.CurrentHP >= npc.MaxHP * 0.6 ? "High" : "Medium";

        return suggestions;
    }

    public async Task<Combat> AutoResolveCombatAsync(Guid combatId, string resolutionMode = "quick")
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var players = combat.Participants.Where(p => p.ParticipantType == "Player" && p.CurrentHP > 0).ToList();
        var npcs = combat.Participants.Where(p => p.ParticipantType == "NPC" && p.CurrentHP > 0).ToList();

        if (resolutionMode == "quick")
        {
            // Quick resolution: compare total HP and initiative
            int totalPlayerHP = players.Sum(p => p.CurrentHP);
            int totalNPCsHP = npcs.Sum(p => p.CurrentHP);
            int totalPlayerInit = players.Sum(p => p.Initiative);
            int totalNPCsInit = npcs.Sum(p => p.Initiative);

            string result;
            if (totalPlayerHP > totalNPCsHP && totalPlayerInit > totalNPCsInit)
            {
                result = "Players win by overwhelming force";
                foreach (var npc in npcs)
                {
                    npc.CurrentHP = 0;
                    await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                        CombatEventType.Death, "System", npc.DisplayName,
                        "Defeated in quick resolution.");
                }
            }
            else if (totalNPCsHP > totalPlayerHP * 1.5)
            {
                result = "NPCs win by overwhelming force";
                foreach (var player in players)
                {
                    player.CurrentHP = 0;
                    await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                        CombatEventType.Death, "System", player.DisplayName,
                        "Defeated in quick resolution.");
                }
            }
            else
            {
                result = "Combat is evenly matched - GM should resolve manually";
            }

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.CombatEnd, "System", "System",
                $"Quick resolution: {result}");
        }
        else
        {
            // Dramatic resolution: simulate a few rounds
            for (int round = 0; round < 3; round++)
            {
                // Players attack
                foreach (var player in players.Where(p => p.CurrentHP > 0))
                {
                    var target = npcs.FirstOrDefault(n => n.CurrentHP > 0);
                    if (target != null)
                    {
                        var dmg = _diceEngine.Roll("2d6");
                        target.CurrentHP = Math.Max(0, target.CurrentHP - dmg.Total);
                        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                            CombatEventType.Damage, player.DisplayName, target.DisplayName,
                            $"Round {round + 1}: {player.DisplayName} attacks {target.DisplayName} for {dmg.Total} damage.");
                    }
                }

                // NPCs attack
                foreach (var npc in npcs.Where(n => n.CurrentHP > 0))
                {
                    var target = players.FirstOrDefault(p => p.CurrentHP > 0);
                    if (target != null)
                    {
                        var dmg = _diceEngine.Roll("1d8");
                        target.CurrentHP = Math.Max(0, target.CurrentHP - dmg.Total);
                        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                            CombatEventType.Damage, npc.DisplayName, target.DisplayName,
                            $"Round {round + 1}: {npc.DisplayName} attacks {target.DisplayName} for {dmg.Total} damage.");
                    }
                }
            }

            var playersAlive = players.Count(p => p.CurrentHP > 0);
            var npcsAlive = npcs.Count(n => n.CurrentHP > 0);

            string result;
            if (playersAlive == 0)
                result = "Players were defeated";
            else if (npcsAlive == 0)
                result = "All NPCs were defeated";
            else
                result = $"Combat ended with {playersAlive} player(s) and {npcsAlive} NPC(s) remaining";

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.CombatEnd, "System", "System",
                $"Dramatic resolution: {result}");
        }

        // Check if combat should end
        var livingPlayers = combat.Participants.Count(p => p.ParticipantType == "Player" && p.CurrentHP > 0);
        var livingNPCs = combat.Participants.Count(p => p.ParticipantType == "NPC" && p.CurrentHP > 0);

        if (livingPlayers == 0 || livingNPCs == 0)
        {
            await EndCombatAsync(combatId, "Combat resolved by auto-resolution");
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

}
