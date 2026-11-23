using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class AnalyticsRouteTrigger : MonoBehaviour
{
    [SerializeField] private AnalyticsSegmentDefinition segment;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player != null && GauntletRunTracker.Instance != null)
            GauntletRunTracker.Instance.ChooseAttemptRoute(player, segment);
    }
}
