using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour {

    public string nextSceneName;
    private bool transitioning;

    private void OnTriggerEnter2D(Collider2D plyr) {
        if (plyr.gameObject.tag == "Player" && !transitioning) {
            string destination = nextSceneName;
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
            if (GauntletRunTracker.Instance != null
                && GauntletRunTracker.Instance.IsCurrentSceneGauntlet) {
                GauntletRunTracker.Instance.RecordPostDecisionOutcome("level_completed");
                GauntletRunTracker.Instance.EndCurrentRun("completed");
            }

            SceneManager.LoadScene(destination);
        }
    }
        

}
