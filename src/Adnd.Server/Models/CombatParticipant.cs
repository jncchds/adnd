using System.Text.Json;

namespace Adnd.Server.Models;

public class CombatParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CombatId { get; set; }
    public CombatParticipantType ParticipantType { get; set; }
    public Guid? CharacterId { get; set; }
    public Guid? NPCId { get; set; }
    public Guid? PlayerId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public float Initiative { get; set; }
    public float InitiativeCount { get; set; }
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public int AC { get; set; }
    public JsonElement Conditions { get; set; }
    public JsonElement TemporaryHP { get; set; }
    public JsonElement SavingThrows { get; set; }
    public JsonElement DeathSaveState { get; set; }
    public int ActionsRemaining { get; set; } = 1;
    public int BonusActionsRemaining { get; set; } = 1;
    public int ReactionsRemaining { get; set; } = 1;
    public int MovementsRemaining { get; set; } = 30;
    public int FreeActions { get; set; }

    public Combat Combat { get; set; } = null!;
}
