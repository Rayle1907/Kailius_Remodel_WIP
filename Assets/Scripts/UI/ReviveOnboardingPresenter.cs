using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns only the first-offer introduction; the offer owns purchasing and timeouts.</summary>
public sealed class ReviveOnboardingPresenter : MonoBehaviour
{
    public enum TutorialState { Typing, AwaitingDismissal, Dismissed }
    public const string IntroductionPageOne = "<b>A spark remains.</b>\n\nYou carry 100 Embers. When you fall, spend them to rise at your last checkpoint with your health restored.";
    public const string IntroductionPageTwo = "Each offer of another chance demands more Embers. Those you keep remain with you between runs.\n\nYou may always begin the gauntlet again, at no cost.";
    public const string Introduction = IntroductionPageOne + "\n\n" + IntroductionPageTwo;

    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform layoutRoot;
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
    private bool applicationPaused;
    private bool applicationFocused;
    private bool waitingForInputRelease;
    private int interruptionFrame;

    private void Awake() => applicationFocused = Application.isFocused;

    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        ResetInterruptionInput();
    }

    private void OnApplicationFocus(bool focused)
    {
        applicationFocused = focused;
        ResetInterruptionInput();
    }

    private void ResetInterruptionInput()
    {
        if (!showing) return;
        inputArmed = false;
        waitingForInputRelease = true;
        interruptionFrame = Time.frameCount;
    }
    private RectTransform offerPanel;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private bool offerWasVisible;

    public void Show(TMP_FontAsset font, RectTransform panel, Action onDismissed)
    {
        RestoreOffer();
        dismissed = onDismissed;
        offerPanel = panel;
        introductionText.font = continuationPrompt.font = font;
        content.gameObject.SetActive(true);
        inputBlocker.raycastTarget = true;
        CurrentPage = 0;
        showing = true;
        RefreshLayout();
        BeginPage();
        offerWasVisible = panel != null && !transform.IsChildOf(panel) && panel.gameObject.activeSelf;
        if (offerWasVisible) panel.gameObject.SetActive(false);
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
        if (!showing || applicationPaused || !applicationFocused) return;
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height) RefreshLayout();
        Advance(Time.unscaledDeltaTime, DismissalInputHeld(), DismissalInputPressed());
    }

    // Explicit input/time arguments allow paused behavior and held-input handling to be tested.
    public void Advance(float unscaledSeconds, bool inputHeld, bool inputPressed)
    {
        if (!showing || State == TutorialState.Dismissed) return;
        if (waitingForInputRelease)
        {
            if (Time.frameCount <= interruptionFrame || inputHeld || inputPressed) return;
            waitingForInputRelease = false;
            return;
        }
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
        RestoreOffer();
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
        if (!showing || layoutRoot == null) return;
        RectTransform root = (RectTransform)transform;
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width > 0 && Screen.height > 0)
            MobileDialogLayout.Place(root,
                new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height),
                new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height));
        MobileDialogLayout.Place(layoutRoot, Vector2.zero, Vector2.one);
        MobileDialogLayout.Panel(content);
        MobileDialogLayout.Place(dialogueBox.rectTransform, new Vector2(0.2f, 0f), Vector2.one);
        MobileDialogLayout.Place(instructor.rectTransform, new Vector2(0f, 0.12f), new Vector2(0.22f, 0.58f));
        instructor.preserveAspect = true;
        MobileDialogLayout.Text(introductionText, MobileDialogLayout.BodySize);
        introductionText.alignment = TextAlignmentOptions.TopLeft;
        MobileDialogLayout.Place(introductionText.rectTransform, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.88f));
        MobileDialogLayout.Text(continuationPrompt, MobileDialogLayout.SecondarySize);
        continuationPrompt.transform.SetParent(content, false);
        continuationPrompt.color = Color.white;
        continuationPrompt.alignment = TextAlignmentOptions.Center;
        MobileDialogLayout.Place(continuationPrompt.rectTransform, new Vector2(0.24f, 0.01f), new Vector2(0.98f, 0.10f));
        inputBlocker.color = new Color(0f, 0f, 0f, 0.85f);
    }

    private void RestoreOffer()
    {
        if (offerWasVisible && offerPanel != null) offerPanel.gameObject.SetActive(true);
        offerWasVisible = false;
    }

    public void Close()
    {
        RestoreOffer();
        showing = false;
        dismissed = null;
        offerPanel = null;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        RestoreOffer();
        showing = false;
        dismissed = null;
        waitingForInputRelease = false;
        interruptionFrame = 0;
    }
}
