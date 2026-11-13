using UnityEngine;

public class PlayerControllerUP : MonoBehaviour {

    public float moveSpeed;
    public float jumpHeight;
    public float groundCheckRadius;
    public Transform groundCheck;
    public LayerMask whatIsGround;
    public Vector3 Movimiento;
    
    private bool grounded;
    private bool canJump;
    private bool canDoubleJump;
    private Animator playerAnimator;
    private PlayerController playerController;
    private readonly System.Collections.Generic.HashSet<Collider2D> groundContacts = new System.Collections.Generic.HashSet<Collider2D>();

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

        if (!HasUpwardContact(collision)) {
            groundContacts.Remove(groundCollider);
            MovingPlatform sideContactPlatform = groundCollider.GetComponent<MovingPlatform>();
            if (sideContactPlatform != null && playerController != null)
                playerController.ClearMovingPlatformSupport(sideContactPlatform);
            RefreshGroundedState();
            return;
        }

        groundContacts.Add(groundCollider);
        MovingPlatform platform = groundCollider.GetComponent<MovingPlatform>();
        if (platform != null && playerController != null)
            playerController.SetMovingPlatformSupport(platform);
        RefreshGroundedState();
    }

    private bool HasUpwardContact(Collision2D collision) {
        for (int i = 0; i < collision.contactCount; i++) {
            if (collision.GetContact(i).normal.y > 0.5f) return true;
        }

        return false;
    }

    private void RefreshGroundedState() {
        canJump = groundContacts.Count > 0;
        if (canJump) canDoubleJump = true;
        if (playerAnimator != null) playerAnimator.SetBool("jumping", !canJump);
    }

    public void ClearGroundedState() {
        groundContacts.Clear();
        canJump = false;
        if (playerAnimator != null) playerAnimator.SetBool("jumping", true);
    }

    public bool getJump() {
        if(canJump) 
            return true;
        
        return false;
    }

    public bool getDoubleJump() {
        if (canDoubleJump)
            return true;

        return false;
    }

    public void setJump(bool jump) {
        this.canJump = jump;
    }
    public void setDoubleJump(bool jump) {
        this.canDoubleJump = jump;
    }

}
