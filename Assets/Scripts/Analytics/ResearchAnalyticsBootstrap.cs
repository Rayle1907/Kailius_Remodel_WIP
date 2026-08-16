using System;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.UnityConsent;

public sealed class ResearchAnalyticsBootstrap : MonoBehaviour
{
    private const string ConsentKey = "research_analytics_consent";
    private static ResearchAnalyticsBootstrap instance;

    public static bool IsReady { get; private set; }
    public static double SessionElapsedSeconds => Time.realtimeSinceStartupAsDouble - sessionStartedAt;
    public static event Action AnalyticsReady;

    private static double sessionStartedAt;

    // Analytics registers itself with Unity Services during BeforeSceneLoad.
    // Starting after the scene loads guarantees that registration has completed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAutomatically()
    {
        if (instance != null)
        {
            return;
        }

        GameObject bootstrapObject = new GameObject("Research Analytics");
        DontDestroyOnLoad(bootstrapObject);
        instance = bootstrapObject.AddComponent<ResearchAnalyticsBootstrap>();
        bootstrapObject.AddComponent<GauntletRunTracker>();
    }

    private async void Awake()
    {
        if (instance != null && instance != this)
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

            // Accessing Instance verifies that the Analytics package, rather than
            // only Unity Services Core, completed its initialization.
            _ = AnalyticsService.Instance;
            IsReady = true;
            AnalyticsReady?.Invoke();

            Debug.Log($"Research Analytics initialized in '{environmentName}'.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Research Analytics initialization failed: {exception}");
        }
    }

    public static void SetAnalyticsConsent(bool granted)
    {
        PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyConsent(granted);
    }

    public static void RequestPlayerDataDeletion()
    {
        SetAnalyticsConsent(false);
        if (IsReady)
        {
            AnalyticsService.Instance.RequestDataDeletion();
        }
    }

    private static void ApplyStoredConsentBeforeInitialization()
    {
#if UNITY_EDITOR
        // Editor sessions contain developer test data only.
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
