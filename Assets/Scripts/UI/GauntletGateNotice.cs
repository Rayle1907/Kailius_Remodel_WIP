using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GauntletGateNotice : MonoBehaviour
{
    public static void Show(string message)
    {
        GauntletGateNotice existing = FindAnyObjectByType<GauntletGateNotice>();
        if (existing != null) Destroy(existing.gameObject);
        GameObject root = new GameObject("Gauntlet Gate Notice", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GameObject panel = new GameObject("Message", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)panel.transform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.8f);
        rect.sizeDelta = new Vector2(750f, 110f);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.08f, 0.06f, 0.12f, 0.94f);
        background.raycastTarget = false;
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = message;
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        root.AddComponent<GauntletGateNotice>().StartCoroutine(Dismiss());
        IEnumerator Dismiss()
        {
            yield return new WaitForSecondsRealtime(5f);
            if (root != null) Destroy(root);
        }
    }
}
