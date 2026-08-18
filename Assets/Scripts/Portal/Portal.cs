using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour {

    public string nextSceneName;

    private void OnTriggerEnter2D(Collider2D plyr) {
        if (plyr.gameObject.tag == "Player") {
            if (GauntletRunTracker.Instance != null
                && GauntletRunTracker.Instance.IsCurrentSceneGauntlet) {
                GauntletRunTracker.Instance.EndCurrentRun("completed");
            }

            if (!string.IsNullOrEmpty(nextSceneName)) {
                SceneManager.LoadScene(nextSceneName);
            } else {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            }
        }
    }
        

}
