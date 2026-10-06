using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CheckpointActivationRegion : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float activationHeight = 1.92f;
    [SerializeField] private BoxCollider2D directTouchRegion;

    private BoxCollider2D region;

    public float ActivationHeight => activationHeight;

    public bool IsDirectTouchCollider(Collider2D checkpointCollider)
    {
        return checkpointCollider == directTouchRegion;
    }

    private void Awake()
    {
        ConfigureRegion();
    }

    private void OnValidate()
    {
        activationHeight = Mathf.Max(0f, activationHeight);
        ConfigureRegion();
    }

    private void ConfigureRegion()
    {
        if (region == null)
        {
            region = GetComponent<BoxCollider2D>();
        }

        if (region == null)
        {
            return;
        }

        float verticalScale = Mathf.Abs(transform.lossyScale.y);
        if (verticalScale <= Mathf.Epsilon)
        {
            return;
        }

        float localHeight = activationHeight / verticalScale;
        region.size = new Vector2(region.size.x, localHeight);
        region.offset = new Vector2(region.offset.x, localHeight * 0.5f);
    }

    public static bool IsPlayerInActivationRange(
        float playerBottomY,
        float checkpointY,
        float height,
        bool directTouch = false)
    {
        if (directTouch)
        {
            return true;
        }

        float heightAboveCheckpoint = playerBottomY - checkpointY;
        return heightAboveCheckpoint >= 0f && heightAboveCheckpoint <= height;
    }
}
