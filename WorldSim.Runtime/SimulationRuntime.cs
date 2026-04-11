using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using WorldSim.AI;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Diplomacy;
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

    private static readonly HashSet<string> KnownTreatyKinds = new(StringComparer.Ordinal)
    {
        "ceasefire",
        "peace_talks"
    };

    private static readonly HashSet<string> KnownCausalConditionMetrics = new(StringComparer.Ordinal)
    {
        "food_reserves_pct",
        "morale_avg",
        "population",
        "economy_output"
    };

    private static readonly HashSet<string> KnownCausalConditionOperators = new(StringComparer.Ordinal)
    {
        "lt",
        "gt",
        "eq"
    };

    private const int MinCausalWindowTicks = 10;
    private const int MaxCausalWindowTicks = 100;
    private const double FloatingCausalEqTolerance = 0.0001d;

    private readonly World _world;
    private readonly double _directorDampeningFactor;
    private readonly Queue<string> _recentAiDecisions = new();
    private readonly DirectorState _directorState = new();
    private DirectorExecutionState _directorExecutionState = DirectorExecutionState.NotTriggered;
    private int _lastObservedDecisionTick = -1;
    private AiDebugSnapshot _latestAiDebugSnapshot;
    private int _trackedNpcCursor;
    private int _trackedActorId = -1;
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
        _world.EnableSiege = ReadBoolEnv("WORLDSIM_ENABLE_SIEGE", fallback: true);
        _world.EnablePredatorHumanAttacks = ReadBoolEnv("WORLDSIM_ENABLE_PREDATOR_ATTACKS", fallback: false);
        _world.RequireFortificationTechUnlock = true;

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
        EvaluatePendingDirectorCausalChains();
        _directorState.Tick();
        RefreshAiDebugSnapshot();
        Tick++;
    }

    public WorldRenderSnapshot GetSnapshot()
    {
        var snapshot = WorldSnapshotBuilder.Build(_world);
        return snapshot with
        {
            Director = BuildDirectorRenderState()
        };
    }

    private DirectorRenderState BuildDirectorRenderState()
    {
        var activeBeats = _directorState.ActiveBeats
            .Select(beat => new DirectorActiveBeatRenderData(
                beat.BeatId,
                beat.Text,
                beat.Severity.ToString(),
                beat.RemainingTicks,
                beat.TotalTicks))
            .ToList();

        var activeDirectives = _directorState.ActiveDirectives
            .Select(directive => new DirectorActiveDirectiveRenderData(
                directive.ColonyId,
                directive.Directive,
                directive.RemainingTicks,
                directive.TotalTicks))
            .ToList();

        var activeModifiers = _world.GetActiveDomainModifiers()
            .Select(modifier => new DirectorDomainModifierRenderData(
                modifier.SourceId,
                modifier.Domain.ToString(),
                modifier.BaseModifier,
                modifier.EffectiveModifier,
                modifier.RemainingTicks,
                modifier.TotalDurationTicks))
            .ToList();

        var activeBiases = _world._colonies
            .SelectMany(colony => _world.GetActiveGoalBiases(colony.Id)
                .Select(bias => new DirectorGoalBiasRenderData(
                    bias.ColonyId,
                    bias.SourceId,
                    bias.GoalCategory,
                    bias.BaseWeight,
                    bias.EffectiveWeight,
                    bias.RemainingTicks,
                    bias.TotalDurationTicks,
                    bias.IsBlendActive)))
            .OrderBy(bias => bias.ColonyId)
            .ThenBy(bias => bias.GoalCategory)
            .ToList();

        var pendingChains = _directorState.PendingCausalChains
            .Select(chain => new DirectorPendingChainRenderData(
                chain.ParentBeatId,
                chain.Status,
                chain.ConditionSummary,
                chain.FollowUpBeatId,
                chain.FollowUpSummary,
                chain.RemainingWindowTicks,
                chain.TriggerCount,
                chain.LastFailureMessage))
            .ToList();

        var stageMarker = _directorExecutionState.Stage;
        var outputMode = _directorExecutionState.EffectiveOutputMode;
        var outputModeSource = _directorExecutionState.EffectiveOutputModeSource;

        return new DirectorRenderState(
            StageMarker: stageMarker,
            OutputMode: outputMode,
            OutputModeSource: outputModeSource,
            ApplyStatus: _directorExecutionState.ApplyStatus,
            BeatCooldownRemainingTicks: _directorState.BeatCooldownRemainingTicks,
            MajorBeatCooldownRemainingTicks: _directorState.MajorBeatCooldownRemainingTicks,
            EpicBeatCooldownRemainingTicks: _directorState.EpicBeatCooldownRemainingTicks,
            MaxInfluenceBudget: _directorState.MaxInfluenceBudget,
            RemainingInfluenceBudget: _directorState.RemainingInfluenceBudget,
            LastCheckpointBudgetUsed: _directorState.LastCheckpointBudgetUsed,
            LastBudgetCheckpointTick: _directorState.LastBudgetCheckpointTick,
            HasBudgetData: _directorState.HasBudgetData,
            ActiveBeats: activeBeats,
            ActiveDirectives: activeDirectives,
            PendingChains: pendingChains,
            ActiveDomainModifiers: activeModifiers,
            ActiveGoalBiases: activeBiases,
            LastActionStatus: LastDirectorActionStatus);
    }

    public void PrepareDirectorCheckpointBudget(double maxBudget, long tick)
    {
        _directorState.BeginCheckpointBudget(tick, maxBudget);
    }

    public void RecordDirectorCheckpointBudgetUsed(double budgetUsed, long tick)
    {
        _directorState.ApplyCheckpointBudgetUsed(tick, budgetUsed);
    }

    public void SetDirectorExecutionState(
        string effectiveOutputMode,
        string effectiveOutputModeSource,
        string stage,
        long tick,
        bool isDirectorGoal,
        string applyStatus = "applied",
        string? actionStatus = null)
    {
        _directorExecutionState = new DirectorExecutionState(
            EffectiveOutputMode: NormalizeOutputMode(effectiveOutputMode),
            EffectiveOutputModeSource: string.IsNullOrWhiteSpace(effectiveOutputModeSource) ? "unknown" : effectiveOutputModeSource.Trim().ToLowerInvariant(),
            Stage: string.IsNullOrWhiteSpace(stage) ? "not_triggered" : stage.Trim(),
            Tick: tick,
            IsDirectorGoal: isDirectorGoal,
            ApplyStatus: NormalizeApplyStatus(applyStatus));

        if (!string.IsNullOrWhiteSpace(actionStatus))
            LastDirectorActionStatus = actionStatus.Trim();
    }

    private static string NormalizeOutputMode(string? outputMode)
    {
        var normalized = string.IsNullOrWhiteSpace(outputMode) ? "unknown" : outputMode.Trim().ToLowerInvariant();
        return normalized is "unknown" or "both" or "story_only" or "nudge_only" or "off"
            ? normalized
            : "unknown";
    }

    private static string NormalizeApplyStatus(string? applyStatus)
    {
        var normalized = string.IsNullOrWhiteSpace(applyStatus) ? "applied" : applyStatus.Trim().ToLowerInvariant();
        return normalized is "not_triggered" or "applied" or "apply_failed" or "request_failed"
            ? normalized
            : "applied";
    }

    public AiDebugSnapshot GetAiDebugSnapshot() => _latestAiDebugSnapshot;

    public void CycleTrackedNpc(int delta)
    {
        var tracked = GetTrackedDecisions();
        if (tracked.Count == 0)
            return;

        var current = ResolveSelectedDecision(tracked);
        var currentIndex = current == null
            ? 0
            : tracked.FindIndex(decision => decision.ActorId == current.ActorId);
        if (currentIndex < 0)
            currentIndex = 0;

        _manualTracking = true;
        _trackedNpcCursor = NormalizeTrackedIndex(currentIndex + Math.Sign(delta), tracked.Count);
        _trackedActorId = tracked[_trackedNpcCursor].ActorId;
        RefreshAiDebugSnapshot();
    }

    public void ResetTrackedNpc()
    {
        _manualTracking = false;
        _trackedNpcCursor = 0;
        _trackedActorId = -1;
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
        IReadOnlyList<DirectorDomainModifierSpec>? effects = null,
        DirectorCausalChainSpec? causalChain = null)
    {
        var (alreadyActive, validatedEffects, severity) = PrepareStoryBeatApplication(beatId, text, durationTicks, effects);
        if (alreadyActive)
        {
            LastDirectorActionStatus = $"Story beat '{beatId}' already active (idempotent)";
            return;
        }

        var result = _directorState.ApplyStoryBeat(beatId, text, (int)durationTicks, severity);
        if (!result.Success)
            throw new InvalidOperationException($"Cannot apply story beat '{beatId}': {result.Message}");

        if (severity != DirectorBeatSeverity.Minor)
        {
            foreach (var (domain, modifier) in validatedEffects)
            {
                _world.RegisterDomainModifier(
                    sourceId: "beat:" + beatId,
                    domain: domain,
                    modifier: modifier,
                    durationTicks: (int)durationTicks,
                    dampeningFactor: _directorDampeningFactor);
            }
        }

        _world.AddExternalEvent($"[Director:{severity.ToString().ToUpperInvariant()}] {text}");

        if (causalChain.HasValue)
        {
            var validatedChain = ValidateCausalChainSpec(beatId, causalChain.Value);
            _directorState.RegisterCausalChain(beatId, validatedChain, Tick);
        }

        LastDirectorActionStatus = result.Message;
    }

    public void ValidateStoryBeat(
        string beatId,
        string text,
        long durationTicks,
        IReadOnlyList<DirectorDomainModifierSpec>? effects = null,
        DirectorCausalChainSpec? causalChain = null)
    {
        _ = PrepareStoryBeatApplication(beatId, text, durationTicks, effects);
        if (causalChain.HasValue)
            _ = ValidateCausalChainSpec(beatId, causalChain.Value);
    }

    private static DirectorBeatSeverity InferBeatSeverity(int effectCount)
    {
        if (effectCount < 0)
            throw new InvalidOperationException($"Cannot infer beat severity: invalid effect count {effectCount}.");
        if (effectCount > 3)
            throw new InvalidOperationException($"Cannot apply story beat: effect count {effectCount} exceeds S3-A cap (max 3).");

        return effectCount switch
        {
            0 => DirectorBeatSeverity.Minor,
            <= 2 => DirectorBeatSeverity.Major,
            _ => DirectorBeatSeverity.Epic
        };
    }

    public void ApplyColonyDirective(
        int colonyId,
        string directive,
        long durationTicks,
        IReadOnlyList<DirectorGoalBiasSpec>? biases = null)
    {
        var biasSpecs = PrepareColonyDirectiveApplication(colonyId, directive, durationTicks, biases);

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

    public void ValidateColonyDirective(
        int colonyId,
        string directive,
        long durationTicks,
        IReadOnlyList<DirectorGoalBiasSpec>? biases = null)
    {
        _ = PrepareColonyDirectiveApplication(colonyId, directive, durationTicks, biases);
    }

    public void DeclareWar(Faction attacker, Faction defender, string? reason = null)
    {
        ValidateDeclareWar(attacker, defender);

        var changed = _world.DeclareWar(attacker, defender, out var previous, out var current);
        var reasonSuffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" ({reason.Trim()})";
        _world.AddExternalEvent($"[Director] DeclareWar: {attacker} -> {defender}{reasonSuffix}");
        LastDirectorActionStatus = changed
            ? $"Declared war: {attacker} -> {defender} ({previous} -> {current})"
            : $"DeclareWar no-op: {attacker} and {defender} already in {current}";
    }

    public void ValidateDeclareWar(Faction attacker, Faction defender)
    {
        ValidateCampaignRuntimeAvailability();
        ValidateFactionValue(attacker, nameof(attacker));
        ValidateFactionValue(defender, nameof(defender));

        if (attacker == defender)
            throw new InvalidOperationException("declareWar requires attackerFactionId != defenderFactionId.");
    }

    public void ProposeTreaty(Faction proposer, Faction receiver, string treatyKind, string? note = null)
    {
        var normalizedKind = ValidateProposeTreaty(proposer, receiver, treatyKind);

        var changed = _world.ProposeTreaty(proposer, receiver, normalizedKind, out var previous, out var current);
        var noteSuffix = string.IsNullOrWhiteSpace(note) ? string.Empty : $" ({note.Trim()})";
        _world.AddExternalEvent($"[Director] ProposeTreaty: {normalizedKind} {proposer} -> {receiver}{noteSuffix}");
        LastDirectorActionStatus = changed
            ? $"Treaty '{normalizedKind}' applied: {proposer} -> {receiver} ({previous} -> {current})"
            : $"Treaty '{normalizedKind}' no-op: {proposer} -> {receiver} remains {current}";
    }

    public string ValidateProposeTreaty(Faction proposer, Faction receiver, string treatyKind)
    {
        ValidateCampaignRuntimeAvailability();
        ValidateFactionValue(proposer, nameof(proposer));
        ValidateFactionValue(receiver, nameof(receiver));

        if (proposer == receiver)
            throw new InvalidOperationException("proposeTreaty requires proposerFactionId != receiverFactionId.");

        if (string.IsNullOrWhiteSpace(treatyKind))
        {
            throw new InvalidOperationException(
                "proposeTreaty.treatyKind is required. Expected one of: ceasefire, peace_talks.");
        }

        var normalized = treatyKind.Trim().ToLowerInvariant();
        if (!KnownTreatyKinds.Contains(normalized))
        {
            throw new InvalidOperationException(
                $"Unsupported proposeTreaty.treatyKind '{treatyKind}'. Expected one of: ceasefire, peace_talks.");
        }

        return normalized;
    }

    private void ValidateCampaignRuntimeAvailability()
    {
        if (!_world.EnableDiplomacy || !_world.EnableCombatPrimitives)
        {
            throw new InvalidOperationException(
                "Campaign commands require WORLDSIM_ENABLE_DIPLOMACY=true and WORLDSIM_ENABLE_COMBAT_PRIMITIVES=true.");
        }
    }

    private static void ValidateFactionValue(Faction faction, string paramName)
    {
        if (!Enum.IsDefined(typeof(Faction), faction))
        {
            throw new InvalidOperationException($"Invalid faction value for {paramName}: {(int)faction}.");
        }
    }

    private (bool alreadyActive, List<(RuntimeDomain domain, double modifier)> effects, DirectorBeatSeverity severity) PrepareStoryBeatApplication(
        string beatId,
        string text,
        long durationTicks,
        IReadOnlyList<DirectorDomainModifierSpec>? effects)
    {
        if (string.IsNullOrWhiteSpace(beatId))
            throw new InvalidOperationException("Cannot apply story beat: beatId is required.");

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Cannot apply story beat: text is required.");

        if (durationTicks <= 0)
            throw new InvalidOperationException($"Cannot apply story beat '{beatId}': durationTicks must be > 0.");

        var alreadyActive = _directorState.ActiveBeats.Any(beat => string.Equals(beat.BeatId, beatId, StringComparison.Ordinal));

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

        var severity = InferBeatSeverity(validatedEffects.Count);
        if (!alreadyActive)
        {
            if (severity == DirectorBeatSeverity.Major && _directorState.MajorBeatCooldownRemainingTicks > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot apply story beat '{beatId}': Major beat cooldown active ({_directorState.MajorBeatCooldownRemainingTicks} ticks)"
                );
            }

            if (severity == DirectorBeatSeverity.Epic && _directorState.EpicBeatCooldownRemainingTicks > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot apply story beat '{beatId}': Epic beat cooldown active ({_directorState.EpicBeatCooldownRemainingTicks} ticks)"
                );
            }
        }

        return (alreadyActive, validatedEffects, severity);
    }

    private List<GoalBiasSpec> PrepareColonyDirectiveApplication(
        int colonyId,
        string directive,
        long durationTicks,
        IReadOnlyList<DirectorGoalBiasSpec>? biases)
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

        return effectiveBiases
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
        var metrics = BuildDirectorConditionMetrics();

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

        var pendingCausalChains = new JsonArray();
        foreach (var chain in _directorState.PendingCausalChains)
        {
            pendingCausalChains.Add(new JsonObject
            {
                ["parentBeatId"] = chain.ParentBeatId,
                ["status"] = chain.Status,
                ["conditionSummary"] = chain.ConditionSummary,
                ["followUpBeatId"] = chain.FollowUpBeatId,
                ["followUpSummary"] = chain.FollowUpSummary,
                ["remainingWindowTicks"] = chain.RemainingWindowTicks,
                ["triggerCount"] = chain.TriggerCount,
                ["lastFailureMessage"] = chain.LastFailureMessage
            });
        }

        return new JsonObject
        {
            ["currentTick"] = Tick,
            ["currentSeason"] = _world.CurrentSeason.ToString(),
            ["effectiveOutputMode"] = _directorExecutionState.EffectiveOutputMode,
            ["effectiveOutputModeSource"] = _directorExecutionState.EffectiveOutputModeSource,
            ["stage"] = _directorExecutionState.Stage,
            ["colonyPopulation"] = metrics.LivingPopulation,
            ["foodReservesPct"] = metrics.FoodReservesPct,
            ["moraleAvg"] = metrics.MoraleAvg,
            ["economyOutput"] = metrics.EconomyOutput,
            ["activeBeats"] = activeBeats,
            ["activeDirectives"] = activeDirectives,
            ["pendingCausalChains"] = pendingCausalChains,
            ["beatCooldownRemainingTicks"] = _directorState.BeatCooldownRemainingTicks,
            ["maxInfluenceBudget"] = _directorState.MaxInfluenceBudget,
            ["remainingInfluenceBudget"] = _directorState.RemainingInfluenceBudget,
            ["lastCheckpointBudgetUsed"] = _directorState.LastCheckpointBudgetUsed,
            ["lastBudgetCheckpointTick"] = _directorState.LastBudgetCheckpointTick,
            ["dampeningFactor"] = _directorDampeningFactor,
            ["activeDomainModifiers"] = activeDomainModifiers,
            ["activeGoalBiases"] = activeGoalBiases
        };
    }

    private void EvaluatePendingDirectorCausalChains()
    {
        var evaluationTick = Tick + 1;
        var metrics = BuildDirectorConditionMetrics();
        var followUps = _directorState.EvaluatePendingCausalChains(
            evaluationTick,
            condition => EvaluateDirectorCondition(condition, metrics));

        foreach (var trigger in followUps)
        {
            try
            {
                ApplyStoryBeat(
                    trigger.FollowUpBeat.BeatId,
                    trigger.FollowUpBeat.Text,
                    trigger.FollowUpBeat.DurationTicks,
                    trigger.FollowUpBeat.Effects,
                    causalChain: null);
            }
            catch (Exception ex)
            {
                _directorState.MarkCausalChainTriggerFailed(trigger.ParentBeatId, ex.Message);
                LastDirectorActionStatus = $"Causal chain trigger failed for '{trigger.ParentBeatId}': {ex.Message}";
            }
        }
    }

    private static bool EvaluateDirectorCondition(DirectorCausalConditionSpec condition, DirectorConditionMetrics metrics)
    {
        var observed = condition.Metric switch
        {
            "food_reserves_pct" => metrics.FoodReservesPct,
            "morale_avg" => metrics.MoraleAvg,
            "population" => metrics.LivingPopulation,
            "economy_output" => metrics.EconomyOutput,
            _ => throw new InvalidOperationException($"Unknown causal condition metric '{condition.Metric}'.")
        };

        return condition.Operator switch
        {
            "lt" => observed < condition.Threshold,
            "gt" => observed > condition.Threshold,
            "eq" when string.Equals(condition.Metric, "population", StringComparison.Ordinal)
                => observed == condition.Threshold,
            "eq" => Math.Abs(observed - condition.Threshold) <= FloatingCausalEqTolerance,
            _ => throw new InvalidOperationException($"Unknown causal condition operator '{condition.Operator}'.")
        };
    }

    private DirectorCausalChainSpec ValidateCausalChainSpec(string parentBeatId, DirectorCausalChainSpec chain)
    {
        var metric = NormalizeCausalConditionMetric(chain.Condition.Metric);
        var op = NormalizeCausalConditionOperator(chain.Condition.Operator);

        if (chain.WindowTicks < MinCausalWindowTicks || chain.WindowTicks > MaxCausalWindowTicks)
        {
            throw new InvalidOperationException(
                $"Cannot apply causal chain for beat '{parentBeatId}': windowTicks must be in [{MinCausalWindowTicks}, {MaxCausalWindowTicks}]."
            );
        }

        if (chain.MaxTriggers != 1)
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': maxTriggers must be 1 in S7-A.");

        if (double.IsNaN(chain.Condition.Threshold) || double.IsInfinity(chain.Condition.Threshold))
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': condition threshold must be finite.");

        if (string.Equals(metric, "population", StringComparison.Ordinal)
            && string.Equals(op, "eq", StringComparison.Ordinal)
            && Math.Abs(chain.Condition.Threshold - Math.Round(chain.Condition.Threshold)) > FloatingCausalEqTolerance)
        {
            throw new InvalidOperationException(
                $"Cannot apply causal chain for beat '{parentBeatId}': population eq threshold must be an integer value."
            );
        }

        var followUpBeat = ValidateFollowUpBeatSpec(parentBeatId, chain.FollowUpBeat);
        return new DirectorCausalChainSpec(
            new DirectorCausalConditionSpec(metric, op, chain.Condition.Threshold),
            followUpBeat,
            chain.WindowTicks,
            1);
    }

    private DirectorFollowUpBeatSpec ValidateFollowUpBeatSpec(string parentBeatId, DirectorFollowUpBeatSpec followUpBeat)
    {
        if (string.IsNullOrWhiteSpace(followUpBeat.BeatId))
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up beatId is required.");
        if (string.Equals(followUpBeat.BeatId, parentBeatId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up beatId must differ from parent beatId.");
        if (string.IsNullOrWhiteSpace(followUpBeat.Text))
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up text is required.");
        if (followUpBeat.DurationTicks <= 0)
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up durationTicks must be > 0.");

        var effects = followUpBeat.Effects ?? Array.Empty<DirectorDomainModifierSpec>();
        if (effects.Count > 3)
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up effect count {effects.Count} exceeds max 3.");

        foreach (var effect in effects)
        {
            if (effect.DurationTicks != (int)followUpBeat.DurationTicks)
            {
                throw new InvalidOperationException(
                    $"Cannot apply causal chain for beat '{parentBeatId}': follow-up effect durationTicks {effect.DurationTicks} must match follow-up durationTicks {(int)followUpBeat.DurationTicks}."
                );
            }

            _ = ParseRuntimeDomain(effect.Domain);
            if (effect.Modifier < -0.30d || effect.Modifier > 0.30d)
            {
                throw new InvalidOperationException(
                    $"Cannot apply causal chain for beat '{parentBeatId}': follow-up modifier {effect.Modifier} out of bounds [-0.30, +0.30] for domain '{effect.Domain}'."
                );
            }
        }

        return new DirectorFollowUpBeatSpec(
            followUpBeat.BeatId,
            followUpBeat.Text,
            followUpBeat.DurationTicks,
            effects.ToList());
    }

    private static string NormalizeCausalConditionMetric(string metric)
    {
        if (string.IsNullOrWhiteSpace(metric))
            throw new InvalidOperationException("Causal condition metric is required.");

        var normalized = metric.Trim().ToLowerInvariant();
        if (!KnownCausalConditionMetrics.Contains(normalized))
            throw new InvalidOperationException($"Unknown causal condition metric '{metric}'.");

        return normalized;
    }

    private static string NormalizeCausalConditionOperator(string op)
    {
        if (string.IsNullOrWhiteSpace(op))
            throw new InvalidOperationException("Causal condition operator is required.");

        var normalized = op.Trim().ToLowerInvariant();
        if (!KnownCausalConditionOperators.Contains(normalized))
            throw new InvalidOperationException($"Unknown causal condition operator '{op}'.");

        return normalized;
    }

    private DirectorConditionMetrics BuildDirectorConditionMetrics()
    {
        int livingPopulation = _world._people.Count(person => person.Health > 0f);
        int totalFood = _world._colonies.Sum(colony => colony.Stock.GetValueOrDefault(Resource.Food, 0));
        double foodReservesPctNormalized = livingPopulation <= 0
            ? 0d
            : Math.Clamp(totalFood / (double)(livingPopulation * 6), 0d, 1d);
        double moraleAvg = _world._colonies.Count == 0
            ? 0d
            : _world._colonies.Average(colony => colony.Morale);
        double economyOutput = _world._colonies.Count == 0
            ? 1d
            : _world._colonies.Average(colony => colony.ColonyWorkMultiplier);

        return new DirectorConditionMetrics(
            LivingPopulation: livingPopulation,
            FoodReservesPctNormalized: foodReservesPctNormalized,
            FoodReservesPct: foodReservesPctNormalized * 100d,
            MoraleAvg: moraleAvg,
            EconomyOutput: economyOutput);
    }

    private readonly record struct DirectorConditionMetrics(
        int LivingPopulation,
        double FoodReservesPctNormalized,
        double FoodReservesPct,
        double MoraleAvg,
        double EconomyOutput);

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
        var latest = ResolveLatestDecision(tracked);

        if (latest == null)
        {
            _latestAiDebugSnapshot = AiDebugSnapshot.Empty(PlannerMode.ToString(), PolicyMode.ToString());
            return;
        }

        var selectedDecision = ResolveSelectedDecision(tracked) ?? latest;
        var selectedIndex = tracked.FindIndex(decision => decision.ActorId == selectedDecision.ActorId);
        if (selectedIndex < 0)
            selectedIndex = 0;

        _trackedNpcCursor = selectedIndex;
        if (_manualTracking)
            _trackedActorId = tracked[selectedIndex].ActorId;

        var selected = tracked[selectedIndex];

        if (latest.WorldTick > _lastObservedDecisionTick)
        {
            _lastObservedDecisionTick = latest.WorldTick;
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
            TrackedActorId: selected.ActorId,
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
            .OrderBy(decision => decision.ActorId)
            .ToList();
    }

    private RuntimeAiDecision? ResolveSelectedDecision(IReadOnlyList<RuntimeAiDecision> tracked)
    {
        var latest = ResolveLatestDecision(tracked);
        if (!_manualTracking)
            return latest;

        if (_trackedActorId >= 0)
        {
            var byActor = tracked.FirstOrDefault(decision => decision.ActorId == _trackedActorId);
            if (byActor != null)
                return byActor;
        }

        _manualTracking = false;
        _trackedActorId = -1;
        return latest;
    }

    private static RuntimeAiDecision? ResolveLatestDecision(IReadOnlyList<RuntimeAiDecision> tracked)
    {
        return tracked
            .OrderByDescending(decision => decision.WorldTick)
            .ThenByDescending(decision => decision.Sequence)
            .ThenBy(decision => decision.ActorId)
            .FirstOrDefault();
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
