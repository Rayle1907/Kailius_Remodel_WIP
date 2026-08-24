using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossWeapon : MonoBehaviour {
	public int attackDamage = 100;

	public Vector3 attackOffset;
	public float attackRange = 1f;
	public LayerMask attackMask;

	public void Attack() {
		Vector3 pos = transform.position;
		pos += transform.right * attackOffset.x;
		pos += transform.up * attackOffset.y;

		Collider2D[] hits = Physics2D.OverlapCircleAll(pos, attackRange, attackMask);
		foreach (Collider2D hit in hits) {
			Stats playerStats = hit.GetComponentInParent<Stats>();
			if (playerStats != null) {
				playerStats.ApplyDamage(attackDamage, "boss_melee", true);
				return;
			}
		}
	}

	void OnDrawGizmosSelected() {
		Vector3 pos = transform.position;
		pos += transform.right * attackOffset.x;
		pos += transform.up * attackOffset.y;

		Gizmos.DrawWireSphere(pos, attackRange);
	}
}
