using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns only the first-offer introduction; the offer owns purchasing and timeouts.</summary>
public sealed class ReviveOnboardingPresenter : MonoBehaviour
{
    public enum TutorialState { Typing, AwaitingDismissal, Dismissed }
    public enum PlacementMode { Manual, AutomaticBesideOffer }
    public const string IntroductionPageOne = "<b>A spark remains.</b>\n\nYou carry 100 Embers. When you fall, spend them to rise at your last checkpoint with your health restored.";
    public const string IntroductionPageTwo = "Each offer of another chance demands more Embers. Those you keep remain with you between runs.\n\nYou may always begin the gauntlet again, at no cost.";
    public const string Introduction = IntroductionPageOne + "\n\n" + IntroductionPageTwo;

    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform layoutRoot;
    [Header("Tutorial layout")]
    [Tooltip("Manual: move LayoutRoot with the Rect tool (T) in the prefab, then save. Its position, anchors and scale are preserved. Automatic Beside Offer: use the offset and screen fitting settings below.")]
    [SerializeField] private PlacementMode placement = PlacementMode.AutomaticBesideOffer;
    [Tooltip("Offset from the automatic left-side placement, in canvas units.")]
    [SerializeField] private Vector2 positionOffset;
    [Tooltip("Shrink the tutorial to fit the safe area. Never enlarges Content or changes its authored scale.")]
    [SerializeField] private bool autoFitToScreen = true;
    [SerializeField] private Image instructor;
    [SerializeField] private Image dialogueBox;
    [SerializeField] private TextMeshProUGUI introductionText;
    [SerializeField] private TextMeshProUGUI continuationPrompt;
    [SerializeField] private Image inputBlocker;

    public TutorialState State { get; private set; }
    public int CurrentPage { get; private set; }
    public bool IsConfigured => content != null && layoutRoot != null && instructor != null && instructor.sprite != null
        && dialogueBox != null && dialogueBox.sprite != null && introductionText != null
        && continuationPrompt != null && inputBlocker != null;

    private Action dismissed;
    private float elapsed;
    private int characterCount;
    private bool inputArmed;
    private bool showing;
    private RectTransform offerPanel;
    private Canvas canvas;
    private RectTransform[] contentRects;
    private readonly Vector3[] corners = new Vector3[4];

    public Vector2 PositionOffset
    {
        get => positionOffset;
        set { positionOffset = value; RefreshLayout(); }
    }
    public bool AutoFitToScreen
    {
        get => autoFitToScreen;
        set { autoFitToScreen = value; RefreshLayout(); }
    }

    public void Show(TMP_FontAsset font, RectTransform panel, Action onDismissed)
    {
        dismissed = onDismissed;
        offerPanel = panel;
        canvas = GetComponentInParent<Canvas>();
        contentRects = content.GetComponentsInChildren<RectTransform>(true);
        introductionText.font = continuationPrompt.font = font;
        content.gameObject.SetActive(true);
        inputBlocker.raycastTarget = true;
        CurrentPage = 0;
        showing = true;
        BeginPage();
        RefreshLayout();
    }

    private void BeginPage()
    {
        introductionText.text = CurrentPage == 0 ? IntroductionPageOne : IntroductionPageTwo;
        introductionText.maxVisibleCharacters = int.MaxValue;
        introductionText.ForceMeshUpdate();
        characterCount = introductionText.textInfo.characterCount;
        introductionText.maxVisibleCharacters = 0;
        continuationPrompt.text = "Tap anywhere to continue";
        continuationPrompt.gameObject.SetActive(false);
        elapsed = 0f;
        inputArmed = false;
        State = TutorialState.Typing;
    }

    private void Update()
    {
        if (!showing) return;
        RefreshLayout();
        Advance(Time.unscaledDeltaTime, DismissalInputHeld(), DismissalInputPressed());
    }

    // Explicit input/time arguments allow paused behavior and held-input handling to be tested.
    public void Advance(float unscaledSeconds, bool inputHeld, bool inputPressed)
    {
        if (!showing || State == TutorialState.Dismissed) return;
        if (State == TutorialState.Typing)
        {
            elapsed += Mathf.Max(0f, unscaledSeconds);
            introductionText.maxVisibleCharacters = Mathf.Min(characterCount, Mathf.FloorToInt(elapsed / 0.05f));
            if (introductionText.maxVisibleCharacters == characterCount)
            {
                State = TutorialState.AwaitingDismissal;
                continuationPrompt.gameObject.SetActive(true);
                inputArmed = false;
            }
            return; // A tap on the finishing frame is still an early tap.
        }
        if (!inputHeld && !inputPressed) inputArmed = true;
        if (!inputArmed || !inputPressed) return;
        if (CurrentPage == 0)
        {
            CurrentPage = 1;
            BeginPage();
            return;
        }
        State = TutorialState.Dismissed;
        content.gameObject.SetActive(false);
        Action callback = dismissed;
        dismissed = null;
        callback?.Invoke();
    }

    public static bool DismissalInputHeld()
    {
        return Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetButton("Submit");
    }

    private static bool DismissalInputPressed()
    {
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        return Input.GetMouseButtonDown(0) || Input.GetButtonDown("Submit");
    }

    public void RefreshLayout()
    {
        if (placement == PlacementMode.Manual) return;
        if (!showing || State == TutorialState.Dismissed || offerPanel == null || canvas == null || layoutRoot == null) return;
        RectTransform root = (RectTransform)transform;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Screen.safeArea.min, camera, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Screen.safeArea.max, camera, out Vector2 max);
        // Measure authored child rectangles, including the hidden continuation prompt.
        // Converting into wrapper space cancels its runtime scale and preserves Content edits.
        Vector2 boundsMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 boundsMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (RectTransform rect in contentRects)
        {
            if (rect == null) continue;
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = layoutRoot.InverseTransformPoint(corner);
                boundsMin = Vector2.Min(boundsMin, point);
                boundsMax = Vector2.Max(boundsMax, point);
            }
        }
        Vector2 size = boundsMax - boundsMin;
        float scale = autoFitToScreen
            ? Mathf.Min(1f, Mathf.Max(0.001f, Mathf.Min((max.x - min.x) / Mathf.Max(1f, size.x), (max.y - min.y) / Mathf.Max(1f, size.y))))
            : 1f;
        layoutRoot.localScale = Vector3.one * scale;

        offerPanel.GetWorldCorners(corners);
        float panelLeft = float.PositiveInfinity;
        float panelBottom = float.PositiveInfinity, panelTop = float.NegativeInfinity;
        foreach (Vector3 corner in corners)
        {
            Vector3 point = root.InverseTransformPoint(corner);
            panelLeft = Mathf.Min(panelLeft, point.x);
            panelBottom = Mathf.Min(panelBottom, point.y);
            panelTop = Mathf.Max(panelTop, point.y);
        }
        dialogueBox.rectTransform.GetWorldCorners(corners);
        float dialogueMinX = float.PositiveInfinity, dialogueMaxX = float.NegativeInfinity;
        foreach (Vector3 corner in corners)
        {
            float x = layoutRoot.InverseTransformPoint(corner).x;
            dialogueMinX = Mathf.Min(dialogueMinX, x);
            dialogueMaxX = Mathf.Max(dialogueMaxX, x);
        }
        Vector2 position = new Vector2(
            panelLeft + (dialogueMaxX - dialogueMinX) * scale * 0.1f - dialogueMaxX * scale,
            (panelBottom + panelTop) * 0.5f - (boundsMin.y + boundsMax.y) * scale * 0.5f) + positionOffset;
        if (autoFitToScreen)
        {
            position.x = Mathf.Clamp(position.x, min.x - boundsMin.x * scale, max.x - boundsMax.x * scale);
            position.y = Mathf.Clamp(position.y, min.y - boundsMin.y * scale, max.y - boundsMax.y * scale);
        }
        layoutRoot.position = root.TransformPoint(position);
    }

    public void Close()
    {
        showing = false;
        dismissed = null;
        offerPanel = null;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        showing = false;
        dismissed = null;
    }
}
