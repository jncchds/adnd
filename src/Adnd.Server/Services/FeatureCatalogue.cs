namespace Adnd.Server.Services;

/// <summary>When a reroll ability may be offered.</summary>
public enum RerollTrigger
{
    /// <summary>Any roll at all, success or failure (the Lucky feat).</summary>
    AnyRoll,

    /// <summary>Only when a d20 came up a natural 1 (Halfling Luck).</summary>
    NaturalOne,

    /// <summary>Only when the roll failed a stated DC. A roll made with no DC cannot be
    /// known to have failed, so nothing with this trigger is offered for one.</summary>
    FailedCheck
}

public enum RestPeriod { ShortRest, LongRest }

/// <param name="MaxUses">Null means unlimited — the ability is offered whenever its trigger
/// fires and never needs recharging.</param>
/// <param name="Recharge">Which rest restores <paramref name="MaxUses"/>. Null iff
/// <paramref name="MaxUses"/> is null.</param>
/// <param name="GrantedByRace">Auto-granted at character creation to this race.</param>
/// <param name="GrantedByClass">Auto-granted to this class once it reaches
/// <paramref name="GrantedAtLevel"/>.</param>
public record FeatureDefinition(
    string Id,
    string Name,
    string Description,
    RerollTrigger Trigger,
    int? MaxUses,
    RestPeriod? Recharge,
    string? GrantedByRace = null,
    string? GrantedByClass = null,
    int GrantedAtLevel = 1);

public interface IFeatureCatalogue
{
    IReadOnlyList<FeatureDefinition> All { get; }
    FeatureDefinition? GetById(string id);

    /// <summary>The features a newly created character of this race/class/level starts with.</summary>
    IReadOnlyList<FeatureDefinition> GrantedTo(string? race, string? className, int level);
}

/// <summary>
/// The rules behind "you may reroll that". Characters store only an id and a remaining-uses
/// count (see <see cref="Models.Character.Features"/>); everything about what an ability
/// does is resolved through here, so correcting a rule is a one-line change rather than a
/// data migration across every character in every campaign.
///
/// Only genuine *rerolls* belong in this list. Abilities that replace a roll with a fixed or
/// pre-rolled number (Portent, Stroke of Luck) are a different mechanic and would need the
/// offer to carry a value rather than a yes/no — they are deliberately absent rather than
/// approximated, since approximating them would quietly give players the wrong numbers.
/// </summary>
public class FeatureCatalogue : IFeatureCatalogue
{
    private static readonly IReadOnlyList<FeatureDefinition> Definitions =
    [
        new("lucky", "Lucky",
            "Spend a luck point to reroll any attack roll, ability check, or saving throw.",
            RerollTrigger.AnyRoll, MaxUses: 3, Recharge: RestPeriod.LongRest),

        new("halfling-luck", "Halfling Luck",
            "When you roll a 1 on the d20, you can reroll and must use the new roll.",
            RerollTrigger.NaturalOne, MaxUses: null, Recharge: null,
            GrantedByRace: "Halfling"),

        // 5e restricts Indomitable to saving throws specifically. Nothing in this codebase
        // distinguishes a save from any other d20 roll -- requestPlayerRoll carries only a
        // free-text reason -- so it is offered on any failed check. Narrowing it would mean
        // inventing a roll-kind concept the GM would then have to populate correctly.
        new("indomitable", "Indomitable",
            "Reroll a saving throw you failed. You must use the new roll.",
            RerollTrigger.FailedCheck, MaxUses: 1, Recharge: RestPeriod.LongRest,
            GrantedByClass: "Fighter", GrantedAtLevel: 9),
    ];

    public IReadOnlyList<FeatureDefinition> All => Definitions;

    public FeatureDefinition? GetById(string id) =>
        Definitions.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<FeatureDefinition> GrantedTo(string? race, string? className, int level) =>
        Definitions.Where(d =>
                (d.GrantedByRace is not null && Matches(d.GrantedByRace, race)) ||
                (d.GrantedByClass is not null && Matches(d.GrantedByClass, className) && level >= d.GrantedAtLevel))
            .ToList();

    private static bool Matches(string expected, string? actual) =>
        actual is not null && expected.Equals(actual.Trim(), StringComparison.OrdinalIgnoreCase);
}
