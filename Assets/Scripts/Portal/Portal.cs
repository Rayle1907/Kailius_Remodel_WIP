using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Portal : MonoBehaviour {

    public string nextSceneName;
    [SerializeField] private string[] requiredGauntlets;
    [SerializeField] private ReviveOnboardingPresenter victoryDialoguePrefab;
    [SerializeField, TextArea] private string victoryMessage;
    [SerializeField] private bool surveyOnWorldExit;
    [SerializeField, Tooltip("Return to the entrance used for this gauntlet visit. Uses Next Scene Name if no entrance was recorded.")]
    private bool returnToEntryPortal;
    public bool ReturnsToEntryPortal => returnToEntryPortal;
    [SerializeField, Tooltip("Safe arrival position outside this entrance's trigger when returning from a gauntlet.")]
    private Transform returnPoint;
    [SerializeField] private bool confirmReplayWhenCleared;
    [SerializeField] private LayeredButton replayButtonTemplate;
    [SerializeField, Min(0f)] private float replayPromptCooldown = 5f;
    private double nextReplayPromptTime = double.NegativeInfinity;
    private readonly HashSet<Collider2D> playerOverlaps = new HashSet<Collider2D>();
    private GauntletReplayPrompt replayPrompt;
    private bool promptedThisVisit;
    private bool transitioning;
    private GameObject victoryOverlay;
    private float previousTimeScale;
    private static readonly HashSet<string> loggedUnavailableRequirementScenes = new HashSet<string>();
    private static string entryScenePath;
    private static Vector3 entryPosition;
    private static bool returningToEntry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() {
        entryScenePath = null;
        returningToEntry = false;
        loggedUnavailableRequirementScenes.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (!returningToEntry) {
            if (!GauntletHealthDropRules.IsGauntletScene(scene.name)) entryScenePath = null;
            return;
        }
        returningToEntry = false;
        string expectedScene = entryScenePath;
        entryScenePath = null;
        if (scene.path != expectedScene) return;

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null || player.gameObject.scene != scene) {
            Debug.LogError("Portal return failed: the entrance scene has no active PlayerController.");
            return;
        }
        player.transform.position = entryPosition;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null) {
            body.position = entryPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        Physics2D.SyncTransforms();
    }

    private void OnTriggerEnter2D(Collider2D plyr) {
        if (!plyr.CompareTag("Player") && plyr.GetComponentInParent<PlayerController>() == null) return;
        playerOverlaps.RemoveWhere(item => item == null || !item.enabled || !item.gameObject.activeInHierarchy);
        if (playerOverlaps.Count == 0) promptedThisVisit = false;
        playerOverlaps.Add(plyr);
        if (transitioning || promptedThisVisit || ResearchSurveyPrompt.IsOpen) return;
        if (confirmReplayWhenCleared && GauntletProgress.IsCleared(nextSceneName)) {
            if (Time.timeAsDouble < nextReplayPromptTime) {
                promptedThisVisit = true;
                return;
            }
            if (GauntletReplayPrompt.BlocksSettings || Time.timeScale <= 0f) return;
            replayPrompt = GauntletReplayPrompt.Show(replayButtonTemplate, () => {
                replayPrompt = null;
                if (this != null && isActiveAndEnabled) Transition();
            }, () => {
                replayPrompt = null;
                nextReplayPromptTime = Time.timeAsDouble + Mathf.Max(0f, replayPromptCooldown);
            });
            promptedThisVisit = replayPrompt != null;
            return;
        }
        Transition();
    }

    private void OnTriggerExit2D(Collider2D player) {
        playerOverlaps.Remove(player);
        playerOverlaps.RemoveWhere(item => item == null || !item.enabled || !item.gameObject.activeInHierarchy);
        if (playerOverlaps.Count == 0) promptedThisVisit = false;
    }

    private void OnDisable() {
        if (victoryOverlay is not null) {
            CloseVictoryDialogue();
            transitioning = false;
        }
        if (replayPrompt != null) replayPrompt.Dismiss();
        replayPrompt = null;
        playerOverlaps.Clear();
        promptedThisVisit = false;
    }

    private void Transition() {
        if (transitioning) return;
        if (requiredGauntlets != null && requiredGauntlets.Length > 0) {
            string[] unfinished = GauntletProgress.GetUnfinishedScenes(requiredGauntlets);
            if (unfinished.Length > 0) {
                promptedThisVisit = true;
                GauntletGateNotice.Show(gameObject.scene, "Portal locked. Please defeat the 3 challenges to proceed.");
                return;
            }
        }
        if (GauntletHealthDropRules.IsGauntletScene(gameObject.scene.name)) {
            GauntletRequirements requirements = FindSceneRequirements();
            if (requirements == null || !requirements.IsInitialized || !requirements.IsConfigured) {
                string sceneName = gameObject.scene.name;
                if (loggedUnavailableRequirementScenes.Add(sceneName))
                    Debug.LogError("Gauntlet exit blocked because requirements are missing, uninitialized, or invalid in " + sceneName + ".", this);
                GauntletGateNotice.Show(gameObject.scene, "Gauntlet requirements are unavailable. You cannot leave yet.");
                return;
            }
            if (!requirements.IsCurrentAttemptComplete) {
                GauntletGateNotice.Show(gameObject.scene,
                    "Defeat the remaining required enemies to leave.", requirements.GetRemainingQuotaCounts());
                return;
            }
        }

        bool useEntry = returnToEntryPortal && !string.IsNullOrEmpty(entryScenePath);
        string destination = useEntry ? entryScenePath : nextSceneName;
        if (string.IsNullOrEmpty(destination)) {
            destination = System.IO.Path.GetFileNameWithoutExtension(
                SceneUtility.GetScenePathByBuildIndex(SceneManager.GetActiveScene().buildIndex + 1));
        }
        if (destination == "Scene_2") {
            string[] unfinished = GauntletProgress.GetUnfinishedScenes();
            if (unfinished.Length > 0) {
                GauntletGateNotice.Show(gameObject.scene, "Scene 2 locked\nClear: " + string.Join(", ", unfinished));
                return;
            }
        }
        if (string.IsNullOrEmpty(destination) || !Application.CanStreamedLevelBeLoaded(destination)) {
            Debug.LogError("Portal destination is unavailable: " + destination, this);
            return;
        }
        transitioning = true;
        if (!string.IsNullOrEmpty(victoryMessage)) {
            if (victoryDialoguePrefab != null && victoryDialoguePrefab.IsConfigured) {
                previousTimeScale = Time.timeScale;
                victoryOverlay = new GameObject("Victory Dialogue", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(victoryOverlay, gameObject.scene);
                Canvas canvas = victoryOverlay.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                MobileDialogLayout.Configure(victoryOverlay);
                ReviveOnboardingPresenter dialogue = Instantiate(victoryDialoguePrefab, victoryOverlay.transform, false);
                Time.timeScale = 0f;
                dialogue.ShowSinglePage(victoryMessage, () => {
                    CloseVictoryDialogue();
                    ContinueTransition(destination, useEntry);
                });
                return;
            }
            Debug.LogError("Portal victory dialogue needs a configured onboarding prefab.", this);
        }
        ContinueTransition(destination, useEntry);
    }

    private void ContinueTransition(string destination, bool useEntry) {
        if (surveyOnWorldExit) {
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            Stats stats = player != null ? player.GetComponent<Stats>() : null;
            TMP_FontAsset font = stats != null && stats.textDamage != null
                ? stats.textDamage.font
                : TMP_Settings.defaultFontAsset;
            if (ResearchSurvey.TryShowWorldExitInvitation(() => LoadDestination(destination, useEntry), font)) return;
        }
        LoadDestination(destination, useEntry);
    }

    private void CloseVictoryDialogue() {
        Time.timeScale = previousTimeScale;
        if (victoryOverlay != null) {
            victoryOverlay.SetActive(false);
            Destroy(victoryOverlay);
        }
        victoryOverlay = null;
    }

    private void LoadDestination(string destination, bool useEntry) {
        if (useEntry) {
            returningToEntry = true;
        } else if (!GauntletHealthDropRules.IsGauntletScene(gameObject.scene.name)
            && GauntletHealthDropRules.IsGauntletScene(destination)) {
            entryScenePath = null;
            if (returnPoint != null) {
                entryScenePath = gameObject.scene.path;
                entryPosition = returnPoint.position;
            } else {
                Debug.LogWarning("Gauntlet entrance has no Return Point. Its exit will use the configured destination.", this);
            }
        }
        if (GauntletRunTracker.Instance != null
            && GauntletRunTracker.Instance.IsCurrentSceneGauntlet) {
            GauntletRunTracker.Instance.EndSegmentForExit(this);
            GauntletRunTracker.Instance.RecordPostDecisionOutcome("level_completed");
            GauntletRunTracker.Instance.EndCurrentRun("completed");
        }

        SceneManager.LoadScene(destination);
    }

    private GauntletRequirements FindSceneRequirements() {
        GauntletRequirements result = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects()) {
            foreach (GauntletRequirements candidate in root.GetComponentsInChildren<GauntletRequirements>(true)) {
                if (result != null) return null;
                result = candidate;
            }
        }
        return result;
    }

}
