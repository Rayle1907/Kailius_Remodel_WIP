using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CameraWin : MonoBehaviour {

    public TextMeshProUGUI scoreTotal;
    public GameObject cameraWin;
    public GameObject stats;

    public Image Star1;
    public Image Star2;
    public Image Star3;
    public Sprite Star;

    public GameObject boss;

    public int stars;
    private Enemy bossEnemy;

    void Start() {
        bossEnemy = boss.GetComponent<Enemy>();
    }

    void Update() {
        if (bossEnemy.health > 0) {
            return;
        }

        cameraWin.SetActive(true);
        stats.SetActive(false);

        scoreTotal.text = ScoreManager.instance.getScoreTotal().ToString();
        stars = ScoreManager.instance.getScoreStars();

        switch (stars) {
            case 1:
                Star1.sprite = Star;
                break;
            case 2:
                Star1.sprite = Star;
                Star2.sprite = Star;
                break;
            case 3:
                Star1.sprite = Star;
                Star2.sprite = Star;
                Star3.sprite = Star;
                break;
        }

        Time.timeScale = 0;
        enabled = false;
    }
}
