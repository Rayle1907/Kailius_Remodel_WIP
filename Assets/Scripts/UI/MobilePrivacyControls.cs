using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MobilePrivacyControls : MonoBehaviour
{
    private const string SeenKey = "research_privacy_prompt_seen_v1";
    [SerializeField] private Button entryButton;
    [SerializeField] private GameObject overlay;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Button allowButton;
    [SerializeField] private Button disableButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform entryArea;
    [SerializeField] private CanvasGroup privacyPanelInteraction;
    [SerializeField] private GameObject deletionConfirmationOverlay;
    [SerializeField] private ConfirmationPopupView deletionConfirmation;
    [SerializeField] private Button initialSelection;
    [SerializeField, TextArea] private string analyticsEnabledText;
    [SerializeField, TextArea] private string analyticsDisabledText;
    [SerializeField, TextArea] private string deletionPendingText;
    private static MobilePrivacyControls active;
    private GameObject previousSelection;
    private float previousTimeScale;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private bool deletionConfirmationOpen;
    private int confirmationClosedFrame = -1;
    public static bool IsOpen => active != null;
    public bool HasRequiredReferences => entryButton != null && overlay != null && status != null
        && allowButton != null && disableButton != null && deleteButton != null
        && closeButton != null && entryArea != null && privacyPanelInteraction != null
        && deletionConfirmationOverlay != null && deletionConfirmation != null
        && deletionConfirmation.ConfirmButton != null && deletionConfirmation.CancelButton != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => active = null;

    private void Awake()
    {
        if (!HasRequiredReferences)
        {
            Debug.LogError("Privacy controls prefab has missing UI references.", this);
            enabled = false;
            return;
        }
        overlay.SetActive(false);
        deletionConfirmationOverlay.SetActive(false);
        overlay.transform.SetParent(null, false);
        Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        if (parentCanvas != null)
        {
            RectTransform rect = (RectTransform)transform;
            rect.SetParent(parentCanvas.rootCanvas.transform, false);
            MobileDialogLayout.Place(rect, Vector2.zero, Vector2.one);
        }
        entryButton.onClick.AddListener(Open);
        allowButton.onClick.AddListener(AllowAnalytics);
        disableButton.onClick.AddListener(DisableAnalytics);
        deleteButton.onClick.AddListener(DeleteAnalytics);
        closeButton.onClick.AddListener(Close);
        deletionConfirmation.ConfirmButton.onClick.AddListener(ConfirmDeletion);
        deletionConfirmation.CancelButton.onClick.AddListener(CancelDeletion);
    }

    private void Start()
    {
        UpdateSafeAreaLayout();
        StartCoroutine(ShowFirstLaunch());
    }

    private IEnumerator ShowFirstLaunch()
    {
        yield return null;
        while (EventSystem.current == null) yield return null;
        if (!ResearchAnalyticsBootstrap.HasAnalyticsConsent ||
            (gameObject.scene.name == "Menu" && !PlayerPrefs.HasKey(SeenKey))) Open();
    }

    public static void ShowRequiredConsent()
    {
        MobilePrivacyControls controls = FindAnyObjectByType<MobilePrivacyControls>();
        if (controls != null) controls.Open();
    }

    private void Update()
    {
        UpdateSafeAreaLayout();
        entryArea.gameObject.SetActive(!ResearchSurveyPrompt.IsOpen && !GauntletReplayPrompt.BlocksSettings);
        if (active == this && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void UpdateSafeAreaLayout()
    {
        if (entryArea == null || Screen.width <= 0 || Screen.height <= 0) return;
        if (lastScreenSize.x == Screen.width && lastScreenSize.y == Screen.height && lastSafeArea == Screen.safeArea) return;
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        entryArea.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
        entryArea.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
        entryArea.offsetMin = entryArea.offsetMax = Vector2.zero;
    }

    private void Open()
    {
        if (!HasRequiredReferences || IsOpen || ResearchSurveyPrompt.IsOpen ||
            GauntletReplayPrompt.BlocksSettings || EventSystem.current == null) return;
        active = this;
        previousSelection = EventSystem.current.currentSelectedGameObject;
        previousTimeScale = Time.timeScale;
        RefreshStatus();
        overlay.SetActive(true);
        Time.timeScale = 0f;
        EventSystem.current.SetSelectedGameObject(initialSelection != null
            ? initialSelection.gameObject : closeButton.gameObject);
    }

    private void AllowAnalytics()
    {
        if (deletionConfirmationOpen) return;
        SetConsent(true);
        if (ResearchAnalyticsBootstrap.HasAnalyticsConsent) Close();
    }
    private void DisableAnalytics()
    {
        if (!deletionConfirmationOpen) SetConsent(false);
    }

    private void DeleteAnalytics()
    {
        if (active != this || deletionConfirmationOpen || confirmationClosedFrame == Time.frameCount
            || EventSystem.current == null) return;
        deletionConfirmationOpen = true;
        privacyPanelInteraction.interactable = false;
        privacyPanelInteraction.blocksRaycasts = false;
        deletionConfirmationOverlay.SetActive(true);
        EventSystem.current.SetSelectedGameObject(deletionConfirmation.CancelButton.gameObject);
    }

    private void ConfirmDeletion()
    {
        if (active != this || !deletionConfirmationOpen) return;
        CancelDeletion();
        ResearchAnalyticsBootstrap.RequestPlayerDataDeletion();
        RefreshStatus();
    }

    private void CancelDeletion()
    {
        if (!deletionConfirmationOpen) return;
        ResetDeletionConfirmation();
        confirmationClosedFrame = Time.frameCount;
        if (active == this && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(deleteButton.gameObject);
    }

    private void ResetDeletionConfirmation()
    {
        deletionConfirmationOpen = false;
        if (deletionConfirmationOverlay != null) deletionConfirmationOverlay.SetActive(false);
        if (privacyPanelInteraction != null)
        {
            privacyPanelInteraction.interactable = true;
            privacyPanelInteraction.blocksRaycasts = true;
        }
    }

    private void SetConsent(bool granted)
    {
        ResearchAnalyticsBootstrap.SetAnalyticsConsent(granted);
        MarkSeen();
        RefreshStatus();
    }

    private static void MarkSeen()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    private void RefreshStatus()
    {
        status.text = ResearchAnalyticsBootstrap.HasAnalyticsConsent
            ? analyticsEnabledText
            : ResearchAnalyticsBootstrap.HasPendingDeletion
                ? deletionPendingText
                : analyticsDisabledText;
    }

    private void Close()
    {
        if (deletionConfirmationOpen)
        {
            CancelDeletion();
            return;
        }
        if (confirmationClosedFrame == Time.frameCount) return;
        MarkSeen();
        Release();
    }

    private void OnDisable() => Release();

    private void Release()
    {
        ResetDeletionConfirmation();
        if (active != this) return;
        active = null;
        if (overlay != null) overlay.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                ? previousSelection : null);
    }

    private void OnDestroy()
    {
        if (entryButton != null) entryButton.onClick.RemoveListener(Open);
        if (allowButton != null) allowButton.onClick.RemoveListener(AllowAnalytics);
        if (disableButton != null) disableButton.onClick.RemoveListener(DisableAnalytics);
        if (deleteButton != null) deleteButton.onClick.RemoveListener(DeleteAnalytics);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (deletionConfirmation != null)
        {
            if (deletionConfirmation.ConfirmButton != null)
                deletionConfirmation.ConfirmButton.onClick.RemoveListener(ConfirmDeletion);
            if (deletionConfirmation.CancelButton != null)
                deletionConfirmation.CancelButton.onClick.RemoveListener(CancelDeletion);
        }
        if (overlay != null && overlay.transform.parent == null) Destroy(overlay);
    }
}
