using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpikeRecovery : MonoBehaviour {
    [SerializeField, Min(0f)] private float protectionDuration = 1f;
    [SerializeField, Min(0f)] private float movementLockDuration = 0.15f;
    [SerializeField, Min(0f)] private float horizontalSpeedMultiplier = 0.6f;
    [SerializeField, Min(0f)] private float jumpSpeedMultiplier = 0.65f;

    private Rigidbody2D body;
    private PlayerController controller;
    private MoveByTouch touchMovement;
    private Stats stats;
    private float protectedUntil;
    private float movementLockedUntil;

    public bool IsMovementLocked => Time.time < movementLockedUntil;

    private void Awake() {
        body = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController>();
        touchMovement = GetComponent<MoveByTouch>();
        stats = GetComponentInChildren<Stats>();
    }

    private void OnDisable() {
        ResetRecovery();
    }

    public void ResetRecovery() {
        protectedUntil = 0f;
        movementLockedUntil = 0f;
    }

    public bool TryHit(int damage, Vector2 source, Transform safePosition = null) {
        if (!isActiveAndEnabled || stats == null || !stats.CanAttack || damage <= 0) return false;
        body?.WakeUp();
        if (Time.time < protectedUntil) return false;
        protectedUntil = Time.time + protectionDuration;
        int healthBefore = stats.health;
        stats.ApplyDamage(damage, "trap", true);
        if (stats.health <= 0) return true;
        if (GauntletRunTracker.Instance != null) {
            GauntletRunTracker.Instance.RecordPlayerFailure("hazard", healthBefore, stats.health);
        }
        movementLockedUntil = Time.time + movementLockDuration;
        if (safePosition != null) {
            Vector3 destination = safePosition.position;
            destination.z = transform.position.z;
            transform.position = destination;
            if (body != null) body.linearVelocity = Vector2.zero;
            return true;
        }
        if (body == null || controller == null) return true;
        float offset = body.position.x - source.x;
        float direction = Mathf.Abs(offset) > 0.05f ? Mathf.Sign(offset)
            : Mathf.Abs(body.linearVelocity.x) > 0.05f ? -Mathf.Sign(body.linearVelocity.x) : -1f;
        bool usingTouch = touchMovement != null && touchMovement.ControlesMoviles != null
            && touchMovement.ControlesMoviles.activeSelf;
        float moveSpeed = usingTouch ? touchMovement.moveSpeed : controller.moveSpeed;
        float jumpSpeed = usingTouch ? touchMovement.jumpHeight : controller.jumpHeight;
        body.linearVelocity = new Vector2(direction * moveSpeed * horizontalSpeedMultiplier,
            jumpSpeed * jumpSpeedMultiplier);
        return true;
    }
}
