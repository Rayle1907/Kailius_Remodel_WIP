using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Star : MonoBehaviour {
    public int starValue = 1;
    public int scoreValue = 50;
    public GameObject sonido;

    private bool collected;

    public void OnTriggerEnter2D(Collider2D collision) {
        ScoreManager scoreManager = ScoreManager.instance;
        if (collected || !collision.gameObject.CompareTag("Player") || scoreManager == null) {
            return;
        }

        collected = true;
        scoreManager.ChangeScore(scoreValue);
        scoreManager.ChangeScoreStar(starValue);
        OneShotAudioPool.Play(sonido, transform.position);
    }
}
