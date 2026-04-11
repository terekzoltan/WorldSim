using System;
using System.IO;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class SimulationRuntimeCampaignCommandTests
{
    [Fact]
    public void DeclareWar_SetsWarStance_AndUpdatesStatus()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "border pressure");

        Assert.Equal("War", GetFactionStance(runtime, Faction.Obsidari, Faction.Aetheri));
        Assert.Contains("Declared war", runtime.LastDirectorActionStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void DeclareWar_IsIdempotent_WhenAlreadyWar()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, null);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, null);

        Assert.Equal("War", GetFactionStance(runtime, Faction.Obsidari, Faction.Aetheri));
        Assert.Contains("no-op", runtime.LastDirectorActionStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void DeclareWar_PersistsAcrossTick_WhenDiplomacyAndCombatEnabled()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "campaign");
        runtime.AdvanceTick(0.25f);

        Assert.Equal("War", GetFactionStance(runtime, Faction.Obsidari, Faction.Aetheri));
    }

    [Fact]
    public void ProposeTreaty_Ceasefire_TransitionsWarToHostile_Only()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, null);
        runtime.ProposeTreaty(Faction.Aetheri, Faction.Obsidari, "ceasefire", "pause");

        Assert.Equal("Hostile", GetFactionStance(runtime, Faction.Obsidari, Faction.Aetheri));

        runtime.ProposeTreaty(Faction.Aetheri, Faction.Obsidari, "ceasefire", "repeat");
        Assert.Equal("Hostile", GetFactionStance(runtime, Faction.Obsidari, Faction.Aetheri));
        Assert.Contains("no-op", runtime.LastDirectorActionStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void ProposeTreaty_PeaceTalks_MovesOneStepTowardNeutral()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, null);
        runtime.ProposeTreaty(Faction.Aetheri, Faction.Obsidari, "peace_talks", "first");
        Assert.Equal("Hostile", GetFactionStance(runtime, Faction.Obsidari, Faction.Aetheri));

        runtime.ProposeTreaty(Faction.Aetheri, Faction.Obsidari, "peace_talks", "second");
        Assert.Equal("Neutral", GetFactionStance(runtime, Faction.Obsidari, Faction.Aetheri));

        runtime.ProposeTreaty(Faction.Aetheri, Faction.Obsidari, "peace_talks", "third");
        Assert.Equal("Neutral", GetFactionStance(runtime, Faction.Obsidari, Faction.Aetheri));
        Assert.Contains("no-op", runtime.LastDirectorActionStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void ProposeTreaty_RejectsUnsupportedTreatyKind_Deterministically()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            runtime.ProposeTreaty(Faction.Aetheri, Faction.Obsidari, "alliance", null));

        Assert.Contains("Unsupported proposeTreaty.treatyKind", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CampaignCommands_Reject_WhenDiplomacyOrCombatDisabled()
    {
        var runtime = CreateRuntime();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, null));

        Assert.Contains("WORLDSIM_ENABLE_DIPLOMACY=true", ex.Message, StringComparison.Ordinal);
        Assert.Contains("WORLDSIM_ENABLE_COMBAT_PRIMITIVES=true", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeclareWar_UpdatesColonyWarState_Immediately()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "campaign");

        var attackerColony = GetColony(runtime, Faction.Obsidari);
        var defenderColony = GetColony(runtime, Faction.Aetheri);

        Assert.Equal("War", attackerColony.WarState);
        Assert.Equal("War", defenderColony.WarState);
        Assert.True(attackerColony.WarriorCount > 0);
        Assert.True(defenderColony.WarriorCount > 0);
    }

    [Fact]
    public void CampaignCommands_Reject_InvalidFactionValues()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            runtime.DeclareWar((Faction)999, Faction.Aetheri, null));

        Assert.Contains("Invalid faction value", ex.Message, StringComparison.Ordinal);
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 10, techPath);
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

    private static string GetFactionStance(SimulationRuntime runtime, Faction left, Faction right)
    {
        var low = Math.Min((int)left, (int)right);
        var high = Math.Max((int)left, (int)right);

        var stance = runtime.GetSnapshot().FactionStances
            .FirstOrDefault(entry => entry.LeftFactionId == low && entry.RightFactionId == high);
        Assert.NotNull(stance);
        return stance!.Stance;
    }

    private static WorldSim.Runtime.ReadModel.ColonyHudData GetColony(SimulationRuntime runtime, Faction faction)
    {
        var colony = runtime.GetSnapshot().Colonies.FirstOrDefault(entry => entry.FactionId == (int)faction);
        Assert.NotNull(colony);
        return colony!;
    }

    private static void EnableDiplomacyAndCombat(SimulationRuntime runtime)
    {
        var worldField = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(worldField);
        var world = worldField!.GetValue(runtime) as World;
        Assert.NotNull(world);
        world!.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;
    }
}
