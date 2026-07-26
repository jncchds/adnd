namespace Adnd.Server.Services;

public interface IGameEngine
{
    DiceResult RollSkillCheck(string skillId, int skillValue, string systemId);
    DiceResult RollAttack(int attackBonus, string systemId);
}

public class GameEngine(IDiceEngine diceEngine) : IGameEngine
{
    public DiceResult RollSkillCheck(string skillId, int skillValue, string systemId)
    {
        var formula = systemId.Equals("coc7e", StringComparison.OrdinalIgnoreCase)
            ? "1d100"
            : skillValue >= 0 ? $"1d20+{skillValue}" : $"1d20{skillValue}";
        return diceEngine.Roll(formula);
    }

    public DiceResult RollAttack(int attackBonus, string systemId)
    {
        var formula = attackBonus >= 0 ? $"1d20+{attackBonus}" : $"1d20{attackBonus}";
        return diceEngine.Roll(formula);
    }
}
