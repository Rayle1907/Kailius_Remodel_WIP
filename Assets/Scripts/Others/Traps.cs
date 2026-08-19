using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Traps : MonoBehaviour {

    public int damage = 100;

    private void OnCollisionEnter2D(Collision2D collision) {
        HandleHazardContact(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other) {
        HandleHazardContact(other.gameObject);
    }

    private void HandleHazardContact(GameObject contactedObject) {
        Stats playerStats = contactedObject.GetComponentInParent<Stats>();
        if (playerStats == null) {
            return;
        }

        playerStats.ApplyDamage(damage, "trap", true);

        // Preserve the original behavior: surviving hazard contact returns to
        // the latest checkpoint; lethal contact follows normal death handling.
        if (playerStats.health > 0) {
            PlayerController controller = contactedObject.GetComponentInParent<PlayerController>();
            if (controller != null) {
                controller.reSpawn();
            }
        }
    }
}
