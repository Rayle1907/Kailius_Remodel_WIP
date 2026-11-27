using TMPro;
using UnityEngine;

public sealed class ConfirmationPopupView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text headingText;
    [SerializeField] private LayeredButton confirmButton;
    [SerializeField] private LayeredButton cancelButton;
    [SerializeField, Min(0f)] private float screenPadding = 48f;
    [SerializeField] private bool fitTextOnAwake = true;
    [SerializeField] private bool fitToParent = true;

    public LayeredButton ConfirmButton => confirmButton;
    public LayeredButton CancelButton => cancelButton;

    private void Awake()
    {
        if (!fitTextOnAwake) return;
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            MobileDialogLayout.FitText(text, text.fontSize);
    }

    private void LateUpdate()
    {
        if (fitToParent) Fit();
    }

    private void Fit()
    {
        RectTransform rect = (RectTransform)transform;
        RectTransform parent = transform.parent as RectTransform;
        if (parent == null || rect.rect.width <= 0f || rect.rect.height <= 0f) return;
        float scale = Mathf.Min(1f,
            Mathf.Max(1f, parent.rect.width - screenPadding * 2f) / rect.rect.width,
            Mathf.Max(1f, parent.rect.height - screenPadding * 2f) / rect.rect.height);
        rect.localScale = new Vector3(scale, scale, 1f);
    }
}
