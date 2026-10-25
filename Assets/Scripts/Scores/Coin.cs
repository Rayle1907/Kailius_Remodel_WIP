using UnityEngine;

public class Coin : MonoBehaviour, IRewardPickup {
    public int coinValue = 1;
    public int scoreValue = 10;
    public GameObject sonido;

    private OptionalLootProgress chestLootOwner;
    public void SetChestLootOwner(OptionalLootProgress owner) => chestLootOwner = owner;

    [SerializeField] private bool collected;

    public bool TryCollect(Stats player) {
        if (collected || player == null || !player.isActiveAndEnabled || !player.CanAttack || ScoreManager.instance == null) {
            return false;
        }

        collected = true;
        if (chestLootOwner != null) chestLootOwner.RecordCollection(coinValue, 0);
        ScoreManager.instance.RecordCoinCollection(this);
        ScoreManager.instance.ChangeScore(scoreValue);
        ScoreManager.instance.ChangeScoreCoin(coinValue);
        GauntletEarnings.ForScene(gameObject.scene)?.RecordCollection(coinValue, 0);
        OneShotAudioPool.Play(sonido, transform.position);
        Destroy(gameObject);
        return true;
    }

    public void OnTriggerEnter2D(Collider2D collision) {
        if (collision.gameObject.CompareTag("Player")) {
            TryCollect(RewardMagnet.ResolvePlayer(collision));
        }

        if (collision.gameObject.CompareTag("Water") && GetComponent<RewardMagnet>() == null) {
            Destroy(gameObject);
        }
    }
}
