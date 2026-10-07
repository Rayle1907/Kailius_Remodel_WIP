using System;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.UnityConsent;

public sealed class ResearchAnalyticsBootstrap : MonoBehaviour
{
    private const string ConsentKey = "research_analytics_consent";
    private const string DeletionPendingKey = "research_analytics_deletion_pending";
    private static ResearchAnalyticsBootstrap instance;

    public static bool IsReady { get; private set; }
    public static bool IsShuttingDown { get; private set; }
    public static bool HasAnalyticsConsent => PlayerPrefs.GetInt(ConsentKey, 0) == 1;
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
    }

    public static void BeginShutdown()
    {
        IsShuttingDown = true;
        IsReady = false;
    }

    private void OnApplicationQuit()
    {
        BeginShutdown();
    }

    private async void Awake()
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

            _ = AnalyticsService.Instance;
            IsReady = true;
            TrySendPendingDeletion();
            AnalyticsReady?.Invoke();

            Debug.Log($"Research Analytics initialized in '{environmentName}'.");
        }
        catch (Exception exception)
        {
            IsReady = false;
            Debug.LogError($"Research Analytics initialization failed: {exception}");
        }
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
        PlayerPrefs.Save();
        ApplyConsent(granted);
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
        if (!IsReady || PlayerPrefs.GetInt(DeletionPendingKey, 0) != 1)
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
#if UNITY_EDITOR
        ApplyConsent(true);
#else
        ApplyConsent(PlayerPrefs.GetInt(ConsentKey, 0) == 1);
#endif
    }

    private static void ApplyConsent(bool granted)
    {
        ConsentState state = EndUserConsent.GetConsentState();
        state.AnalyticsIntent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
        state.AdsIntent = ConsentStatus.Denied;
        EndUserConsent.SetConsentState(state);
    }
}
