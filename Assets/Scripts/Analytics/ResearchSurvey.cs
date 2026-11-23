using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ResearchSurvey : MonoBehaviour
{
    private const string EligibleKey = "research_survey_eligible_playthrough_v1";
    private const string SubmittedKey = "research_survey_submitted_playthrough_v1";
    private const string ClearInvitationKey = "research_survey_clear_invitation_playthrough_v1";
    private const string InvitationHistoryKey = "research_survey_invitation_history_v1";
    private const string FirstClearMilestone = "first_clear";
    private const string MenuExitMilestone = "gauntlet_menu_exit";
    private static ResearchSurveyResponse activeResponse;
    private static Scene pendingClearScene;
    private static TMP_FontAsset pendingClearFont;
    private static int clearFrame;
    private bool applicationPaused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        activeResponse = null;
        pendingClearScene = default;
        pendingClearFont = null;
    }

    public static void QueueClearInvitation(Scene scene, TMP_FontAsset font)
    {
        pendingClearScene = scene;
        pendingClearFont = font;
        clearFrame = Time.frameCount;
    }

    private void Update()
    {
        if (pendingClearScene.IsValid() && pendingClearScene != SceneManager.GetActiveScene())
        {
            pendingClearScene = default;
            pendingClearFont = null;
        }
        if (applicationPaused || Time.timeScale <= 0f || ResearchSurveyPrompt.IsOpen) return;
        GauntletRunTracker tracker = GauntletRunTracker.Instance;
        if (tracker == null || !tracker.IsCurrentSceneGauntlet) return;
        string playthroughId = ResearchPlayerState.PlaythroughId;
        if (PlayerPrefs.GetString(SubmittedKey) != playthroughId
            && PlayerPrefs.GetString(EligibleKey) != playthroughId)
        {
            PlayerPrefs.SetString(EligibleKey, playthroughId);
            PlayerPrefs.Save();
        }

        if (!pendingClearScene.IsValid() || Time.frameCount == clearFrame) return;
        TMP_FontAsset font = pendingClearFont;
        pendingClearScene = default;
        pendingClearFont = null;
        TryShowInvitation(FirstClearMilestone, null, font, false);
    }

    private void OnApplicationPause(bool paused) => applicationPaused = paused;

    public static bool TryShowInvitation(Action onQuit, TMP_FontAsset font) =>
        TryShowInvitation(MenuExitMilestone, onQuit, font, false);

    public static bool TryShowWorldExitInvitation(Action onContinue, TMP_FontAsset font) =>
        TryShowInvitation(SceneManager.GetActiveScene().name, onContinue, font, true);

    private static bool TryShowInvitation(string milestone, Action onClose, TMP_FontAsset font, bool worldExit)
    {
        if (ResearchSurveyPrompt.IsOpen) return false;
        if (!ResearchAnalyticsBootstrap.HasAnalyticsConsent || !ResearchAnalyticsBootstrap.IsReady
            || ResearchAnalyticsBootstrap.IsShuttingDown) return false;
        GauntletRunTracker tracker = GauntletRunTracker.Instance;
        if (!worldExit && (tracker == null || !tracker.IsCurrentSceneGauntlet)) return false;
        string playthroughId = ResearchPlayerState.PlaythroughId;
        if (PlayerPrefs.GetString(SubmittedKey) == playthroughId
            || (!worldExit && PlayerPrefs.GetString(EligibleKey) != playthroughId)) return false;

        string history = PlayerPrefs.GetString(InvitationHistoryKey);
        string[] entries = history.Split('|');
        if (entries[0] != playthroughId)
        {
            history = PlayerPrefs.GetString(ClearInvitationKey) == playthroughId
                ? playthroughId + "|" + FirstClearMilestone
                : playthroughId;
            entries = history.Split('|');
        }
        if (entries.Length >= 3 || Array.IndexOf(entries, milestone, 1) >= 0) return false;

        ResearchSurveyResponse response = new ResearchSurveyResponse(playthroughId,
            SceneManager.GetActiveScene().name, ResearchPlayerState.OfferVariant,
            GauntletRunTracker.ExperimentVersion, ResearchPlayerState.PlayerId,
            worldExit ? null : tracker.CurrentRunId, worldExit ? null : tracker.CurrentGauntletSessionId);
        if (!ResearchSurveyPrompt.Show(response, onClose, font)) return false;
        activeResponse = response;
        PlayerPrefs.SetString(InvitationHistoryKey, history + "|" + milestone);
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
