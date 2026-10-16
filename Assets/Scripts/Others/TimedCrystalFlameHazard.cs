using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(Traps))]
public sealed class TimedCrystalFlameHazard : MonoBehaviour
{
    private enum Phase
    {
        Crystal,
        Eruption,
        FlameInit,
        FlameBurn,
        FlameRetreat,
        FlameHalf,
        FlameOff
    }

    [SerializeField, Min(0.01f)] private float crystalDuration = 1.5f;
    [SerializeField, Min(0.01f)] private float eruptionDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float flameInitDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float flameBurnDuration = 0.6f;
    [SerializeField, Min(0.01f)] private float flameHalfDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float flameOffDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float width = 0.65f;
    [SerializeField, Min(0.01f)] private float lowHeight = 0.75f;
    [SerializeField, Min(0.01f)] private float spikeHeight = 7f;
    [SerializeField, Min(0.01f)] private float crystalFramesPerSecond = 10f;
    [SerializeField] private SpriteRenderer crystalVisual;
    [SerializeField] private SpriteRenderer flameVisual;
    [SerializeField] private Sprite[] crystalFrames;
    [SerializeField] private Rect crystalArtworkRect = new Rect(3f, 0f, 10f, 15f);
    [SerializeField] private Sprite flameInit;
    [SerializeField] private Sprite flameHalf;
    [SerializeField] private Sprite flameOff;

    private BoxCollider2D hitbox;
    private float elapsed;
    private float crystalElapsed;
    private int crystalFrame;
    private Phase phase;
    private bool initialized;

    public bool IsSpiking => phase != Phase.Crystal && phase != Phase.FlameOff;

    private void Awake()
    {
        hitbox = GetComponent<BoxCollider2D>();
        if (!HasValidVisuals())
        {
            Debug.LogError("TimedCrystalFlameHazard requires separate child renderers, four crystal frames, a valid artwork rectangle, and all three flame sprites.", this);
            hitbox.enabled = false;
            enabled = false;
            return;
        }

        hitbox.isTrigger = true;
        crystalVisual.sortingLayerID = flameVisual.sortingLayerID;
        crystalVisual.sortingOrder = flameVisual.sortingOrder + 1;
        initialized = true;
        ResetCycle();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            ResetCycle();
        }
    }

    private void OnDisable()
    {
        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
    }

    private void Update()
    {
        crystalElapsed += Time.deltaTime;
        float frameDuration = 1f / crystalFramesPerSecond;
        while (crystalElapsed >= frameDuration)
        {
            crystalElapsed -= frameDuration;
            crystalFrame = (crystalFrame + 1) % crystalFrames.Length;
            ApplyCrystalFrame();
        }
    }

    private void FixedUpdate()
    {
        elapsed += Time.fixedDeltaTime;
        while (elapsed >= GetDuration(phase))
        {
            elapsed -= GetDuration(phase);
            ApplyPhase((Phase)(((int)phase + 1) % 7));
        }
    }

    private void ResetCycle()
    {
        elapsed = 0f;
        crystalElapsed = 0f;
        crystalFrame = 0;
        crystalVisual.enabled = true;
        ApplyCrystalFrame();
        ApplyPhase(Phase.Crystal);
    }

    private void ApplyCrystalFrame()
    {
        Sprite sprite = crystalFrames[crystalFrame];
        crystalVisual.sprite = sprite;
        float scaleX = width * sprite.pixelsPerUnit / crystalArtworkRect.width;
        float scaleY = lowHeight * sprite.pixelsPerUnit / crystalArtworkRect.height;
        Vector2 artworkCenter = crystalArtworkRect.center - sprite.pivot;
        Transform visual = crystalVisual.transform;
        visual.localScale = new Vector3(scaleX, scaleY, 1f);
        visual.localRotation = Quaternion.identity;
        visual.localPosition = new Vector3(
            -artworkCenter.x / sprite.pixelsPerUnit * scaleX,
            lowHeight * 0.5f - artworkCenter.y / sprite.pixelsPerUnit * scaleY,
            visual.localPosition.z);
    }

    private float GetDuration(Phase currentPhase)
    {
        switch (currentPhase)
        {
            case Phase.Crystal: return crystalDuration;
            case Phase.Eruption: return eruptionDuration;
            case Phase.FlameInit:
            case Phase.FlameRetreat: return flameInitDuration;
            case Phase.FlameBurn: return flameBurnDuration;
            case Phase.FlameHalf: return flameHalfDuration;
            default: return flameOffDuration;
        }
    }

    private void ApplyPhase(Phase nextPhase)
    {
        phase = nextPhase;
        float height = IsSpiking ? spikeHeight : lowHeight;
        hitbox.offset = new Vector2(0f, height * 0.5f);
        hitbox.size = new Vector2(width, height);
        hitbox.enabled = nextPhase != Phase.FlameOff;

        flameVisual.enabled = nextPhase != Phase.Crystal;
        flameVisual.sprite = nextPhase == Phase.FlameOff ? flameOff
            : nextPhase == Phase.FlameBurn || nextPhase == Phase.FlameHalf ? flameHalf
            : flameInit;
        Sprite sprite = flameVisual.sprite;
        Transform visual = flameVisual.transform;
        visual.localScale = new Vector3(
            width * sprite.pixelsPerUnit / sprite.rect.width,
            spikeHeight * sprite.pixelsPerUnit / sprite.rect.height,
            1f);
        visual.localRotation = Quaternion.identity;
        visual.localPosition = new Vector3(
            (sprite.pivot.x / sprite.rect.width - 0.5f) * width,
            sprite.pivot.y / sprite.rect.height * spikeHeight,
            visual.localPosition.z);
    }

    private bool HasValidVisuals()
    {
        if (crystalVisual == null || flameVisual == null || crystalVisual == flameVisual
            || crystalVisual.transform == transform || flameVisual.transform == transform
            || !crystalVisual.transform.IsChildOf(transform) || !flameVisual.transform.IsChildOf(transform)
            || crystalFrames == null || crystalFrames.Length != 4
            || crystalArtworkRect.width <= 0f || crystalArtworkRect.height <= 0f
            || flameInit == null || flameHalf == null || flameOff == null)
        {
            return false;
        }

        foreach (Sprite sprite in crystalFrames)
        {
            if (sprite == null)
            {
                return false;
            }
        }

        return true;
    }

    private void OnValidate()
    {
        crystalDuration = Mathf.Max(0.01f, crystalDuration);
        eruptionDuration = Mathf.Max(0.01f, eruptionDuration);
        flameInitDuration = Mathf.Max(0.01f, flameInitDuration);
        flameBurnDuration = Mathf.Max(0.01f, flameBurnDuration);
        flameHalfDuration = Mathf.Max(0.01f, flameHalfDuration);
        flameOffDuration = Mathf.Max(0.01f, flameOffDuration);
        width = Mathf.Max(0.01f, width);
        lowHeight = Mathf.Max(0.01f, lowHeight);
        spikeHeight = Mathf.Max(lowHeight + 0.01f, spikeHeight);
        crystalFramesPerSecond = Mathf.Max(0.01f, crystalFramesPerSecond);
    }
}
