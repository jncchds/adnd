using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services.Combat;

/// <summary>
/// Call of Cthulhu 7e sanity check service.
/// Sanity is tracked as a custom field in CombatParticipant.Conditions jsonb
/// under the key "sanity": {"current": 70, "max": 99}.
/// </summary>
public sealed class SANService(AppDbContext db, CombatEventLogger logger) : ISANService
{
    private const int TemporaryInsanityThreshold = 5;

    public async Task<SanityCheckResult> PerformSanityCheckAsync(
        Guid participantId,
        int sanityLoss,
        string trigger,
        CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var conditions = DeserializeConditions(participant.Conditions);

        // Read current sanity
        int currentSanity = 99;
        int maxSanity = 99;
        if (conditions.TryGetValue("sanity", out var sanityJson))
        {
            using var sanDoc = JsonDocument.Parse(sanityJson);
            if (sanDoc.RootElement.TryGetProperty("current", out var cur))
                currentSanity = cur.GetInt32();
            if (sanDoc.RootElement.TryGetProperty("max", out var max))
                maxSanity = max.GetInt32();
        }

        var newSanity = currentSanity - sanityLoss;
        var wentInsane = sanityLoss >= TemporaryInsanityThreshold || newSanity < 0;

        // Indefinite insanity: sanity below 0
        if (newSanity < 0)
        {
            conditions["indefiniteInsanity"] = "true";
            newSanity = 0;
        }

        if (wentInsane)
            conditions["temporaryInsanity"] = trigger;

        // Write back sanity
        conditions["sanity"] = JsonSerializer.Serialize(new { current = newSanity, max = maxSanity });
        participant.Conditions = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(conditions));

        logger.Log(
            participant.Combat,
            CombatEventType.SaveThrow,
            participantId,
            null,
            $"SAN check: -{sanityLoss} from {trigger}, now {newSanity}",
            new { sanityLoss, trigger, newSanity, wentInsane });

        await db.SaveChangesAsync(ct);

        return new SanityCheckResult(participantId, sanityLoss, wentInsane, newSanity);
    }

    private static Dictionary<string, string> DeserializeConditions(System.Text.Json.JsonElement el)
    {
        if (el.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null)
            return new();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(el.GetRawText()) ?? new();
    }
}
