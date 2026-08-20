using System;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GauntletRunTracker : MonoBehaviour
{
    public const string ExperimentVersion = "revive_study_v1";

    public static GauntletRunTracker Instance { get; private set; }

    public string CurrentRunId { get; private set; }
    public string CurrentGauntletSessionId { get; private set; }
    public string CurrentSegmentId { get; private set; }
    public bool IsCurrentSceneGauntlet { get; private set; }
    public int DeathsInCurrentRun => deathsInRun;
    public int DeathsInCurrentGauntletSession => deathsInGauntletSession;
    public int CurrentOfferPrice => pendingOffer == null ? 0 : pendingOffer.Price;

    private string currentSceneName;
    private string currentGauntletId;
    private string currentRunStartReason;
    private string pendingRunStartReason;
    private int deathsInSegment;
    private int deathsInRun;
    private int deathsInGauntletSession;
    private int offersShownInRun;
    private int acceptedRevives;
    private int lastDeathTotal;
    // These survive scene object recreation while the application remains in
    // the same gauntlet session.
    private static string persistentGauntletSessionId;
    private static int persistentGauntletDeaths;
    private double runStartedAt;
    private double lastDeathAt = -1;
    private double lastFailureAt = -1;
    private bool startEventRecorded;
    private PendingOffer pendingOffer;

    private sealed class PendingOffer
    {
        public string OfferId;
        public int Price;
        public int BalanceBefore;
        public double ShownAt;
        public double ResolvedAt;
        public bool Resolved;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        // Keep the tracker alive across scene reloads so retries remain part of
        // the same gauntlet session. The session is reset explicitly when the
        // player leaves the gauntlet.
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ResearchAnalyticsBootstrap.AnalyticsReady += OnAnalyticsReady;
        StartRunForScene(SceneManager.GetActiveScene(), "application_start");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ResearchAnalyticsBootstrap.AnalyticsReady -= OnAnalyticsReady;
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
#if UNITY_EDITOR
        // Stopping Play Mode tears down Analytics before quit callbacks complete.
        ResearchAnalyticsBootstrap.BeginShutdown();
#endif
        if (pendingOffer != null && !pendingOffer.Resolved)
        {
            TryRecordDuringShutdown(() => ResolveOffer("quit"));
        }

        TryRecordDuringShutdown(() => EndCurrentRun("application_quit"));
    }

    private static void TryRecordDuringShutdown(Action recordAction)
    {
        try
        {
            recordAction();
        }
        catch (ServicesInitializationException)
        {
            // Unity can tear down UGS before MonoBehaviour quit callbacks in the Editor.
        }
        catch (NullReferenceException)
        {
            // Some Analytics internals are already released during Editor teardown.
        }
    }

    public void SetSegment(string segmentId)
    {
        if (string.IsNullOrWhiteSpace(segmentId) || segmentId == CurrentSegmentId)
        {
            return;
        }

        CurrentSegmentId = segmentId;
        deathsInSegment = 0;
    }

    public void RecordPlayerDeath(string causeOfDeath, int healthBeforeDeath)
    {
        // If a revive was accepted, the outcome is not known until a later
        // gameplay event. A new death is the first definitive "died_again"
        // outcome.
        if (pendingOffer != null && pendingOffer.Resolved)
        {
            RecordPostDecisionOutcome("died_again");
        }

        deathsInSegment++;
        deathsInRun++;
        int totalDeaths = ResearchPlayerState.IncrementTotalDeaths();
        double now = Time.realtimeSinceStartupAsDouble;
        double sinceLastDeath = lastDeathAt < 0
            ? now - runStartedAt
            : now - lastDeathAt;
        lastDeathAt = now;
        lastDeathTotal = totalDeaths;

        deathsInGauntletSession++;
        persistentGauntletDeaths = deathsInGauntletSession;
        bool offerEligible = IsCurrentSceneGauntlet && ShouldShowOffer(deathsInGauntletSession);

        if (!ResearchAnalyticsBootstrap.IsReady)
        {
            return;
        }

        PlayerDeathEvent deathEvent = new PlayerDeathEvent
        {
            RunId = CurrentRunId,
            GauntletSessionId = CurrentGauntletSessionId,
            SceneName = currentSceneName,
            SegmentId = CurrentSegmentId,
            DeathTotal = totalDeaths,
            DeathInSegment = deathsInSegment,
            DeathInGauntletSession = deathsInGauntletSession,
            SessionSeconds = (float)ResearchAnalyticsBootstrap.SessionElapsedSeconds,
            SinceLastDeath = (float)sinceLastDeath,
            Cause = string.IsNullOrWhiteSpace(causeOfDeath) ? "unknown" : causeOfDeath,
            HealthBefore = healthBeforeDeath,
            Balance = ResearchPlayerState.PremiumCurrencyBalance,
            OfferEligible = offerEligible,
            // The offer UI is created after this event is recorded. The
            // definitive record of an actually displayed offer is the
            // separate revive_offer_shown event, so do not infer it from
            // schedule eligibility here.
            OfferShown = false,
            Variant = ResearchPlayerState.OfferVariant,
            ExperimentVersion = ExperimentVersion
        };
        ResearchAnalytics.RecordDeath(deathEvent);
    }

    public void RecordPlayerFailure(string failureReason, int healthBeforeFailure, int healthAfterFailure)
    {
        if (!IsCurrentSceneGauntlet) return;

        double now = Time.realtimeSinceStartupAsDouble;
        double sinceLastFailure = lastFailureAt < 0 ? -1 : now - lastFailureAt;
        lastFailureAt = now;
        if (!ResearchAnalyticsBootstrap.IsReady) return;

        ResearchAnalytics.RecordFailure(new PlayerFailureEvent
        {
            RunId = CurrentRunId,
            GauntletSessionId = CurrentGauntletSessionId,
            SceneName = currentSceneName,
            SegmentId = CurrentSegmentId,
            DeathTotal = lastDeathTotal,
            DeathInSegment = deathsInSegment,
            DeathInGauntletSession = deathsInGauntletSession,
            SessionSeconds = (float)ResearchAnalyticsBootstrap.SessionElapsedSeconds,
            FailureReason = string.IsNullOrWhiteSpace(failureReason) ? "unknown" : failureReason,
            HealthBefore = healthBeforeFailure,
            HealthAfter = healthAfterFailure,
            Balance = ResearchPlayerState.PremiumCurrencyBalance,
            SinceLastFailure = (float)sinceLastFailure,
            Variant = ResearchPlayerState.OfferVariant,
            ExperimentVersion = ExperimentVersion
        });
    }

    public static bool ShouldShowOffer(int deathNumberInGauntlet)
    {
        return deathNumberInGauntlet == 3
            || deathNumberInGauntlet == 4
            || deathNumberInGauntlet == 6
            || (deathNumberInGauntlet >= 8 && deathNumberInGauntlet % 2 == 0);
    }

    public int GetNextRevivePrice()
    {
        return 1 << Mathf.Min(offersShownInRun, 30);
    }

    public string RecordOfferShown()
    {
        if (!IsCurrentSceneGauntlet || pendingOffer != null && !pendingOffer.Resolved)
        {
            return null;
        }

        int price = GetNextRevivePrice();
        pendingOffer = new PendingOffer
        {
            OfferId = Guid.NewGuid().ToString("N"),
            Price = price,
            BalanceBefore = ResearchPlayerState.PremiumCurrencyBalance,
            ShownAt = Time.realtimeSinceStartupAsDouble
        };
        offersShownInRun++;

        if (ResearchAnalyticsBootstrap.IsReady)
        {
            ReviveOfferShownEvent shownEvent = new ReviveOfferShownEvent
            {
                RunId = CurrentRunId,
                GauntletSessionId = CurrentGauntletSessionId,
                OfferId = pendingOffer.OfferId,
                SceneName = currentSceneName,
                SegmentId = CurrentSegmentId,
                DeathTotal = lastDeathTotal,
                DeathInSegment = deathsInSegment,
                Balance = pendingOffer.BalanceBefore,
                RevivePrice = price,
                OfferNumber = offersShownInRun,
                CanAfford = pendingOffer.BalanceBefore >= price,
                Variant = ResearchPlayerState.OfferVariant,
                ExperimentVersion = ExperimentVersion
            };
            ResearchAnalytics.RecordOfferShown(shownEvent);
        }

        return pendingOffer.OfferId;
    }

    public bool ResolveOffer(string response)
    {
        if (pendingOffer == null || pendingOffer.Resolved || !IsValidResponse(response))
        {
            return false;
        }

        if (response == "accepted" && !ResearchPlayerState.TrySpendPremiumCurrency(pendingOffer.Price))
        {
            return false;
        }

        pendingOffer.Resolved = true;
        pendingOffer.ResolvedAt = Time.realtimeSinceStartupAsDouble;
        if (response == "accepted")
        {
            acceptedRevives++;
        }

        if (ResearchAnalyticsBootstrap.IsReady)
        {
            ReviveOfferResolvedEvent resolvedEvent = new ReviveOfferResolvedEvent
            {
                RunId = CurrentRunId,
                GauntletSessionId = CurrentGauntletSessionId,
                OfferId = pendingOffer.OfferId,
                Response = response,
                DecisionSeconds = (float)(pendingOffer.ResolvedAt - pendingOffer.ShownAt),
                RevivePrice = pendingOffer.Price,
                BalanceBefore = pendingOffer.BalanceBefore,
                BalanceAfter = ResearchPlayerState.PremiumCurrencyBalance,
                Variant = ResearchPlayerState.OfferVariant,
                ExperimentVersion = ExperimentVersion
            };
            ResearchAnalytics.RecordOfferResolved(resolvedEvent);
        }

        return true;
    }

    public void RecordPostDecisionOutcome(string outcome)
    {
        if (pendingOffer == null || !pendingOffer.Resolved || string.IsNullOrWhiteSpace(outcome))
        {
            return;
        }

        if (ResearchAnalyticsBootstrap.IsReady)
        {
            ReviveOutcomeObservedEvent outcomeEvent = new ReviveOutcomeObservedEvent
            {
                RunId = CurrentRunId,
                GauntletSessionId = CurrentGauntletSessionId,
                OfferId = pendingOffer.OfferId,
                Outcome = outcome,
                SecondsAfterDecision = (float)(Time.realtimeSinceStartupAsDouble - pendingOffer.ResolvedAt),
                SceneName = currentSceneName,
                SegmentId = CurrentSegmentId,
                Variant = ResearchPlayerState.OfferVariant,
                ExperimentVersion = ExperimentVersion
            };
            ResearchAnalytics.RecordOutcome(outcomeEvent);
        }

        pendingOffer = null;
    }

    public void EndCurrentRun(string reason)
    {
        if (string.IsNullOrEmpty(CurrentRunId) || !IsCurrentSceneGauntlet)
        {
            return;
        }

        if (ResearchAnalyticsBootstrap.IsReady && startEventRecorded)
        {
            GauntletRunEndedEvent endedEvent = new GauntletRunEndedEvent
            {
                RunId = CurrentRunId,
                GauntletSessionId = CurrentGauntletSessionId,
                GauntletId = currentGauntletId,
                EndReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason,
                RunDuration = (float)(Time.realtimeSinceStartupAsDouble - runStartedAt),
                RunDeaths = deathsInRun,
                AcceptedRevives = acceptedRevives,
                FinalBalance = ResearchPlayerState.PremiumCurrencyBalance,
                Variant = ResearchPlayerState.OfferVariant,
                ExperimentVersion = ExperimentVersion
            };
            ResearchAnalytics.RecordRunEnded(endedEvent);
        }

        CurrentRunId = null;
    }

    public void RestartCurrentRun()
    {
        EndCurrentRun("restarted");
    }

    public void SetNextRunStartReason(string reason)
    {
        pendingRunStartReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        bool movingWithinGauntlet = IsCurrentSceneGauntlet
            && IsGauntletSceneName(next.name)
            && !string.Equals(previous.name, next.name, StringComparison.OrdinalIgnoreCase);

        if (IsCurrentSceneGauntlet && !movingWithinGauntlet)
        {
            string endReason = next.name.Equals("Menu", StringComparison.OrdinalIgnoreCase)
                ? "returned_to_menu"
                : "returned_to_outer_world";
            EndCurrentRun(endReason);
        }
        string startReason = pendingRunStartReason;
        pendingRunStartReason = null;
        if (string.IsNullOrWhiteSpace(startReason))
        {
            startReason = IsGauntletSceneName(previous.name)
                ? "restarted_within_gauntlet"
                : "entered_from_outer_world";
        }
        StartRunForScene(next, startReason, movingWithinGauntlet);
    }

    private void StartRunForScene(Scene scene, string startReason, bool preserveGauntletSession = false)
    {
        bool continuingRun = preserveGauntletSession
            && IsCurrentSceneGauntlet
            && !string.IsNullOrEmpty(CurrentRunId);
        currentSceneName = scene.name;
        currentRunStartReason = string.IsNullOrWhiteSpace(startReason) ? "unknown" : startReason;
        CurrentSegmentId = string.IsNullOrWhiteSpace(scene.name) ? "unknown" : scene.name;
        IsCurrentSceneGauntlet = IsGauntletSceneName(scene.name);
        currentGauntletId = IsCurrentSceneGauntlet ? scene.name : string.Empty;
        // A gauntlet scene reload is a retry, not a new gauntlet session. The
        // persistent session is cleared explicitly when entering an outer
        // world, so its presence is the reliable source of truth here.
        bool startsNewGauntletSession = IsCurrentSceneGauntlet
            && string.IsNullOrEmpty(persistentGauntletSessionId);
        if (startsNewGauntletSession)
        {
            persistentGauntletSessionId = Guid.NewGuid().ToString("N");
            persistentGauntletDeaths = 0;
            CurrentGauntletSessionId = persistentGauntletSessionId;
            deathsInGauntletSession = 0;
            offersShownInRun = 0;
        }
        else if (IsCurrentSceneGauntlet)
        {
            CurrentGauntletSessionId = persistentGauntletSessionId;
            deathsInGauntletSession = persistentGauntletDeaths;
        }
        else if (!IsCurrentSceneGauntlet)
        {
            CurrentGauntletSessionId = null;
            persistentGauntletSessionId = null;
            persistentGauntletDeaths = 0;
            deathsInGauntletSession = 0;
            offersShownInRun = 0;
        }
        if (continuingRun)
        {
            // Keep run-wide counters and timing across Gauntlet scene segments.
            deathsInSegment = 0;
        }
        else
        {
            CurrentRunId = Guid.NewGuid().ToString("N");
            deathsInSegment = 0;
            deathsInRun = 0;
            acceptedRevives = 0;
            lastDeathAt = -1;
            lastFailureAt = -1;
            runStartedAt = Time.realtimeSinceStartupAsDouble;
            pendingOffer = null;
            startEventRecorded = false;
        }

        TryRecordRunStarted();
    }

    private static bool IsGauntletSceneName(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName)
            && sceneName.IndexOf("Gauntlet", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnAnalyticsReady()
    {
        TryRecordRunStarted();
    }

    private void TryRecordRunStarted()
    {
        if (!IsCurrentSceneGauntlet || startEventRecorded || !ResearchAnalyticsBootstrap.IsReady)
        {
            return;
        }

        GauntletRunStartedEvent startedEvent = new GauntletRunStartedEvent
        {
            RunId = CurrentRunId,
            GauntletSessionId = CurrentGauntletSessionId,
            GauntletId = currentGauntletId,
            SceneName = currentSceneName,
            SegmentId = CurrentSegmentId,
            Balance = ResearchPlayerState.PremiumCurrencyBalance,
            RunStartReason = currentRunStartReason,
            Variant = ResearchPlayerState.OfferVariant,
            ExperimentVersion = ExperimentVersion
        };
        startEventRecorded = ResearchAnalytics.RecordRunStarted(startedEvent);
    }

    private static bool IsValidResponse(string response)
    {
        return response == "accepted"
            || response == "declined"
            || response == "ignored"
            || response == "quit";
    }
}
