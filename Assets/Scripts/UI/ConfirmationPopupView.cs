using TMPro;
using UnityEngine;

public sealed class ConfirmationPopupView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text headingText;
    [SerializeField] private LayeredButton confirmButton;
    [SerializeField] private LayeredButton cancelButton;
    [SerializeField, Min(0f)] private float screenPadding = 48f;

    public LayeredButton ConfirmButton => confirmButton;
    public LayeredButton CancelButton => cancelButton;

    public void Configure(string message, string confirmLabel, string cancelLabel, string heading)
    {
        messageText.text = message;
        headingText.text = heading;
        headingText.gameObject.SetActive(!string.IsNullOrEmpty(heading));
        confirmButton.SetLabel(confirmLabel);
        cancelButton.SetLabel(cancelLabel);
        Fit();
    }

    private void LateUpdate() => Fit();

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
