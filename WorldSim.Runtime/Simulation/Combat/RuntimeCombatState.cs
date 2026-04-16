using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation.Combat;

internal sealed class RuntimeCombatGroup
{
    public RuntimeCombatGroup(int groupId, Colony colony, Formation formation, IReadOnlyList<Person> members)
    {
        GroupId = groupId;
        Colony = colony;
        Formation = formation;
        Members = members;
        Anchor = ComputeAnchor(members);
        Commander = members
            .OrderByDescending(person => person.Intelligence)
            .ThenByDescending(person => person.Strength + person.Defense)
            .FirstOrDefault();
        CommanderIntelligence = Commander?.Intelligence ?? 0;
        CommanderMoraleStabilityBonus = Math.Clamp(CommanderIntelligence / 30f, 0f, 0.45f);
    }

    public int GroupId { get; }
    public Colony Colony { get; }
    public Formation Formation { get; }
    public IReadOnlyList<Person> Members { get; }
    public Person? Commander { get; }
    public int CommanderIntelligence { get; }
    public float CommanderMoraleStabilityBonus { get; }
    public (int x, int y) Anchor { get; }
    public int BattleId { get; set; } = -1;

    public int RoutingMemberCount => Members.Count(person => person.IsRouting);
    public bool IsRouting => RoutingMemberCount >= Math.Max(1, Members.Count / 2);
    public float AverageMorale => Members.Count == 0 ? 0f : Members.Average(person => person.CombatMorale);

    public float StrengthScore
        => Members.Sum(member => MathF.Max(1f, member.Strength + (member.Defense * 0.5f) + (member.Home.WeaponLevel * 2f)));

    public float DefenseScore
        => Members.Sum(member => MathF.Max(1f, member.Defense + (member.Home.ArmorLevel * 2f) + 2f));

    private static (int x, int y) ComputeAnchor(IReadOnlyList<Person> members)
    {
        if (members.Count == 0)
            return (0, 0);

        var x = (int)MathF.Round((float)members.Average(member => member.Pos.x));
        var y = (int)MathF.Round((float)members.Average(member => member.Pos.y));
        return (x, y);
    }
}

internal sealed class RuntimeBattleState
{
    public RuntimeBattleState(int battleId, RuntimeCombatGroup left, RuntimeCombatGroup right)
    {
        BattleId = battleId;
        Left = left;
        Right = right;
    }

    public int BattleId { get; }
    public RuntimeCombatGroup Left { get; }
    public RuntimeCombatGroup Right { get; }
    public int ElapsedTicks { get; set; }
    public int Intensity { get; set; }
    public bool HadDamageThisTick { get; set; }
    public bool HadDeathThisTick { get; set; }

    public int Radius
    {
        get
        {
            var dx = Math.Abs(Left.Anchor.x - Right.Anchor.x);
            var dy = Math.Abs(Left.Anchor.y - Right.Anchor.y);
            return Math.Max(1, (dx + dy) / 2 + 1);
        }
    }

    public (int x, int y) Center
        => ((Left.Anchor.x + Right.Anchor.x) / 2, (Left.Anchor.y + Right.Anchor.y) / 2);
}
