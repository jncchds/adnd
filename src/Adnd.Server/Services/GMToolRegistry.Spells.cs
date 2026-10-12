using System.Text.Json;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services;

/// <summary>
/// A player character's spell, resolved in one call the way <c>attack</c> resolves a blow.
/// castSpell used to spend the slot and nothing else, leaving the model to chain a roll, a
/// damage call per target and nextTurn — chosen blind, so Burning Hands asked the player
/// whether they wanted to roll damage, gave nobody a saving throw and burned everything.
/// </summary>
public partial class GMToolRegistry
{
    private const string CastSpell = "castSpell";

    /// <summary>Someone a spell lands on: a combatant in the fight, or a player character's sheet outside one.</summary>
    private sealed record SpellTarget(string Name, CombatParticipant? Participant, Character? Sheet);

    private sealed record SpellCall(Character Caster, CombatEntity? Fight, CombatParticipant? CasterInFight, string Spell, int Level,
        List<SpellTarget> Targets, List<string> Notes, bool AttackRoll, string? Save, int SaveBonus, string? Damage, string? Kind,
        bool HalfOnSave, string? Heal, string? Condition, bool EndTurn, bool Reaction);

    /// <summary>The spell as the model asked for it, checked against the sheet and the fight —
    /// or what to tell the model instead. Changes nothing, so it can be asked at every stage
    /// of a player's roll.</summary>
    private async Task<(SpellCall? Spell, string? Problem)> ReadSpellAsync(Guid gameId, JsonElement arguments, CancellationToken ct)
    {
        var name = ReadOptionalString(arguments, "character");
        var spell = ReadOptionalString(arguments, "spell") ?? "a spell";
        if (name is null) return (null, "Missing character");
        if (!TryGetInt(arguments, "level", out var level) || level < 0 || level > 9)
            return (null, "level must be the spell's level: 0 for a cantrip, 1-9 otherwise.");

        var caster = await characters.FindAsync(gameId, name, ct);
        if (caster is null)
            return (null, $"No player character called \"{name}\". The party: {await characters.PartyNamesAsync(gameId, ct)}.");
        if (CharacterStateService.IsDead(caster)) return (null, $"{caster.Name} is dead and casts nothing.");
        if (level > 0 && !ClassRules.RefitSlots(caster.SpellSlots, caster.Class, caster.Level)
                .Any(kv => int.Parse(kv.Key) >= level && kv.Value.Remaining > 0))
            return (null, $"{caster.Name} has no spell slot of level {level} or higher left — {spell} cannot be cast. Say so and let their player choose something else.");

        // A fight the caster is not part of is none of this spell's business.
        var fight = await combats.GetActiveAsync(gameId, ct);
        var own = fight?.Participants.FirstOrDefault(p => p.CharacterId == caster.Id);
        if (own is null) fight = null;
        var reaction = ReadFlag(arguments, "reaction") == true;
        if (fight is not null && own is not null)
        {
            if (own.HP == 0) return (null, $"{caster.Name} is down and cannot cast.");
            if (!reaction && combats.Acting(fight) is { } acting && acting != own)
                return (null, $"It is {acting.DisplayName}'s turn, not {caster.Name}'s. (A spell cast outside the caster's own turn needs \"reaction\": true.)");
        }

        var named = ReadStrings(arguments, "targets") ?? [];
        if (ReadOptionalString(arguments, "target") is { } single) named.Add(single);
        var targets = new List<SpellTarget>();
        var notes = new List<string>();
        foreach (var targetName in named.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (fight is not null && combats.FindByName(fight, targetName) is { } p)
            {
                if (p.HP == 0 && p.ParticipantType != CombatParticipantType.Character)
                    notes.Add($"{p.DisplayName} is already defeated and is left out.");
                else
                    targets.Add(new SpellTarget(p.DisplayName, p, null));
            }
            else if (await characters.FindAsync(gameId, targetName, ct) is { } sheet)
                targets.Add(new SpellTarget(sheet.Name, null, sheet));
            else if (fight is not null)
                return (null, $"No combatant called \"{targetName}\". Combatants: {string.Join(", ", fight.Participants.Select(x => x.DisplayName))}.");
            else
                notes.Add($"{targetName} is not a player character, so nothing is tracked for them here — narrate what the spell does to them.");
        }

        var attackRoll = ReadFlag(arguments, "attackRoll") == true;
        var save = ReadOptionalString(arguments, "save")?.Trim().ToUpperInvariant();
        if (save is { Length: > 3 }) save = save[..3];
        TryGetInt(arguments, "saveBonus", out var saveBonus);
        var damage = ReadOptionalString(arguments, "damage");
        var heal = ReadOptionalString(arguments, "heal");
        var condition = ReadOptionalString(arguments, "condition");

        foreach (var formula in new[] { damage, heal })
        {
            if (formula is null) continue;
            try { diceEngine.Roll(formula); }
            catch (Exception ex) { return (null, $"\"{formula}\" is not a formula the dice engine can roll ({ex.Message}). Use e.g. 3d6 or 1d8+3."); }
        }

        if (attackRoll)
        {
            if (fight is null) return (null, "A spell attack is rolled against a combatant in a fight. Outside one, use requestPlayerRoll for the roll.");
            if (targets.Count != 1) return (null, "A spell attack has exactly one target still standing: name it in \"targets\".");
            if (damage is null) return (null, "Missing damage: the spell's damage formula, e.g. 1d10.");
        }

        var hasEffect = attackRoll || save is not null || damage is not null || heal is not null || condition is not null;
        if (hasEffect && targets.Count == 0 && fight is not null)
            return (null, notes.Count > 0 ? $"{string.Join(" ", notes)} Pick a target still standing." : "Name who the spell affects in \"targets\".");

        return (new SpellCall(caster, fight, own, spell, level, targets, notes, attackRoll, save, saveBonus, damage,
            ReadOptionalString(arguments, "type"), ReadFlag(arguments, "halfOnSave") == true, heal, condition,
            fight is not null && !reaction && ReadFlag(arguments, "endTurn") != false, reaction), null);
    }

    /// <summary>
    /// Spends the slot and lands the spell: the saves, the damage, the healing, the condition,
    /// one line in the log, and the end of the caster's turn. <paramref name="total"/> and
    /// <paramref name="naturalD20"/> are the caster's spell attack, already rolled as their
    /// player's roll; null for a spell that makes none.
    /// </summary>
    private Task<string> ResolveSpellAsync(Guid gameId, Guid sessionId, SpellCall s, int? total, int? naturalD20, CancellationToken ct)
        => CombatToolAsync(async () =>
        {
            var caster = s.Caster;
            int? spent = null;
            if (s.Level > 0)
            {
                spent = await characters.SpendSpellSlotAsync(caster, s.Level, ct);
                if (spent is null) return TurnLog.Refused + $"{caster.Name} has no spell slot of level {s.Level} or higher left — {s.Spell} cannot be cast.";
            }

            var kind = s.Kind is null ? "" : $" {s.Kind}";
            var head = $"✨ {caster.Name} casts {s.Spell}" + (spent > s.Level ? $" with a level {spent} slot" : "");
            var outcomes = new List<string>();

            if (s.AttackRoll)
            {
                var target = s.Targets[0];
                var ac = target.Participant?.AC ?? target.Sheet!.ArmourClass;
                var critical = naturalD20 == 20;
                if (naturalD20 == 1 || (!critical && total < ac))
                {
                    outcomes.Add($"it misses {target.Name}");
                }
                else
                {
                    var roll = diceEngine.Roll(critical ? DoubleDice(s.Damage!) : s.Damage!);
                    var effects = new List<string> { $"{roll.Breakdown}{kind} damage", await ApplySpellHpAsync(s, target, -Math.Max(0, roll.Total), critical, ct) };
                    if (s.Condition is not null) effects.Add(await ApplySpellConditionAsync(s, target, ct));
                    outcomes.Add($"{(critical ? "a critical hit on" : "it hits")} {target.Name}: {string.Join(", ", effects)}");
                }
            }
            else
            {
                // An area's damage is rolled once and everyone saves against the same number.
                var damageRoll = s.Damage is null ? null : diceEngine.Roll(s.Damage);
                var dc = ClassRules.SpellSaveDc(caster.Class, caster.Level, caster.Attributes);
                if (s.Save is not null) head += $" (DC {dc} {s.Save} save)";
                if (damageRoll is not null) head += $", {damageRoll.Breakdown}{kind} damage";

                foreach (var target in s.Targets)
                {
                    var effects = new List<string>();
                    var failed = true;
                    if (s.Save is not null)
                    {
                        var saveRoll = diceEngine.Roll($"1d20{await SaveBonusAsync(s, target, ct):+0;-0}");
                        failed = saveRoll.Total < dc;
                        effects.Add($"saves with {saveRoll.Total} — {(failed ? "fails" : "succeeds")}");
                    }
                    if (damageRoll is not null)
                    {
                        var amount = Math.Max(0, failed ? damageRoll.Total : s.HalfOnSave ? damageRoll.Total / 2 : 0);
                        effects.Add(amount == 0 ? "no damage" : $"{amount} damage, {await ApplySpellHpAsync(s, target, -amount, false, ct)}");
                    }
                    if (s.Heal is not null)
                    {
                        var healRoll = diceEngine.Roll(s.Heal);
                        effects.Add($"healed {healRoll.Breakdown}, {await ApplySpellHpAsync(s, target, Math.Max(0, healRoll.Total), false, ct)}");
                    }
                    if (s.Condition is not null && failed) effects.Add(await ApplySpellConditionAsync(s, target, ct));
                    if (effects.Count > 0) outcomes.Add($"{target.Name}: {string.Join(", ", effects)}");
                }
            }

            var line = outcomes.Count == 0 ? $"{head}." : $"{head} — {string.Join("; ", outcomes)}.";
            if (s.Fight is { } fight)
                await BroadcastRollAsync(gameId, fight.SessionId, line, "Combat", new { combatId = fight.Id }, ct);
            else
                await characters.AnnounceAsync(gameId, sessionId, line, ct);

            var slots = ClassRules.ReadSlots(caster.SpellSlots);
            var remaining = string.Join(", ", slots.OrderBy(kv => int.Parse(kv.Key)).Select(kv => $"L{kv.Key} {kv.Value.Remaining}/{kv.Value.Total}"));
            var result = line
                + (s.Notes.Count > 0 ? " " + string.Join(" ", s.Notes) : "")
                + (s.Level > 0 ? $" Slots left: {remaining}." : "")
                + (s.Targets.Count > 0 ? " Hit points and conditions are already updated — do not call damage, heal or setCondition for this spell." : "");

            if (s.Fight is not { } combat || s.CasterInFight is not { } own) return result;
            if (combats.FightOver(combat) is { } over) return $"{result} {over}";
            if (!s.EndTurn || combats.Acting(combat) != own)
                return s.Reaction
                    ? $"{result} That was a reaction; the turn has not changed."
                    : $"{result} It is still {caster.Name}'s turn: finish it, then call nextTurn.";

            var next = await combats.AdvanceTurnAsync(combat, ct);
            return $"{result} {caster.Name}'s turn is over." + (next is null ? "" : $" {DescribeTurn(combat, next)}");
        });

    /// <summary>A player character saves with their own ability modifier; for anyone else the
    /// GM supplies the bonus, since the engine keeps no ability scores for an enemy.</summary>
    private async Task<int> SaveBonusAsync(SpellCall s, SpellTarget target, CancellationToken ct)
    {
        var sheet = target.Sheet;
        if (sheet is null && target.Participant?.CharacterId is { } characterId)
            sheet = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct);
        return sheet is null ? s.SaveBonus : ClassRules.Modifier(sheet.Attributes, s.Save!);
    }

    /// <summary>Changes the target's hit points and says where that leaves them.</summary>
    private async Task<string> ApplySpellHpAsync(SpellCall s, SpellTarget target, int signed, bool critical, CancellationToken ct)
    {
        if (target.Participant is { } p)
        {
            if (signed > 0 && combats.IsDead(p)) return "the dead are beyond healing";
            var hp = await combats.ChangeHpAsync(s.Fight!, p.Id, signed, ct, critical);
            return hp > 0 ? $"{hp}/{p.MaxHP} HP"
                : p.ParticipantType != CombatParticipantType.Character ? "defeated"
                : combats.IsDead(p) ? "dead" : "down and dying";
        }

        var sheet = target.Sheet!;
        if (CharacterStateService.IsDead(sheet)) return signed > 0 ? "the dead are beyond healing" : "already dead";
        await characters.ChangeHpAsync(sheet, signed, ct);
        return sheet.CurrentHP > 0 ? $"{sheet.CurrentHP}/{sheet.MaxHP} HP" : "unconscious at 0 HP";
    }

    private async Task<string> ApplySpellConditionAsync(SpellCall s, SpellTarget target, CancellationToken ct)
    {
        if (target.Participant is { } p) await combats.SetConditionAsync(s.Fight!, p.Id, s.Condition!, true, ct);
        else await characters.SetConditionAsync(target.Sheet!, s.Condition!, true, ct);
        return $"now {s.Condition}";
    }
}
