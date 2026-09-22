using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteHurtFlash : MonoBehaviour {

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hurtColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField, Min(0f)] private float flashDuration = 0.12f;

    private Coroutine flashRoutine;
    private Color originalColor;

    private void Awake() {
        if (spriteRenderer == null) {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

    }

    private void OnDisable() {
        if (flashRoutine != null) {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
            if (spriteRenderer != null) {
                spriteRenderer.color = originalColor;
            }
        }
    }

    public void Flash() {
        if (spriteRenderer == null) {
            return;
        }

        if (flashRoutine != null) {
            StopCoroutine(flashRoutine);
        } else {
            originalColor = spriteRenderer.color;
        }

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine() {
        spriteRenderer.color = hurtColor;
        yield return new WaitForSecondsRealtime(flashDuration);
        spriteRenderer.color = originalColor;
        flashRoutine = null;
    }
}
