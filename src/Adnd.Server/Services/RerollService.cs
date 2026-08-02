using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>One entry of <see cref="Character.Features"/>.</summary>
/// <param name="UsesRemaining">Null for an unlimited ability. A limited ability that has
/// never been spent still carries its starting count, so null here always means unlimited
/// and never "unknown".</param>
public record CharacterFeature(string Id, string Name, int? UsesRemaining);

/// <summary>
/// The one serialization contract for <see cref="Character.Features"/>. A bare
/// <c>JsonSerializer.Deserialize</c> is case-*sensitive* by default — the same default that
/// silently broke spell-slot consumption — so both directions are pinned here rather than
/// left to whichever call site happens to write the column.
/// </summary>
public static class FeatureJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>A reroll the player may take on a roll that has just happened.</summary>
public record RerollOption(string FeatureId, string Name, string Description, int? UsesRemaining);

public interface IRerollService
{
    /// <summary>
    /// Which of the character's abilities apply to this roll, right now. Empty when the
    /// character has none, none of their triggers fired, or the ones that did are spent.
    /// </summary>
    Task<IReadOnlyList<RerollOption>> GetOptionsAsync(Guid characterId, DiceResult roll, int? dc, CancellationToken ct = default);

    /// <inheritdoc cref="GetOptionsAsync(Guid, DiceResult, int?, CancellationToken)"/>
    Task<IReadOnlyList<RerollOption>> GetOptionsForPlayerAsync(Guid playerId, DiceResult roll, int? dc, CancellationToken ct = default);

    /// <summary>
    /// Spends one use. False when the character doesn't have the ability or is out of uses,
    /// which is the check that stops a replayed or forged accept from granting free rerolls.
    /// </summary>
    Task<bool> TryConsumeAsync(Guid characterId, string featureId, CancellationToken ct = default);

    /// <summary>Refills every ability that recharges on this rest. Returns how many were refilled.</summary>
    Task<int> RestoreOnRestAsync(Guid characterId, RestPeriod period, CancellationToken ct = default);

    IReadOnlyList<CharacterFeature> Read(JsonElement features);
    JsonElement Write(IEnumerable<CharacterFeature> features);
}

public class RerollService(AppDbContext db, IFeatureCatalogue catalogue) : IRerollService
{
    public IReadOnlyList<CharacterFeature> Read(JsonElement features)
    {
        if (features.ValueKind != JsonValueKind.Array) return [];
        try
        {
            return JsonSerializer.Deserialize<List<CharacterFeature>>(features.GetRawText(), FeatureJson.Options) ?? [];
        }
        catch (JsonException)
        {
            // A hand-edited sheet can put anything in here. Treating it as "no features" is
            // wrong in a way the player will notice and report; throwing would break their
            // character sheet entirely.
            return [];
        }
    }

    public JsonElement Write(IEnumerable<CharacterFeature> features)
        => JsonSerializer.SerializeToElement(features.ToList(), FeatureJson.Options);

    public async Task<IReadOnlyList<RerollOption>> GetOptionsAsync(Guid characterId, DiceResult roll, int? dc, CancellationToken ct = default)
    {
        var character = await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == characterId, ct);
        return character is null ? [] : Evaluate(character, roll, dc);
    }

    public async Task<IReadOnlyList<RerollOption>> GetOptionsForPlayerAsync(Guid playerId, DiceResult roll, int? dc, CancellationToken ct = default)
    {
        var character = await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.PlayerId == playerId, ct);
        return character is null ? [] : Evaluate(character, roll, dc);
    }

    private List<RerollOption> Evaluate(Character character, DiceResult roll, int? dc)
    {
        var failed = dc.HasValue && roll.Total < dc.Value;

        var options = new List<RerollOption>();
        foreach (var owned in Read(character.Features))
        {
            if (owned.UsesRemaining is <= 0) continue;

            var def = catalogue.GetById(owned.Id);
            if (def is null) continue;

            var applies = def.Trigger switch
            {
                RerollTrigger.AnyRoll => true,
                RerollTrigger.NaturalOne => roll.NaturalD20 == 1,
                // A roll with no stated DC cannot be known to have failed. Offering here
                // would mean burning a limited resource on a roll that may have succeeded.
                RerollTrigger.FailedCheck => failed,
                _ => false
            };
            if (!applies) continue;

            options.Add(new RerollOption(def.Id, def.Name, def.Description, owned.UsesRemaining));
        }

        return options;
    }

    public async Task<bool> TryConsumeAsync(Guid characterId, string featureId, CancellationToken ct = default)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct);
        if (character is null) return false;

        var owned = Read(character.Features).ToList();
        var index = owned.FindIndex(f => f.Id.Equals(featureId, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;

        var feature = owned[index];
        if (feature.UsesRemaining is null)
            return true; // Unlimited — nothing to spend, nothing to write.

        if (feature.UsesRemaining <= 0) return false;

        owned[index] = feature with { UsesRemaining = feature.UsesRemaining - 1 };
        character.Features = Write(owned);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RestoreOnRestAsync(Guid characterId, RestPeriod period, CancellationToken ct = default)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct);
        if (character is null) return 0;

        var owned = Read(character.Features).ToList();
        var restored = 0;

        for (var i = 0; i < owned.Count; i++)
        {
            var def = catalogue.GetById(owned[i].Id);
            // A long rest also confers everything a short rest would, so it restores both.
            var recharges = def?.Recharge == period || (period == RestPeriod.LongRest && def?.Recharge == RestPeriod.ShortRest);
            if (def?.MaxUses is not { } max || !recharges) continue;
            if (owned[i].UsesRemaining == max) continue;

            owned[i] = owned[i] with { UsesRemaining = max };
            restored++;
        }

        if (restored > 0)
        {
            character.Features = Write(owned);
            await db.SaveChangesAsync(ct);
        }

        return restored;
    }
}
