using System.Linq;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Diplomacy;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave5FormationCombatTests
{
    [Fact]
    public void GroupCombatResolver_FormationChoice_ChangesDamage()
    {
        var rng = new System.Random(1234);
        var wedgeAttack = GroupCombatResolver.ComputeGroupAttackScore(basePower: 140f, formation: Formation.Wedge, weaponLevel: 1);
        var circleDefense = GroupCombatResolver.ComputeGroupDefenseScore(basePower: 120f, formation: Formation.DefensiveCircle, armorLevel: 1);
        var lineAttack = GroupCombatResolver.ComputeGroupAttackScore(basePower: 140f, formation: Formation.Line, weaponLevel: 1);
        var lineDefense = GroupCombatResolver.ComputeGroupDefenseScore(basePower: 120f, formation: Formation.Line, armorLevel: 1);

        var wedgeDamage = GroupCombatResolver.ComputePerHitDamage(rng, wedgeAttack, lineDefense, attackerCount: 6, defenderCount: 6);
        var defensiveDamage = GroupCombatResolver.ComputePerHitDamage(rng, lineAttack, circleDefense, attackerCount: 6, defenderCount: 6);

        Assert.True(wedgeDamage > defensiveDamage);
    }

    [Fact]
    public void GroupCombat_WorldSnapshot_ExportsGroupsAndBattles()
    {
        var world = CreateHostileCombatWorld(seed: 9001);

        var sawGroup = false;
        var sawBattle = false;
        for (var i = 0; i < 12; i++)
        {
            world.Update(0.25f);
            var snapshot = WorldSnapshotBuilder.Build(world);
            sawGroup |= snapshot.CombatGroups.Count > 0;
            sawBattle |= snapshot.Battles.Count > 0;
        }

        Assert.True(sawGroup, "Expected at least one active combat group in snapshot.");
        Assert.True(sawBattle, "Expected at least one active battle in snapshot.");
        Assert.True(world.TotalCombatEngagements > 0, "Expected combat engagements to be reported.");
    }

    [Fact]
    public void GroupCombat_ResolvesAcrossMultipleTicks()
    {
        var world = CreateHostileCombatWorld(seed: 9002);

        world.Update(0.25f);
        world.Update(0.25f);
        var engagementsAfterWarmup = world.TotalCombatEngagements;
        var healthAfterWarmup = world._people.Where(p => p.Health > 0f).Sum(p => p.Health);

        for (var i = 0; i < 4; i++)
            world.Update(0.25f);

        var engagementsAfterCombat = world.TotalCombatEngagements;
        var healthAfterCombat = world._people.Where(p => p.Health > 0f).Sum(p => p.Health);

        Assert.True(engagementsAfterCombat > engagementsAfterWarmup);
        Assert.True(healthAfterCombat < healthAfterWarmup);
        Assert.True(world.TotalBattleTicks > 1, "Expected group combat to span multiple battle ticks.");
    }

    [Fact]
    public void GroupCombat_LowMoraleSide_RoutesAndDisengages()
    {
        var world = new World(width: 32, height: 20, initialPop: 24, randomSeed: 9010)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true
        };

        world._animals.Clear();
        var colonyA = world._colonies[0];
        var colonyB = world._colonies[1];
        world.SetFactionStance(colonyA.Faction, colonyB.Faction, Stance.War);

        colonyA.Stock[Resource.Food] = 0;
        colonyB.Stock[Resource.Food] = 120;

        var loneA = world._people.Where(p => p.Home == colonyA).OrderByDescending(p => p.Strength + p.Defense).First();
        loneA.Pos = (12, 10);
        loneA.Health = 1200f;
        loneA.Profession = Profession.Hunter;

        var teamB = world._people
            .Where(p => p.Home == colonyB)
            .OrderByDescending(p => p.Strength + p.Defense)
            .Take(4)
            .ToList();
        var positions = new[] { (13, 10), (13, 9), (14, 10), (14, 9) };
        for (var i = 0; i < teamB.Count; i++)
        {
            teamB[i].Pos = positions[i];
            teamB[i].Health = 180f;
        }

        foreach (var bystander in world._people.Where(p => p != loneA && !teamB.Contains(p)))
            bystander.Pos = (0, 0);

        Person? routedPerson = null;
        for (var i = 0; i < 14; i++)
        {
            world.Update(0.25f);
            routedPerson = world._people.FirstOrDefault(person => person.IsRouting);
            if (routedPerson != null)
            {
                break;
            }
        }

        Assert.NotNull(routedPerson);

        for (var i = 0; i < 4; i++)
            world.Update(0.25f);

        Assert.True(routedPerson.Current == Job.Flee || routedPerson.RoutingTicksRemaining > 0);
        Assert.Equal(-1, routedPerson.ActiveBattleId);
    }

    [Fact]
    public void Snapshot_ExportsMoraleAndRoutingFields()
    {
        var world = CreateHostileCombatWorld(seed: 9011);

        for (var i = 0; i < 8; i++)
            world.Update(0.25f);

        var snapshot = WorldSnapshotBuilder.Build(world);
        Assert.NotEmpty(snapshot.People);
        Assert.All(snapshot.People, person => Assert.InRange(person.CombatMorale, 0f, 100f));
        Assert.All(snapshot.CombatGroups, group => Assert.InRange(group.AverageMorale, 0f, 100f));
        Assert.All(snapshot.Colonies, colony => Assert.InRange(colony.AverageCombatMorale, 0f, 100f));
    }

    [Fact]
    public void CommanderSelection_HighestIntelligenceMemberSelected()
    {
        var world = new World(width: 32, height: 20, initialPop: 24, randomSeed: 9012)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true
        };

        world._animals.Clear();
        var colonyA = world._colonies[0];
        var colonyB = world._colonies[1];
        world.SetFactionStance(colonyA.Faction, colonyB.Faction, Stance.War);

        var teamA = world._people.Where(person => person.Home == colonyA).Take(3).ToList();
        var teamB = world._people.Where(person => person.Home == colonyB).Take(3).ToList();

        teamA[0].Intelligence = 4;
        teamA[1].Intelligence = 15;
        teamA[2].Intelligence = 10;
        foreach (var person in teamA)
        {
            person.Profession = Profession.Hunter;
            person.Current = Job.Fight;
        }
        foreach (var person in teamB)
        {
            person.Profession = Profession.Hunter;
            person.Current = Job.Fight;
        }

        var aPos = new[] { (12, 10), (12, 11), (11, 10) };
        var bPos = new[] { (13, 10), (13, 11), (14, 10) };
        for (var i = 0; i < teamA.Count; i++)
            teamA[i].Pos = aPos[i];
        for (var i = 0; i < teamB.Count; i++)
            teamB[i].Pos = bPos[i];

        foreach (var bystander in world._people.Except(teamA).Except(teamB))
            bystander.Pos = (0, 0);

        world.Update(0.25f);

        var snapshot = WorldSnapshotBuilder.Build(world);
        var commander = teamA.OrderByDescending(person => person.Intelligence).First();
        Assert.Contains(snapshot.CombatGroups, group => group.ColonyId == colonyA.Id && group.CommanderActorId == commander.Id);
    }

    [Fact]
    public void CommanderBonus_HigherIntelligenceImprovesAverageMorale()
    {
        var low = CreateCommanderComparisonWorld(seed: 9013, commanderIntelligence: 4);
        var high = CreateCommanderComparisonWorld(seed: 9013, commanderIntelligence: 18);

        float lowMorale = 0f;
        float highMorale = 0f;
        for (var i = 0; i < 8; i++)
        {
            low.Update(0.25f);
            high.Update(0.25f);
        }

        var lowSnapshot = WorldSnapshotBuilder.Build(low);
        var highSnapshot = WorldSnapshotBuilder.Build(high);

        lowMorale = lowSnapshot.Colonies.First(colony => colony.Id == low._colonies[0].Id).AverageCombatMorale;
        highMorale = highSnapshot.Colonies.First(colony => colony.Id == high._colonies[0].Id).AverageCombatMorale;

        Assert.True(highMorale >= lowMorale);
    }

    private static World CreateHostileCombatWorld(int seed)
    {
        var world = new World(width: 32, height: 20, initialPop: 24, randomSeed: seed)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true
        };

        world._animals.Clear();

        var colonyA = world._colonies[0];
        var colonyB = world._colonies[1];
        world.SetFactionStance(colonyA.Faction, colonyB.Faction, Stance.War);

        var teamA = world._people
            .Where(person => person.Home == colonyA)
            .OrderByDescending(person => person.Strength + person.Defense)
            .Take(3)
            .ToList();
        var teamB = world._people
            .Where(person => person.Home == colonyB)
            .OrderByDescending(person => person.Strength + person.Defense)
            .Take(3)
            .ToList();

        var teamAPositions = new[] { (12, 9), (12, 10), (11, 10) };
        var teamBPositions = new[] { (13, 9), (13, 10), (14, 10) };
        for (var i = 0; i < teamA.Count; i++)
        {
            teamA[i].Pos = teamAPositions[i];
            teamA[i].Health = 200f;
        }

        for (var i = 0; i < teamB.Count; i++)
        {
            teamB[i].Pos = teamBPositions[i];
            teamB[i].Health = 200f;
        }

        foreach (var bystander in world._people.Except(teamA).Except(teamB))
            bystander.Pos = (0, 0);

        return world;
    }

    private static World CreateCommanderComparisonWorld(int seed, int commanderIntelligence)
    {
        var world = CreateHostileCombatWorld(seed);
        var colonyA = world._colonies[0];
        var teamA = world._people
            .Where(person => person.Home == colonyA)
            .OrderByDescending(person => person.Strength + person.Defense)
            .Take(3)
            .ToList();

        teamA[0].Intelligence = commanderIntelligence;
        teamA[1].Intelligence = Math.Max(1, commanderIntelligence - 3);
        teamA[2].Intelligence = Math.Max(1, commanderIntelligence - 5);

        return world;
    }
}
