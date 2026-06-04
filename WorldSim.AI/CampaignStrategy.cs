using System;
using System.Collections.Generic;

namespace WorldSim.AI;

public interface ICampaignStrategist
{
    CampaignStrategyDecision Decide(in CampaignStrategyContext context);
}

public enum CampaignStrategyDecisionKind
{
    HoldDefensivePosture,
    LaunchCampaign,
    AbortCampaign,
    RequestConvoy,
    ReinforceCampaign
}

public enum CampaignStrategyReasonCode
{
    None,
    LaunchDisabled,
    CampaignCapReached,
    HomeDefenseBelowMinimum,
    SupplyReadinessTooLow,
    NoAvailableWarriors,
    NoViableTarget,
    TargetPressureAndAdvantage,
    CampaignOutOfSupply,
    CampaignStalled,
    CampaignNotRecoverable,
    CampaignSupplyLow,
    CampaignAdvantageForReinforcement
}

public readonly record struct CampaignStrategyContext(
    int FactionId,
    int AvailableWarriors,
    int AvailableCarriers,
    int ActiveCampaignCount,
    int MaxActiveCampaigns,
    float HomeDefenseScore,
    float MinimumHomeDefenseScore,
    float SupplyReadiness,
    float VisibleEnemyPressure,
    bool CanLaunchCampaign,
    bool CanAbortCampaign,
    bool CanRequestConvoy,
    bool CanReinforceCampaign,
    IReadOnlyList<CampaignTargetOption>? Targets = null,
    IReadOnlyList<ActiveCampaignStrategyFact>? ActiveCampaigns = null);

public readonly record struct CampaignTargetOption(
    int TargetFactionId,
    int TargetColonyId,
    float PressureScore,
    float AdvantageScore,
    int MinimumWarriors,
    int RequestedWarriors,
    int RequestedCarriers,
    float DistancePenalty = 0f,
    bool IsKnown = true,
    bool HasScoutIntel = false,
    int ScoutIntelTicksSinceRefresh = int.MaxValue,
    float ScoutIntelConfidence = 0f);

public readonly record struct ActiveCampaignStrategyFact(
    int CampaignId,
    int TargetFactionId,
    int TargetColonyId,
    float SupplyReadiness,
    float AdvantageScore,
    int StalledTicks,
    bool IsRecoverable = true);

public readonly record struct CampaignStrategyDecision(
    CampaignStrategyDecisionKind Kind,
    CampaignStrategyReasonCode ReasonCode,
    int TargetFactionId = -1,
    int TargetColonyId = -1,
    int CampaignId = -1,
    int RequestedWarriors = 0,
    int RequestedCarriers = 0,
    float Score = 0f);

public sealed class DefaultCampaignStrategist : ICampaignStrategist
{
    private const float MinimumLaunchScore = 0.55f;
    private const float MinimumLaunchSupplyReadiness = 0.45f;
    private const float ConvoySupplyThreshold = 0.45f;
    private const float CriticalSupplyThreshold = 0.15f;
    private const float ReinforceSupplyThreshold = 0.5f;
    private const float ReinforceAdvantageThreshold = 0.35f;
    private const int CriticalStalledTicks = 600;
    private const float ScoreEpsilon = 0.0001f;

    public CampaignStrategyDecision Decide(in CampaignStrategyContext context)
    {
        if (context.CanAbortCampaign && TrySelectAbort(context, out var abortDecision))
            return abortDecision;

        if (context.CanRequestConvoy && TrySelectConvoyRequest(context, out var convoyDecision))
            return convoyDecision;

        if (context.CanReinforceCampaign && TrySelectReinforcement(context, out var reinforceDecision))
            return reinforceDecision;

        if (!context.CanLaunchCampaign)
            return Hold(CampaignStrategyReasonCode.LaunchDisabled);

        if (context.MaxActiveCampaigns <= 0 || context.ActiveCampaignCount >= context.MaxActiveCampaigns)
            return Hold(CampaignStrategyReasonCode.CampaignCapReached);

        if (context.HomeDefenseScore < context.MinimumHomeDefenseScore)
            return Hold(CampaignStrategyReasonCode.HomeDefenseBelowMinimum);

        if (context.SupplyReadiness < MinimumLaunchSupplyReadiness)
            return Hold(CampaignStrategyReasonCode.SupplyReadinessTooLow);

        if (context.AvailableWarriors <= 0)
            return Hold(CampaignStrategyReasonCode.NoAvailableWarriors);

        return TrySelectLaunch(context, out var launchDecision)
            ? launchDecision
            : Hold(CampaignStrategyReasonCode.NoViableTarget);
    }

    private static bool TrySelectAbort(in CampaignStrategyContext context, out CampaignStrategyDecision decision)
    {
        ActiveCampaignStrategyFact? selected = null;
        var reason = CampaignStrategyReasonCode.None;

        foreach (var campaign in context.ActiveCampaigns ?? Array.Empty<ActiveCampaignStrategyFact>())
        {
            var candidateReason = GetAbortReason(campaign);
            if (candidateReason == CampaignStrategyReasonCode.None)
                continue;

            if (selected == null || IsBetterAbortCandidate(campaign, selected.Value))
            {
                selected = campaign;
                reason = candidateReason;
            }
        }

        if (selected == null)
        {
            decision = default;
            return false;
        }

        var campaignToAbort = selected.Value;
        decision = new CampaignStrategyDecision(
            CampaignStrategyDecisionKind.AbortCampaign,
            reason,
            campaignToAbort.TargetFactionId,
            campaignToAbort.TargetColonyId,
            campaignToAbort.CampaignId,
            Score: CalculateActiveCampaignRisk(campaignToAbort));
        return true;
    }

    private static CampaignStrategyReasonCode GetAbortReason(in ActiveCampaignStrategyFact campaign)
    {
        if (!campaign.IsRecoverable)
            return CampaignStrategyReasonCode.CampaignNotRecoverable;

        if (campaign.SupplyReadiness <= CriticalSupplyThreshold)
            return CampaignStrategyReasonCode.CampaignOutOfSupply;

        return campaign.StalledTicks >= CriticalStalledTicks && campaign.AdvantageScore < 0f
            ? CampaignStrategyReasonCode.CampaignStalled
            : CampaignStrategyReasonCode.None;
    }

    private static bool IsBetterAbortCandidate(
        in ActiveCampaignStrategyFact candidate,
        in ActiveCampaignStrategyFact current)
    {
        var candidateRisk = CalculateActiveCampaignRisk(candidate);
        var currentRisk = CalculateActiveCampaignRisk(current);
        if (candidateRisk > currentRisk + ScoreEpsilon)
            return true;

        if (Math.Abs(candidateRisk - currentRisk) <= ScoreEpsilon)
            return candidate.CampaignId < current.CampaignId;

        return false;
    }

    private static float CalculateActiveCampaignRisk(in ActiveCampaignStrategyFact campaign)
    {
        var supplyRisk = 1f - Math.Clamp(campaign.SupplyReadiness, 0f, 1f);
        var stallRisk = Math.Clamp(campaign.StalledTicks / (float)CriticalStalledTicks, 0f, 1f);
        var advantageRisk = Math.Clamp(-campaign.AdvantageScore, 0f, 1f);
        var recoverableRisk = campaign.IsRecoverable ? 0f : 1f;
        return supplyRisk + stallRisk + advantageRisk + recoverableRisk;
    }

    private static bool TrySelectConvoyRequest(in CampaignStrategyContext context, out CampaignStrategyDecision decision)
    {
        ActiveCampaignStrategyFact? selected = null;

        foreach (var campaign in context.ActiveCampaigns ?? Array.Empty<ActiveCampaignStrategyFact>())
        {
            if (!campaign.IsRecoverable
                || campaign.SupplyReadiness <= CriticalSupplyThreshold
                || campaign.SupplyReadiness >= ConvoySupplyThreshold)
                continue;

            if (selected == null || IsLowerSupplyCandidate(campaign, selected.Value))
                selected = campaign;
        }

        if (selected == null)
        {
            decision = default;
            return false;
        }

        var campaignForConvoy = selected.Value;
        decision = new CampaignStrategyDecision(
            CampaignStrategyDecisionKind.RequestConvoy,
            CampaignStrategyReasonCode.CampaignSupplyLow,
            campaignForConvoy.TargetFactionId,
            campaignForConvoy.TargetColonyId,
            campaignForConvoy.CampaignId,
            Score: 1f - Math.Clamp(campaignForConvoy.SupplyReadiness, 0f, 1f));
        return true;
    }

    private static bool IsLowerSupplyCandidate(
        in ActiveCampaignStrategyFact candidate,
        in ActiveCampaignStrategyFact current)
    {
        if (candidate.SupplyReadiness < current.SupplyReadiness - ScoreEpsilon)
            return true;

        if (Math.Abs(candidate.SupplyReadiness - current.SupplyReadiness) <= ScoreEpsilon)
            return candidate.CampaignId < current.CampaignId;

        return false;
    }

    private static bool TrySelectReinforcement(in CampaignStrategyContext context, out CampaignStrategyDecision decision)
    {
        if (context.AvailableWarriors <= 0 || context.HomeDefenseScore < context.MinimumHomeDefenseScore)
        {
            decision = default;
            return false;
        }

        ActiveCampaignStrategyFact? selected = null;

        foreach (var campaign in context.ActiveCampaigns ?? Array.Empty<ActiveCampaignStrategyFact>())
        {
            if (!campaign.IsRecoverable
                || campaign.SupplyReadiness < ReinforceSupplyThreshold
                || campaign.AdvantageScore < ReinforceAdvantageThreshold)
                continue;

            if (selected == null || IsBetterReinforceCandidate(campaign, selected.Value))
                selected = campaign;
        }

        if (selected == null)
        {
            decision = default;
            return false;
        }

        var campaignToReinforce = selected.Value;
        decision = new CampaignStrategyDecision(
            CampaignStrategyDecisionKind.ReinforceCampaign,
            CampaignStrategyReasonCode.CampaignAdvantageForReinforcement,
            campaignToReinforce.TargetFactionId,
            campaignToReinforce.TargetColonyId,
            campaignToReinforce.CampaignId,
            RequestedWarriors: Math.Min(context.AvailableWarriors, Math.Max(1, context.AvailableWarriors / 3)),
            Score: campaignToReinforce.AdvantageScore);
        return true;
    }

    private static bool IsBetterReinforceCandidate(
        in ActiveCampaignStrategyFact candidate,
        in ActiveCampaignStrategyFact current)
    {
        if (candidate.AdvantageScore > current.AdvantageScore + ScoreEpsilon)
            return true;

        if (Math.Abs(candidate.AdvantageScore - current.AdvantageScore) <= ScoreEpsilon)
            return candidate.CampaignId < current.CampaignId;

        return false;
    }

    private static bool TrySelectLaunch(in CampaignStrategyContext context, out CampaignStrategyDecision decision)
    {
        CampaignTargetOption? selected = null;
        var selectedScore = 0f;

        foreach (var target in context.Targets ?? Array.Empty<CampaignTargetOption>())
        {
            if (!IsLaunchTargetViable(context, target))
                continue;

            var score = CalculateLaunchScore(context, target);
            if (score < MinimumLaunchScore)
                continue;

            if (selected == null || IsBetterLaunchTarget(target, score, selected.Value, selectedScore))
            {
                selected = target;
                selectedScore = score;
            }
        }

        if (selected == null)
        {
            decision = default;
            return false;
        }

        var targetToLaunch = selected.Value;
        decision = new CampaignStrategyDecision(
            CampaignStrategyDecisionKind.LaunchCampaign,
            CampaignStrategyReasonCode.TargetPressureAndAdvantage,
            targetToLaunch.TargetFactionId,
            targetToLaunch.TargetColonyId,
            RequestedWarriors: ClampUnitRequest(targetToLaunch.RequestedWarriors, Math.Max(1, targetToLaunch.MinimumWarriors), context.AvailableWarriors),
            RequestedCarriers: ClampUnitRequest(targetToLaunch.RequestedCarriers, 0, context.AvailableCarriers),
            Score: selectedScore);
        return true;
    }

    private static bool IsLaunchTargetViable(
        in CampaignStrategyContext context,
        in CampaignTargetOption target)
        => target.IsKnown
            && target.TargetFactionId >= 0
            && target.TargetFactionId != context.FactionId
            && target.TargetColonyId >= 0
            && target.MinimumWarriors >= 0
            && context.AvailableWarriors >= Math.Max(1, target.MinimumWarriors);

    private static float CalculateLaunchScore(
        in CampaignStrategyContext context,
        in CampaignTargetOption target)
    {
        var score = (target.PressureScore * 0.55f)
            + (target.AdvantageScore * 0.45f)
            + (context.VisibleEnemyPressure * 0.15f)
            - target.DistancePenalty;
        return Math.Clamp(score, 0f, 1f);
    }

    private static bool IsBetterLaunchTarget(
        in CampaignTargetOption candidate,
        float candidateScore,
        in CampaignTargetOption current,
        float currentScore)
    {
        if (candidateScore > currentScore + ScoreEpsilon)
            return true;

        if (Math.Abs(candidateScore - currentScore) > ScoreEpsilon)
            return false;

        if (candidate.TargetColonyId != current.TargetColonyId)
            return candidate.TargetColonyId < current.TargetColonyId;

        return candidate.TargetFactionId < current.TargetFactionId;
    }

    private static int ClampUnitRequest(int requested, int minimum, int available)
    {
        if (available <= 0)
            return 0;

        var safeMinimum = Math.Clamp(minimum, 0, available);
        var safeRequest = requested <= 0 ? safeMinimum : requested;
        return Math.Clamp(safeRequest, safeMinimum, available);
    }

    private static CampaignStrategyDecision Hold(CampaignStrategyReasonCode reason)
        => new(CampaignStrategyDecisionKind.HoldDefensivePosture, reason);
}
