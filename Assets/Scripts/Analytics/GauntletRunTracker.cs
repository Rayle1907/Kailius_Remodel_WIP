using System;
using Unity.Services.Analytics;
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
            SessionSeconds = ResearchAnalyticsBootstrap.SessionElapsedSeconds,
            SinceLastDeath = sinceLastDeath,
            Cause = string.IsNullOrWhiteSpace(causeOfDeath) ? "unknown" : causeOfDeath,
            HealthBefore = healthBeforeDeath,
            Balance = ResearchPlayerState.PremiumCurrencyBalance,
            OfferEligible = offerEligible,
            OfferShown = false,
            Variant = ResearchPlayerState.OfferVariant,
            ExperimentVersion = ExperimentVersion
        };
        AnalyticsService.Instance.RecordEvent(deathEvent);
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
            AnalyticsService.Instance.RecordEvent(shownEvent);
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
                OfferId = pendingOffer.OfferId,
                Response = response,
                DecisionSeconds = pendingOffer.ResolvedAt - pendingOffer.ShownAt,
                RevivePrice = pendingOffer.Price,
                BalanceBefore = pendingOffer.BalanceBefore,
                BalanceAfter = ResearchPlayerState.PremiumCurrencyBalance,
                Variant = ResearchPlayerState.OfferVariant,
                ExperimentVersion = ExperimentVersion
            };
            AnalyticsService.Instance.RecordEvent(resolvedEvent);
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
                SecondsAfterDecision = Time.realtimeSinceStartupAsDouble - pendingOffer.ResolvedAt,
                SceneName = currentSceneName,
                SegmentId = CurrentSegmentId,
                Variant = ResearchPlayerState.OfferVariant,
                ExperimentVersion = ExperimentVersion
            };
            AnalyticsService.Instance.RecordEvent(outcomeEvent);
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
                RunDuration = Time.realtimeSinceStartupAsDouble - runStartedAt,
                RunDeaths = deathsInRun,
                AcceptedRevives = acceptedRevives,
                FinalBalance = ResearchPlayerState.PremiumCurrencyBalance,
                Variant = ResearchPlayerState.OfferVariant,
                ExperimentVersion = ExperimentVersion
            };
            AnalyticsService.Instance.RecordEvent(endedEvent);
        }

        CurrentRunId = null;
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (IsCurrentSceneGauntlet)
        {
            EndCurrentRun("scene_changed");
        }
        StartRunForScene(next);
    }

    private void StartRunForScene(Scene scene)
    {
        currentSceneName = scene.name;
        CurrentSegmentId = string.IsNullOrWhiteSpace(scene.name) ? "unknown" : scene.name;
        IsCurrentSceneGauntlet = scene.name.IndexOf("Gauntlet", StringComparison.OrdinalIgnoreCase) >= 0;
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
        AnalyticsService.Instance.RecordEvent(startedEvent);
        startEventRecorded = true;
    }

    private static bool IsValidResponse(string response)
    {
        return response == "accepted"
            || response == "declined"
            || response == "ignored"
            || response == "quit";
    }
}
