using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class GameplayOnboardingBannerView
{
    private readonly GameObject root;
    private readonly TextMeshProUGUI message;
    private readonly Canvas canvas;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private float lastScaleFactor;

    private GameplayOnboardingBannerView(GameObject root, TextMeshProUGUI message, Canvas canvas)
    {
        this.root = root;
        this.message = message;
        this.canvas = canvas;
        RefreshLayout();
        root.SetActive(false);
    }

    public bool IsAvailable => root != null && canvas != null;
    public bool IsVisible => IsAvailable && root.activeInHierarchy;

    public static GameplayOnboardingBannerView Create(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null) return null;

        GameObject root = new GameObject("Gameplay onboarding banner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        Image background = root.GetComponent<Image>();
        background.color = new Color(0.12f, 0.025f, 0.025f, 0.63f);
        background.raycastTarget = false;

        GameObject label = new GameObject("Explanation", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.SetParent(rootRect, false);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.raycastTarget = false;

        return new GameplayOnboardingBannerView(root, text, canvas);
    }

    public void Show(string text)
    {
        if (root == null) return;
        message.text = text;
        RefreshLayout();
        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    public void RefreshLayoutIfNeeded()
    {
        if (!IsAvailable) return;
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height
            || !Mathf.Approximately(lastScaleFactor, canvas.scaleFactor))
        {
            RefreshLayout();
        }
    }

    public void Dispose()
    {
        if (root == null) return;
        if (Application.isPlaying) Object.Destroy(root);
        else Object.DestroyImmediate(root);
    }

    private void RefreshLayout()
    {
        if (root == null || canvas == null) return;
        Rect safe = Screen.safeArea;
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, (safe.yMin + safe.height * 0.76f) / height);
        rootRect.anchorMax = new Vector2(1f, (safe.yMin + safe.height * 0.89f) / height);
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

        RectTransform labelRect = (RectTransform)message.transform;
        labelRect.anchorMin = new Vector2(safe.xMin / width, 0f);
        labelRect.anchorMax = new Vector2(safe.xMax / width, 1f);
        labelRect.offsetMin = new Vector2(12f, 6f);
        labelRect.offsetMax = new Vector2(-12f, -6f);
        message.fontSizeMax = 54f * Mathf.Max(1f, safe.height) / 1080f / Mathf.Max(0.001f, canvas.scaleFactor);
        message.fontSizeMin = message.fontSizeMax * 0.75f;

        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastScaleFactor = canvas.scaleFactor;
    }
}
