using UnityEngine;

public class Gem : MonoBehaviour, IRewardPickup {
    public int gemValue = 1;
    public int scoreValue = 15;
    public GameObject sonido;

    [SerializeField] private bool collected;

    public bool TryCollect(Stats player) {
        if (collected || player == null || !player.isActiveAndEnabled || !player.CanAttack || ScoreManager.instance == null) {
            return false;
        }

        collected = true;
        ScoreManager.instance.ChangeScore(scoreValue);
        ScoreManager.instance.ChangeScoreGem(gemValue);
        GauntletEarnings.ForScene(gameObject.scene)?.RecordCollection(0, gemValue);
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
