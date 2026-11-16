using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class DoubleJumpOnboardingTrigger : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float triggerRadius = 3f;

    private void Awake()
    {
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        triggerRadius = Mathf.Max(0f, triggerRadius);
        ConfigureTrigger();
    }

    private void ConfigureTrigger()
    {
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        if (trigger == null) return;
        trigger.isTrigger = true;
        trigger.radius = triggerRadius;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        FuryFeedback feedback = other.GetComponentInParent<FuryFeedback>();
        if (feedback == null) return;
        feedback.ShowDoubleJumpIntroduction();
    }
}
