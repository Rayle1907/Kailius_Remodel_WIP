using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour {

    public string nextSceneName;
    [SerializeField, Tooltip("Return to the entrance used for this gauntlet visit. Uses Next Scene Name if no entrance was recorded.")]
    private bool returnToEntryPortal;
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
    private static string entryScenePath;
    private static Vector3 entryPosition;
    private static bool returningToEntry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() {
        entryScenePath = null;
        returningToEntry = false;
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
        if (transitioning || promptedThisVisit) return;
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
        if (replayPrompt != null) replayPrompt.Dismiss();
        replayPrompt = null;
        playerOverlaps.Clear();
        promptedThisVisit = false;
    }

    private void Transition() {
        if (transitioning) return;
        bool useEntry = returnToEntryPortal && !string.IsNullOrEmpty(entryScenePath);
        string destination = useEntry ? entryScenePath : nextSceneName;
        if (string.IsNullOrEmpty(destination)) {
            destination = System.IO.Path.GetFileNameWithoutExtension(
                SceneUtility.GetScenePathByBuildIndex(SceneManager.GetActiveScene().buildIndex + 1));
        }
        if (destination == "Scene_2") {
            string[] unfinished = GauntletProgress.GetUnfinishedScenes();
            if (unfinished.Length > 0) {
                GauntletGateNotice.Show("Scene 2 locked\nClear: " + string.Join(", ", unfinished));
                return;
            }
        }
        if (string.IsNullOrEmpty(destination) || !Application.CanStreamedLevelBeLoaded(destination)) {
            Debug.LogError("Portal destination is unavailable: " + destination, this);
            return;
        }
        transitioning = true;
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
            GauntletRunTracker.Instance.RecordPostDecisionOutcome("level_completed");
            GauntletRunTracker.Instance.EndCurrentRun("completed");
        }

        SceneManager.LoadScene(destination);
    }
        

}
