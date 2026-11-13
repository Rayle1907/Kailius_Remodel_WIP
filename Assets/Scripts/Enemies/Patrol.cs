using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Patrol : MonoBehaviour {

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float wallCheckDistance = 0.1f;

    public float speed;
    public float distance;
    private bool movingRight = true;
    public Transform groundDetection;
    private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[16];
    private ContactFilter2D groundFilter;
    private Collider2D bodyCollider;
    private float bodyColliderHalfWidth;

    private void Awake()
    {
        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
        }

        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;
        bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null)
        {
            bodyColliderHalfWidth = bodyCollider.bounds.extents.x;
        }
    }

    private void Update() {

        Vector2 movementDirection = transform.right;
        bool hasGround = HasGroundAhead();
        bool hasWall = HasWallAhead(movementDirection);
        if (!hasGround || hasWall)
        {
            Vector3 oppositeProbe = transform.TransformPoint(new Vector3(
                -groundDetection.localPosition.x,
                groundDetection.localPosition.y,
                groundDetection.localPosition.z
            ));
            Vector2 oppositeDirection = -movementDirection;
            if (HasGroundAt(oppositeProbe) && !HasWallAhead(oppositeDirection))
            {
                TurnAround();
            }
            return;
        }

        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    private bool HasGroundAhead()
    {
        return HasGroundAt(groundDetection.position);
    }

    private bool HasGroundAt(Vector2 origin)
    {
        int hitCount = Physics2D.Raycast(
            origin,
            Vector2.down,
            groundFilter,
            raycastHits,
            distance
        );

        return HasValidHit(hitCount);
    }

    private bool HasWallAhead(Vector2 direction)
    {
        Vector2 origin = groundDetection.position;
        if (bodyCollider != null)
        {
            Vector3 bodyCenter = bodyCollider.transform.TransformPoint(bodyCollider.offset);
            origin = new Vector2(bodyCenter.x + Mathf.Sign(direction.x) * bodyColliderHalfWidth, bodyCenter.y);
        }

        int hitCount = Physics2D.Raycast(
            origin,
            direction,
            groundFilter,
            raycastHits,
            wallCheckDistance + Mathf.Abs(speed) * Time.deltaTime
        );

        return HasValidHit(hitCount);
    }

    private bool HasValidHit(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = raycastHits[i].collider;
            if (hitCollider != null && !hitCollider.isTrigger
                && hitCollider.transform != transform
                && !hitCollider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private void TurnAround()
    {
        if (movingRight)
        {
            transform.eulerAngles = new Vector3(0, -180, 0);
            movingRight = false;
        }
        else
        {
            transform.eulerAngles = new Vector3(0, 0, 0);
            movingRight = true;
        }
    }
}
