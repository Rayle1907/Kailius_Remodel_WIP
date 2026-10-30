using TMPro;
using UnityEngine;

public sealed class ReviveOfferView : MonoBehaviour
{
    [SerializeField] private TMP_Text headingText;
    [SerializeField] private TMP_Text explanationText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text balanceText;
    [SerializeField] private TMP_Text affordabilityText;
    [SerializeField] private LayeredButton reviveButton;
    [SerializeField] private LayeredButton restartButton;
    [SerializeField, Min(0f)] private float screenPadding = 48f;
    private RectTransform rect;

    public LayeredButton ReviveButton => reviveButton;
    public LayeredButton RestartButton => restartButton;
    public bool IsConfigured => headingText != null && explanationText != null && costText != null
        && balanceText != null && affordabilityText != null && reviveButton != null && restartButton != null;

    public void Configure(int price, int balance)
    {
        costText.text = $"Cost: {price} Embers";
        balanceText.text = $"You have: {balance} Embers";
        affordabilityText.text = balance >= price ? "" : "Not enough Embers";
        reviveButton.interactable = balance >= price;
        restartButton.interactable = true;
        Fit();
    }

    private void OnEnable() => Fit();
    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (rect == null) rect = (RectTransform)transform;
        RectTransform parent = transform.parent as RectTransform;
        if (parent == null || rect.rect.width <= 0f || rect.rect.height <= 0f) return;
        float scale = Mathf.Min(1f,
            Mathf.Max(1f, parent.rect.width - screenPadding * 2f) / rect.rect.width,
            Mathf.Max(1f, parent.rect.height - screenPadding * 2f) / rect.rect.height);
        rect.localScale = new Vector3(scale, scale, 1f);
    }
}
