using System;
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
        return ShowConfirmation(buttonTemplate, "Replay this gauntlet?", "Replay", "Keep exploring", onReplay, onDismiss);
    }

    public static GauntletReplayPrompt ShowConfirmation(LayeredButton buttonTemplate, string message,
        string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel = null, string heading = null)
    {
        if (BlocksSettings) return null;
        GameObject prefabRoot = Resources.Load<GameObject>("UI/ReplayPopup");
        ConfirmationPopupView prefab = prefabRoot != null ? prefabRoot.GetComponent<ConfirmationPopupView>() : null;
        if (prefab == null || EventSystem.current == null)
        {
            Debug.LogError("Gauntlet confirmation needs the UI/ReplayPopup prefab and an active EventSystem.");
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
        MobileDialogLayout.Configure(root);
        RectTransform blocker = Panel(root.transform, "Input Blocker", new Color(0f, 0f, 0f, 0.7f));
        blocker.gameObject.AddComponent<MobileSafeAreaFullScreen>();
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.offsetMin = blocker.offsetMax = Vector2.zero;
        MobileSafeArea safeArea = root.AddComponent<MobileSafeArea>();
        Button exploreButton = prompt.BuildDialog(safeArea.ContentRoot, prefab, message, confirmLabel, cancelLabel, heading);
        Time.timeScale = 0f;
        EventSystem.current.SetSelectedGameObject(exploreButton.gameObject);
        return prompt;
    }

    private Button BuildDialog(Transform parent, ConfirmationPopupView prefab, string message,
        string confirmLabel, string cancelLabel, string heading)
    {
        ConfirmationPopupView view = Instantiate(prefab, parent, false);
        view.Configure(message, confirmLabel, cancelLabel, heading);
        Button replayButton = view.ConfirmButton;
        Button exploreButton = view.CancelButton;
        replayButton.onClick = new Button.ButtonClickedEvent();
        exploreButton.onClick = new Button.ButtonClickedEvent();
        replayButton.onClick.AddListener(() => Close(true));
        exploreButton.onClick.AddListener(() => Close(false));
        Navigation replayNavigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = exploreButton, selectOnDown = exploreButton };
        Navigation exploreNavigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = replayButton, selectOnUp = replayButton };
        replayButton.navigation = replayNavigation;
        exploreButton.navigation = exploreNavigation;
        return exploreButton;
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
