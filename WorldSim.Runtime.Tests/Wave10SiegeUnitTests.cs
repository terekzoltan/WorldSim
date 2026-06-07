using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Defense;
using WorldSim.Simulation.Diplomacy;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave10SiegeUnitTests
{
    [Fact]
    public void SiegeCraftDisabled_DoesNotSpawnDedicatedSiegeUnits()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: false);

        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());

        Assert.Empty(fixture.Runtime.SiegeUnits);
        Assert.Empty(fixture.Runtime.GetSnapshot().SiegeUnits);
        Assert.Equal(fixture.TargetWall!.MaxHp, fixture.TargetWall.Hp);
    }

    [Fact]
    public void SiegeCraftEnabled_SpawnsDeterministicCampaignScopedUnitSet()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);

        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());

        var units = fixture.Runtime.SiegeUnits.OrderBy(unit => unit.SiegeUnitId).ToArray();

        Assert.Equal(new[] { SiegeUnitKind.Ram, SiegeUnitKind.SiegeTower, SiegeUnitKind.MobileCatapult }, units.Select(unit => unit.Kind).ToArray());
        Assert.All(units, unit =>
        {
            Assert.Equal(fixture.CampaignId, unit.CampaignId);
            Assert.Equal(fixture.ArmyId, unit.ArmyId);
            Assert.Equal(Faction.Obsidari, unit.OwnerFaction);
            Assert.Equal(SiegeUnitPhase.Active, unit.Phase);
            Assert.Equal(fixture.TargetWall!.Id, unit.TargetStructureId);
            Assert.Equal(fixture.Member.Pos, (unit.X, unit.Y));
        });
    }

    [Fact]
    public void RepeatedTick_DoesNotDuplicateDedicatedSiegeUnits()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);

        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());
        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());

        var units = fixture.Runtime.SiegeUnits.ToArray();
        Assert.Equal(3, units.Length);
        Assert.Equal(3, units.Select(unit => unit.SiegeUnitId).Distinct().Count());
        Assert.Equal(1, units.Count(unit => unit.Kind == SiegeUnitKind.Ram));
        Assert.Equal(1, units.Count(unit => unit.Kind == SiegeUnitKind.SiegeTower));
        Assert.Equal(1, units.Count(unit => unit.Kind == SiegeUnitKind.MobileCatapult));
    }

    [Fact]
    public void ResolvedCampaign_MarksDedicatedSiegeUnitsInactive()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);
        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());
        ResolveCampaign(GetLiveCampaigns(fixture.Runtime).Single());

        fixture.Runtime.AdvanceTick(1f);

        Assert.All(fixture.Runtime.SiegeUnits, unit =>
        {
            Assert.Equal(SiegeUnitPhase.Inactive, unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.CampaignResolved, unit.InactiveReason);
        });
    }

    [Fact]
    public void SnapshotExportsAllDedicatedSiegeUnitKinds()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);

        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());
        var renderUnits = fixture.Runtime.GetSnapshot().SiegeUnits.OrderBy(unit => unit.SiegeUnitId).ToArray();

        Assert.Equal(new[] { "ram", "siege_tower", "mobile_catapult" }, renderUnits.Select(unit => unit.Kind).ToArray());
        Assert.All(renderUnits, unit =>
        {
            Assert.Equal(fixture.CampaignId, unit.CampaignId);
            Assert.Equal(fixture.ArmyId, unit.ArmyId);
            Assert.Equal("active", unit.Phase);
            Assert.Equal(fixture.TargetWall!.Id, unit.TargetStructureId);
            Assert.True(unit.Health > 0f);
            Assert.True(unit.MaxHealth >= unit.Health);
            Assert.NotEqual("ready", unit.RecentActionEffect);
        });
    }

    [Fact]
    public void DedicatedSiegeUnits_HaveDistinctRuntimeEffectsThroughExistingSiegePath()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);
        var hpBefore = fixture.TargetWall!.Hp;

        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());

        Assert.True(fixture.TargetWall.Hp < hpBefore);
        Assert.Contains(fixture.Runtime.SiegeUnits, unit => unit.Kind == SiegeUnitKind.Ram && unit.RecentActionEffect == "ram_wall_gate_pressure");
        Assert.Contains(fixture.Runtime.SiegeUnits, unit => unit.Kind == SiegeUnitKind.SiegeTower && unit.RecentActionEffect == "siege_tower_access_pressure");
        Assert.Contains(fixture.Runtime.SiegeUnits, unit => unit.Kind == SiegeUnitKind.MobileCatapult && unit.RecentActionEffect == "mobile_catapult_ranged_pressure");
    }

    [Fact]
    public void UnitEnabledFlow_DiffersDeterministicallyFromNoUnitBaseline()
    {
        var baseline = CreateEncounterCampaign(unlockSiegeCraft: false);
        var enabled = CreateEncounterCampaign(unlockSiegeCraft: true);

        QueueCampaignSiegePressure(baseline.Runtime, 1f, new HashSet<int>());
        QueueCampaignSiegePressure(enabled.Runtime, 1f, new HashSet<int>());

        Assert.Empty(baseline.Runtime.SiegeUnits);
        Assert.Equal(baseline.TargetWall!.MaxHp, baseline.TargetWall.Hp);
        Assert.Equal(3, enabled.Runtime.SiegeUnits.Count);
        Assert.True(enabled.TargetWall!.Hp < baseline.TargetWall.Hp);
    }

    [Fact]
    public void InvalidRoster_SuppressesSiegeAndMarksDedicatedSiegeUnitsInactiveIdempotently()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);
        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());
        var firstUnitIds = fixture.Runtime.SiegeUnits.Select(unit => unit.SiegeUnitId).Order().ToArray();
        Assert.Equal(3, firstUnitIds.Length);

        fixture.Runtime.AdvanceTick(0f);
        fixture.Member.Health = 0f;
        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.None, campaign.Siege.Status);
        Assert.Equal(-1, campaign.Siege.TargetStructureId);
        Assert.All(fixture.Runtime.SiegeUnits, unit =>
        {
            Assert.Equal(SiegeUnitPhase.Inactive, unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.CampaignInvalid, unit.InactiveReason);
        });
        Assert.All(fixture.Runtime.GetSnapshot().SiegeUnits, unit =>
        {
            Assert.Equal("inactive", unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.CampaignInvalid, unit.InactiveReason);
        });

        fixture.Runtime.AdvanceTick(1f);

        Assert.Equal(3, fixture.Runtime.SiegeUnits.Count);
        Assert.Equal(firstUnitIds, fixture.Runtime.SiegeUnits.Select(unit => unit.SiegeUnitId).Order().ToArray());
        Assert.All(fixture.Runtime.SiegeUnits, unit => Assert.Equal(SiegeUnitPhase.Inactive, unit.Phase));
    }

    [Fact]
    public void SameTickSyncAliveButPressureInvalidRoster_SuppressesSiegeAndMarksDedicatedSiegeUnitsInactive()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);
        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());
        var campaign = Assert.Single(GetLiveCampaigns(fixture.Runtime));
        Assert.Equal(CampaignSiegeStatus.SeekingTarget, campaign.Siege.Status);
        Assert.Equal(3, fixture.Runtime.SiegeUnits.Count(unit => unit.Phase == SiegeUnitPhase.Active));
        Assert.True(campaign.Siege.LastPressureTick >= 0);

        fixture.Member.Health = Math.Max(1f, fixture.Member.Health);
        fixture.Member.Current = Job.AttackStructure;
        Assert.True(fixture.Member.Health > 0f);
        Assert.Equal(Job.AttackStructure, fixture.Member.Current);

        SyncCampaignSiegeStates(fixture.Runtime, new HashSet<int> { fixture.Member.Id });

        Assert.Equal(CampaignSiegeStatus.None, campaign.Siege.Status);
        Assert.Equal(-1, campaign.Siege.TargetStructureId);
        Assert.All(fixture.Runtime.SiegeUnits, unit =>
        {
            Assert.Equal(SiegeUnitPhase.Inactive, unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.CampaignInvalid, unit.InactiveReason);
        });
        Assert.All(fixture.Runtime.GetSnapshot().SiegeUnits, unit =>
        {
            Assert.Equal("inactive", unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.CampaignInvalid, unit.InactiveReason);
        });
    }

    [Fact]
    public void ResolverDisabledAfterSpawn_MarksDedicatedSiegeUnitsInactiveWithSiegeDisabled()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);
        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());
        Assert.Equal(3, fixture.Runtime.SiegeUnits.Count(unit => unit.Phase == SiegeUnitPhase.Active));

        fixture.World.EnableSiege = false;
        fixture.Runtime.AdvanceTick(1f);

        Assert.All(fixture.Runtime.SiegeUnits, unit =>
        {
            Assert.Equal(SiegeUnitPhase.Inactive, unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.SiegeDisabled, unit.InactiveReason);
        });
        Assert.All(fixture.Runtime.GetSnapshot().SiegeUnits, unit =>
        {
            Assert.Equal("inactive", unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.SiegeDisabled, unit.InactiveReason);
        });
    }

    [Fact]
    public void NoTargetReporterPath_MarksSpawnedDedicatedSiegeUnitsInactive()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);
        var campaign = GetLiveCampaigns(fixture.Runtime).Single();
        EnsureSiegeUnits(fixture.Runtime, campaign, fixture.Owner, fixture.TargetWall!);
        Assert.Equal(3, fixture.Runtime.SiegeUnits.Count(unit => unit.Phase == SiegeUnitPhase.Active));

        fixture.Member.Current = Job.Idle;
        fixture.Member.SetCombatAssignment(null, null, Formation.Line, isCommander: false);
        Assert.True(fixture.World.DefensiveStructures.RemoveAll(_ => true) > 0);
        Assert.Empty(fixture.World.DefensiveStructures);
        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());

        Assert.Equal(CampaignSiegeStatus.NoTarget, campaign.Siege.Status);
        Assert.All(fixture.Runtime.SiegeUnits, unit =>
        {
            Assert.Equal(SiegeUnitPhase.Inactive, unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.CampaignInvalid, unit.InactiveReason);
        });
        Assert.All(fixture.Runtime.GetSnapshot().SiegeUnits, unit =>
        {
            Assert.Equal("inactive", unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.CampaignInvalid, unit.InactiveReason);
        });
    }

    [Fact]
    public void EnsureSiegeUnits_CreatesFreshActiveSetWithoutReactivatingInactiveHistory()
    {
        var fixture = CreateEncounterCampaign(unlockSiegeCraft: true);
        QueueCampaignSiegePressure(fixture.Runtime, 1f, new HashSet<int>());
        var campaign = GetLiveCampaigns(fixture.Runtime).Single();
        MarkSiegeUnitsInvalid(fixture.Runtime, campaign);
        var inactiveIds = fixture.Runtime.SiegeUnits.Select(unit => unit.SiegeUnitId).Order().ToArray();
        Assert.All(fixture.Runtime.SiegeUnits, unit => Assert.Equal(SiegeUnitPhase.Inactive, unit.Phase));

        EnsureSiegeUnits(fixture.Runtime, campaign, fixture.Owner, fixture.TargetWall!);

        var units = fixture.Runtime.SiegeUnits.ToArray();
        Assert.Equal(6, units.Length);
        Assert.All(units.Where(unit => inactiveIds.Contains(unit.SiegeUnitId)), unit =>
        {
            Assert.Equal(SiegeUnitPhase.Inactive, unit.Phase);
            Assert.Equal(SiegeUnitInactiveReasons.CampaignInvalid, unit.InactiveReason);
        });
        var active = units.Where(unit => unit.Phase == SiegeUnitPhase.Active).ToArray();
        Assert.Equal(3, active.Length);
        Assert.Equal(3, active.Select(unit => unit.Kind).Distinct().Count());
        Assert.Equal(1, active.Count(unit => unit.Kind == SiegeUnitKind.Ram));
        Assert.Equal(1, active.Count(unit => unit.Kind == SiegeUnitKind.SiegeTower));
        Assert.Equal(1, active.Count(unit => unit.Kind == SiegeUnitKind.MobileCatapult));
    }

    private static EncounterCampaignFixture CreateEncounterCampaign(bool unlockSiegeCraft)
    {
        var runtime = CreateRuntime();
        EnableDiplomacyCombatAndSiege(runtime);
        var world = GetWorld(runtime);
        world.BirthRateMultiplier = 0f;
        world._animals.Clear();
        SetAllCampaignCandidatesIneligible(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        if (unlockSiegeCraft)
            owner.UnlockedTechs.Add("siege_craft");

        Assert.Equal(1, runtime.PrepareWave9CampaignScenario(Faction.Obsidari, candidateCount: 1, carriedFoodPerCandidate: 2));
        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        world.EnableCombatPrimitives = false;

        for (var i = 0; i < 80 && runtime.Campaigns.Any(campaign => campaign.Army.AssignedMemberCount < 1); i++)
            runtime.AdvanceTick(0f);

        var campaign = runtime.Campaigns.Single();
        var member = world._people.Single(person => campaign.Army.MemberActorIds.Contains(person.Id));
        member.Pos = (campaign.Army.RallyX, campaign.Army.RallyY);

        for (var i = 0; i < 8 && runtime.Campaigns.Single().Phase != CampaignPhase.Marching; i++)
            runtime.AdvanceTick(0f);

        campaign = runtime.Campaigns.Single();
        Assert.Equal(CampaignPhase.Marching, campaign.Phase);
        member.Pos = FindEncounterTestPosition(world, target);
        runtime.AdvanceTick(0f);
        campaign = runtime.Campaigns.Single();
        Assert.Equal(CampaignPhase.Encounter, campaign.Phase);

        NeutralizeNonCampaignActors(world, member.Id);
        member.Profession = Profession.Generalist;
        member.Current = Job.Idle;
        member.Needs["Hunger"] = 0f;
        member.ApplyStaminaDelta(100f);
        world.EnableCombatPrimitives = true;
        world.EnableSiege = true;
        world.SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.Neutral);

        var wallPos = FindBuildableNear(world, target, minDistanceFromOrigin: 3);
        Assert.True(world.TryAddWoodWall(target, wallPos));
        var wall = Assert.Single(world.DefensiveStructures, structure => structure.Pos == wallPos);

        return new EncounterCampaignFixture(runtime, world, owner, target, member, wall, campaign.CampaignId, campaign.Army.ArmyId);
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 12, techPath, aiOptions: null, randomSeed: 9601);
    }

    private static void EnableDiplomacyCombatAndSiege(SimulationRuntime runtime)
    {
        var world = GetWorld(runtime);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;
        world.EnableSiege = true;
    }

    private static void SetAllCampaignCandidatesIneligible(SimulationRuntime runtime)
    {
        foreach (var person in GetWorld(runtime)._people)
        {
            person.ClearRole(PersonRole.Warrior | PersonRole.SupplyCarrier | PersonRole.Scout | PersonRole.Commander);
            person.Profession = Profession.Generalist;
            person.Current = Job.Idle;
            person.SetCombatAssignment(null, null, Formation.Line, isCommander: false);
        }
    }

    private static void NeutralizeNonCampaignActors(World world, int campaignMemberId)
    {
        foreach (var person in world._people)
        {
            if (person.Id != campaignMemberId)
                person.Health = 0f;
        }
    }

    private static void MarkSiegeUnitsInvalid(SimulationRuntime runtime, CampaignState campaign)
    {
        var method = typeof(SimulationRuntime).GetMethod("MarkCampaignSiegeUnitsInactive", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(runtime, new object[] { campaign, SiegeUnitInactiveReasons.CampaignInvalid });
    }

    private static void EnsureSiegeUnits(SimulationRuntime runtime, CampaignState campaign, Colony attacker, DefensiveStructure target)
    {
        var method = typeof(SimulationRuntime).GetMethod("EnsureCampaignSiegeUnits", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(runtime, new object[] { campaign, attacker, target });
    }

    private static void QueueCampaignSiegePressure(SimulationRuntime runtime, float dt, HashSet<int> blockedCampaignActorIds)
    {
        var method = typeof(SimulationRuntime).GetMethod("QueueCampaignSiegePressureForActiveEncounters", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(runtime, new object[] { dt, blockedCampaignActorIds });
    }

    private static void SyncCampaignSiegeStates(SimulationRuntime runtime, HashSet<int> blockedCampaignActorIds)
    {
        var method = typeof(SimulationRuntime).GetMethod("SyncCampaignSiegeStates", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(runtime, new object[] { blockedCampaignActorIds });
    }

    private static void ResolveCampaign(CampaignState campaign)
        => Assert.True(campaign.Resolve(new CampaignResolutionApplication(
            CampaignResolutionKind.DefenderHeld,
            CampaignResolutionReasons.DefenderTimeout,
            ResolvedTick: 99,
            AttackerFaction: campaign.OwnerFaction,
            DefenderFaction: campaign.TargetFaction,
            OriginColonyId: campaign.OriginColonyId,
            TargetColonyId: campaign.TargetColonyId,
            TargetStructureId: campaign.Siege.TargetStructureId,
            LootFood: 0,
            LootWood: 0,
            LootStone: 0,
            LootGold: 0,
            WarScoreDelta: 0,
            CumulativeWarScore: 0,
            PeaceEligible: false,
            PeaceApplied: false,
            TreatyKind: CampaignResolutionReasons.None)));

    private static Colony GetColony(World world, Faction faction)
    {
        var colony = world._colonies.FirstOrDefault(candidate => candidate.Faction == faction);
        Assert.NotNull(colony);
        return colony!;
    }

    private static List<CampaignState> GetLiveCampaigns(SimulationRuntime runtime)
    {
        var campaignsField = typeof(SimulationRuntime).GetField("_campaigns", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(campaignsField);
        return Assert.IsAssignableFrom<List<CampaignState>>(campaignsField!.GetValue(runtime));
    }

    private static World GetWorld(SimulationRuntime runtime)
    {
        var worldField = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(worldField);
        var world = worldField!.GetValue(runtime) as World;
        Assert.NotNull(world);
        return world!;
    }

    private static (int x, int y) FindBuildableNear(World world, Colony colony, int minDistanceFromOrigin)
    {
        for (var radius = Math.Max(1, minDistanceFromOrigin); radius <= Math.Max(world.Width, world.Height); radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) < minDistanceFromOrigin)
                        continue;

                    int x = colony.Origin.x + dx;
                    int y = colony.Origin.y + dy;
                    if (world.CanPlaceStructureAt(x, y))
                        return (x, y);
                }
            }
        }

        throw new InvalidOperationException("Could not find a buildable siege-unit test tile.");
    }

    private static (int x, int y) FindEncounterTestPosition(World world, Colony target)
    {
        var offsets = new[]
        {
            (dx: 0, dy: 0),
            (dx: 1, dy: 0),
            (dx: -1, dy: 0),
            (dx: 0, dy: 1),
            (dx: 0, dy: -1)
        };

        foreach (var offset in offsets)
        {
            int x = target.Origin.x + offset.dx;
            int y = target.Origin.y + offset.dy;
            if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                continue;
            if (!world.IsMovementBlocked(x, y, target.Id))
                return (x, y);
        }

        return target.Origin;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var techPath = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(techPath))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }

    private sealed record EncounterCampaignFixture(
        SimulationRuntime Runtime,
        World World,
        Colony Owner,
        Colony Target,
        Person Member,
        DefensiveStructure? TargetWall,
        int CampaignId,
        int ArmyId);
}
