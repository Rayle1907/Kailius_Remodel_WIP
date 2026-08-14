using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossRun : StateMachineBehaviour {

	public float speed = 2.5f;
	public float attackRange = 3f;
	public float ledgeCheckDistance = 1.2f;
	public LayerMask groundLayer;

	private Transform groundDetection;
	private Transform player;
	private Rigidbody2D rb;
	private Boss boss;

	// OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
	override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
		player = GameObject.FindGameObjectWithTag("Player").transform;
		rb = animator.GetComponent<Rigidbody2D>();
		boss = animator.GetComponent<Boss>();
		groundDetection = animator.transform.Find("GroundDetection");
	}

	// OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
	override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		if (Vector2.Distance(player.position, rb.position) <= 15)
		{
			boss.LookAtPlayer();

			RaycastHit2D groundAhead = Physics2D.Raycast(
				groundDetection.position,
				Vector2.down,
				ledgeCheckDistance
			); 
			if (groundAhead.collider != null)
			{
				Vector2 target = new Vector2(player.position.x, rb.position.y);
				Vector2 newPos = Vector2.MoveTowards(
					rb.position,
					target,
					speed * Time.fixedDeltaTime
				);
				rb.MovePosition(newPos);

			} else
            {
				rb.linearVelocity = Vector2.zero;
				animator.SetTrigger("idle");
			}

			if (Vector2.Distance(player.position, rb.position) <= attackRange)
			{
				animator.SetTrigger("attack");
			}
		}
	}

	// OnStateExit is called when a transition ends and the state machine finishes evaluating this state
	override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
		animator.ResetTrigger("attack");
	}
}