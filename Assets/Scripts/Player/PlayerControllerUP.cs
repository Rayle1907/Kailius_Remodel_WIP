using UnityEngine;

public class PlayerControllerUP : MonoBehaviour {

    private const float GroundJumpGracePeriod = 0.1f;

    public float moveSpeed;
    public float jumpHeight;
    public float groundCheckRadius;
    public Transform groundCheck;
    public LayerMask whatIsGround;
    public Vector3 Movimiento;
    
    private bool canJump;
    private bool canDoubleJump;
    private float groundJumpGraceUntil = float.NegativeInfinity;
    private Animator playerAnimator;
    private PlayerController playerController;
    private readonly System.Collections.Generic.Dictionary<Collider2D, Vector2> groundContacts = new System.Collections.Generic.Dictionary<Collider2D, Vector2>();

    private void Awake() {
        playerAnimator = GetComponentInParent<Animator>();
        playerController = GetComponentInParent<PlayerController>();
    }

    private void OnCollisionEnter2D(Collision2D collision) {
        UpdateGroundContact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision) {
        UpdateGroundContact(collision);
    }

    private void OnCollisionExit2D(Collision2D collision) {
        Collider2D groundCollider = collision.collider;
        if (groundCollider == null) return;

        groundContacts.Remove(groundCollider);
        MovingPlatform platform = groundCollider.GetComponent<MovingPlatform>();
        if (platform != null && playerController != null)
            playerController.ClearMovingPlatformSupport(platform);
        RefreshGroundedState();
    }

    private void UpdateGroundContact(Collision2D collision) {
        Collider2D groundCollider = collision.collider;
        if (groundCollider == null || (!groundCollider.CompareTag("Ground") && !groundCollider.CompareTag("Water"))) return;

        Vector2 groundNormal = GetGroundNormal(collision);
        if (groundNormal == Vector2.zero) {
            groundContacts.Remove(groundCollider);
            MovingPlatform sideContactPlatform = groundCollider.GetComponent<MovingPlatform>();
            if (sideContactPlatform != null && playerController != null)
                playerController.ClearMovingPlatformSupport(sideContactPlatform);
            RefreshGroundedState();
            return;
        }

        groundContacts[groundCollider] = groundNormal;
        MovingPlatform platform = groundCollider.GetComponent<MovingPlatform>();
        if (platform != null && playerController != null)
            playerController.SetMovingPlatformSupport(platform);
        RefreshGroundedState();
    }

    private Vector2 GetGroundNormal(Collision2D collision) {
        Vector2 groundNormal = Vector2.zero;
        for (int i = 0; i < collision.contactCount; i++) {
            Vector2 normal = collision.GetContact(i).normal;
            if (normal.y > 0.5f && (groundNormal == Vector2.zero || normal.y < groundNormal.y))
                groundNormal = normal;
        }

        return groundNormal;
    }

    internal Vector2 GetSlopeNormal(float horizontalVelocity) {
        Vector2 slopeNormal = Vector2.up;
        foreach (var contact in groundContacts) {
            if (contact.Key == null || !contact.Key.isActiveAndEnabled) continue;
            Vector2 normal = contact.Value;
            bool uphill = normal.x * horizontalVelocity < 0f;
            bool selectedUphill = slopeNormal.x * horizontalVelocity < 0f;
            if ((uphill && !selectedUphill) || (uphill == selectedUphill && normal.y < slopeNormal.y))
                slopeNormal = normal;
        }
        return slopeNormal;
    }

    private void RefreshGroundedState() {
        bool wasGrounded = canJump;
        canJump = groundContacts.Count > 0;
        if (canJump) {
            canDoubleJump = true;
            groundJumpGraceUntil = float.NegativeInfinity;
        } else if (wasGrounded) {
            groundJumpGraceUntil = Time.fixedTime + GroundJumpGracePeriod;
        }
        if (playerAnimator != null) playerAnimator.SetBool("jumping", !canJump);
    }

    public void ClearGroundedState() {
        groundContacts.Clear();
        canJump = false;
        groundJumpGraceUntil = float.NegativeInfinity;
        if (playerAnimator != null) playerAnimator.SetBool("jumping", true);
    }

    public bool TryConsumeJump() {
        if (canJump || Time.time <= groundJumpGraceUntil) {
            canJump = false;
            groundJumpGraceUntil = float.NegativeInfinity;
            return true;
        }

        if (!canDoubleJump) return false;
        canDoubleJump = false;
        return true;
    }

}
