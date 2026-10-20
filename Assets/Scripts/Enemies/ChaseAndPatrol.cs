using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseAndPatrol : StateMachineBehaviour {

	public float speed = 2.5f;
	public float attackRange = 3f;
	public float chaseRange = 15f;
	public float verticalChaseRange = 1.5f;
	public float ledgeCheckDistance = 1.2f;
	public float ledgeLookAhead = 0.35f;
	public LayerMask groundLayer;

	private Collider2D bodyCollider;
	private Transform player;
	private Rigidbody2D rb;
	private NpcFacing facing;
	private float patrolDirection = 1f;

	// OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
		override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
		player = GameObject.FindGameObjectWithTag("Player").transform;
		rb = animator.GetComponent<Rigidbody2D>();
		facing = animator.GetComponent<NpcFacing>();
		bodyCollider = animator.GetComponent<Collider2D>();
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
			FaceMovementDirection(Mathf.Sign(player.position.x - rb.position.x));

			float movementDirection = Mathf.Sign(player.position.x - rb.position.x);
			Vector2 ledgeOrigin = GroundProbeOrigin(movementDirection);
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
		Vector2 origin = GroundProbeOrigin(patrolDirection);
		if (!HasGroundAhead(origin))
		{
			Vector2 oppositeOrigin = GroundProbeOrigin(-patrolDirection);
			if (!HasGroundAhead(oppositeOrigin))
			{
				rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
				return;
			}

			patrolDirection *= -1f;
		}

		FaceMovementDirection(patrolDirection);
		Vector2 patrolTarget = rb.position + Vector2.right * patrolDirection;
		rb.MovePosition(Vector2.MoveTowards(rb.position, patrolTarget, speed * Time.fixedDeltaTime));
	}

	private void FaceMovementDirection(float direction)
	{
		if (bodyCollider == null)
		{
			facing.FaceDirection(direction);
			return;
		}

		Vector2 center = bodyCollider.bounds.center;
		bool wasFlipped = facing.isFlipped;
		facing.FaceDirection(direction);
		if (wasFlipped != facing.isFlipped)
		{
			Physics2D.SyncTransforms();
			rb.position += center - (Vector2)bodyCollider.bounds.center;
			Physics2D.SyncTransforms();
		}
	}

	private Vector2 GroundProbeOrigin(float direction)
	{
		if (bodyCollider == null)
		{
			return rb.position + new Vector2(direction * 0.55f, 0f);
		}

		Bounds bounds = bodyCollider.bounds;
		return new Vector2(bounds.center.x + direction * (bounds.extents.x + Mathf.Max(ledgeLookAhead, speed * Time.fixedDeltaTime)), bounds.min.y + 0.05f);
	}

	private bool HasGroundAhead(Vector2 origin)
	{
		RaycastHit2D[] hits = Physics2D.RaycastAll(
			origin,
			Vector2.down,
			Mathf.Min(ledgeCheckDistance, 0.2f),
			groundLayer
		);
		foreach (RaycastHit2D hit in hits)
		{
			if (hit.collider != null && !hit.collider.isTrigger
				&& hit.rigidbody != rb && hit.normal.y > 0.5f)
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
