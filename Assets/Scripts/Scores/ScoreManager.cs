using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour {
    public static ScoreManager instance;
    public TextMeshProUGUI Score;
    public TextMeshProUGUI textScore;
    public TextMeshProUGUI textCoins;
    public TextMeshProUGUI textGems;
    public TextMeshProUGUI textStars;

    private int score;
    private int scoreCoins;
    private int scoreGems;
    private int scoreStars;

    private static bool created = false;

    // Start is called before the first frame update
    void Start() {
        if(instance == null) {
            instance = this;
        }

        this.score = (int)PlayerPrefs.GetInt("Score", 0);
        this.scoreCoins = (int)PlayerPrefs.GetInt("ScoreCoins", 0);
        this.scoreGems = (int)PlayerPrefs.GetInt("ScoreGems", 0);
        this.scoreStars = (int)PlayerPrefs.GetInt("ScoreStars", 0);

       
        UpdateScoreText();
    }

    public void ChangeScore(int scoreValue) {
        score += scoreValue;
        UpdateScoreText();

        PlayerPrefs.SetInt("Score", score);
    }

    public void ChangeScoreCoin(int coinValue) {
        scoreCoins += coinValue;
        UpdateScoreText();
        PlayerPrefs.SetInt("ScoreCoins", scoreCoins);
    }

    public void ChangeScoreGem(int gemValue) {
        scoreGems += gemValue;
        UpdateScoreText();
        PlayerPrefs.SetInt("ScoreGems", scoreGems);
    }

    public void ChangeScoreStar(int starsValue) {
        scoreStars += starsValue;
        UpdateScoreText();
        PlayerPrefs.SetInt("ScoreStars", scoreStars);
    }

    private void UpdateScoreText() {
        if (Score != null) {
            Score.text = "" + score.ToString();
        }
        if (textScore != null) {
            textScore.text = "" + score.ToString();
        }
        if (textCoins != null) {
            textCoins.text = "150/" + scoreCoins.ToString();
        }
        if (textGems != null) {
            textGems.text = "60/" + scoreGems.ToString();
        }
        if (textStars != null) {
            textStars.text = "3/" + scoreStars.ToString();
        }
    }

    public int getScoreTotal() {
        return this.score;
    }

    public int getScoreStars() {
        return this.scoreStars;
    }
}
