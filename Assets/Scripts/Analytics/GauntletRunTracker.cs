using System;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GauntletRunTracker : MonoBehaviour
{
    public const string ExperimentVersion = "revive_study_v1";

    public static GauntletRunTracker Instance { get; private set; }

    public string CurrentRunId { get; private set; }
    public string CurrentSegmentId { get; private set; }
    public bool IsCurrentSceneGauntlet { get; private set; }
    public int PendingOfferPrice => pendingOffer?.Price ?? 0;
    public bool CanAffordPendingOffer => pendingOffer != null
        && CanAffordRevive(ResearchPlayerState.PremiumCurrencyBalance, pendingOffer.Price);

    private string currentSceneName;
    private string currentGauntletId;
    private int deathsInSegment;
    private int deathsInRun;
    private int offersShownInRun;
    private int acceptedRevives;
    private int lastDeathTotal;
    private double runStartedAt;
    private double lastDeathAt = -1;
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
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ResearchAnalyticsBootstrap.AnalyticsReady += OnAnalyticsReady;
        StartRunForScene(SceneManager.GetActiveScene());
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
        deathsInSegment++;
        deathsInRun++;
        int totalDeaths = ResearchPlayerState.IncrementTotalDeaths();
        double now = Time.realtimeSinceStartupAsDouble;
        double sinceLastDeath = lastDeathAt < 0 ? -1 : now - lastDeathAt;
        lastDeathAt = now;
        lastDeathTotal = totalDeaths;

        bool offerEligible = IsCurrentSceneGauntlet && ShouldShowOffer(deathsInRun);

        if (!ResearchAnalyticsBootstrap.IsReady)
        {
            return;
        }

        PlayerDeathEvent deathEvent = new PlayerDeathEvent
        {
            RunId = CurrentRunId,
            SceneName = currentSceneName,
            SegmentId = CurrentSegmentId,
            DeathTotal = totalDeaths,
            DeathInSegment = deathsInSegment,
            SessionSeconds = (float)ResearchAnalyticsBootstrap.SessionElapsedSeconds,
            SinceLastDeath = (float)sinceLastDeath,
            Cause = string.IsNullOrWhiteSpace(causeOfDeath) ? "unknown" : causeOfDeath,
            HealthBefore = healthBeforeDeath,
            Balance = ResearchPlayerState.PremiumCurrencyBalance,
            OfferEligible = offerEligible,
            OfferShown = false,
            Variant = ResearchPlayerState.OfferVariant,
            ExperimentVersion = ExperimentVersion
        };
        ResearchAnalytics.RecordDeath(deathEvent);
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
        return GetRevivePrice(offersShownInRun);
    }

    public static int GetRevivePrice(int zeroBasedOfferIndex)
    {
        if (zeroBasedOfferIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBasedOfferIndex));
        }

        // Currency and analytics fields are signed ints. Saturating at 2^30
        // preserves a positive, deterministically unaffordable price.
        return 1 << Math.Min(zeroBasedOfferIndex, 30);
    }

    public static bool CanAffordRevive(int balance, int revivePrice)
    {
        return balance >= 0 && revivePrice > 0 && balance >= revivePrice;
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
                OfferId = pendingOffer.OfferId,
                SceneName = currentSceneName,
                SegmentId = CurrentSegmentId,
                DeathTotal = lastDeathTotal,
                DeathInSegment = deathsInSegment,
                Balance = pendingOffer.BalanceBefore,
                RevivePrice = price,
                OfferNumber = offersShownInRun,
                CanAfford = CanAffordRevive(pendingOffer.BalanceBefore, price),
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

        if (response == "accepted"
            && (!CanAffordPendingOffer
                || !ResearchPlayerState.TrySpendPremiumCurrency(pendingOffer.Price)))
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

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (IsCurrentSceneGauntlet)
        {
            string endReason = previous.name == next.name
                ? "restarted"
                : next.name.Equals("Menu", StringComparison.OrdinalIgnoreCase)
                    ? "returned_to_menu"
                    : IsGauntletSceneName(next.name)
                        ? "gauntlet_changed"
                        : "returned_to_outer_world";
            EndCurrentRun(endReason);
        }
        StartRunForScene(next);
    }

    private void StartRunForScene(Scene scene)
    {
        currentSceneName = scene.name;
        CurrentSegmentId = string.IsNullOrWhiteSpace(scene.name) ? "unknown" : scene.name;
        IsCurrentSceneGauntlet = IsGauntletSceneName(scene.name);
        currentGauntletId = IsCurrentSceneGauntlet ? scene.name : string.Empty;
        CurrentRunId = Guid.NewGuid().ToString("N");
        deathsInSegment = 0;
        deathsInRun = 0;
        offersShownInRun = 0;
        acceptedRevives = 0;
        lastDeathAt = -1;
        runStartedAt = Time.realtimeSinceStartupAsDouble;
        pendingOffer = null;
        startEventRecorded = false;

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
            GauntletId = currentGauntletId,
            SceneName = currentSceneName,
            SegmentId = CurrentSegmentId,
            Balance = ResearchPlayerState.PremiumCurrencyBalance,
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
