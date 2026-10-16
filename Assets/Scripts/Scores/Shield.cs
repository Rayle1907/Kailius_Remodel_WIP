using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shield : MonoBehaviour {
    public int shieldValue = 10;
    public GameObject sonido;
    private bool collected;

    public void OnTriggerEnter2D(Collider2D collision) {
        if (collision.gameObject.CompareTag("Player") && !collected) {
            collected = true;
            Stats.instance.addDefense(shieldValue);
            OneShotAudioPool.Play(sonido, transform.position);
            Destroy(gameObject);
        }

        if (collision.gameObject.CompareTag("Water")) {
            Destroy(gameObject);
        }
    }
}
