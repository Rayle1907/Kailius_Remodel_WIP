using Unity.Services.Analytics;
using UnityEngine;

internal abstract class ResearchTelemetryEvent : Unity.Services.Analytics.Event
{
    private static int sequence;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSequence()
    {
        sequence = 0;
    }

    internal static ResearchTelemetryContext CaptureContext(string offerVariant,
        string sceneName, string playthroughId, string runId, string gauntletSessionId = null,
        string experimentVersion = GauntletRunTracker.ExperimentVersion)
    {
        return new ResearchTelemetryContext(++sequence,
            (float)ResearchAnalyticsBootstrap.SessionElapsedSeconds,
            offerVariant ?? "unknown", sceneName ?? string.Empty,
            playthroughId ?? string.Empty, runId ?? string.Empty, gauntletSessionId ?? string.Empty,
            experimentVersion ?? string.Empty);
    }

    protected ResearchTelemetryEvent(string eventName, ResearchTelemetryContext context) : base(eventName)
    {
        SetParameter("schema_version", context.SchemaVersion);
        SetParameter("event_sequence", context.EventSequence);
        SetParameter("time_since_session_start", context.SessionSeconds);
        SetParameter("offer_variant", context.OfferVariant);
        SetParameter("scene_name", context.SceneName);
        SetParameter("playthrough_id", context.PlaythroughId);
        SetParameter("run_id", context.RunId);
        SetParameter("gauntlet_session_id", context.GauntletSessionId);
        SetParameter("experiment_version", context.ExperimentVersion);
    }
}

internal sealed class ResearchTelemetryContext
{
    public int SchemaVersion => 2;
    public int EventSequence { get; }
    public float SessionSeconds { get; }
    public string OfferVariant { get; }
    public string SceneName { get; }
    public string PlaythroughId { get; }
    public string RunId { get; }
    public string GauntletSessionId { get; }
    public string ExperimentVersion { get; }

    public ResearchTelemetryContext(int eventSequence, float sessionSeconds,
        string offerVariant, string sceneName,
        string playthroughId, string runId, string gauntletSessionId = "",
        string experimentVersion = GauntletRunTracker.ExperimentVersion)
    {
        EventSequence = eventSequence;
        SessionSeconds = sessionSeconds;
        OfferVariant = offerVariant;
        SceneName = sceneName;
        PlaythroughId = playthroughId;
        RunId = runId;
        GauntletSessionId = gauntletSessionId;
        ExperimentVersion = experimentVersion;
    }
}


internal abstract class ResearchSurveyEvent : ResearchTelemetryEvent
{
    protected ResearchSurveyEvent(string eventName, ResearchSurveyResponse response)
        : this(eventName, response, ResearchTelemetryEvent.CaptureContext(
            response.OfferVariant, response.SceneName, response.PlaythroughId, response.RunId,
            response.GauntletSessionId, response.ExperimentVersion)) { }

    protected ResearchSurveyEvent(string eventName, ResearchSurveyResponse response,
        ResearchTelemetryContext context) : base(eventName, context)
    {
        SetParameter("survey_id", response.SurveyId);
        SetParameter("survey_version", ResearchSurveyResponse.Version);
    }
}

internal sealed class SurveyInvitationShownEvent : ResearchSurveyEvent
{
    public SurveyInvitationShownEvent(ResearchSurveyResponse response)
        : base("survey_invitation_shown", response) { }

    internal SurveyInvitationShownEvent(ResearchSurveyResponse response, ResearchTelemetryContext context)
        : base("survey_invitation_shown", response, context) { }
}

internal sealed class SurveyInvitationResolvedEvent : ResearchSurveyEvent
{
    public SurveyInvitationResolvedEvent(ResearchSurveyResponse response, bool accepted)
        : base("survey_invitation_resolved", response)
    {
        SetParameter("survey_invitation_response", accepted ? "accepted" : "declined");
    }

    internal SurveyInvitationResolvedEvent(ResearchSurveyResponse response, bool accepted,
        ResearchTelemetryContext context) : base("survey_invitation_resolved", response, context)
    {
        SetParameter("survey_invitation_response", accepted ? "accepted" : "declined");
    }
}

internal sealed class SurveySubmittedEvent : ResearchSurveyEvent
{
    public SurveySubmittedEvent(ResearchSurveyResponse response) : base("survey_submitted", response)
    {
        SetAnswers(response);
    }

    internal SurveySubmittedEvent(ResearchSurveyResponse response, ResearchTelemetryContext context)
        : base("survey_submitted", response, context)
    {
        SetAnswers(response);
    }

    private void SetAnswers(ResearchSurveyResponse response)
    {
        if (response.AnsweredCount == 0) throw new System.ArgumentException("A submission needs at least one answer.", nameof(response));
        for (int index = 0; index < ResearchSurveyResponse.ItemCount; index++)
        {
            int? answer = response.Answer(index);
            if (answer.HasValue) SetParameter(ResearchSurveyResponse.ParameterName(index), answer.Value);
        }
        SetParameter("survey_answered_count", response.AnsweredCount);
        SetParameter("survey_pxi_answered_count", response.PxiAnsweredCount);
        SetParameter("survey_complete", response.Complete);
    }
}

internal sealed class GauntletRunStartedEvent : ResearchTelemetryEvent
{
    public GauntletRunStartedEvent(ResearchTelemetryContext context) : base("gauntlet_run_started", context) { }
    public int OffersShown { set => SetParameter("offers_shown_in_gauntlet", value); }
    public int NextRevivePrice { set => SetParameter("next_revive_price_int", value); }

    public string GauntletId { set => SetParameter("gauntlet_id", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public int Balance { set => SetParameter("premium_currency_balance", value); }
    public string RunStartReason { set => SetParameter("run_start_reason", value); }
}

internal sealed class PlayerDeathEvent : ResearchTelemetryEvent
{
    public PlayerDeathEvent(ResearchTelemetryContext context) : base("player_death", context) { }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public int DeathTotal { set => SetParameter("death_number_total", value); }
    public int DeathInSegment { set => SetParameter("death_number_in_segment", value); }
    public int DeathInGauntletSession { set => SetParameter("death_number_in_gauntlet_session", value); }
    public float SinceLastDeath { set => SetParameter("time_since_last_death", value); }
    public string Cause { set => SetParameter("cause_of_death", value); }
    public int HealthBefore { set => SetParameter("player_health_before_death", value); }
    public int Balance { set => SetParameter("premium_currency_balance", value); }
    public bool OfferEligible { set => SetParameter("offer_eligible", value); }
    public bool OfferShown { set => SetParameter("offer_shown", value); }
    public string AttemptId { set => SetParameter("attempt_id", value); }
}

internal sealed class PlayerFailureEvent : ResearchTelemetryEvent
{
    public PlayerFailureEvent(ResearchTelemetryContext context) : base("player_failure", context) { }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public int DeathTotal { set => SetParameter("death_number_total", value); }
    public int DeathInSegment { set => SetParameter("death_number_in_segment", value); }
    public int DeathInGauntletSession { set => SetParameter("death_number_in_gauntlet_session", value); }
    public string FailureReason { set => SetParameter("failureReason", value); }
    public int HealthBefore { set => SetParameter("player_health_before_failure", value); }
    public int HealthAfter { set => SetParameter("player_health_after_failure", value); }
    public int Balance { set => SetParameter("premium_currency_balance", value); }
    public float SinceLastFailure { set => SetParameter("time_since_last_failure", value); }
    public string AttemptId { set => SetParameter("attempt_id", value); }
}

internal sealed class SegmentAttemptStartedEvent : ResearchTelemetryEvent
{
    public SegmentAttemptStartedEvent(ResearchTelemetryContext context) : base("segment_attempt_started", context) { }
    public string AttemptId { set => SetParameter("attempt_id", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public string SegmentVersion { set => SetParameter("segment_version", value); }
    public string GeometryHash { set => SetParameter("geometry_hash", value); }
    public string StartReason { set => SetParameter("start_reason", value); }
    public int AttemptNumber { set => SetParameter("attempt_number", value); }
    public int Health { set => SetParameter("health", value); }
    public int Defense { set => SetParameter("defense", value); }
    public int AttackDamage { set => SetParameter("attack_damage", value); }
    public float MoveSpeed { set => SetParameter("move_speed", value); }
    public float JumpHeight { set => SetParameter("jump_height", value); }
    public float Gravity { set => SetParameter("gravity", value); }
    public float WallDistance { set => SetParameter("wall_distance", value); }
    public bool WallActive { set => SetParameter("wall_active", value); }
    public int DefeatedRequiredEnemies { set => SetParameter("defeated_required_enemies", value); }
    public int TotalRequiredEnemies { set => SetParameter("total_required_enemies", value); }
}

internal sealed class SegmentAttemptEndedEvent : ResearchTelemetryEvent
{
    public string GeometryHash { set => SetParameter("geometry_hash", value); }
    public SegmentAttemptEndedEvent(ResearchTelemetryContext context) : base("segment_attempt_ended", context) { }
    public string AttemptId { set => SetParameter("attempt_id", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public string SegmentVersion { set => SetParameter("segment_version", value); }
    public string Outcome { set => SetParameter("outcome", value); }
    public string Reason { set => SetParameter("reason", value); }
    public float DurationSeconds { set => SetParameter("duration_seconds", value); }
    public float ProgressX { set => SetParameter("progress_x", value); }
    public float ProgressY { set => SetParameter("progress_y", value); }
}

internal sealed class ReviveOfferShownEvent : ResearchTelemetryEvent
{
    public ReviveOfferShownEvent(ResearchTelemetryContext context) : base("revive_offer_shown", context) { }
    public string OfferId { set => SetParameter("offer_id", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public int DeathTotal { set => SetParameter("death_number_total", value); }
    public int DeathInSegment { set => SetParameter("death_number_in_segment", value); }
    public int Balance { set => SetParameter("premium_currency_balance", value); }
    public int RevivePrice { set => SetParameter("revive_price_int", value); }
    public int OfferNumber { set => SetParameter("offer_number_in_gauntlet", value); }
    public bool CanAfford { set => SetParameter("can_afford", value); }
    public string AttemptId { set => SetParameter("attempt_id", value); }
}

internal sealed class ReviveOfferResolvedEvent : ResearchTelemetryEvent
{
    public ReviveOfferResolvedEvent(ResearchTelemetryContext context) : base("revive_offer_resolved", context) { }
    public string OfferId { set => SetParameter("offer_id", value); }
    public string Response { set => SetParameter("offer_response", value); }
    public float DecisionSeconds { set => SetParameter("time_to_decision", value); }
    public int RevivePrice { set => SetParameter("revive_price_int", value); }
    public int BalanceBefore { set => SetParameter("premium_currency_balance", value); }
    public int BalanceAfter { set => SetParameter("balance_after_decision", value); }
    public string AttemptId { set => SetParameter("attempt_id", value); }
}

internal sealed class ReviveOutcomeObservedEvent : ResearchTelemetryEvent
{
    public ReviveOutcomeObservedEvent(ResearchTelemetryContext context) : base("revive_outcome_observed", context) { }
    public string OfferId { set => SetParameter("offer_id", value); }
    public string Outcome { set => SetParameter("post_decision_outcome", value); }
    public float SecondsAfterDecision { set => SetParameter("seconds_after_decision", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public string AttemptId { set => SetParameter("attempt_id", value); }
}

internal sealed class GauntletRunEndedEvent : ResearchTelemetryEvent
{
    public GauntletRunEndedEvent(ResearchTelemetryContext context) : base("gauntlet_run_ended", context) { }
    public int OffersShown { set => SetParameter("offers_shown_in_gauntlet", value); }
    public int NextRevivePrice { set => SetParameter("next_revive_price_int", value); }
    public string ExitCheckpoint { set => SetParameter("exit_checkpoint_id", value); }
    public int ExitHealth { set => SetParameter("exit_health", value); }
    public int DefeatedRequiredEnemies { set => SetParameter("defeated_required_enemies", value); }
    public int TotalRequiredEnemies { set => SetParameter("total_required_enemies", value); }

    public string GauntletId { set => SetParameter("gauntlet_id", value); }
    public string EndReason { set => SetParameter("run_end_reason", value); }
    public float RunDuration { set => SetParameter("run_duration_seconds", value); }
    public int RunDeaths { set => SetParameter("run_deaths", value); }
    public int AcceptedRevives { set => SetParameter("accepted_revives", value); }
    public int FinalBalance { set => SetParameter("premium_currency_balance", value); }
}
