using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour {

    public void PlayGame () {
        Debug.Log("Menu.PlayGame loading 1_1Gauntlet");
        PlayerPrefs.SetInt("AttackDamage", 0);
        PlayerPrefs.SetInt("Defense", 0);
        PlayerPrefs.SetInt("Score", 0);
        PlayerPrefs.SetInt("ScoreCoins", 0);
        PlayerPrefs.SetInt("ScoreGems", 0);
        PlayerPrefs.SetInt("ScoreStars", 0);
        SceneManager.LoadScene("1_1Gauntlet");
    }

    public void ResumeGame() {
        Time.timeScale = 1;  
    }

    public void QuitGame () {
        Application.Quit();
    }

    public void PlayAgain() {
        SceneManager.LoadScene("Menu");
    }
}
