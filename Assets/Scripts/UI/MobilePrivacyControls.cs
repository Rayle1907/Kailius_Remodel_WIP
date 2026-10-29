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
    private static MobilePrivacyControls active;
    private GameObject previousSelection;
    private float previousTimeScale;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    public static bool IsOpen => active != null;
    public bool HasRequiredReferences => entryButton != null && overlay != null && status != null
        && allowButton != null && disableButton != null && deleteButton != null
        && closeButton != null && entryArea != null;

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
        overlay.transform.SetParent(null, false);
        MobileDialogLayout.Configure(overlay);
        entryButton.onClick.AddListener(Open);
        allowButton.onClick.AddListener(AllowAnalytics);
        disableButton.onClick.AddListener(DisableAnalytics);
        deleteButton.onClick.AddListener(DeleteAnalytics);
        closeButton.onClick.AddListener(Close);
    }

    private void Start()
    {
        UpdateEntrySafeArea();
        if (gameObject.scene.name == "Menu") StartCoroutine(ShowFirstLaunch());
    }

    private IEnumerator ShowFirstLaunch()
    {
        yield return null;
        while (EventSystem.current == null) yield return null;
        if (!PlayerPrefs.HasKey(SeenKey)) Open();
    }

    private void Update()
    {
        UpdateEntrySafeArea();
        if (active == this && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void UpdateEntrySafeArea()
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
        EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    private void AllowAnalytics() => SetConsent(true);
    private void DisableAnalytics() => SetConsent(false);

    private void DeleteAnalytics()
    {
        ResearchAnalyticsBootstrap.RequestPlayerDataDeletion();
        RefreshStatus();
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
            ? "Research analytics: ON" : "Research analytics: OFF";
    }

    private void Close()
    {
        MarkSeen();
        Release();
    }

    private void OnDisable() => Release();

    private void Release()
    {
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
        if (overlay != null && overlay.transform.parent == null) Destroy(overlay);
    }
}
