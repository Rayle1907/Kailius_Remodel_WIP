using UnityEngine;

public sealed class RewardMagnet : MonoBehaviour {
    [SerializeField] private float popOutDuration = 0.2f;
    [SerializeField] private float initialSpeed = 8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float maximumSpeed = 30f;
    [SerializeField] private float collectionDistance = 0.25f;

    private IRewardPickup reward;
    private Rigidbody2D[] bodies;
    private Collider2D[] colliders;
    private Stats player;
    private Collider2D playerCollider;
    private float elapsed;
    private float speed;
    private float nextPlayerSearch;
    private bool homing;

    public static void Attach(GameObject instance, string sourceSceneName) {
        if (instance == null || !GauntletHealthDropRules.IsGauntletScene(sourceSceneName)
            || instance.GetComponent<IRewardPickup>() == null || instance.GetComponent<RewardMagnet>() != null) {
            return;
        }

        instance.AddComponent<RewardMagnet>();
    }

    public static Stats ResolvePlayer(Collider2D collider) {
        Stats stats = collider.GetComponentInParent<Stats>();
        if (stats != null) return stats;
        PlayerController controller = collider.GetComponentInParent<PlayerController>();
        return controller != null ? controller.GetComponentInChildren<Stats>() : null;
    }

    private void Awake() {
        reward = GetComponent<IRewardPickup>();
        bodies = GetComponentsInChildren<Rigidbody2D>();
        colliders = GetComponentsInChildren<Collider2D>();
        speed = initialSpeed;
        FindPlayer();
    }

    private void FindPlayer() {
        nextPlayerSearch = Time.time + 0.5f;
        player = Stats.instance;
        if (player == null || player.gameObject.scene != gameObject.scene) {
            player = null;
            foreach (Stats candidate in FindObjectsByType<Stats>()) {
                if (candidate.gameObject.scene == gameObject.scene) {
                    player = candidate;
                    break;
                }
            }
        }
        playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        if (player != null && playerCollider == null) {
            playerCollider = player.GetComponentInParent<Collider2D>();
        }
    }

    private void Update() {
        if (Time.deltaTime <= 0f) return;

        elapsed += Time.deltaTime;
        if (elapsed < popOutDuration) return;

        if (!homing) {
            foreach (Rigidbody2D body in bodies) body.simulated = false;
            foreach (Collider2D rewardCollider in colliders) rewardCollider.enabled = false;
            homing = true;
        }

        if (player == null && Time.time >= nextPlayerSearch) FindPlayer();
        if (player == null || !player.isActiveAndEnabled || !player.CanAttack) return;

        Vector3 target = playerCollider != null && playerCollider.enabled
            ? playerCollider.bounds.center : player.transform.position;
        target.z = transform.position.z;
        speed = Mathf.MoveTowards(speed, maximumSpeed, acceleration * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude <= collectionDistance * collectionDistance) {
            reward.TryCollect(player);
        }
    }
}
