using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour {

    public int health = 500;
    public float timeDestroy = 1.5f;
    public Animator animator;
    public GameObject sonidoMuerte;

    public GameObject coins;
    public GameObject hearts;
    public GameObject sword;
    public GameObject shield;

    public int maxCoins = 5;
    public int maxHearts = 3;
    public int maxSwords = 2;
    public int maxShields = 2;
    private bool isDying;
    private SpriteHurtFlash hurtFlash;

    private void Awake() {
        hurtFlash = GetComponent<SpriteHurtFlash>();
    }

    public void TakeDamage(int damage) {
        if (isDying) {
            return;
        }

        this.health -= damage;

        // Hurt feedback is a tint only; do not replace the current sprite.
        hurtFlash?.Flash();

        if(health <= 0) {
            Die();
        }
    }

    void Die() {
        if (isDying) {
            return;
        }

        isDying = true;
        Rigidbody2D rb = gameObject.GetComponent<Rigidbody2D>();
        if (rb != null) {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        // Dropear items
        dropItems();

        // Play animacion de muerto
        if (HasAnimatorParameter("isDead", AnimatorControllerParameterType.Bool)) {
            animator.SetBool("isDead", true);
        }

        // Añadir puntuacion 
        ScoreManager.instance.ChangeScore(100);

        // Stop enemy logic/collisions across the whole hierarchy while the death animation finishes.
        foreach (Collider2D enemyCollider in GetComponentsInChildren<Collider2D>()) {
            enemyCollider.enabled = false;
        }

        this.enabled = false;

        // Sonido
        if (sonidoMuerte != null) {
            OneShotAudioPool.Play(sonidoMuerte, transform.position);
        }
        Object.Destroy(gameObject, timeDestroy);
    }

    void dropItems() {
        int numCoins = Random.Range(1, maxCoins);
        int numHearts = Random.Range(0, maxHearts);
        int numSwords = Random.Range(0, maxSwords);
        int numShields = Random.Range(0, maxShields);

        for (int i = 0; i < numCoins; i++) {
            Instantiate(coins, new Vector3(gameObject.transform.position.x - 1.0f, gameObject.transform.position.y + 2.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; i < numHearts; i++) {
            Instantiate(hearts, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 2.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; i < numSwords; i++) {
            Instantiate(sword, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 2.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; i < numShields; i++) {
            Instantiate(shield, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 2.0f, gameObject.transform.position.z), Quaternion.identity);
        }
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType) {
        if (animator == null) {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters) {
            if (parameter.name == parameterName && parameter.type == parameterType) {
                return true;
            }
        }

        return false;
    }

}
