using System.Collections.Generic;
using UnityEngine;

public enum DeathWallDirection
{
    Right = 0,
    Left = 1,
    Down = 2,
    Up = 3
}

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class DeathWall : MonoBehaviour
{
    private const float CameraCatchupTolerance = 0.05f;
    private const int GradientTextureWidth = 256;
    private const int GradientTextureHeight = 16;

    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform activationCheckpoint;
    [SerializeField] private SpriteRenderer solidFillRenderer;
    [SerializeField] private SpriteRenderer gradientRenderer;
    [SerializeField, Min(0f)] private float speed = 10f;
    [SerializeField, Min(0f)] private float headStart = 8f;
    [SerializeField, Min(0f)] private float directionChangeRetreatDuration = 0.4f;
    [SerializeField, Min(0f)] private float directionChangeRetreatDistance = 2f;
    [SerializeField, Range(0.01f, 0.5f)] private float gradientViewportFraction = 0.15f;
    [SerializeField, Range(0f, 0.25f)] private float fillOverscanViewportFraction = 0.05f;
    [SerializeField, Min(0f)] private float verticalOverscan = 2f;
    [SerializeField, Range(0f, 1f)] private float fillOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float middleOpacity = 0.4f;
    [SerializeField, Range(0f, 1f)] private float outerOpacity = 0.15f;
    [SerializeField, Range(0.05f, 0.9f)] private float middleGradientStop = 0.45f;
    [SerializeField, Range(0.1f, 0.99f)] private float outerGradientStop = 0.8f;

    public bool AnalyticsActive => activated && !stoppedForDeath;
    public float AnalyticsSpeed => speed;
    public float AnalyticsHeadStart => headStart;

    private Rigidbody2D body;
    private BoxCollider2D lethalCollider;
    private Camera gameplayCamera;
    private CameraController cameraController;
    private DeathWallDirection direction = DeathWallDirection.Right;
    private DeathWallDirection checkpointDirection = DeathWallDirection.Right;
    private Stats playerStats;
    private Texture2D generatedSolidTexture;
    private Sprite generatedSolidSprite;
    private Texture2D generatedGradientTexture;
    private Sprite generatedGradientSprite;
    private bool activated;
    private bool stoppedForDeath;
    private bool resettingAfterRespawn;
    private bool hiddenForRevive;
    private readonly Dictionary<SpriteRenderer, bool> reviveRendererStates = new Dictionary<SpriteRenderer, bool>();
    private readonly List<RetreatingWall> retreatingWalls = new List<RetreatingWall>();

    private sealed class RetreatingWall
    {
        public Transform Root;
        public SpriteRenderer SolidFill;
        public SpriteRenderer Gradient;
        public DeathWallDirection Direction;
        public float StartOffset;
        public float EndOffset;
        public float Duration;
        public float Elapsed;
    }

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
            playerController.SafePositionRespawned += HandleSafePositionRespawned;
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

    private void Start()
    {
        if (activationCheckpoint != null
            && playerController != null
            && playerController.CurrentRespawnCheckpoint == activationCheckpoint)
        {
            checkpointDirection = direction;
            ActivateAt(activationCheckpoint);
        }
    }

    private void OnDisable()
    {
        ClearRetreatingWalls();
        if (playerController != null)
        {
            playerController.CheckpointTouched -= HandleCheckpointTouched;
            playerController.CheckpointRespawned -= HandleCheckpointRespawned;
            playerController.SafePositionRespawned -= HandleSafePositionRespawned;
        }
    }

    private void OnDestroy()
    {
        ClearRetreatingWalls();
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
        ResetAfterRespawn(checkpoint.position);
    }

    private void HandleSafePositionRespawned(Vector2 position)
    {
        if (!activated)
        {
            return;
        }

        ResetAfterRespawn(position);
    }

    private void ResetAfterRespawn(Vector2 respawnPosition)
    {
        ClearRetreatingWalls();
        stoppedForDeath = playerStats != null && playerStats.health <= 0;
        resettingAfterRespawn = !stoppedForDeath && gameplayCamera != null;

        Vector2 resetPosition = respawnPosition;
        SetAxisPosition(ref resetPosition, direction, GetAxisPosition(respawnPosition, direction) - DirectionSign(direction) * headStart);
        if (gameplayCamera != null)
        {
            resetPosition = CameraPosition2D;
            SetAxisPosition(ref resetPosition, direction, GetCameraEdge(direction));
            resetPosition = SetCrossAxisPosition(resetPosition, direction, CameraPosition2D);
        }

        body.position = resetPosition;
        if (lethalCollider != null)
        {
            lethalCollider.enabled = !stoppedForDeath && !resettingAfterRespawn;
        }
        UpdatePresentationAndCollider();
    }

    private void ActivateAt(Transform checkpoint)
    {
        activated = true;
        SetPresentationVisible(true);
        ResetAfterRespawn(checkpoint.position);
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

        if (resettingAfterRespawn)
        {
            return;
        }

        body.MovePosition(body.position + DirectionVector(direction) * speed * Time.fixedDeltaTime);
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
                Vector2 cameraPosition = CameraPosition2D;
                SetAxisPosition(ref cameraPosition, direction, GetCameraEdge(direction));
                body.position = cameraPosition;
            }

            UpdatePresentationAndCollider();
            return;
        }

        if (gameplayCamera == null)
        {
            UpdateRetreatingWalls(Time.deltaTime);
            return;
        }

        if (playerStats != null && playerStats.health <= 0)
        {
            PauseAtCheckpointOnDeath();
        }

        UpdateRetreatingWalls(Time.deltaTime);

        if (resettingAfterRespawn)
        {
            Vector2 resetPosition = CameraPosition2D;
            SetAxisPosition(ref resetPosition, direction, GetCameraEdge(direction));
            body.position = resetPosition;
            if (IsCameraAheadOfPlayer(CameraCatchupTolerance))
            {
                UpdatePresentationAndCollider();
                return;
            }

            resettingAfterRespawn = false;
            if (lethalCollider != null)
            {
                lethalCollider.enabled = true;
            }
        }

        FollowCameraCrossAxis();
        UpdatePresentationAndCollider();
    }

    private void FollowCameraCrossAxis()
    {
        if (gameplayCamera == null)
        {
            return;
        }

        body.position = SetCrossAxisPosition(body.position, direction, CameraPosition2D);
    }

    private void PauseAtCheckpointOnDeath()
    {
        ClearRetreatingWalls();
        resettingAfterRespawn = false;
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
        SetAxisPosition(ref position, direction, GetAxisPosition(checkpoint.position, direction) - DirectionSign(direction) * headStart);
        body.position = position;
    }

    private bool IsCameraAheadOfPlayer(float tolerance)
    {
        return gameplayCamera != null
            && playerStats != null
            && (GetAxisPosition(CameraPosition2D, direction) - GetAxisPosition(playerStats.transform.position, direction)) * DirectionSign(direction) > tolerance;
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

        float fillLength = UpdatePresentation(body.position, direction, solidFillRenderer, gradientRenderer);
        Vector2 directionVector = DirectionVector(direction);
        lethalCollider.offset = -directionVector * fillLength * 0.5f;
        lethalCollider.size = IsHorizontal(direction)
            ? new Vector2(fillLength, solidFillRenderer.transform.localScale.y)
            : new Vector2(solidFillRenderer.transform.localScale.x, fillLength);
    }

    private float UpdatePresentation(Vector2 frontier, DeathWallDirection chaseDirection, SpriteRenderer solidFill, SpriteRenderer gradient)
    {
        bool horizontal = IsHorizontal(chaseDirection);
        float directionSign = DirectionSign(chaseDirection);
        float viewportWidth = GetViewportWidth();
        float viewportHeight = GetViewportHeight();
        float viewportAxisLength = horizontal ? viewportWidth : viewportHeight;
        float crossAxisLength = (horizontal ? viewportHeight : viewportWidth) + verticalOverscan;
        float trailingEdge = GetCameraEdge(chaseDirection) - directionSign * viewportAxisLength * fillOverscanViewportFraction;
        float frontierAxis = GetAxisPosition(frontier, chaseDirection);
        float fillLength = Mathf.Max(0.01f, (frontierAxis - trailingEdge) * directionSign);
        float fadeLength = Mathf.Max(0.01f, viewportAxisLength * gradientViewportFraction);
        Vector2 directionVector = DirectionVector(chaseDirection);

        solidFill.transform.localPosition = new Vector3(-directionVector.x * fillLength * 0.5f, -directionVector.y * fillLength * 0.5f, 0f);
        solidFill.transform.localRotation = Quaternion.identity;
        solidFill.transform.localScale = horizontal
            ? new Vector3(fillLength, crossAxisLength, 1f)
            : new Vector3(crossAxisLength, fillLength, 1f);
        solidFill.color = new Color(0f, 0f, 0f, fillOpacity);

        gradient.transform.localPosition = new Vector3(directionVector.x * fadeLength * 0.5f, directionVector.y * fadeLength * 0.5f, 0f);
        gradient.transform.localRotation = Quaternion.Euler(0f, 0f, horizontal ? 0f : 90f);
        gradient.transform.localScale = new Vector3(fadeLength / (GradientTextureWidth / (float)GradientTextureHeight), crossAxisLength, 1f);
        gradient.flipX = directionSign < 0f;
        gradient.color = Color.white;

        return fillLength;
    }

    private void BeginRetreat()
    {
        if (directionChangeRetreatDuration <= 0f)
        {
            return;
        }

        Transform root = new GameObject("DeathWallRetreat").transform;
        root.gameObject.hideFlags = HideFlags.DontSave;
        root.SetParent(transform.parent, false);
        root.SetPositionAndRotation(transform.position, transform.rotation);
        root.localScale = transform.localScale;
        float startOffset = (GetAxisPosition(body.position, direction) - GetAxisPosition(CameraPosition2D, direction)) * DirectionSign(direction);
        retreatingWalls.Add(new RetreatingWall
        {
            Root = root,
            SolidFill = CopyRenderer(solidFillRenderer, root),
            Gradient = CopyRenderer(gradientRenderer, root),
            Direction = direction,
            StartOffset = startOffset,
            EndOffset = startOffset - directionChangeRetreatDistance,
            Duration = directionChangeRetreatDuration
        });
        if (hiddenForRevive)
        {
            RetreatingWall wall = retreatingWalls[retreatingWalls.Count - 1];
            HideReviveRenderer(wall.SolidFill);
            HideReviveRenderer(wall.Gradient);
        }
    }

    private static SpriteRenderer CopyRenderer(SpriteRenderer source, Transform parent)
    {
        SpriteRenderer copy = new GameObject(source.name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        copy.transform.SetParent(parent, false);
        copy.transform.localPosition = source.transform.localPosition;
        copy.transform.localRotation = source.transform.localRotation;
        copy.transform.localScale = source.transform.localScale;
        copy.sprite = source.sprite;
        copy.sharedMaterials = source.sharedMaterials;
        copy.sortingLayerID = source.sortingLayerID;
        copy.sortingOrder = source.sortingOrder;
        copy.color = source.color;
        copy.flipX = source.flipX;
        copy.flipY = source.flipY;
        copy.enabled = source.enabled;
        return copy;
    }

    private void UpdateRetreatingWalls(float deltaTime)
    {
        if (retreatingWalls.Count == 0)
        {
            return;
        }

        for (int i = retreatingWalls.Count - 1; i >= 0; i--)
        {
            RetreatingWall wall = retreatingWalls[i];
            if (wall.Root == null)
            {
                retreatingWalls.RemoveAt(i);
                continue;
            }

            wall.Elapsed += deltaTime;
            float viewportAxisLength = GetViewportAxisLength(wall.Direction);
            float offscreenOffset = -viewportAxisLength * (0.5f + fillOverscanViewportFraction)
                - Mathf.Max(0.01f, viewportAxisLength * gradientViewportFraction);
            wall.EndOffset = Mathf.Min(wall.EndOffset, offscreenOffset);
            float progress = Mathf.Clamp01(wall.Elapsed / wall.Duration);
            float offset = Mathf.Lerp(wall.StartOffset, wall.EndOffset, Mathf.SmoothStep(0f, 1f, progress));
            Vector2 cameraPosition = CameraPosition2D;
            float axisPosition = GetAxisPosition(cameraPosition, wall.Direction) + DirectionSign(wall.Direction) * offset;
            SetAxisPosition(ref cameraPosition, wall.Direction, axisPosition);
            wall.Root.position = new Vector3(cameraPosition.x, cameraPosition.y, wall.Root.position.z);
            UpdatePresentation(cameraPosition, wall.Direction, wall.SolidFill, wall.Gradient);

            if (progress >= 1f)
            {
                wall.Root.gameObject.SetActive(false);
                DestroyGeneratedObject(wall.Root.gameObject);
                retreatingWalls.RemoveAt(i);
            }
        }
    }

    private void ClearRetreatingWalls()
    {
        foreach (RetreatingWall wall in retreatingWalls)
        {
            if (wall.Root != null)
            {
                wall.Root.gameObject.SetActive(false);
                DestroyGeneratedObject(wall.Root.gameObject);
            }
        }

        retreatingWalls.Clear();
    }

    private float GetViewportWidth()
    {
        if (gameplayCamera == null || !gameplayCamera.orthographic)
        {
            return 20f;
        }

        return gameplayCamera.orthographicSize * 2f * gameplayCamera.aspect;
    }

    private float GetViewportHeight()
    {
        if (gameplayCamera == null)
        {
            return 100f;
        }

        return gameplayCamera.orthographic
            ? gameplayCamera.orthographicSize * 2f
            : 20f;
    }

    private float GetViewportAxisLength(DeathWallDirection chaseDirection)
    {
        return IsHorizontal(chaseDirection) ? GetViewportWidth() : GetViewportHeight();
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

    private Vector2 CameraPosition2D => new Vector2(CameraPosition.x, CameraPosition.y);

    private float GetCameraEdge(DeathWallDirection chaseDirection)
    {
        return GetAxisPosition(CameraPosition2D, chaseDirection)
            - DirectionSign(chaseDirection) * GetViewportAxisLength(chaseDirection) * 0.5f;
    }

    private static bool IsHorizontal(DeathWallDirection chaseDirection)
    {
        return chaseDirection == DeathWallDirection.Right || chaseDirection == DeathWallDirection.Left;
    }

    private static float DirectionSign(DeathWallDirection chaseDirection)
    {
        return chaseDirection == DeathWallDirection.Right || chaseDirection == DeathWallDirection.Up ? 1f : -1f;
    }

    private static Vector2 DirectionVector(DeathWallDirection chaseDirection)
    {
        switch (chaseDirection)
        {
            case DeathWallDirection.Right:
                return Vector2.right;
            case DeathWallDirection.Left:
                return Vector2.left;
            case DeathWallDirection.Down:
                return Vector2.down;
            case DeathWallDirection.Up:
                return Vector2.up;
            default:
                return Vector2.zero;
        }
    }

    private static float GetAxisPosition(Vector2 position, DeathWallDirection chaseDirection)
    {
        return IsHorizontal(chaseDirection) ? position.x : position.y;
    }

    private static void SetAxisPosition(ref Vector2 position, DeathWallDirection chaseDirection, float value)
    {
        if (IsHorizontal(chaseDirection))
        {
            position.x = value;
        }
        else
        {
            position.y = value;
        }
    }

    private static Vector2 SetCrossAxisPosition(Vector2 position, DeathWallDirection chaseDirection, Vector2 reference)
    {
        if (IsHorizontal(chaseDirection))
        {
            position.y = reference.y;
        }
        else
        {
            position.x = reference.x;
        }

        return position;
    }

    public DeathWallDirection Direction => direction;

    public void SetRevivePresentationHidden(bool hidden)
    {
        hiddenForRevive = hidden;
        if (hidden)
        {
            HideReviveRenderer(solidFillRenderer);
            HideReviveRenderer(gradientRenderer);
            foreach (RetreatingWall wall in retreatingWalls)
            {
                HideReviveRenderer(wall.SolidFill);
                HideReviveRenderer(wall.Gradient);
            }
            return;
        }

        foreach (KeyValuePair<SpriteRenderer, bool> state in reviveRendererStates)
        {
            if (state.Key != null)
            {
                state.Key.forceRenderingOff = state.Value;
            }
        }
        reviveRendererStates.Clear();
    }

    private void HideReviveRenderer(SpriteRenderer renderer)
    {
        if (renderer == null || reviveRendererStates.ContainsKey(renderer))
        {
            return;
        }

        reviveRendererStates.Add(renderer, renderer.forceRenderingOff);
        renderer.forceRenderingOff = true;
    }

    public bool TryReverseDirection()
    {
        switch (direction)
        {
            case DeathWallDirection.Right:
                return TrySetDirection(DeathWallDirection.Left);
            case DeathWallDirection.Left:
                return TrySetDirection(DeathWallDirection.Right);
            case DeathWallDirection.Down:
                return TrySetDirection(DeathWallDirection.Up);
            case DeathWallDirection.Up:
                return TrySetDirection(DeathWallDirection.Down);
            default:
                return false;
        }
    }

    public bool TrySetDirection(DeathWallDirection newDirection)
    {
        if (!IsValidDirection(newDirection)
            || !isActiveAndEnabled || !activated || playerStats == null || playerStats.health <= 0
            || solidFillRenderer == null || gradientRenderer == null)
        {
            return false;
        }

        if (direction == newDirection)
        {
            return true;
        }

        bool wasResettingAfterRespawn = resettingAfterRespawn;
        BeginRetreat();
        direction = newDirection;
        if (wasResettingAfterRespawn)
        {
            Vector2 edgePosition = CameraPosition2D;
            SetAxisPosition(ref edgePosition, direction, GetCameraEdge(direction));
            body.position = edgePosition;
            stoppedForDeath = false;
            UpdatePresentationAndCollider();
            return true;
        }

        float directionSign = DirectionSign(direction);
        float safeAxis = GetAxisPosition(playerStats.transform.position, direction) - directionSign * Mathf.Max(headStart, 0.01f);
        float fadeLength = Mathf.Max(0.01f, GetViewportAxisLength(direction) * gradientViewportFraction);
        float safeEdge = GetCameraEdge(direction) - directionSign * fadeLength;
        float nextAxis = directionSign > 0f ? Mathf.Min(safeEdge, safeAxis) : Mathf.Max(safeEdge, safeAxis);
        Vector2 nextPosition = CameraPosition2D;
        SetAxisPosition(ref nextPosition, direction, nextAxis);
        body.position = nextPosition;
        stoppedForDeath = false;
        resettingAfterRespawn = false;
        if (lethalCollider != null)
        {
            lethalCollider.enabled = true;
        }
        UpdatePresentationAndCollider();
        return true;
    }

    private static bool IsValidDirection(DeathWallDirection value)
    {
        return value == DeathWallDirection.Right
            || value == DeathWallDirection.Left
            || value == DeathWallDirection.Down
            || value == DeathWallDirection.Up;
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
