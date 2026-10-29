using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
internal sealed class MobileDialogLayout : MonoBehaviour
{
    internal const float BodySize = 54f;
    internal const float HeadingSize = 72f;
    internal const float SecondarySize = 44f;
    internal const float ButtonSize = 48f;
    internal const float ButtonHeight = 120f;
    private CanvasScaler scaler;
    private Rect safeArea;
    private Vector2Int screenSize;
    private Coroutine measurements;

    internal static void Configure(GameObject root)
    {
        MobileDialogLayout layout = root.GetComponent<MobileDialogLayout>();
        if (layout == null) layout = root.AddComponent<MobileDialogLayout>();
        layout.Refresh();
    }

    private void OnEnable()
    {
        scaler = GetComponent<CanvasScaler>();
        Refresh();
    }

    private void Update()
    {
        if (safeArea != Screen.safeArea || screenSize.x != Screen.width || screenSize.y != Screen.height)
            Refresh();
    }

    private void Refresh()
    {
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        safeArea = Screen.safeArea;
        screenSize = new Vector2Int(Screen.width, Screen.height);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = Mathf.Max(1f, safeArea.height) / 1080f;
        if (!Application.isPlaying || !Debug.isDebugBuild || !isActiveAndEnabled) return;
        if (measurements != null) StopCoroutine(measurements);
        measurements = StartCoroutine(RecordMeasurements());
    }

    private IEnumerator RecordMeasurements()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        LogMeasurements();
        measurements = null;
    }

    [ContextMenu("Log Dialog Measurements")]
    private void LogMeasurements()
    {
        StringBuilder report = new StringBuilder();
        Canvas canvas = GetComponent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3[] corners = new Vector3[4];
        report.Append($"Dialog layout {name}: screen={Screen.width}x{Screen.height}, safeArea={Screen.safeArea}, canvasScale={canvas.scaleFactor}, dpi={Screen.dpi}");
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            text.rectTransform.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            report.Append($"\n{text.name}: font={text.fontSize}, scale={text.transform.lossyScale}, bounds={text.rectTransform.rect}, screenBounds={min}..{max}, preferred={text.GetPreferredValues()}, active={text.gameObject.activeInHierarchy}");
        }
        Debug.Log(report.ToString(), this);
    }

    private void OnDisable()
    {
        if (measurements != null) StopCoroutine(measurements);
        measurements = null;
    }

    internal static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    internal static void Panel(RectTransform panel, bool dense = false)
    {
        float margin = dense ? 0.03f : 0.1f;
        Place(panel, new Vector2(margin, 0.05f), new Vector2(1f - margin, 0.95f));
        FitArtwork(panel);
    }

    internal static void FitArtwork(RectTransform panel)
    {
        foreach (string childName in new[] { "PanelBackground", "PanelFrame", "Grass" })
        {
            RectTransform child = panel.Find(childName) as RectTransform;
            if (child == null) continue;
            AspectRatioFitter fitter = child.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.enabled = false;
            Place(child, new Vector2(0f, childName == "PanelBackground" ? 0f : -0.16f),
                new Vector2(1f, childName == "PanelBackground" ? 1f : 1.44f));
        }
    }

    internal static void Text(TMP_Text text, float size)
    {
        text.transform.localScale = Vector3.one;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.margin = Vector4.zero;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    internal static void Button(Button button, Vector2 min, Vector2 max)
    {
        Place((RectTransform)button.transform, min, max);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        Text(label, ButtonSize);
        Place(label.rectTransform, Vector2.zero, Vector2.one);
        label.rectTransform.offsetMin = new Vector2(24f, 8f);
        label.rectTransform.offsetMax = new Vector2(-24f, -8f);
    }

    internal static ScrollRect ReadingArea(TMP_Text text, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject root = new GameObject(text.name + " Reading Area", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        root.transform.SetParent(parent, false);
        RectTransform viewport = (RectTransform)root.transform;
        Place(viewport, min, max);
        root.GetComponent<Image>().color = Color.clear;
        text.transform.SetParent(viewport, false);
        Place(text.rectTransform, new Vector2(0f, 1f), Vector2.one);
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Text(text, BodySize);
        text.raycastTarget = true;
        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = text.rectTransform;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60f;
        return scroll;
    }
}
