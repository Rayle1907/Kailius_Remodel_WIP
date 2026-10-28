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
    [SerializeField] private string rewardSourceId;
    private bool isDying;
    private SpriteHurtFlash hurtFlash;

    private void Awake() {
        hurtFlash = GetComponent<SpriteHurtFlash>();
    }

    public void TakeDamage(int damage) {
        TryTakeDamage(damage);
    }

    public bool TryTakeDamage(int damage) {
        if (isDying || health <= 0 || damage <= 0) return false;

        this.health -= damage;

        // Hurt feedback is a tint only; do not replace the current sprite.
        hurtFlash?.Flash();

        if(health <= 0) {
            Die();
        }
        return true;
    }

    void Die() {
        if (isDying) {
            return;
        }

        isDying = true;
        GetComponent<RequiredGauntletEnemy>()?.RecordDefeat();
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
        if (ScoreManager.instance != null) ScoreManager.instance.ChangeScore(100);

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
        int numHearts = GauntletHealthDropRules.IsGauntletScene(gameObject.scene.name)
            ? 0
            : Random.Range(0, maxHearts);
        bool canDropUpgrades = GauntletProgress.TrySpendUpgrade(gameObject.scene.name, rewardSourceId);
        int numSwords = canDropUpgrades ? Random.Range(0, maxSwords) : 0;
        int numShields = canDropUpgrades ? Random.Range(0, maxShields) : 0;

        for (int i = 0; i < numCoins; i++) {
            SpawnReward(coins, new Vector3(gameObject.transform.position.x - 1.0f, gameObject.transform.position.y + 2.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; hearts != null && i < numHearts; i++) {
            SpawnReward(hearts, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 2.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; i < numSwords; i++) {
            SpawnReward(sword, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 2.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; i < numShields; i++) {
            SpawnReward(shield, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 2.0f, gameObject.transform.position.z), Quaternion.identity);
        }
    }

    private void SpawnReward(GameObject prefab, Vector3 position, Quaternion rotation) {
        GameObject reward = Instantiate(prefab, position, rotation);
        RewardMagnet.Attach(reward, gameObject.scene.name);
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
