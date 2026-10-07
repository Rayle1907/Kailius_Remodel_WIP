using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(Traps))]
public sealed class TimedFlameHazard : MonoBehaviour
{
    private enum Phase
    {
        LavaBubble,
        BubblePop,
        GeyserInit,
        GeyserBurn,
        GeyserInitAfterBurn,
        GeyserHalf,
        GeyserOff
    }

    [SerializeField, Min(0.01f)] private float lavaBubbleDuration = 1.5f;
    [SerializeField, Min(0.01f)] private float bubblePopDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float geyserInitDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float geyserBurnDuration = 0.6f;
    [SerializeField, Min(0.01f)] private float geyserHalfDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float geyserOffDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float width = 0.65f;
    [SerializeField, Min(0.01f)] private float lowHeight = 0.75f;
    [SerializeField, Min(0.01f)] private float spikeHeight = 7f;
    [SerializeField, Min(0f)] private float geyserOffVisualOffset = 0.375f;
    [SerializeField] private SpriteRenderer flameVisual;
    [SerializeField] private Sprite lavaBubble;
    [SerializeField] private Sprite bubblePop;
    [SerializeField] private Sprite geyserInit;
    [SerializeField] private Sprite geyserBurn;
    [SerializeField] private Sprite geyserHalf;
    [SerializeField] private Sprite geyserOff;

    private BoxCollider2D hitbox;
    private Vector3 visualBaseScale;
    private float elapsed;
    private Phase phase;
    private bool initialized;

    public bool IsSpiking => phase == Phase.BubblePop
        || phase == Phase.GeyserInit
        || phase == Phase.GeyserBurn
        || phase == Phase.GeyserInitAfterBurn
        || phase == Phase.GeyserHalf;

    private void Awake()
    {
        hitbox = GetComponent<BoxCollider2D>();
        if (hitbox == null || flameVisual == null || !HasAllSprites())
        {
            Debug.LogError("TimedFlameHazard requires a BoxCollider2D, a SpriteRenderer, and all six phase sprites.", this);
            enabled = false;
            return;
        }

        if (flameVisual.transform == transform || !flameVisual.transform.IsChildOf(transform))
        {
            Debug.LogError("TimedFlameHazard flame visual must be a child of the hazard.", this);
            enabled = false;
            return;
        }

        visualBaseScale = flameVisual.transform.localScale;
        hitbox.isTrigger = true;
        initialized = true;
        ApplyPhase(Phase.LavaBubble);
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            return;
        }

        elapsed = 0f;
        ApplyPhase(Phase.LavaBubble);
    }

    private void OnDisable()
    {
        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        elapsed += Time.fixedDeltaTime;
        while (elapsed >= GetDuration(phase))
        {
            elapsed -= GetDuration(phase);
            ApplyPhase(GetNextPhase(phase));
        }
    }

    private float GetDuration(Phase currentPhase)
    {
        switch (currentPhase)
        {
            case Phase.LavaBubble: return lavaBubbleDuration;
            case Phase.BubblePop: return bubblePopDuration;
            case Phase.GeyserInit:
            case Phase.GeyserInitAfterBurn: return geyserInitDuration;
            case Phase.GeyserBurn: return geyserBurnDuration;
            case Phase.GeyserHalf: return geyserHalfDuration;
            default: return geyserOffDuration;
        }
    }

    private static Phase GetNextPhase(Phase currentPhase)
    {
        return (Phase)(((int)currentPhase + 1) % 7);
    }

    private void ApplyPhase(Phase nextPhase)
    {
        phase = nextPhase;
        float height = nextPhase == Phase.LavaBubble || nextPhase == Phase.GeyserOff
            ? lowHeight
            : spikeHeight;

        hitbox.offset = new Vector2(0f, height * 0.5f);
        hitbox.size = new Vector2(width, height);
        hitbox.enabled = nextPhase != Phase.GeyserOff;

        flameVisual.sprite = GetSprite(nextPhase);
        Transform visualTransform = flameVisual.transform;
        Vector2 sourceSize = flameVisual.sprite.bounds.size;
        bool rotateForVerticalGeyser = sourceSize.x > sourceSize.y;
        float visualOffset = nextPhase == Phase.GeyserOff ? geyserOffVisualOffset : 0f;
        visualTransform.localPosition = new Vector3(0f, height * 0.5f + visualOffset, visualTransform.localPosition.z);
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, rotateForVerticalGeyser ? 90f : 0f);

        if (sourceSize.x <= 0f || sourceSize.y <= 0f)
        {
            return;
        }

        float scaleX = rotateForVerticalGeyser ? height / sourceSize.x : width / sourceSize.x;
        float scaleY = rotateForVerticalGeyser ? width / sourceSize.y : height / sourceSize.y;
        visualTransform.localScale = new Vector3(
            visualBaseScale.x * scaleX,
            visualBaseScale.y * scaleY,
            visualBaseScale.z);
    }

    private Sprite GetSprite(Phase currentPhase)
    {
        switch (currentPhase)
        {
            case Phase.LavaBubble: return lavaBubble;
            case Phase.BubblePop: return bubblePop;
            case Phase.GeyserInit:
            case Phase.GeyserInitAfterBurn: return geyserInit;
            case Phase.GeyserBurn: return geyserBurn;
            case Phase.GeyserHalf: return geyserHalf;
            default: return geyserOff;
        }
    }

    private bool HasAllSprites()
    {
        return lavaBubble != null
            && bubblePop != null
            && geyserInit != null
            && geyserBurn != null
            && geyserHalf != null
            && geyserOff != null;
    }

    private void OnValidate()
    {
        lavaBubbleDuration = Mathf.Max(0.01f, lavaBubbleDuration);
        bubblePopDuration = Mathf.Max(0.01f, bubblePopDuration);
        geyserInitDuration = Mathf.Max(0.01f, geyserInitDuration);
        geyserBurnDuration = Mathf.Max(0.01f, geyserBurnDuration);
        geyserHalfDuration = Mathf.Max(0.01f, geyserHalfDuration);
        geyserOffDuration = Mathf.Max(0.01f, geyserOffDuration);
        width = Mathf.Max(0.01f, width);
        lowHeight = Mathf.Max(0.01f, lowHeight);
        spikeHeight = Mathf.Max(lowHeight + 0.01f, spikeHeight);
        geyserOffVisualOffset = Mathf.Max(0f, geyserOffVisualOffset);
    }

    private void OnDrawGizmosSelected()
    {
        DrawHitbox(lowHeight, new Color(1f, 0.6f, 0.1f, 0.35f));
        DrawHitbox(spikeHeight, new Color(1f, 0.1f, 0.05f, 0.2f));
    }

    private void DrawHitbox(float height, Color color)
    {
        Gizmos.color = color;
        Vector3 center = transform.TransformPoint(new Vector3(0f, height * 0.5f, 0f));
        Vector3 size = transform.TransformVector(new Vector3(width, height, 0f));
        Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0.01f));
    }
}
