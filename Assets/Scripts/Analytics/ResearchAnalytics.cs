using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Services.Analytics;
using UnityEngine;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

internal static class ResearchAnalytics
{
    private static readonly Queue<Unity.Services.Analytics.Event> pendingEvents = new Queue<Unity.Services.Analytics.Event>();

    internal static int PendingEventCount => pendingEvents.Count;
    internal static Func<Unity.Services.Analytics.Event, bool> RecorderOverride { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        pendingEvents.Clear();
        RecorderOverride = null;
    }

    public static bool RecordRunStarted(GauntletRunStartedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordDeath(PlayerDeathEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordFailure(PlayerFailureEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordOfferShown(ReviveOfferShownEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordOfferResolved(ReviveOfferResolvedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordOutcome(ReviveOutcomeObservedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordRunEnded(GauntletRunEndedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordAttemptStarted(SegmentAttemptStartedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordAttemptEnded(SegmentAttemptEndedEvent analyticsEvent) => Record(analyticsEvent);
    public static bool RecordSurveyInvitationShown(SurveyInvitationShownEvent analyticsEvent) => RecordSurvey(analyticsEvent);
    public static bool RecordSurveyInvitationResolved(SurveyInvitationResolvedEvent analyticsEvent) => RecordSurvey(analyticsEvent);
    public static bool RecordSurveySubmitted(SurveySubmittedEvent analyticsEvent) => RecordSurvey(analyticsEvent);

    private static bool RecordSurvey(ResearchSurveyEvent analyticsEvent)
    {
        return ResearchAnalyticsBootstrap.HasAnalyticsConsent && Record(analyticsEvent);
    }

    private static bool Record(Unity.Services.Analytics.Event analyticsEvent)
    {
        if (analyticsEvent == null || ResearchAnalyticsBootstrap.IsShuttingDown
            || !ResearchAnalyticsBootstrap.HasAnalyticsConsent)
        {
            return false;
        }

        if (!ResearchAnalyticsBootstrap.IsReady)
        {
            pendingEvents.Enqueue(analyticsEvent);
            return true;
        }

        if (!FlushPendingEvents() || !Send(analyticsEvent))
        {
            pendingEvents.Enqueue(analyticsEvent);
        }
        return true;
    }

    internal static bool FlushPendingEvents()
    {
        if (ResearchAnalyticsBootstrap.IsShuttingDown || !ResearchAnalyticsBootstrap.IsReady) return false;
        if (!ResearchAnalyticsBootstrap.HasAnalyticsConsent)
        {
            DiscardPendingEvents();
            return false;
        }

        while (pendingEvents.Count > 0)
        {
            Unity.Services.Analytics.Event analyticsEvent = pendingEvents.Peek();
            if (!Send(analyticsEvent)) return false;
            pendingEvents.Dequeue();
        }
        return true;
    }

    internal static void DiscardPendingEvents() => pendingEvents.Clear();

    private static bool Send(Unity.Services.Analytics.Event analyticsEvent)
    {
        try
        {
            if (RecorderOverride != null) return RecorderOverride(analyticsEvent);
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
