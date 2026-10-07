using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MobilePrivacyControls : MonoBehaviour
{
    private GameObject panel;
    private TextMeshProUGUI status;

    private void OnEnable()
    {
        if (panel != null)
        {
            return;
        }

        MobileSafeArea safeArea = GetComponent<MobileSafeArea>();
        Transform parent = safeArea != null && safeArea.ContentRoot != null
            ? safeArea.ContentRoot : transform;

        Button open = CreateButton(parent, "Privacy", "PRIVACY", new Vector2(-118f, -36f),
            new Vector2(210f, 56f), new Vector2(1f, 1f), () =>
            {
                RefreshStatus();
                panel.SetActive(true);
            });
        open.transform.SetAsLastSibling();

        panel = new GameObject("Privacy Settings", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.sizeDelta = new Vector2(560f, 350f);
        panel.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.97f);
        status = CreateText(panel.transform, "Consent Status", new Vector2(0f, 115f),
            new Vector2(520f, 60f));

        CreateButton(panel.transform, "Allow Analytics", "ALLOW ANALYTICS", new Vector2(0f, 48f),
            new Vector2(440f, 55f), new Vector2(0.5f, 0.5f), () => SetConsent(true));
        CreateButton(panel.transform, "Disable Analytics", "DISABLE ANALYTICS", new Vector2(0f, -18f),
            new Vector2(440f, 55f), new Vector2(0.5f, 0.5f), () => SetConsent(false));
        CreateButton(panel.transform, "Delete Analytics", "DELETE ANALYTICS DATA", new Vector2(0f, -84f),
            new Vector2(440f, 55f), new Vector2(0.5f, 0.5f), () =>
            {
                ResearchAnalyticsBootstrap.RequestPlayerDataDeletion();
                RefreshStatus();
            });
        CreateButton(panel.transform, "Close Privacy", "CLOSE", new Vector2(0f, -145f),
            new Vector2(240f, 50f), new Vector2(0.5f, 0.5f), () => panel.SetActive(false));
        panel.SetActive(false);
    }

    private void SetConsent(bool granted)
    {
        ResearchAnalyticsBootstrap.SetAnalyticsConsent(granted);
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        status.text = ResearchAnalyticsBootstrap.HasAnalyticsConsent
            ? "Research analytics: ON" : "Research analytics: OFF";
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name,
        Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = 27f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string caption,
        Vector2 position, Vector2 size, Vector2 anchor, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.25f, 0.36f, 1f);
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(action);
        TextMeshProUGUI label = CreateText(go.transform, "Label", Vector2.zero, size);
        label.text = caption;
        label.fontSize = 23f;
        return button;
    }
}
