using System;
using Unity.Services.Analytics;
using UnityEngine;

internal static class ResearchAnalytics
{
    public static bool RecordRunStarted(GauntletRunStartedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordDeath(PlayerDeathEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordFailure(PlayerFailureEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordOfferShown(ReviveOfferShownEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordOfferResolved(ReviveOfferResolvedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordOutcome(ReviveOutcomeObservedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordRunEnded(GauntletRunEndedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordSurveyInvitationShown(SurveyInvitationShownEvent analyticsEvent) => RecordSurvey(analyticsEvent);
    public static bool RecordSurveyInvitationResolved(SurveyInvitationResolvedEvent analyticsEvent) => RecordSurvey(analyticsEvent);
    public static bool RecordSurveySubmitted(SurveySubmittedEvent analyticsEvent) => RecordSurvey(analyticsEvent);

    private static bool RecordSurvey(ResearchSurveyEvent analyticsEvent)
    {
        return ResearchAnalyticsBootstrap.HasAnalyticsConsent && Record(analyticsEvent);
    }

    private static bool Record(Unity.Services.Analytics.Event analyticsEvent)
    {
        if (analyticsEvent == null
            || !ResearchAnalyticsBootstrap.IsReady
            || ResearchAnalyticsBootstrap.IsShuttingDown)
        {
            return false;
        }

        try
        {
            AnalyticsService.Instance.RecordEvent(analyticsEvent);
            return true;
        }
        catch (Unity.Services.Core.ServicesInitializationException exception)
        {
            if (!ResearchAnalyticsBootstrap.IsShuttingDown && Application.isPlaying)
            {
                Debug.LogError($"Research Analytics lost its initialized service: {exception}");
            }
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Research Analytics could not record an event: {exception}");
            return false;
        }
    }
}
