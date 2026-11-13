using System;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.UnityConsent;
using System.Threading.Tasks;

public sealed class ResearchAnalyticsBootstrap : MonoBehaviour
{
    private const string ConsentKey = "research_analytics_consent";
    private const string ConsentChoiceKey = "research_analytics_consent_choice_made";
    private const string DeletionPendingKey = "research_analytics_deletion_pending";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const int DefaultAnalyticsConsent = 1;
#else
    private const int DefaultAnalyticsConsent = 0;
#endif
    private static ResearchAnalyticsBootstrap instance;
    private bool initializationInProgress;
    private double nextPendingFlushAt;

    public static bool IsReady { get; private set; }
    public static bool IsShuttingDown { get; private set; }
    public static bool HasAnalyticsChoice => PlayerPrefs.GetInt(ConsentChoiceKey, DefaultAnalyticsConsent) == 1;
    public static bool HasAnalyticsConsent => HasAnalyticsChoice && !HasPendingDeletion && PlayerPrefs.GetInt(ConsentKey, DefaultAnalyticsConsent) == 1;
    public static bool HasPendingDeletion => PlayerPrefs.GetInt(DeletionPendingKey, 0) == 1;
    public static double SessionElapsedSeconds => Time.realtimeSinceStartupAsDouble - sessionStartedAt;
    public static event Action AnalyticsReady;

    private static double sessionStartedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAutomatically()
    {
        IsReady = false;
        IsShuttingDown = false;

        if (instance != null || FindAnyObjectByType<ResearchAnalyticsBootstrap>() != null)
        {
            return;
        }

        GameObject bootstrapObject = new GameObject("Research Analytics");
        DontDestroyOnLoad(bootstrapObject);
        instance = bootstrapObject.AddComponent<ResearchAnalyticsBootstrap>();
        bootstrapObject.AddComponent<GauntletRunTracker>();
        bootstrapObject.AddComponent<ResearchSurvey>();
    }

    public static void BeginShutdown()
    {
        IsShuttingDown = true;
        IsReady = false;
        ResearchAnalytics.DiscardPendingEvents();
    }

    private void OnApplicationQuit()
    {
        if (GauntletRunTracker.Instance != null) GauntletRunTracker.Instance.PrepareForShutdown();
        BeginShutdown();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ResearchAnalyticsBootstrap existing =
            FindAnyObjectByType<ResearchAnalyticsBootstrap>();
        if (existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStartedAt = Time.realtimeSinceStartupAsDouble;
        ResearchPlayerState.EnsureInitialized();
        InitializeServicesWithRetries();
    }

    private async void InitializeServicesWithRetries()
    {
        if (initializationInProgress || IsReady || IsShuttingDown) return;
        initializationInProgress = true;
        try
        {
            for (int attempt = 0; attempt < 3 && !IsShuttingDown; attempt++)
            {
                try
                {
                    ApplyStoredConsentBeforeInitialization();
                    UnityServices.ExternalUserId = ResearchPlayerState.PlayerId;

                    InitializationOptions options = new InitializationOptions();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    const string environmentName = "development";
#else
                    const string environmentName = "production";
#endif
                    options.SetEnvironmentName(environmentName);
                    await UnityServices.InitializeAsync(options);
                    if (IsShuttingDown) return;

                    _ = AnalyticsService.Instance;
                    IsReady = true;
                    TrySendPendingDeletion();
                    ResearchAnalytics.FlushPendingEvents();
                    try
                    {
                        AnalyticsReady?.Invoke();
                    }
                    catch (Exception callbackException)
                    {
                        Debug.LogError($"Research Analytics ready callback failed: {callbackException}");
                    }

                    Debug.Log($"Research Analytics initialized in '{environmentName}'.");
                    return;
                }
                catch (Exception exception)
                {
                    IsReady = false;
                    if (attempt == 2 || IsShuttingDown)
                    {
                        Debug.LogError($"Research Analytics initialization failed after {attempt + 1} attempt(s): {exception}");
                        return;
                    }

                    Debug.LogWarning($"Research Analytics initialization attempt {attempt + 1} failed; retrying: {exception.Message}");
                    await Task.Delay(1000 * (attempt + 1));
                }
            }
        }
        finally
        {
            initializationInProgress = false;
        }
    }

    private void Update()
    {
        if (!IsReady || !HasAnalyticsConsent || ResearchAnalytics.PendingEventCount == 0
            || Time.realtimeSinceStartupAsDouble < nextPendingFlushAt)
        {
            return;
        }

        nextPendingFlushAt = Time.realtimeSinceStartupAsDouble + 5d;
        ResearchAnalytics.FlushPendingEvents();
    }

    public static void SetAnalyticsConsent(bool granted)
    {
        if (granted && PlayerPrefs.GetInt(DeletionPendingKey, 0) == 1)
        {
            TrySendPendingDeletion();
            if (PlayerPrefs.GetInt(DeletionPendingKey, 0) == 1)
            {
                return;
            }
        }

        PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
        PlayerPrefs.SetInt(ConsentChoiceKey, 1);
        PlayerPrefs.Save();
        ApplyConsent(granted);
        if (!granted)
        {
            ResearchAnalytics.DiscardPendingEvents();
            ResearchSurvey.SuppressPendingAnalytics();
            if (GauntletRunTracker.Instance != null) GauntletRunTracker.Instance.SuppressPendingAnalytics();
        }
        else
        {
            if (GauntletRunTracker.Instance != null) GauntletRunTracker.Instance.BeginTelemetryAfterConsentGrant();
            if (instance != null && !IsReady) instance.InitializeServicesWithRetries();
        }
    }

    public static void RequestPlayerDataDeletion()
    {
        SetAnalyticsConsent(false);
        PlayerPrefs.SetInt(DeletionPendingKey, 1);
        PlayerPrefs.Save();
        TrySendPendingDeletion();
    }

    private static void TrySendPendingDeletion()
    {
        if (!IsReady || !HasPendingDeletion)
        {
            return;
        }

        try
        {
            AnalyticsService.Instance.RequestDataDeletion();
            PlayerPrefs.SetInt(DeletionPendingKey, 0);
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Research Analytics deletion request will retry: " + exception.Message);
        }
    }

    private static void ApplyStoredConsentBeforeInitialization()
    {
        ApplyConsent(HasAnalyticsConsent);
    }

    private static void ApplyConsent(bool granted)
    {
        ConsentState state = EndUserConsent.GetConsentState();
        state.AnalyticsIntent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
        state.AdsIntent = ConsentStatus.Denied;
        EndUserConsent.SetConsentState(state);
    }
}
