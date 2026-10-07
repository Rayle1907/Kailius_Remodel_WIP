using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class DeathWall : MonoBehaviour
{
    private const int GradientTextureWidth = 256;
    private const int GradientTextureHeight = 16;

    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform activationCheckpoint;
    [SerializeField] private SpriteRenderer solidFillRenderer;
    [SerializeField] private SpriteRenderer gradientRenderer;
    [SerializeField, Min(0f)] private float speed = 10f;
    [SerializeField, Min(0f)] private float headStart = 8f;
    [SerializeField, Range(0.01f, 0.5f)] private float gradientViewportFraction = 0.15f;
    [SerializeField, Range(0f, 0.25f)] private float fillOverscanViewportFraction = 0.05f;
    [SerializeField, Min(0f)] private float verticalOverscan = 2f;
    [SerializeField, Range(0f, 1f)] private float fillOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float middleOpacity = 0.4f;
    [SerializeField, Range(0f, 1f)] private float outerOpacity = 0.15f;
    [SerializeField, Range(0.05f, 0.9f)] private float middleGradientStop = 0.45f;
    [SerializeField, Range(0.1f, 0.99f)] private float outerGradientStop = 0.8f;

    private Rigidbody2D body;
    private BoxCollider2D lethalCollider;
    private Camera gameplayCamera;
    private CameraController cameraController;
    private int direction = 1;
    private int checkpointDirection = 1;
    private Stats playerStats;
    private Texture2D generatedSolidTexture;
    private Sprite generatedSolidSprite;
    private Texture2D generatedGradientTexture;
    private Sprite generatedGradientSprite;
    private bool activated;
    private bool stoppedForDeath;
    private bool waitingForCameraCatchup;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        lethalCollider = GetComponent<BoxCollider2D>();
        gameplayCamera = Camera.main;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.None;
        lethalCollider.isTrigger = true;

        if (solidFillRenderer == null || gradientRenderer == null)
        {
            Debug.LogError("DeathWall prefab needs SolidFill and FadeGradient SpriteRenderers assigned.", this);
            return;
        }

        CreateSolidSprite();
        CreateGradientSprite();
        solidFillRenderer.sprite = generatedSolidSprite;
        gradientRenderer.sprite = generatedGradientSprite;
        solidFillRenderer.sortingLayerName = "Tile";
        solidFillRenderer.sortingOrder = 10;
        gradientRenderer.sortingLayerName = "Tile";
        gradientRenderer.sortingOrder = 11;
        lethalCollider.enabled = false;
        solidFillRenderer.enabled = false;
        gradientRenderer.enabled = true;
    }

    private void OnEnable()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.CheckpointTouched += HandleCheckpointTouched;
            playerController.CheckpointRespawned += HandleCheckpointRespawned;
            playerStats = playerController.GetComponentInChildren<Stats>(true);
        }
        else
        {
            playerStats = FindAnyObjectByType<Stats>();
            Debug.LogError("DeathWall could not find a PlayerController to observe checkpoints.", this);
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }
    }

    private void OnDisable()
    {
        if (playerController != null)
        {
            playerController.CheckpointTouched -= HandleCheckpointTouched;
            playerController.CheckpointRespawned -= HandleCheckpointRespawned;
        }
    }

    private void OnDestroy()
    {
        DestroyGeneratedObject(generatedSolidSprite);
        DestroyGeneratedObject(generatedSolidTexture);
        DestroyGeneratedObject(generatedGradientSprite);
        DestroyGeneratedObject(generatedGradientTexture);
    }

    private void HandleCheckpointTouched(Transform checkpoint)
    {
        if (!activated && checkpoint == activationCheckpoint)
        {
            ActivateAt(checkpoint);
        }

        checkpointDirection = direction;
    }

    private void HandleCheckpointRespawned(Transform checkpoint)
    {
        if (!activated || checkpoint == null)
        {
            return;
        }

        direction = checkpointDirection;
        Vector2 resetPosition = body.position;
        resetPosition.x = checkpoint.position.x - direction * headStart;
        if (gameplayCamera != null)
        {
            resetPosition.y = CameraPosition.y;
        }

        body.position = resetPosition;
        stoppedForDeath = false;
        waitingForCameraCatchup = IsCameraAheadOfPlayer();
        UpdatePresentationAndCollider();
    }

    private void ActivateAt(Transform checkpoint)
    {
        activated = true;
        Vector2 position = body.position;
        position.x = gameplayCamera != null
            ? GetCameraLeft()
            : checkpoint.position.x - direction * headStart;
        if (gameplayCamera != null)
        {
            position.y = CameraPosition.y;
        }
        body.position = position;

        lethalCollider.enabled = true;
        SetPresentationVisible(true);
        FollowCameraHeight();
        UpdatePresentationAndCollider();
    }

    private void FixedUpdate()
    {
        if (!activated)
        {
            return;
        }

        if (playerStats != null && playerStats.health <= 0)
        {
            PauseAtCheckpointOnDeath();
            return;
        }

        if (waitingForCameraCatchup)
        {
            if (IsCameraAheadOfPlayer())
            {
                return;
            }

            waitingForCameraCatchup = false;
        }

        if (stoppedForDeath && playerStats != null)
        {
            Vector2 revivedPosition = body.position;
            revivedPosition.x = playerStats.transform.position.x - direction * headStart;
            body.position = revivedPosition;
            stoppedForDeath = false;
        }

        float nextX = body.position.x + direction * speed * Time.fixedDeltaTime;
        body.MovePosition(new Vector2(nextX, body.position.y));
    }

    private void LateUpdate()
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }

        if (!activated)
        {
            if (gameplayCamera != null)
            {
                body.position = new Vector2(GetCameraLeft(), CameraPosition.y);
            }

            UpdatePresentationAndCollider();
            return;
        }

        if (gameplayCamera == null)
        {
            return;
        }

        if (playerStats != null && playerStats.health <= 0)
        {
            PauseAtCheckpointOnDeath();
        }

        if (waitingForCameraCatchup)
        {
            if (!IsCameraAheadOfPlayer())
            {
                waitingForCameraCatchup = false;
            }
            else
            {
                Vector2 heldPosition = body.position;
                heldPosition.y = CameraPosition.y;
                body.position = heldPosition;
                UpdatePresentationAndCollider();
                return;
            }
        }

        FollowCameraHeight();
        UpdatePresentationAndCollider();
    }

    private void FollowCameraHeight()
    {
        if (gameplayCamera == null)
        {
            return;
        }

        Vector2 position = body.position;
        position.y = CameraPosition.y;
        body.position = position;
    }

    private void PauseAtCheckpointOnDeath()
    {
        if (stoppedForDeath)
        {
            return;
        }

        stoppedForDeath = true;
        direction = checkpointDirection;
        Transform checkpoint = playerController != null
            ? playerController.CurrentRespawnCheckpoint
            : null;
        if (checkpoint == null)
        {
            return;
        }

        Vector2 position = body.position;
        position.x = checkpoint.position.x - direction * headStart;
        body.position = position;
    }

    private bool IsCameraAheadOfPlayer()
    {
        return gameplayCamera != null
            && playerStats != null
            && (CameraPosition.x - playerStats.transform.position.x) * direction > 0f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController hitPlayer = other.GetComponentInParent<PlayerController>();
        if (hitPlayer == null)
        {
            hitPlayer = other.GetComponentInChildren<PlayerController>();
        }

        if (hitPlayer == null)
        {
            return;
        }

        Stats hitStats = hitPlayer.GetComponentInChildren<Stats>(true);
        if (hitStats != null && hitStats.health > 0)
        {
            hitStats.ApplyDamage(hitStats.health, "death_wall", true);
        }
    }

    private void CreateSolidSprite()
    {
        generatedSolidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "DeathWallSolidTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        generatedSolidTexture.SetPixel(0, 0, Color.white);
        generatedSolidTexture.Apply(false, true);
        generatedSolidSprite = Sprite.Create(
            generatedSolidTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f,
            0,
            SpriteMeshType.FullRect);
        generatedSolidSprite.name = "DeathWallSolidSprite";
        generatedSolidSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private static void DestroyGeneratedObject(Object generatedObject)
    {
        if (generatedObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generatedObject);
        }
        else
        {
            DestroyImmediate(generatedObject);
        }
    }

    private void CreateGradientSprite()
    {
        generatedGradientTexture = new Texture2D(GradientTextureWidth, GradientTextureHeight, TextureFormat.RGBA32, false)
        {
            name = "DeathWallRuntimeGradient",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[GradientTextureWidth * GradientTextureHeight];
        float middleStop = Mathf.Clamp(middleGradientStop, 0.01f, 0.98f);
        float outerStop = Mathf.Clamp(outerGradientStop, middleStop + 0.01f, 0.99f);
        for (int i = 0; i < GradientTextureWidth; i++)
        {
            float t = i / (float)(GradientTextureWidth - 1);
            float alpha;
            if (t <= middleStop)
            {
                alpha = Mathf.Lerp(fillOpacity, middleOpacity, t / middleStop);
            }
            else if (t <= outerStop)
            {
                alpha = Mathf.Lerp(middleOpacity, outerOpacity, (t - middleStop) / (outerStop - middleStop));
            }
            else
            {
                alpha = Mathf.Lerp(outerOpacity, 0f, (t - outerStop) / (1f - outerStop));
            }

            Color pixel = new Color(0f, 0f, 0f, alpha);
            for (int y = 0; y < GradientTextureHeight; y++)
            {
                pixels[y * GradientTextureWidth + i] = pixel;
            }
        }

        generatedGradientTexture.SetPixels(pixels);
        generatedGradientTexture.Apply(false, true);
        generatedGradientSprite = Sprite.Create(
            generatedGradientTexture,
            new Rect(0f, 0f, GradientTextureWidth, GradientTextureHeight),
            new Vector2(0.5f, 0.5f),
            GradientTextureHeight,
            0,
            SpriteMeshType.FullRect);
        generatedGradientSprite.name = "DeathWallRuntimeGradientSprite";
        generatedGradientSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void UpdatePresentationAndCollider()
    {
        if (solidFillRenderer == null || gradientRenderer == null)
        {
            return;
        }

        float viewportWidth = GetViewportWidth();
        float viewportHeight = gameplayCamera != null
            ? gameplayCamera.orthographicSize * 2f
            : 100f;
        float height = viewportHeight + verticalOverscan;
        float trailingEdge = GetCameraEdge(direction) - direction * viewportWidth * fillOverscanViewportFraction;
        float fillWidth = Mathf.Max(0.01f, (body.position.x - trailingEdge) * direction);
        float fadeWidth = Mathf.Max(0.01f, viewportWidth * gradientViewportFraction);

        solidFillRenderer.transform.localPosition = new Vector3(-direction * fillWidth * 0.5f, 0f, 0f);
        solidFillRenderer.transform.localScale = new Vector3(fillWidth, height, 1f);
        solidFillRenderer.color = new Color(0f, 0f, 0f, fillOpacity);

        gradientRenderer.transform.localPosition = new Vector3(direction * fadeWidth * 0.5f, 0f, 0f);
        gradientRenderer.transform.localScale = new Vector3(fadeWidth / (GradientTextureWidth / (float)GradientTextureHeight), height, 1f);
        gradientRenderer.flipX = direction < 0;
        gradientRenderer.color = Color.white;

        lethalCollider.offset = new Vector2(-direction * fillWidth * 0.5f, 0f);
        lethalCollider.size = new Vector2(fillWidth, height);
    }

    private float GetViewportWidth()
    {
        if (gameplayCamera == null || !gameplayCamera.orthographic)
        {
            return 20f;
        }

        return gameplayCamera.orthographicSize * 2f * gameplayCamera.aspect;
    }

    // DeathWall runs after camera follow; shake must never affect physics geometry.
    private Vector3 CameraPosition
    {
        get
        {
            if (gameplayCamera == null)
            {
                return body.transform.position;
            }

            if (cameraController == null)
            {
                cameraController = gameplayCamera.GetComponent<CameraController>();
            }

            return cameraController != null
                ? cameraController.UnshakenPosition
                : gameplayCamera.transform.position;
        }
    }

    private float GetCameraLeft()
    {
        return GetCameraEdge(1);
    }

    private float GetCameraEdge(int chaseDirection)
    {
        return CameraPosition.x - chaseDirection * GetViewportWidth() * 0.5f;
    }

    public int Direction => direction;

    public bool TryReverseDirection()
    {
        return TrySetDirection(-direction);
    }

    public bool TrySetDirection(int newDirection)
    {
        if ((newDirection != -1 && newDirection != 1)
            || !isActiveAndEnabled || !activated || playerStats == null || playerStats.health <= 0
            || solidFillRenderer == null || gradientRenderer == null)
        {
            return false;
        }

        if (direction == newDirection)
        {
            return true;
        }

        direction = newDirection;
        float safeX = playerStats.transform.position.x - direction * Mathf.Max(headStart, 0.01f);
        float edgeX = GetCameraEdge(direction);
        float nextX = direction > 0 ? Mathf.Min(edgeX, safeX) : Mathf.Max(edgeX, safeX);
        body.position = new Vector2(nextX, CameraPosition.y);
        stoppedForDeath = false;
        waitingForCameraCatchup = false;
        UpdatePresentationAndCollider();
        return true;
    }

    private void SetPresentationVisible(bool visible)
    {
        if (solidFillRenderer != null)
        {
            solidFillRenderer.enabled = visible;
        }

        if (gradientRenderer != null)
        {
            gradientRenderer.enabled = visible;
        }
    }
}
