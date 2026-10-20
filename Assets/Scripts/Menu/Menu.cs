using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour {

    public void PlayGame () {
        GauntletProgress.StartNewGame();
        Debug.Log("Menu.PlayGame loading Scene_1");
        PlayerPrefs.SetInt("AttackDamage", 0);
        PlayerPrefs.SetInt("Defense", 0);
        PlayerPrefs.SetInt("Score", 0);
        PlayerPrefs.SetInt("ScoreCoins", 0);
        PlayerPrefs.SetInt("ScoreGems", 0);
        PlayerPrefs.SetInt("ScoreStars", 0);
        SceneManager.LoadScene("Scene_1");
    }

    public void ResumeGame() {
        Time.timeScale = 1;  
    }

    public void QuitGame () {
        Application.Quit();
    }

    public void PlayAgain() {
        Time.timeScale = 1;

        if (GauntletRunTracker.Instance != null) {
            GauntletRunTracker.Instance.SetNextRunStartReason("retry_button");
            GauntletRunTracker.Instance.RestartCurrentRun();
        }

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}
