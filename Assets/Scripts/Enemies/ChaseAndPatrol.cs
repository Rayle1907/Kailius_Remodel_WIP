using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseAndPatrol : StateMachineBehaviour {

	public float speed = 2.5f;
	public float attackRange = 3f;
	public float chaseRange = 15f;
	public float verticalChaseRange = 1.5f;
	public float ledgeCheckDistance = 1.2f;
	public LayerMask groundLayer;

	private Transform groundDetection;
	private Transform player;
	private Rigidbody2D rb;
	private NpcFacing facing;
	private float patrolDirection = 1f;

	// OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
		override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
		player = GameObject.FindGameObjectWithTag("Player").transform;
		rb = animator.GetComponent<Rigidbody2D>();
		facing = animator.GetComponent<NpcFacing>();
		groundDetection = animator.transform.Find("GroundDetection");
		if (groundLayer.value == 0)
		{
			groundLayer = LayerMask.GetMask("Ground", "Water");
		}
		patrolDirection = 1f;
	}

	// OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
	override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		// Chase only when the player is nearby and on roughly the same vertical level.
		if (Vector2.Distance(player.position, rb.position) <= chaseRange
			&& Mathf.Abs(player.position.y - rb.position.y) <= verticalChaseRange)
		{
			facing.LookAtPlayer();

			float movementDirection = Mathf.Sign(player.position.x - rb.position.x);
			Vector2 ledgeOrigin = (Vector2)rb.position
				+ new Vector2(movementDirection * 0.55f, 0f);
			if (HasGroundAhead(ledgeOrigin))
			{
				Vector2 target = new Vector2(player.position.x, rb.position.y);
				Vector2 newPos = Vector2.MoveTowards(
					rb.position,
					target,
					speed * Time.fixedDeltaTime
				);
				rb.MovePosition(newPos);

			}
			else
			{
				rb.linearVelocity = Vector2.zero;
				patrolDirection = -movementDirection;
			}

			if (Vector2.Distance(player.position, rb.position) <= attackRange)
			{
				animator.SetTrigger("attack");
			}
		}
		else
		{
			Patrol(animator);
		}
	}

	private void Patrol(Animator animator)
	{
		facing.FaceDirection(patrolDirection);
		Vector2 origin = (Vector2)rb.position + new Vector2(patrolDirection * 0.55f, 0f);
		if (!HasGroundAhead(origin))
		{
			patrolDirection *= -1f;
			return;
		}

		Vector2 patrolTarget = rb.position + Vector2.right * patrolDirection;
		rb.MovePosition(Vector2.MoveTowards(rb.position, patrolTarget, speed * Time.fixedDeltaTime));
	}

	private bool HasGroundAhead(Vector2 origin)
	{
		RaycastHit2D[] hits = Physics2D.RaycastAll(
			origin,
			Vector2.down,
			ledgeCheckDistance,
			groundLayer
		);
		foreach (RaycastHit2D hit in hits)
		{
			if (hit.collider != null)
			{
				return true;
			}
		}
		return false;
	}

	// OnStateExit is called when a transition ends and the state machine finishes evaluating this state
	override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
		animator.ResetTrigger("attack");
	}
}
