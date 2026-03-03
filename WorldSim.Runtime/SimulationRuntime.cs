using System;
using System.Linq;
using System.Text.Json.Nodes;
using WorldSim.AI;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;

namespace WorldSim.Runtime;

public sealed class SimulationRuntime
{
    private static readonly HashSet<string> KnownDirectorDirectives = new(StringComparer.Ordinal)
    {
        "PrioritizeFood",
        "StabilizeMorale",
        "BoostIndustry"
    };

    private readonly World _world;
    private readonly Queue<string> _recentAiDecisions = new();
    private readonly DirectorState _directorState = new();
    private long _lastObservedDecisionSequence;
    private AiDebugSnapshot _latestAiDebugSnapshot;
    private int _trackedNpcCursor;
    private bool _manualTracking;
    public NpcPlannerMode PlannerMode { get; }
    public NpcPolicyMode PolicyMode { get; }

    public long Tick { get; private set; }
    public string LastTechActionStatus { get; private set; } = "No tech action";
    public string LastDirectorActionStatus { get; private set; } = "No director action";
    public int LoadedTechCount => TechTree.Techs.Count;

    public int Width => _world.Width;
    public int Height => _world.Height;
    public int ColonyCount => _world._colonies.Count;

    public SimulationRuntime(int width, int height, int initialPopulation, string technologyFilePath)
        : this(width, height, initialPopulation, technologyFilePath, null)
    {
    }

    public SimulationRuntime(int width, int height, int initialPopulation, string technologyFilePath, RuntimeAiOptions? aiOptions = null)
    {
        var resolvedOptions = aiOptions ?? RuntimeAiOptions.FromEnvironment();
        PlannerMode = resolvedOptions.PlannerMode;
        PolicyMode = resolvedOptions.PolicyMode;
        _world = new World(width, height, initialPopulation, colony => CreateBrain(colony, resolvedOptions));
        _latestAiDebugSnapshot = AiDebugSnapshot.Empty(PlannerMode.ToString(), PolicyMode.ToString());
        TechTree.Load(technologyFilePath);

        if (LoadedTechCount == 0)
        {
            throw new InvalidOperationException("SimulationRuntime started with zero loaded technologies.");
        }
    }

    public void AdvanceTick(float dt)
    {
        _world.Update(dt);
        _directorState.Tick();
        RefreshAiDebugSnapshot();
        Tick++;
    }

    public WorldRenderSnapshot GetSnapshot() => WorldSnapshotBuilder.Build(_world);

    public AiDebugSnapshot GetAiDebugSnapshot() => _latestAiDebugSnapshot;

    public void CycleTrackedNpc(int delta)
    {
        var tracked = GetTrackedDecisions();
        if (tracked.Count == 0)
            return;

        _manualTracking = true;
        _trackedNpcCursor = NormalizeTrackedIndex(_trackedNpcCursor + Math.Sign(delta), tracked.Count);
        RefreshAiDebugSnapshot();
    }

    public void ResetTrackedNpc()
    {
        _manualTracking = false;
        _trackedNpcCursor = 0;
        RefreshAiDebugSnapshot();
    }

    public int NormalizeColonyIndex(int index)
    {
        if (ColonyCount == 0)
            return 0;

        var normalized = index % ColonyCount;
        if (normalized < 0)
            normalized += ColonyCount;
        return normalized;
    }

    public int GetColonyId(int index)
    {
        if (ColonyCount == 0)
            return -1;

        var colony = _world._colonies[NormalizeColonyIndex(index)];
        return colony.Id;
    }

    public IReadOnlyList<string> GetLockedTechNames(int colonyIndex)
    {
        if (ColonyCount == 0)
            return Array.Empty<string>();

        var colony = _world._colonies[NormalizeColonyIndex(colonyIndex)];
        return TechTree.Techs
            .Where(t => !colony.UnlockedTechs.Contains(t.Id))
            .Select(t => t.Name)
            .ToList();
    }

    public void UnlockLockedTechBySlot(int colonyIndex, int slot)
    {
        if (ColonyCount == 0)
        {
            LastTechActionStatus = "No colonies available";
            return;
        }

        var colony = _world._colonies[NormalizeColonyIndex(colonyIndex)];
        var locked = TechTree.Techs
            .Where(t => !colony.UnlockedTechs.Contains(t.Id))
            .ToList();

        if (slot < 0 || slot >= locked.Count)
        {
            LastTechActionStatus = "Invalid tech slot";
            return;
        }

        var selected = locked[slot];
        var result = TechTree.TryUnlock(selected.Id, _world, colony);
        LastTechActionStatus = result.Success
            ? $"Unlocked: {selected.Name}"
            : $"Tech blocked: {selected.Name} ({result.Reason})";
    }

    public JsonObject BuildRefinerySnapshot()
    {
        var colonies = new JsonArray();
        foreach (var colony in _world._colonies)
        {
            colonies.Add(new JsonObject
            {
                ["id"] = colony.Id,
                ["unlockedTechCount"] = colony.UnlockedTechs.Count,
                ["houseCount"] = colony.HouseCount
            });
        }

        return new JsonObject
        {
            ["world"] = new JsonObject
            {
                ["width"] = _world.Width,
                ["height"] = _world.Height,
                ["peopleCount"] = _world._people.Count,
                ["colonyCount"] = _world._colonies.Count,
                ["foodYield"] = _world.FoodYield,
                ["woodYield"] = _world.WoodYield,
                ["stoneYield"] = _world.StoneYield,
                ["ironYield"] = _world.IronYield,
                ["goldYield"] = _world.GoldYield
            },
            ["colonies"] = colonies,
            ["director"] = BuildDirectorSnapshotJson()
        };
    }

    public bool IsKnownTech(string techId) => TechTree.Techs.Any(t => t.Id == techId);

    public bool IsKnownDirectorDirective(string directive)
    {
        return !string.IsNullOrWhiteSpace(directive) && KnownDirectorDirectives.Contains(directive);
    }

    public void UnlockTechForPrimaryColony(string techId)
    {
        if (ColonyCount == 0)
            throw new InvalidOperationException("Cannot unlock tech: world has no colonies.");

        var colony = _world._colonies[0];
        var result = TechTree.TryUnlock(techId, _world, colony);
        if (!result.Success)
            throw new InvalidOperationException($"Cannot unlock tech '{techId}': {result.Reason}");
    }

    public void ApplyStoryBeat(string beatId, string text, long durationTicks)
    {
        if (string.IsNullOrWhiteSpace(beatId))
            throw new InvalidOperationException("Cannot apply story beat: beatId is required.");

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Cannot apply story beat: text is required.");

        if (durationTicks <= 0)
            throw new InvalidOperationException($"Cannot apply story beat '{beatId}': durationTicks must be > 0.");

        var result = _directorState.ApplyStoryBeat(beatId, text, (int)durationTicks, DirectorBeatSeverity.Major);
        if (!result.Success)
            throw new InvalidOperationException($"Cannot apply story beat '{beatId}': {result.Message}");

        LastDirectorActionStatus = result.Message;
    }

    public void ApplyColonyDirective(int colonyId, string directive, long durationTicks)
    {
        if (colonyId < 0 || colonyId >= ColonyCount)
        {
            throw new InvalidOperationException(
                $"Cannot apply colony directive: unknown colonyId '{colonyId}'. colonyCount={ColonyCount}"
            );
        }

        if (string.IsNullOrWhiteSpace(directive))
            throw new InvalidOperationException("Cannot apply colony directive: directive is required.");

        if (!IsKnownDirectorDirective(directive))
            throw new InvalidOperationException($"Cannot apply colony directive: unknown directive '{directive}'.");

        if (durationTicks <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot apply colony directive '{directive}': durationTicks must be > 0."
            );
        }

        var result = _directorState.ApplyDirective(colonyId, directive, (int)durationTicks);
        LastDirectorActionStatus = result.Message;
    }

    private JsonObject BuildDirectorSnapshotJson()
    {
        int livingPopulation = _world._people.Count(person => person.Health > 0f);
        int totalFood = _world._colonies.Sum(colony => colony.Stock.GetValueOrDefault(Resource.Food, 0));
        double foodReservesPct = livingPopulation <= 0
            ? 0d
            : Math.Clamp(totalFood / (double)(livingPopulation * 6), 0d, 1d);
        double moraleAvg = _world._colonies.Count == 0
            ? 0d
            : _world._colonies.Average(colony => colony.Morale);
        double economyOutput = _world._colonies.Count == 0
            ? 1d
            : _world._colonies.Average(colony => colony.ColonyWorkMultiplier);

        var activeBeats = new JsonArray();
        foreach (var beat in _directorState.ActiveBeats)
        {
            activeBeats.Add(new JsonObject
            {
                ["beatId"] = beat.BeatId,
                ["severity"] = beat.Severity.ToString(),
                ["remainingTicks"] = beat.RemainingTicks
            });
        }

        var activeDirectives = new JsonArray();
        foreach (var directive in _directorState.ActiveDirectives)
        {
            activeDirectives.Add(new JsonObject
            {
                ["colonyId"] = directive.ColonyId,
                ["directive"] = directive.Directive,
                ["remainingTicks"] = directive.RemainingTicks
            });
        }

        return new JsonObject
        {
            ["currentTick"] = Tick,
            ["currentSeason"] = _world.CurrentSeason.ToString(),
            ["colonyPopulation"] = livingPopulation,
            ["foodReservesPct"] = foodReservesPct,
            ["moraleAvg"] = moraleAvg,
            ["economyOutput"] = economyOutput,
            ["activeBeats"] = activeBeats,
            ["activeDirectives"] = activeDirectives,
            ["beatCooldownRemainingTicks"] = _directorState.BeatCooldownRemainingTicks,
            ["remainingInfluenceBudget"] = 1.0
        };
    }

    private RuntimeNpcBrain CreateBrain(Colony colony, RuntimeAiOptions options)
    {
        return options.PolicyMode switch
        {
            NpcPolicyMode.FactionMix => CreateFactionPolicyBrain(colony, options),
            NpcPolicyMode.HtnPilot => new RuntimeNpcBrain(NpcPlannerMode.Htn, "HtnPilot"),
            _ => new RuntimeNpcBrain(options.PlannerMode, $"Global:{options.PlannerMode}")
        };
    }

    private static RuntimeNpcBrain CreateFactionPolicyBrain(Colony colony, RuntimeAiOptions options)
    {
        var planner = options.ResolveFactionPlanner(colony.Faction);
        return new RuntimeNpcBrain(planner, $"FactionMix:{colony.Faction}->{planner}");
    }

    private void RefreshAiDebugSnapshot()
    {
        var tracked = GetTrackedDecisions();
        var latest = tracked.FirstOrDefault();

        if (latest == null)
        {
            _latestAiDebugSnapshot = AiDebugSnapshot.Empty(PlannerMode.ToString(), PolicyMode.ToString());
            return;
        }

        if (_manualTracking)
            _trackedNpcCursor = NormalizeTrackedIndex(_trackedNpcCursor, tracked.Count);
        else
            _trackedNpcCursor = 0;

        var selectedIndex = _manualTracking ? _trackedNpcCursor : 0;
        var selected = tracked[selectedIndex];

        if (latest.Sequence > _lastObservedDecisionSequence)
        {
            _lastObservedDecisionSequence = latest.Sequence;
            var summary = $"{latest.Trace.PolicyName} | Goal {latest.Trace.SelectedGoal} -> {latest.Job}";
            _recentAiDecisions.Enqueue(summary);
            while (_recentAiDecisions.Count > 24)
                _recentAiDecisions.Dequeue();
        }

        _latestAiDebugSnapshot = new AiDebugSnapshot(
            HasData: true,
            PlannerMode: selected.Trace.PlannerName,
            PolicyMode: selected.Trace.PolicyName,
            TrackingMode: _manualTracking ? "Manual" : "Latest",
            TrackedNpcIndex: selectedIndex + 1,
            TrackedNpcCount: tracked.Count,
            DecisionSequence: selected.Sequence,
            TrackedColonyId: selected.ColonyId,
            TrackedX: selected.X,
            TrackedY: selected.Y,
            SelectedGoal: selected.Trace.SelectedGoal,
            NextCommand: selected.Job.ToString(),
            PlanLength: selected.Trace.PlanLength,
            PlanCost: selected.Trace.PlanCost,
            ReplanReason: selected.Trace.ReplanReason,
            MethodName: selected.Trace.MethodName,
            GoalScores: selected.Trace.GoalScores
                .OrderByDescending(score => score.Score)
                .Select(score => new AiGoalScoreData(score.GoalName, score.Score, score.IsOnCooldown))
                .ToList(),
            RecentDecisions: _recentAiDecisions.ToList());
    }

    private List<RuntimeAiDecision> GetTrackedDecisions()
    {
        return _world._people
            .Select(person => person.LastAiDecision)
            .Where(decision => decision != null)
            .Select(decision => decision!)
            .OrderByDescending(decision => decision.Sequence)
            .ToList();
    }

    private static int NormalizeTrackedIndex(int index, int count)
    {
        if (count <= 0)
            return 0;

        var normalized = index % count;
        if (normalized < 0)
            normalized += count;
        return normalized;
    }
}
