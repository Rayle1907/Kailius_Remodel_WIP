using UnityEngine;

public class Attack : MonoBehaviour, IRewardPickup {
    public int attackValue = 10;
    public GameObject sonido;

    private bool collected;

    public bool TryCollect(Stats player) {
        if (collected || player == null || !player.isActiveAndEnabled || !player.CanAttack) {
            return false;
        }

        collected = true;
        player.addAttackDamage(attackValue);
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
