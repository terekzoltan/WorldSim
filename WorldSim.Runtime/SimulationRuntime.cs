using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using WorldSim.AI;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Effects;

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
    private readonly double _directorDampeningFactor;
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

        _world.EnableDiplomacy = ReadBoolEnv("WORLDSIM_ENABLE_DIPLOMACY", fallback: false);
        _world.EnableCombatPrimitives = ReadBoolEnv("WORLDSIM_ENABLE_COMBAT_PRIMITIVES", fallback: false);
        _world.EnablePredatorHumanAttacks = ReadBoolEnv("WORLDSIM_ENABLE_PREDATOR_ATTACKS", fallback: false);

        _directorDampeningFactor = ReadClampedDoubleEnv("REFINERY_DIRECTOR_DAMPENING", fallback: 1.0);

        if (LoadedTechCount == 0)
        {
            throw new InvalidOperationException("SimulationRuntime started with zero loaded technologies.");
        }
    }

    private static double ReadClampedDoubleEnv(string key, double fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return Math.Clamp(fallback, 0d, 1d);

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return Math.Clamp(fallback, 0d, 1d);

        return Math.Clamp(parsed, 0d, 1d);
    }

    private static bool ReadBoolEnv(string key, bool fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        if (bool.TryParse(raw, out var parsed))
            return parsed;

        raw = raw.Trim();
        if (string.Equals(raw, "1", StringComparison.Ordinal))
            return true;
        if (string.Equals(raw, "0", StringComparison.Ordinal))
            return false;

        return fallback;
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

    public void ApplyStoryBeat(
        string beatId,
        string text,
        long durationTicks,
        IReadOnlyList<DirectorDomainModifierSpec>? effects = null)
    {
        if (string.IsNullOrWhiteSpace(beatId))
            throw new InvalidOperationException("Cannot apply story beat: beatId is required.");

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Cannot apply story beat: text is required.");

        if (durationTicks <= 0)
            throw new InvalidOperationException($"Cannot apply story beat '{beatId}': durationTicks must be > 0.");

        // Idempotence: do not register effects twice.
        if (_directorState.ActiveBeats.Any(beat => string.Equals(beat.BeatId, beatId, StringComparison.Ordinal)))
        {
            LastDirectorActionStatus = $"Story beat '{beatId}' already active (idempotent)";
            return;
        }

        var validatedEffects = new List<(RuntimeDomain domain, double modifier)>();
        if (effects != null)
        {
            foreach (var effect in effects)
            {
                if (effect.DurationTicks != (int)durationTicks)
                {
                    throw new InvalidOperationException(
                        $"Cannot apply story beat '{beatId}': effect durationTicks {effect.DurationTicks} must match beat durationTicks {(int)durationTicks}."
                    );
                }

                var domain = ParseRuntimeDomain(effect.Domain);
                if (effect.Modifier < -0.30d || effect.Modifier > 0.30d)
                {
                    throw new InvalidOperationException(
                        $"Cannot apply story beat '{beatId}': modifier {effect.Modifier} out of bounds [-0.30, +0.30] for domain '{effect.Domain}'."
                    );
                }

                validatedEffects.Add((domain, effect.Modifier));
            }
        }

        var result = _directorState.ApplyStoryBeat(beatId, text, (int)durationTicks, DirectorBeatSeverity.Major);
        if (!result.Success)
            throw new InvalidOperationException($"Cannot apply story beat '{beatId}': {result.Message}");

        foreach (var (domain, modifier) in validatedEffects)
        {
            _world.RegisterDomainModifier(
                sourceId: "beat:" + beatId,
                domain: domain,
                modifier: modifier,
                durationTicks: (int)durationTicks,
                dampeningFactor: _directorDampeningFactor);
        }

        _world.AddExternalEvent("[Director] " + text);

        LastDirectorActionStatus = result.Message;
    }

    public void ApplyColonyDirective(
        int colonyId,
        string directive,
        long durationTicks,
        IReadOnlyList<DirectorGoalBiasSpec>? biases = null)
    {
        if (colonyId < 0 || colonyId >= ColonyCount)
        {
            throw new InvalidOperationException(
                $"Cannot apply colony directive: unknown colonyId '{colonyId}'. colonyCount={ColonyCount}"
            );
        }

        if (string.IsNullOrWhiteSpace(directive))
            throw new InvalidOperationException("Cannot apply colony directive: directive is required.");

        if (durationTicks <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot apply colony directive '{directive}': durationTicks must be > 0."
            );
        }

        var effectiveBiases = (biases != null && biases.Count > 0)
            ? biases
            : BuildDefaultDirectiveBiases(directive);

        var biasSpecs = effectiveBiases
            .Select(bias =>
            {
                if (string.IsNullOrWhiteSpace(bias.GoalCategory))
                    throw new InvalidOperationException("Cannot apply colony directive: goalCategory is required in biases.");
                if (!IsKnownGoalBiasCategory(bias.GoalCategory))
                    throw new InvalidOperationException($"Cannot apply colony directive: unknown goalCategory '{bias.GoalCategory}'.");
                if (bias.Weight < 0d || bias.Weight > 0.50d)
                    throw new InvalidOperationException($"Cannot apply colony directive: bias weight {bias.Weight} out of bounds [0.0, 0.50].");
                if (bias.DurationTicks.HasValue && bias.DurationTicks.Value != (int)durationTicks)
                {
                    throw new InvalidOperationException(
                        $"Cannot apply colony directive '{directive}': bias durationTicks {bias.DurationTicks.Value} must match directive durationTicks {(int)durationTicks}."
                    );
                }
                return new GoalBiasSpec(bias.GoalCategory, bias.Weight);
            })
            .ToList();

        _world.ReplaceGoalBiases(
            sourceId: $"directive:{colonyId}:{directive}",
            colonyId: colonyId,
            biases: biasSpecs,
            durationTicks: (int)durationTicks,
            dampeningFactor: _directorDampeningFactor);

        var result = _directorState.ApplyDirective(colonyId, directive, (int)durationTicks);
        _world.AddExternalEvent($"[Director] Directive: {directive} (C{colonyId}, {durationTicks} ticks)");
        LastDirectorActionStatus = result.Message;
    }

    private static RuntimeDomain ParseRuntimeDomain(string domainRaw)
    {
        if (string.IsNullOrWhiteSpace(domainRaw))
            throw new InvalidOperationException("Domain is required.");

        return domainRaw.Trim().ToLowerInvariant() switch
        {
            "food" => RuntimeDomain.Food,
            "morale" => RuntimeDomain.Morale,
            "economy" => RuntimeDomain.Economy,
            "military" => RuntimeDomain.Military,
            "research" => RuntimeDomain.Research,
            _ => throw new InvalidOperationException($"Unknown domain '{domainRaw}'.")
        };
    }

    private static bool IsKnownGoalBiasCategory(string category)
    {
        var trimmed = category.Trim();
        return string.Equals(trimmed, GoalBiasCategories.Farming, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Gathering, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Building, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Crafting, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Rest, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Social, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Military, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<DirectorGoalBiasSpec> BuildDefaultDirectiveBiases(string directive)
    {
        if (!IsKnownDirectorDirective(directive))
            throw new InvalidOperationException($"Cannot apply colony directive: unknown directive '{directive}'.");

        // Transitional mapping: known directive IDs map to bias compositions.
        return directive switch
        {
            "PrioritizeFood" => new[]
            {
                new DirectorGoalBiasSpec(GoalBiasCategories.Farming, 0.25, null),
                new DirectorGoalBiasSpec(GoalBiasCategories.Gathering, 0.15, null)
            },
            "StabilizeMorale" => new[]
            {
                new DirectorGoalBiasSpec(GoalBiasCategories.Building, 0.20, null),
                new DirectorGoalBiasSpec(GoalBiasCategories.Farming, 0.15, null)
            },
            "BoostIndustry" => new[]
            {
                new DirectorGoalBiasSpec(GoalBiasCategories.Crafting, 0.20, null),
                new DirectorGoalBiasSpec(GoalBiasCategories.Building, 0.12, null),
                new DirectorGoalBiasSpec(GoalBiasCategories.Gathering, 0.08, null)
            },
            _ => Array.Empty<DirectorGoalBiasSpec>()
        };
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

        var activeDomainModifiers = new JsonArray();
        foreach (var modifier in _world.GetActiveDomainModifiers())
        {
            activeDomainModifiers.Add(new JsonObject
            {
                ["sourceId"] = modifier.SourceId,
                ["domain"] = modifier.Domain.ToString().ToLowerInvariant(),
                ["baseModifier"] = modifier.BaseModifier,
                ["effectiveModifier"] = modifier.EffectiveModifier,
                ["remainingTicks"] = modifier.RemainingTicks,
                ["totalDurationTicks"] = modifier.TotalDurationTicks
            });
        }

        var activeGoalBiases = new JsonArray();
        foreach (var colony in _world._colonies)
        {
            foreach (var bias in _world.GetActiveGoalBiases(colony.Id))
            {
                activeGoalBiases.Add(new JsonObject
                {
                    ["colonyId"] = bias.ColonyId,
                    ["sourceId"] = bias.SourceId,
                    ["goalCategory"] = bias.GoalCategory,
                    ["baseWeight"] = bias.BaseWeight,
                    ["effectiveWeight"] = bias.EffectiveWeight,
                    ["remainingTicks"] = bias.RemainingTicks,
                    ["totalDurationTicks"] = bias.TotalDurationTicks,
                    ["isBlendActive"] = bias.IsBlendActive
                });
            }
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
            ["remainingInfluenceBudget"] = 1.0,
            ["dampeningFactor"] = _directorDampeningFactor,
            ["activeDomainModifiers"] = activeDomainModifiers,
            ["activeGoalBiases"] = activeGoalBiases
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
