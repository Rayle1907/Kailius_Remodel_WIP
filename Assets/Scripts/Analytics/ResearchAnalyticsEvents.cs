using Unity.Services.Analytics;

internal sealed class GauntletRunStartedEvent : Unity.Services.Analytics.Event
{
    public GauntletRunStartedEvent() : base("gauntlet_run_started") { }
    public string RunId { set => SetParameter("run_id", value); }
    public string GauntletId { set => SetParameter("gauntlet_id", value); }
    public string SceneName { set => SetParameter("scene_name", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public int Balance { set => SetParameter("premium_currency_balance", value); }
    public string Variant { set => SetParameter("offer_variant", value); }
    public string ExperimentVersion { set => SetParameter("experiment_version", value); }
}

internal sealed class PlayerDeathEvent : Unity.Services.Analytics.Event
{
    public PlayerDeathEvent() : base("player_death") { }
    public string RunId { set => SetParameter("run_id", value); }
    public string SceneName { set => SetParameter("scene_name", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public int DeathTotal { set => SetParameter("death_number_total", value); }
    public int DeathInSegment { set => SetParameter("death_number_in_segment", value); }
    public float SessionSeconds { set => SetParameter("time_since_session_start", value); }
    public float SinceLastDeath { set => SetParameter("time_since_last_death", value); }
    public string Cause { set => SetParameter("cause_of_death", value); }
    public int HealthBefore { set => SetParameter("player_health_before_death", value); }
    public int Balance { set => SetParameter("premium_currency_balance", value); }
    public bool OfferEligible { set => SetParameter("offer_eligible", value); }
    public bool OfferShown { set => SetParameter("offer_shown", value); }
    public string Variant { set => SetParameter("offer_variant", value); }
    public string ExperimentVersion { set => SetParameter("experiment_version", value); }
}

internal sealed class ReviveOfferShownEvent : Unity.Services.Analytics.Event
{
    public ReviveOfferShownEvent() : base("revive_offer_shown") { }
    public string RunId { set => SetParameter("run_id", value); }
    public string OfferId { set => SetParameter("offer_id", value); }
    public string SceneName { set => SetParameter("scene_name", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public int DeathTotal { set => SetParameter("death_number_total", value); }
    public int DeathInSegment { set => SetParameter("death_number_in_segment", value); }
    public int Balance { set => SetParameter("premium_currency_balance", value); }
    public int RevivePrice { set => SetParameter("revive_price", value); }
    public int OfferNumber { set => SetParameter("offer_number_in_gauntlet", value); }
    public bool CanAfford { set => SetParameter("can_afford", value); }
    public string Variant { set => SetParameter("offer_variant", value); }
    public string ExperimentVersion { set => SetParameter("experiment_version", value); }
}

internal sealed class ReviveOfferResolvedEvent : Unity.Services.Analytics.Event
{
    public ReviveOfferResolvedEvent() : base("revive_offer_resolved") { }
    public string RunId { set => SetParameter("run_id", value); }
    public string OfferId { set => SetParameter("offer_id", value); }
    public string Response { set => SetParameter("offer_response", value); }
    public float DecisionSeconds { set => SetParameter("time_to_decision", value); }
    public int RevivePrice { set => SetParameter("revive_price", value); }
    public int BalanceBefore { set => SetParameter("premium_currency_balance", value); }
    public int BalanceAfter { set => SetParameter("balance_after_decision", value); }
    public string Variant { set => SetParameter("offer_variant", value); }
    public string ExperimentVersion { set => SetParameter("experiment_version", value); }
}

internal sealed class ReviveOutcomeObservedEvent : Unity.Services.Analytics.Event
{
    public ReviveOutcomeObservedEvent() : base("revive_outcome_observed") { }
    public string RunId { set => SetParameter("run_id", value); }
    public string OfferId { set => SetParameter("offer_id", value); }
    public string Outcome { set => SetParameter("post_decision_outcome", value); }
    public float SecondsAfterDecision { set => SetParameter("seconds_after_decision", value); }
    public string SceneName { set => SetParameter("scene_name", value); }
    public string SegmentId { set => SetParameter("level_segment_id", value); }
    public string Variant { set => SetParameter("offer_variant", value); }
    public string ExperimentVersion { set => SetParameter("experiment_version", value); }
}

internal sealed class GauntletRunEndedEvent : Unity.Services.Analytics.Event
{
    public GauntletRunEndedEvent() : base("gauntlet_run_ended") { }
    public string RunId { set => SetParameter("run_id", value); }
    public string GauntletId { set => SetParameter("gauntlet_id", value); }
    public string EndReason { set => SetParameter("run_end_reason", value); }
    public float RunDuration { set => SetParameter("run_duration_seconds", value); }
    public int RunDeaths { set => SetParameter("run_deaths", value); }
    public int AcceptedRevives { set => SetParameter("accepted_revives", value); }
    public int FinalBalance { set => SetParameter("premium_currency_balance", value); }
    public string Variant { set => SetParameter("offer_variant", value); }
    public string ExperimentVersion { set => SetParameter("experiment_version", value); }
}
