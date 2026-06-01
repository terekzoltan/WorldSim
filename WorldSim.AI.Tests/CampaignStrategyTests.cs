using System.Collections.Generic;
using WorldSim.AI;
using Xunit;

namespace WorldSim.AI.Tests;

public class CampaignStrategyTests
{
    [Fact]
    public void DefaultStrategist_Holds_WhenLaunchCapabilityIsDisabled()
    {
        var strategist = new DefaultCampaignStrategist();

        var decision = strategist.Decide(CreateContext(
            canLaunchCampaign: false,
            targets: new[] { CreateTarget() }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, decision.Kind);
        Assert.Equal(CampaignStrategyReasonCode.LaunchDisabled, decision.ReasonCode);
    }

    [Fact]
    public void DefaultStrategist_Holds_WhenHomeDefenseFallsBelowMinimum()
    {
        var strategist = new DefaultCampaignStrategist();

        var decision = strategist.Decide(CreateContext(
            homeDefenseScore: 18f,
            minimumHomeDefenseScore: 30f,
            targets: new[] { CreateTarget() }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, decision.Kind);
        Assert.Equal(CampaignStrategyReasonCode.HomeDefenseBelowMinimum, decision.ReasonCode);
    }

    [Fact]
    public void DefaultStrategist_Holds_WhenActiveCampaignCapIsReached()
    {
        var strategist = new DefaultCampaignStrategist();

        var decision = strategist.Decide(CreateContext(
            activeCampaignCount: 2,
            maxActiveCampaigns: 2,
            targets: new[] { CreateTarget() }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, decision.Kind);
        Assert.Equal(CampaignStrategyReasonCode.CampaignCapReached, decision.ReasonCode);
    }

    [Fact]
    public void DefaultStrategist_Holds_WhenSupplyReadinessIsTooLow()
    {
        var strategist = new DefaultCampaignStrategist();

        var decision = strategist.Decide(CreateContext(
            supplyReadiness: 0.3f,
            targets: new[] { CreateTarget() }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, decision.Kind);
        Assert.Equal(CampaignStrategyReasonCode.SupplyReadinessTooLow, decision.ReasonCode);
    }

    [Fact]
    public void DefaultStrategist_LaunchesHighestPressureAdvantageTarget()
    {
        var strategist = new DefaultCampaignStrategist();
        var lowValueTarget = CreateTarget(
            targetFactionId: 2,
            targetColonyId: 20,
            pressureScore: 0.35f,
            advantageScore: 0.35f);
        var highValueTarget = CreateTarget(
            targetFactionId: 3,
            targetColonyId: 30,
            pressureScore: 0.85f,
            advantageScore: 0.75f);

        var decision = strategist.Decide(CreateContext(
            targets: new[] { lowValueTarget, highValueTarget }));

        Assert.Equal(CampaignStrategyDecisionKind.LaunchCampaign, decision.Kind);
        Assert.Equal(CampaignStrategyReasonCode.TargetPressureAndAdvantage, decision.ReasonCode);
        Assert.Equal(3, decision.TargetFactionId);
        Assert.Equal(30, decision.TargetColonyId);
        Assert.True(decision.Score > 0.75f);
    }

    [Fact]
    public void DefaultStrategist_LaunchTieBreaksByTargetColonyThenFaction()
    {
        var strategist = new DefaultCampaignStrategist();
        var higherColony = CreateTarget(targetFactionId: 1, targetColonyId: 40);
        var sameColonyHigherFaction = CreateTarget(targetFactionId: 4, targetColonyId: 10);
        var sameColonyLowerFaction = CreateTarget(targetFactionId: 2, targetColonyId: 10);

        var decision = strategist.Decide(CreateContext(
            targets: new[] { higherColony, sameColonyHigherFaction, sameColonyLowerFaction }));

        Assert.Equal(CampaignStrategyDecisionKind.LaunchCampaign, decision.Kind);
        Assert.Equal(2, decision.TargetFactionId);
        Assert.Equal(10, decision.TargetColonyId);
    }

    [Fact]
    public void DefaultStrategist_ClampsLaunchCompositionToAvailableUnits()
    {
        var strategist = new DefaultCampaignStrategist();
        var target = CreateTarget(
            minimumWarriors: 2,
            requestedWarriors: 10,
            requestedCarriers: 5);

        var decision = strategist.Decide(CreateContext(
            availableWarriors: 3,
            availableCarriers: 1,
            targets: new[] { target }));

        Assert.Equal(CampaignStrategyDecisionKind.LaunchCampaign, decision.Kind);
        Assert.Equal(3, decision.RequestedWarriors);
        Assert.Equal(1, decision.RequestedCarriers);
    }

    [Fact]
    public void DefaultStrategist_RejectsLaunchTarget_WhenMinimumWarriorsAreUnavailable()
    {
        var strategist = new DefaultCampaignStrategist();
        var target = CreateTarget(minimumWarriors: 4, requestedWarriors: 6);

        var decision = strategist.Decide(CreateContext(
            availableWarriors: 3,
            targets: new[] { target }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, decision.Kind);
        Assert.Equal(CampaignStrategyReasonCode.NoViableTarget, decision.ReasonCode);
    }

    [Fact]
    public void DefaultStrategist_RejectsLaunchTarget_WhenTargetFactionMatchesSelf()
    {
        var strategist = new DefaultCampaignStrategist();
        var target = CreateTarget(targetFactionId: 0);

        var decision = strategist.Decide(CreateContext(
            factionId: 0,
            targets: new[] { target }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, decision.Kind);
        Assert.Equal(CampaignStrategyReasonCode.NoViableTarget, decision.ReasonCode);
    }

    [Fact]
    public void DefaultStrategist_LaunchRequestsAtLeastOneWarrior_WhenTargetMinimumIsZero()
    {
        var strategist = new DefaultCampaignStrategist();
        var target = CreateTarget(minimumWarriors: 0, requestedWarriors: 0);

        var decision = strategist.Decide(CreateContext(
            availableWarriors: 3,
            targets: new[] { target }));

        Assert.Equal(CampaignStrategyDecisionKind.LaunchCampaign, decision.Kind);
        Assert.Equal(1, decision.RequestedWarriors);
    }

    [Fact]
    public void DefaultStrategist_AbortCampaignIsCapabilityGated()
    {
        var strategist = new DefaultCampaignStrategist();
        var campaign = CreateActiveCampaign(supplyReadiness: 0.05f);

        var disabled = strategist.Decide(CreateContext(
            canLaunchCampaign: false,
            canAbortCampaign: false,
            activeCampaigns: new[] { campaign }));
        var enabled = strategist.Decide(CreateContext(
            canLaunchCampaign: false,
            canAbortCampaign: true,
            activeCampaigns: new[] { campaign }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, disabled.Kind);
        Assert.Equal(CampaignStrategyReasonCode.LaunchDisabled, disabled.ReasonCode);
        Assert.Equal(CampaignStrategyDecisionKind.AbortCampaign, enabled.Kind);
        Assert.Equal(CampaignStrategyReasonCode.CampaignOutOfSupply, enabled.ReasonCode);
        Assert.Equal(campaign.CampaignId, enabled.CampaignId);
    }

    [Fact]
    public void DefaultStrategist_RequestConvoyIsCapabilityGated()
    {
        var strategist = new DefaultCampaignStrategist();
        var campaign = CreateActiveCampaign(supplyReadiness: 0.25f);

        var disabled = strategist.Decide(CreateContext(
            canLaunchCampaign: false,
            canRequestConvoy: false,
            activeCampaigns: new[] { campaign }));
        var enabled = strategist.Decide(CreateContext(
            canLaunchCampaign: false,
            canRequestConvoy: true,
            activeCampaigns: new[] { campaign }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, disabled.Kind);
        Assert.Equal(CampaignStrategyReasonCode.LaunchDisabled, disabled.ReasonCode);
        Assert.Equal(CampaignStrategyDecisionKind.RequestConvoy, enabled.Kind);
        Assert.Equal(CampaignStrategyReasonCode.CampaignSupplyLow, enabled.ReasonCode);
        Assert.Equal(campaign.CampaignId, enabled.CampaignId);
    }

    [Fact]
    public void DefaultStrategist_ReinforceCampaignIsCapabilityGated()
    {
        var strategist = new DefaultCampaignStrategist();
        var campaign = CreateActiveCampaign(
            supplyReadiness: 0.8f,
            advantageScore: 0.7f);

        var disabled = strategist.Decide(CreateContext(
            canLaunchCampaign: false,
            canReinforceCampaign: false,
            activeCampaigns: new[] { campaign }));
        var enabled = strategist.Decide(CreateContext(
            availableWarriors: 6,
            canLaunchCampaign: false,
            canReinforceCampaign: true,
            activeCampaigns: new[] { campaign }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, disabled.Kind);
        Assert.Equal(CampaignStrategyReasonCode.LaunchDisabled, disabled.ReasonCode);
        Assert.Equal(CampaignStrategyDecisionKind.ReinforceCampaign, enabled.Kind);
        Assert.Equal(CampaignStrategyReasonCode.CampaignAdvantageForReinforcement, enabled.ReasonCode);
        Assert.Equal(campaign.CampaignId, enabled.CampaignId);
        Assert.Equal(2, enabled.RequestedWarriors);
    }

    [Fact]
    public void DefaultStrategist_HoldsReinforcement_WhenHomeDefenseFallsBelowMinimum()
    {
        var strategist = new DefaultCampaignStrategist();
        var campaign = CreateActiveCampaign(
            supplyReadiness: 0.8f,
            advantageScore: 0.7f);

        var decision = strategist.Decide(CreateContext(
            homeDefenseScore: 18f,
            minimumHomeDefenseScore: 30f,
            canReinforceCampaign: true,
            activeCampaigns: new[] { campaign },
            targets: new[] { CreateTarget() }));

        Assert.Equal(CampaignStrategyDecisionKind.HoldDefensivePosture, decision.Kind);
        Assert.Equal(CampaignStrategyReasonCode.HomeDefenseBelowMinimum, decision.ReasonCode);
    }

    private static CampaignStrategyContext CreateContext(
        int factionId = 0,
        int availableWarriors = 12,
        int availableCarriers = 4,
        int activeCampaignCount = 0,
        int maxActiveCampaigns = 2,
        float homeDefenseScore = 80f,
        float minimumHomeDefenseScore = 30f,
        float supplyReadiness = 0.8f,
        float visibleEnemyPressure = 0.2f,
        bool canLaunchCampaign = true,
        bool canAbortCampaign = false,
        bool canRequestConvoy = false,
        bool canReinforceCampaign = false,
        IReadOnlyList<CampaignTargetOption>? targets = null,
        IReadOnlyList<ActiveCampaignStrategyFact>? activeCampaigns = null)
        => new(
            factionId,
            availableWarriors,
            availableCarriers,
            activeCampaignCount,
            maxActiveCampaigns,
            homeDefenseScore,
            minimumHomeDefenseScore,
            supplyReadiness,
            visibleEnemyPressure,
            canLaunchCampaign,
            canAbortCampaign,
            canRequestConvoy,
            canReinforceCampaign,
            targets,
            activeCampaigns);

    private static CampaignTargetOption CreateTarget(
        int targetFactionId = 1,
        int targetColonyId = 10,
        float pressureScore = 0.7f,
        float advantageScore = 0.7f,
        int minimumWarriors = 3,
        int requestedWarriors = 6,
        int requestedCarriers = 2,
        float distancePenalty = 0f,
        bool isKnown = true)
        => new(
            targetFactionId,
            targetColonyId,
            pressureScore,
            advantageScore,
            minimumWarriors,
            requestedWarriors,
            requestedCarriers,
            distancePenalty,
            isKnown);

    private static ActiveCampaignStrategyFact CreateActiveCampaign(
        int campaignId = 100,
        int targetFactionId = 1,
        int targetColonyId = 10,
        float supplyReadiness = 0.8f,
        float advantageScore = 0.2f,
        int stalledTicks = 0,
        bool isRecoverable = true)
        => new(
            campaignId,
            targetFactionId,
            targetColonyId,
            supplyReadiness,
            advantageScore,
            stalledTicks,
            isRecoverable);
}
