using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour {

    public Animator animator;
    public Transform attackPoint;
    public LayerMask enemyLayers;
    public GameObject sonido;

    public float attactRate = 0.6f;
    public float attackRange = 1f;
    private float nextAttactTime = 0.5f;
    private readonly Collider2D[] hitEnemies = new Collider2D[8];
    private ContactFilter2D enemyFilter;
    private Stats playerStats;

    void Awake() {
        playerStats = GetComponent<Stats>();
        enemyFilter = new ContactFilter2D();
        enemyFilter.SetLayerMask(enemyLayers);
        enemyFilter.useTriggers = true;
    }

    // Update is called once per frame
    void Update() {

        // Hace que no se pueda spamear 
        if(Time.time >= nextAttactTime) {
            if (Input.GetKeyDown(KeyCode.F)) {
                Attack();
                nextAttactTime = Time.time + attactRate / attackRange;
            }
        }
        
    }

    void Attack() {
        // Play una animacion
        animator.SetTrigger("attack");

        // Sonido
        OneShotAudioPool.Play(sonido, transform.position);

        // Detectar enemigos
        int hitCount = Physics2D.OverlapCircle(
            attackPoint.position,
            attackRange,
            enemyFilter,
            hitEnemies
        );

        // Resta el daño al primer enemigo encontrado.
        if (hitCount > 0) {
            Collider2D enemy = hitEnemies[0];
            enemy.GetComponent<Enemy>().TakeDamage(Stats.instance.getAttackDamage());

            // Successful melee hits build Fury. The bow no longer consumes it.
            if(playerStats.getPower() < 4) {
                playerStats.takePower(1);
            }
        }
    }

    private void OnDrawGizmosSelected() {

        if (attackPoint == null)
            return;

        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    public void AttackButton() {
        // Hace que no se pueda spamear 
        if (Time.time >= nextAttactTime) {
            Attack();
            nextAttactTime = Time.time + attactRate / attackRange;
        }
    }
}
