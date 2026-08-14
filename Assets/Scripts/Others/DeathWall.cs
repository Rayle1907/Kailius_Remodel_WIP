using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathWall : MonoBehaviour
{
    public float speed = 1.5f;
    public int damage = 9999;
    public bool respawnInsteadOfKill = false;

    void Update()
    {
        transform.position += Vector3.right * speed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Player")
        {
            Stats stats = collision.GetComponentInParent<Stats>();
            PlayerController player = collision.GetComponentInParent<PlayerController>();

            if (stats == null)
            {
                return;
            }

            stats.takeTrueDamage(damage);

            if (respawnInsteadOfKill)
            {
                {
                    player.reSpawn();
                }
            }
        }
    }
}
