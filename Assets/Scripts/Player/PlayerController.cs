using UnityEngine;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour {

    public event System.Action<Transform> CheckpointTouched;
    public event System.Action<Transform> CheckpointRespawned;
    public event System.Action<Vector2> SafePositionRespawned;
    public Transform CurrentRespawnCheckpoint => currentRespawn;
    public bool IsAlive => !dead && playerStats != null && playerStats.health > 0;

    public SpikeRecovery SpikeRecovery { get; private set; }
    public bool AnalyticsCanControl => !dead && isActiveAndEnabled && (SpikeRecovery == null || !SpikeRecovery.IsMovementLocked);
    public float AnalyticsMoveSpeed => controllerMobile != null && controllerMobile.activeSelf && touchMovement != null ? touchMovement.moveSpeed : moveSpeed;
    public float AnalyticsJumpHeight => controllerMobile != null && controllerMobile.activeSelf && touchMovement != null ? touchMovement.jumpHeight : jumpHeight;
    public float AnalyticsGravity => body == null ? Physics2D.gravity.y : Physics2D.gravity.y * body.gravityScale;

    public float moveSpeed;
    public float jumpHeight;
  
    public GameObject row;
    public GameObject menu;
    public GameObject stats;
    public GameObject controllerMobile;
    public int damagePatrols = 25;

    private bool canJump;
    private bool canDoubleJump;
    private bool rotacionA = false;
    private bool rotacionD = false;

    private bool dead = false;
    private float attactRate = 0.6f;
    private float nextAttactTime = 0.5f;
    private Transform currentRespawn;
    public ParticleSystem dust;

    private Rigidbody2D body;
    private Collider2D mainCollider;
    private readonly HashSet<Collider2D> pendingCheckpointTriggers = new HashSet<Collider2D>();
    private readonly Dictionary<Collider2D, int> enemyHeadSupportDirections = new Dictionary<Collider2D, int>();
    private readonly List<Collider2D> staleEnemyHeadSupportColliders = new List<Collider2D>();
    private readonly List<CheckpointRequirements.Shortfall> checkpointRequirementsRemaining = new List<CheckpointRequirements.Shortfall>();
    private readonly List<CheckpointRequirements> checkpointRequirementCandidates = new List<CheckpointRequirements>();
    private readonly List<CheckpointRequirements> nearbyCheckpointRequirements = new List<CheckpointRequirements>();
    private GauntletRequirements checkpointRequirementsOwner;
    private GauntletRequirementsHud checkpointRequirementsHud;
    private Transform lastCheckpointTouched;
    private int lastCheckpointTouchedFrame = -1;
    private Animator playerAnimator;
    private SpriteRenderer playerSprite;
    private PlayerControllerUP jumpController;
    private Stats playerStats;
    private Transform rowTransform;
    private bool isMoving;
    private MoveByTouch touchMovement;
    private MovingPlatform movingPlatformSupport;
    private float horizontalInput;
    private float horizontalMoveSpeed;

    void Awake() {
        SpikeRecovery = GetComponent<SpikeRecovery>();
        if (SpikeRecovery == null) SpikeRecovery = gameObject.AddComponent<SpikeRecovery>();
        body = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<Collider2D>();
        playerAnimator = GetComponent<Animator>();
        touchMovement = GetComponent<MoveByTouch>();
        playerSprite = GetComponent<SpriteRenderer>();
        jumpController = GetComponentInChildren<PlayerControllerUP>();
        playerStats = GetComponentInChildren<Stats>();
        rowTransform = row.transform;
    }

    void Start() {
        currentRespawn = FindClosestRespawn();
        if (GauntletRunTracker.Instance != null) GauntletRunTracker.Instance.BindAttemptPlayer(this);
    }

    void Update() {

        RefreshCheckpointRequirementsNotice();

        if(!dead) {

            if (Time.timeScale <= 0f || SpikeRecovery.IsMovementLocked) return;

            canJump = jumpController.getJump();
            canDoubleJump = jumpController.getDoubleJump();

            if (Input.GetKeyDown(KeyCode.Space)) {
                if(canJump) {
                    CreateDust();
                    ReleasePlatformSupport();
                    body.linearVelocity = new Vector2(body.linearVelocity.x, jumpHeight);
                    jumpController.setJump(false);
                } else if(canDoubleJump) {
                    CreateDust();
                    ReleasePlatformSupport();
                    body.linearVelocity = new Vector2(body.linearVelocity.x, jumpHeight);
                    jumpController.setDoubleJump(false);
                    canDoubleJump = false;
                }

            }

            bool moveLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool moveRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

            bool keyboardInput = moveLeft || moveRight;
            float horizontalInput = keyboardInput
                ? moveLeft == moveRight ? 0f : moveLeft ? -1f : 1f
                : 0f;
            bool usingMobileControls = controllerMobile != null && controllerMobile.activeSelf;
            if (!keyboardInput && usingMobileControls && touchMovement != null)
            {
                horizontalInput = touchMovement.HorizontalInput;
            }

            bool moving = Mathf.Abs(horizontalInput) > 0.1f;
            float speed = usingMobileControls && touchMovement != null ? touchMovement.moveSpeed : moveSpeed;
            this.horizontalInput = horizontalInput;
            horizontalMoveSpeed = speed;
            SetMoving(moving);

            if (moving)
            {
                CreateDust();
                playerSprite.flipX = horizontalInput < 0f;
                bool facingLeft = horizontalInput < 0f;
                if (facingLeft != rotacionA && facingLeft == rotacionD)
                {
                    rowTransform.Rotate(0f, 180f, 0f);
                }
                rotacionA = facingLeft;
                rotacionD = !facingLeft;
            }
        }
    }

    private void FixedUpdate() {
        if (dead || Time.timeScale <= 0f || SpikeRecovery.IsMovementLocked) return;

        if (movingPlatformSupport != null && !movingPlatformSupport.isActiveAndEnabled) {
            ReleasePlatformSupport();
        }

        bool moving = Mathf.Abs(horizontalInput) > 0.1f;
        float requestedVelocity = moving ? horizontalInput * horizontalMoveSpeed : 0f;

        if (movingPlatformSupport != null) {
            Vector2 platformVelocity = movingPlatformSupport.Velocity;
            float dampingFactor = 1f + body.linearDamping * Time.fixedDeltaTime;
            body.linearVelocity = new Vector2(
                requestedVelocity + platformVelocity.x * dampingFactor,
                body.linearVelocity.y);

            if (Mathf.Abs(platformVelocity.y) > 0.001f) {
                float gravityStep = Physics2D.gravity.y * body.gravityScale * Time.fixedDeltaTime;
                body.linearVelocity = new Vector2(
                    body.linearVelocity.x,
                    platformVelocity.y * dampingFactor - gravityStep);
            }
        } else if (moving) {
            body.linearVelocity = new Vector2(requestedVelocity, body.linearVelocity.y);
        }

        EnforceEnemyHeadSeparation();
    }

    public void SetMovingPlatformSupport(MovingPlatform platform) {
        if (movingPlatformSupport == platform) return;

        if (movingPlatformSupport != null) movingPlatformSupport.SetRider(this, false);
        movingPlatformSupport = platform;
        if (movingPlatformSupport != null) movingPlatformSupport.SetRider(this, true);
    }

    public void ClearMovingPlatformSupport(MovingPlatform platform) {
        if (movingPlatformSupport == platform) SetMovingPlatformSupport(null);
    }

    public void ReleasePlatformSupport() {
        if (movingPlatformSupport != null && body != null)
            body.linearVelocity -= movingPlatformSupport.Velocity;
        jumpController?.ClearGroundedState();
        SetMovingPlatformSupport(null);
        ClearEnemyHeadSupport();
    }

    private void OnDisable() {
        SetMovingPlatformSupport(null);
        ClearEnemyHeadSupport();
    }

    private void UpdateEnemyHeadSupport(Collision2D collision) {
        Collider2D enemyCollider = collision.collider;
        if (enemyCollider == null) return;

        bool alreadyTracked = enemyHeadSupportDirections.ContainsKey(enemyCollider);
        if (enemyCollider.GetComponentInParent<Enemy>() == null
            || body == null
            || !HasUpwardContact(collision)) {
            enemyHeadSupportDirections.Remove(enemyCollider);
            return;
        }

        if (!alreadyTracked && body.linearVelocity.y > 0.001f) return;

        if (!alreadyTracked)
            enemyHeadSupportDirections.Add(enemyCollider, GetEnemySlideDirection(enemyCollider));
    }

    private bool HasUpwardContact(Collision2D collision) {
        for (int i = 0; i < collision.contactCount; i++) {
            if (collision.GetContact(i).normal.y > 0.5f) return true;
        }

        return false;
    }

    private int GetEnemySlideDirection(Collider2D enemyCollider) {
        float playerCenterX = mainCollider != null ? mainCollider.bounds.center.x : transform.position.x;
        float horizontalOffset = playerCenterX - enemyCollider.bounds.center.x;
        if (Mathf.Abs(horizontalOffset) > 0.05f) return horizontalOffset > 0f ? 1 : -1;
        if (Mathf.Abs(horizontalInput) > 0.1f) return horizontalInput > 0f ? 1 : -1;
        return playerSprite != null && playerSprite.flipX ? -1 : 1;
    }

    private void EnforceEnemyHeadSeparation() {
        if (enemyHeadSupportDirections.Count == 0) return;

        staleEnemyHeadSupportColliders.Clear();
        foreach (KeyValuePair<Collider2D, int> support in enemyHeadSupportDirections) {
            Collider2D enemyCollider = support.Key;
            if (enemyCollider == null
                || !enemyCollider.enabled
                || !enemyCollider.gameObject.activeInHierarchy
                || enemyCollider.GetComponentInParent<Enemy>() == null) {
                staleEnemyHeadSupportColliders.Add(enemyCollider);
            }
        }

        for (int i = 0; i < staleEnemyHeadSupportColliders.Count; i++)
            enemyHeadSupportDirections.Remove(staleEnemyHeadSupportColliders[i]);

        if (enemyHeadSupportDirections.Count == 0) return;

        float separationSpeed = Mathf.Abs(horizontalMoveSpeed);
        foreach (KeyValuePair<Collider2D, int> support in enemyHeadSupportDirections) {
            Rigidbody2D enemyBody = support.Key.attachedRigidbody;
            float enemyVelocityX = enemyBody != null ? enemyBody.linearVelocity.x : 0f;
            float relativeSeparationSpeed = (body.linearVelocity.x - enemyVelocityX) * support.Value;
            if (relativeSeparationSpeed < separationSpeed) {
                body.linearVelocity = new Vector2(
                    enemyVelocityX + support.Value * separationSpeed,
                    body.linearVelocity.y);
            }
        }
    }

    private void ClearEnemyHeadSupport() {
        enemyHeadSupportDirections.Clear();
        staleEnemyHeadSupportColliders.Clear();
    }

    // Salto solo cuando pisa el suelo
    private void OnCollisionEnter2D(Collision2D collision) {
        UpdateEnemyHeadSupport(collision);

        if (collision.gameObject.CompareTag("ReSpawn")) {
            TryHandleCheckpointTouched(collision.collider);
        }

        if (collision.transform.tag == "Patrols") {
            if (Time.time >= nextAttactTime) {
                playerStats.ApplyDamage(damagePatrols, "enemy_contact");
                nextAttactTime = Time.time + attactRate;

            }
            body.AddForce(new Vector2(5, 5) * 2, ForceMode2D.Impulse);
        }
    }

    private void OnCollisionStay2D(Collision2D collision) {
        UpdateEnemyHeadSupport(collision);
    }

    private void OnCollisionExit2D(Collision2D collision) {
        enemyHeadSupportDirections.Remove(collision.collider);
    }

    // Scores
    private void OnTriggerEnter2D(Collider2D collision) {
        IRewardPickup reward = collision.GetComponent<IRewardPickup>();
        if (reward != null) {
            reward.TryCollect(playerStats);
            return;
        }

        if (collision.gameObject.CompareTag("Coins")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Gems")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Stars")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Heart")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Sword")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Shield")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("ReSpawn")) {
            if (!TryHandleCheckpointTouched(collision))
            {
                pendingCheckpointTriggers.Add(collision);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("ReSpawn")
            || !pendingCheckpointTriggers.Contains(collision))
        {
            return;
        }

        if (currentRespawn == collision.transform || TryHandleCheckpointTouched(collision))
        {
            pendingCheckpointTriggers.Remove(collision);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        pendingCheckpointTriggers.Remove(collision);
        if (collision.CompareTag("ReSpawn") && !HasPendingCheckpointCollider(collision.transform))
            CheckpointRequirementsNotice.HideFor(this, collision.transform.GetComponent<CheckpointRequirements>());
    }

    private void HandleCheckpointTouched(Transform checkpoint) {
        if (lastCheckpointTouched == checkpoint && lastCheckpointTouchedFrame == Time.frameCount)
        {
            return;
        }

        lastCheckpointTouched = checkpoint;
        lastCheckpointTouchedFrame = Time.frameCount;
        bool reachedNextCheckpoint = currentRespawn != checkpoint;
        currentRespawn = checkpoint;
        CheckpointTouched?.Invoke(checkpoint);
        if (reachedNextCheckpoint && GauntletRunTracker.Instance != null) {
            GauntletRunTracker.Instance.ReachCheckpoint(checkpoint);
        }
    }

    private bool TryHandleCheckpointTouched(Collider2D checkpointCollider)
    {
        CheckpointActivationRegion activationRegion = checkpointCollider.GetComponent<CheckpointActivationRegion>();
        if (activationRegion == null || mainCollider == null
            || !CheckpointActivationRegion.IsPlayerInActivationRange(
                mainCollider.bounds.min.y,
                checkpointCollider.transform.position.y,
                activationRegion.ActivationHeight,
                activationRegion.IsDirectTouchCollider(checkpointCollider)))
        {
            return false;
        }

        Transform checkpoint = checkpointCollider.transform;
        CheckpointRequirements checkpointRequirements = checkpoint.GetComponent<CheckpointRequirements>();
        if (checkpointRequirements != null)
        {
            GauntletRequirements owner = GetCheckpointRequirementsOwner();
            CheckpointRequirements.GateState gateState = checkpointRequirements.Evaluate(owner, checkpointRequirementsRemaining);
            if (gateState != CheckpointRequirements.GateState.Cleared)
            {
                return false;
            }
        }

        CheckpointRequirementsNotice.HideForActivation(this);
        if (checkpointRequirements != null && currentRespawn == checkpoint)
            return true;

        HandleCheckpointTouched(checkpoint);
        return true;
    }

    private void RefreshCheckpointRequirementsNotice()
    {
        if (!IsAlive || Time.timeScale <= 0f || (menu != null && menu.activeInHierarchy)
            || ResearchSurveyPrompt.IsOpen || MobilePrivacyControls.IsOpen || GauntletReplayPrompt.BlocksSettings)
        {
            CheckpointRequirementsNotice.HideForPlayer(this);
            return;
        }

        CheckpointRequirements nearest = null;
        float nearestDistance = float.MaxValue;
        checkpointRequirementCandidates.Clear();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            checkpointRequirementCandidates.AddRange(root.GetComponentsInChildren<CheckpointRequirements>(true));
        foreach (CheckpointRequirements candidate in checkpointRequirementCandidates)
        {
            if (candidate.gameObject.scene != gameObject.scene) continue;
            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > candidate.WarningRadius || distance >= nearestDistance) continue;
            GauntletRequirements owner = GetCheckpointRequirementsOwner();
            CheckpointRequirements.GateState state = candidate.Evaluate(owner, checkpointRequirementsRemaining);
            if (state != CheckpointRequirements.GateState.RequirementsRemain
                && state != CheckpointRequirements.GateState.Misconfigured) continue;
            nearest = candidate;
            nearestDistance = distance;
        }

        if (nearest == null)
        {
            CheckpointRequirementsNotice.HideForPlayer(this);
            return;
        }

        CheckpointRequirementsNotice.Show(this, nearest, GetCheckpointRequirementsOwner(), GetCheckpointRequirementsHud(), playerStats);
    }

    private GauntletRequirements GetCheckpointRequirementsOwner()
    {
        if (checkpointRequirementsOwner == null
            || checkpointRequirementsOwner.gameObject.scene != gameObject.scene)
        {
            checkpointRequirementsOwner = FindAnyObjectByType<GauntletRequirements>();
            if (checkpointRequirementsOwner != null
                && checkpointRequirementsOwner.gameObject.scene != gameObject.scene)
                checkpointRequirementsOwner = null;
        }

        return checkpointRequirementsOwner;
    }

    private GauntletRequirementsHud GetCheckpointRequirementsHud()
    {
        if (checkpointRequirementsHud == null
            || checkpointRequirementsHud.gameObject.scene != gameObject.scene)
        {
            checkpointRequirementsHud = FindAnyObjectByType<GauntletRequirementsHud>();
            if (checkpointRequirementsHud != null
                && checkpointRequirementsHud.gameObject.scene != gameObject.scene)
                checkpointRequirementsHud = null;
        }

        return checkpointRequirementsHud;
    }

    private bool HasPendingCheckpointCollider(Transform checkpoint)
    {
        pendingCheckpointTriggers.RemoveWhere(item => item == null || !item.enabled || !item.gameObject.activeInHierarchy);
        foreach (Collider2D item in pendingCheckpointTriggers)
            if (item.transform == checkpoint) return true;
        return false;
    }

    public void reSpawn()
    {
        TryRespawn();
    }

    public bool TryRespawn()
    {
        if (currentRespawn == null)
        {
            currentRespawn = FindClosestRespawn();
        }

        if (currentRespawn == null)
        {
            Debug.LogError("No Respawn checkpoint found.");
            return false;
        }

        ReleasePlatformSupport();
        Debug.Log("Respawning at: " + currentRespawn.name);

        transform.position = new Vector3(
            currentRespawn.position.x,
            currentRespawn.position.y,
            transform.position.z
        );
        CheckpointRespawned?.Invoke(currentRespawn);
        if (GauntletRunTracker.Instance != null)
        {
            GauntletRunTracker.Instance.BeginAttemptAfterRespawn("checkpoint_respawn");
        }
        return true;
    }

    internal void NotifySafePositionRespawned(Vector2 position)
    {
        SafePositionRespawned?.Invoke(position);
    }

    private Transform FindClosestRespawn() {
        GameObject[] respawns = GameObject.FindGameObjectsWithTag("ReSpawn");
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach(GameObject respawn in respawns) {
            if (respawn.GetComponent<CheckpointRequirements>() != null) continue;
            float distance = (respawn.transform.position - transform.position).sqrMagnitude;
            if(distance < closestDistance) {
                closestDistance = distance;
                closest = respawn.transform;
            }
        }

        return closest;
    }

    public void destroy() {
        ReleasePlatformSupport();
        Object.Destroy(gameObject, 5.0f);
        dead = true;
    }

    public bool isDead() {
        ReleasePlatformSupport();
        return this.dead = true;
    }

    public void Revive() {
        ReleasePlatformSupport();
        SpikeRecovery.ResetRecovery();
        dead = false;
    }

    void CreateDust() {
        if (!dust.isPlaying) {
            dust.Play();
        }
    }

    void SetMoving(bool moving) {
        if (isMoving == moving) {
            return;
        }

        isMoving = moving;
        playerAnimator.SetBool("moving", moving);
    }
}
