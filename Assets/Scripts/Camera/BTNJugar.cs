using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BTNJugar : MonoBehaviour {
    
    private void OnMouseDown() {
        GauntletProgress.StartNewGame();
        PlayerPrefs.SetInt("AttackDamage", 0);
        PlayerPrefs.SetInt("Defense", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene("1_1Gauntlet");
    }
}
