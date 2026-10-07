using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class DeathWallDirectionTrigger : MonoBehaviour
{
    [SerializeField] private DeathWall deathWall;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraController gameplayCamera;
    [SerializeField, Min(0f)] private float shakeDuration = 0.2f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.12f;

    private Stats playerStats;
    private bool consumed;
    private bool consumedAtCheckpoint;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnEnable()
    {
        if (deathWall == null)
        {
            deathWall = FindAnyObjectByType<DeathWall>();
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        if (gameplayCamera == null && Camera.main != null)
        {
            gameplayCamera = Camera.main.GetComponent<CameraController>();
        }

        if (deathWall == null || playerController == null || gameplayCamera == null)
        {
            Debug.LogError("DeathWallDirectionTrigger could not find the active DeathWall, PlayerController, and CameraController in the scene.", this);
            enabled = false;
            return;
        }

        playerStats = playerController.GetComponentInChildren<Stats>(true);
        playerController.CheckpointTouched += HandleCheckpointTouched;
        playerController.CheckpointRespawned += HandleCheckpointRespawned;
    }

    private void OnDisable()
    {
        if (playerController != null)
        {
            playerController.CheckpointTouched -= HandleCheckpointTouched;
            playerController.CheckpointRespawned -= HandleCheckpointRespawned;
        }
    }

    private void HandleCheckpointTouched(Transform checkpoint)
    {
        consumedAtCheckpoint = consumed;
    }

    private void HandleCheckpointRespawned(Transform checkpoint)
    {
        consumed = consumedAtCheckpoint;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || deathWall == null || gameplayCamera == null || playerController == null
            || playerStats == null || playerStats.health <= 0
            || other.GetComponentInParent<PlayerController>() != playerController)
        {
            return;
        }

        if (deathWall.TryReverseDirection())
        {
            consumed = true;
            gameplayCamera.Shake(shakeDuration, shakeAmplitude);
        }
    }

    private void OnValidate()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D region = GetComponent<BoxCollider2D>();
        if (region == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.75f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(region.offset, region.size);
    }
}
