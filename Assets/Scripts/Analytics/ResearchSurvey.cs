using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ResearchSurvey : MonoBehaviour
{
    private const string EligibleKey = "research_survey_eligible_playthrough_v1";
    private const string SubmittedKey = "research_survey_submitted_playthrough_v1";
    private static ResearchSurveyResponse activeResponse;
    private bool applicationPaused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        activeResponse = null;
    }

    private void Update()
    {
        if (applicationPaused || Time.timeScale <= 0f || ResearchSurveyPrompt.IsOpen) return;
        GauntletRunTracker tracker = GauntletRunTracker.Instance;
        if (tracker == null || !tracker.IsCurrentSceneGauntlet) return;
        string playthroughId = ResearchPlayerState.PlaythroughId;
        if (PlayerPrefs.GetString(SubmittedKey) == playthroughId
            || PlayerPrefs.GetString(EligibleKey) == playthroughId) return;
        PlayerPrefs.SetString(EligibleKey, playthroughId);
        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool paused) => applicationPaused = paused;

    public static bool TryShowInvitation(Action onQuit, TMPro.TMP_FontAsset font)
    {
        if (ResearchSurveyPrompt.IsOpen) return true;
        if (!ResearchAnalyticsBootstrap.HasAnalyticsConsent || !ResearchAnalyticsBootstrap.IsReady
            || ResearchAnalyticsBootstrap.IsShuttingDown) return false;
        GauntletRunTracker tracker = GauntletRunTracker.Instance;
        if (tracker == null || !tracker.IsCurrentSceneGauntlet) return false;
        string playthroughId = ResearchPlayerState.PlaythroughId;
        if (PlayerPrefs.GetString(EligibleKey) != playthroughId
            || PlayerPrefs.GetString(SubmittedKey) == playthroughId) return false;

        ResearchSurveyResponse response = new ResearchSurveyResponse(playthroughId,
            SceneManager.GetActiveScene().name, ResearchPlayerState.OfferVariant,
            GauntletRunTracker.ExperimentVersion, ResearchPlayerState.PlayerId,
            tracker.CurrentRunId, tracker.CurrentGauntletSessionId);
        if (!ResearchSurveyPrompt.Show(response, onQuit, font)) return false;
        activeResponse = response;
        PlayerPrefs.DeleteKey(EligibleKey);
        PlayerPrefs.Save();
        ResearchAnalytics.RecordSurveyInvitationShown(new SurveyInvitationShownEvent(response));
        return true;
    }

    public static bool Submit(ResearchSurveyResponse response)
    {
        if (response == null || response.AnsweredCount == 0
            || !ResearchAnalyticsBootstrap.HasAnalyticsConsent
            || response.AnalyticsSuppressed
            || PlayerPrefs.GetString(SubmittedKey) == response.PlaythroughId) return false;
        if (!ResearchAnalytics.RecordSurveySubmitted(new SurveySubmittedEvent(response))) return false;
        PlayerPrefs.SetString(SubmittedKey, response.PlaythroughId);
        PlayerPrefs.Save();
        if (activeResponse == response) activeResponse = null;
        return true;
    }

    public static bool ResolveInvitation(ResearchSurveyResponse response, bool accepted)
    {
        if (response == null || response.AnalyticsSuppressed || !ResearchAnalyticsBootstrap.HasAnalyticsConsent)
        {
            return false;
        }

        bool recorded = ResearchAnalytics.RecordSurveyInvitationResolved(
            new SurveyInvitationResolvedEvent(response, accepted));
        if (!accepted && activeResponse == response) activeResponse = null;
        return recorded;
    }

    public static void SuppressPendingAnalytics()
    {
        if (activeResponse != null) activeResponse.SuppressAnalytics();
    }
}
