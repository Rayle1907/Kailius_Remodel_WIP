using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GauntletReplayPrompt : MonoBehaviour
{
    private static GauntletReplayPrompt active;
    private static int dismissedFrame = -1;
    private Action replay;
    private Action dismissed;
    private GameObject previousSelection;
    private float previousTimeScale;
    private bool closed;
    public static bool BlocksSettings => active != null || dismissedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        active = null;
        dismissedFrame = -1;
    }

    public static GauntletReplayPrompt Show(LayeredButton buttonTemplate, Action onReplay, Action onDismiss = null)
    {
        if (Time.timeScale <= 0f) return null;
        return ShowConfirmation(buttonTemplate, "Replay this gauntlet?", "Replay", "Keep exploring", onReplay, onDismiss, bodyFontSize: 32f);
    }

    public static GauntletReplayPrompt ShowConfirmation(LayeredButton buttonTemplate, string message,
        string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel = null, string heading = null,
        float bodyFontSize = 28f)
    {
        if (BlocksSettings) return null;
        if (buttonTemplate == null || buttonTemplate.PopUpPrefab == null || EventSystem.current == null)
        {
            Debug.LogError("Gauntlet confirmation needs a button template with a PopUp prefab and an active EventSystem.");
            return null;
        }
        GameObject root = new GameObject("Gauntlet Replay Prompt", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        GauntletReplayPrompt prompt = root.AddComponent<GauntletReplayPrompt>();
        active = prompt;
        prompt.replay = onConfirm;
        prompt.dismissed = onCancel;
        prompt.previousSelection = EventSystem.current.currentSelectedGameObject;
        prompt.previousTimeScale = Time.timeScale;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        RectTransform blocker = Panel(root.transform, "Input Blocker", new Color(0f, 0f, 0f, 0.7f));
        blocker.gameObject.AddComponent<MobileSafeAreaFullScreen>();
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.offsetMin = blocker.offsetMax = Vector2.zero;
        MobileSafeArea safeArea = root.AddComponent<MobileSafeArea>();
        RectTransform panel = Instantiate(buttonTemplate.PopUpPrefab, safeArea.ContentRoot, false);
        panel.name = "PopUp";
        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform titleRect = (RectTransform)titleObject.transform;
        titleRect.SetParent(panel, false);
        titleRect.sizeDelta = new Vector2(1100f, 100f);
        titleRect.anchoredPosition = new Vector2(0f, -65f);
        TMP_Text title = titleObject.GetComponent<TMP_Text>();
        TMP_Text templateText = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
        title.font = templateText.font;
        title.fontSharedMaterial = templateText.fontSharedMaterial;
        title.text = message;
        title.enableAutoSizing = true;
        title.fontSizeMin = 18f;
        title.fontSizeMax = bodyFontSize;
        titleRect.sizeDelta = new Vector2(1000f, 150f);
        titleRect.anchoredPosition = new Vector2(0f, -90f);
        title.fontSize = bodyFontSize;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        if (!string.IsNullOrEmpty(heading))
        {
            GameObject headingObject = new GameObject("Heading", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform headingRect = (RectTransform)headingObject.transform;
            headingRect.SetParent(panel, false);
            headingRect.anchorMin = new Vector2(0.5f, 1f);
            headingRect.anchorMax = new Vector2(0.5f, 1f);
            headingRect.sizeDelta = new Vector2(1000f, -110f);
            headingRect.anchoredPosition = new Vector2(0f, -350f);
            TMP_Text headingText = headingObject.GetComponent<TMP_Text>();
            headingText.font = templateText.font;
            headingText.fontSharedMaterial = templateText.fontSharedMaterial;
            headingText.text = heading;
            headingText.enableAutoSizing = false;
            headingText.fontSize = 56f;
            headingText.alignment = TextAlignmentOptions.Center;
            headingText.raycastTarget = false;
        }
        Button replayButton = prompt.CreateButton(buttonTemplate, panel, confirmLabel, -260f, true);
        Button exploreButton = prompt.CreateButton(buttonTemplate, panel, cancelLabel, 260f, false);
        Navigation replayNavigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = exploreButton, selectOnDown = exploreButton };
        Navigation exploreNavigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = replayButton, selectOnUp = replayButton };
        replayButton.navigation = replayNavigation;
        exploreButton.navigation = exploreNavigation;
        Time.timeScale = 0f;
        EventSystem.current.SetSelectedGameObject(exploreButton.gameObject);
        return prompt;
    }

    private static RectTransform Panel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)panel.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        panel.GetComponent<Image>().color = color;
        return rect;
    }

    private Button CreateButton(LayeredButton template, Transform parent, string label, float x, bool accept)
    {
        LayeredButton button = Instantiate(template, parent, false);
        button.name = label;
        button.SetLabel(label);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(420f, 64f);
        rect.anchoredPosition = new Vector2(x, -190f);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = 32f;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => Close(accept));
        return button;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Dismiss();
    }

    public void Dismiss() => Close(false);

    private void Close(bool accept)
    {
        if (closed) return;
        Action action = accept ? replay : dismissed;
        Release();
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
        action?.Invoke();
    }

    private void Release()
    {
        if (closed) return;
        closed = true;
        replay = null;
        dismissed = null;
        if (active != this) return;
        active = null;
        dismissedFrame = Time.frameCount;
        Time.timeScale = previousTimeScale;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
    }

    private void OnDisable() => Release();
}
