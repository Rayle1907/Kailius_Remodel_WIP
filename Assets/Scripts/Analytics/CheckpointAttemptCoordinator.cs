using System;
using System.Linq;
using UnityEngine;

internal sealed class CheckpointAttemptCoordinator
{
    private readonly SegmentAttemptState state = new SegmentAttemptState();
    private readonly Func<SegmentAttemptStartedEvent, bool> recordStarted;
    private readonly Action<SegmentAttemptEndedEvent> recordEnded;
    private AnalyticsSegmentDefinition[] definitions = Array.Empty<AnalyticsSegmentDefinition>();
    private AnalyticsSegmentDefinition definition;
    private SegmentSnapshot snapshot;
    private AnalyticsSegmentDefinition[] routeChoices = Array.Empty<AnalyticsSegmentDefinition>();
    private string routeStartReason;
    private PlayerController player;
    private Stats stats;
    private GauntletRequirements requirements;
    private bool pendingStart;
    private string pendingReason;
    private int resumeFrame;
    private string attemptRunId;
    private string attemptSceneName;
    private bool attemptTelemetryRecorded;
    private bool attemptTelemetrySuppressed;

    public string AttemptId => state.AttemptId;
    public string TelemetryAttemptId => attemptTelemetryRecorded && !attemptTelemetrySuppressed ? state.AttemptId : null;
    public string SegmentId => snapshot == null ? null : snapshot.segment_id;
    public bool AwaitingRoute => routeChoices.Length > 1;

    public CheckpointAttemptCoordinator(Func<SegmentAttemptStartedEvent, bool> recordStarted = null,
        Action<SegmentAttemptEndedEvent> recordEnded = null)
    {
        this.recordStarted = recordStarted ?? (value => ResearchAnalytics.RecordAttemptStarted(value));
        this.recordEnded = recordEnded ?? (value => ResearchAnalytics.RecordAttemptEnded(value));
    }

    public bool Bind(PlayerController boundPlayer, Stats boundStats, GauntletRequirements boundRequirements,
        AnalyticsSegmentDefinition[] sceneDefinitions, bool isGauntlet)
    {
        if (boundPlayer == null || player == boundPlayer) return false;
        player = boundPlayer;
        stats = boundStats;
        requirements = boundRequirements;
        definitions = sceneDefinitions ?? Array.Empty<AnalyticsSegmentDefinition>();
        SetContinuations(item => item.IsSceneEntry, "scene_entry");
        if (snapshot != null && stats != null && stats.health > 0 && Time.timeScale > 0f)
            Start("scene_entry", isGauntlet,
                GauntletRunTracker.Instance == null ? null : GauntletRunTracker.Instance.CurrentRunId);
        else if (snapshot != null)
            Schedule("scene_entry", Time.frameCount + 1);
        return true;
    }

    public void Schedule(string reason, int startFrame)
    {
        if (state.AttemptId != null || snapshot == null) return;
        pendingStart = true;
        pendingReason = reason;
        resumeFrame = startFrame;
    }

    public void Tick(int frame, double elapsedSeconds, float timeScale, bool isGauntlet, string runId)
    {
        bool controllable = player != null && player.AnalyticsCanControl && stats != null
            && stats.health > 0 && timeScale > 0f;
        if (state.AttemptId != null && controllable) state.Advance(elapsedSeconds);
        if (pendingStart && controllable && frame >= resumeFrame)
            Start(pendingReason, isGauntlet, runId);
    }

    public bool ReachCheckpoint(Transform checkpoint, int frame)
    {
        if (definition == null || definition.TargetCheckpoint != checkpoint) return false;
        End("success", "next_checkpoint_reached");
        SetContinuations(item => item.StartCheckpoint == checkpoint, "checkpoint_reached");
        Schedule("checkpoint_reached", frame + 1);
        return true;
    }

    public bool ChooseRoute(PlayerController enteringPlayer, AnalyticsSegmentDefinition segment, int frame)
    {
        if (player != enteringPlayer || stats == null || stats.health <= 0 || !AwaitingRoute
            || segment == null || segment.BakedSnapshot == null || !routeChoices.Contains(segment)) return false;
        definition = segment;
        snapshot = segment.BakedSnapshot;
        routeChoices = Array.Empty<AnalyticsSegmentDefinition>();
        Schedule(routeStartReason, frame);
        return true;
    }

    private void SetContinuations(Func<AnalyticsSegmentDefinition, bool> matches, string reason)
    {
        CancelPendingStart();
        routeChoices = definitions.Where(item => item != null && matches(item)).ToArray();
        definition = routeChoices.Length == 1 ? routeChoices[0] : null;
        snapshot = definition == null ? null : definition.BakedSnapshot;
        routeStartReason = reason;
    }

    public bool ReachExit(Portal exit)
    {
        if (definition == null || definition.TargetExit != exit) return false;
        End("success", "level_exit_reached");
        return true;
    }

    public void End(string outcome, string reason)
    {
        CompletedSegmentAttempt completed = state.End(outcome);
        if (completed == null) return;
        if (attemptTelemetryRecorded && !attemptTelemetrySuppressed)
        {
            Vector3 position = player == null ? Vector3.zero : player.transform.position;
            ResearchTelemetryContext context = ResearchTelemetryEvent.CaptureContext(
                ResearchPlayerState.OfferVariant,
                attemptSceneName, ResearchPlayerState.PlaythroughId, attemptRunId,
                GauntletRunTracker.Instance == null ? string.Empty : GauntletRunTracker.Instance.CurrentGauntletSessionId);
            recordEnded(new SegmentAttemptEndedEvent(context)
            {
                AttemptId = completed.AttemptId,
                SegmentId = completed.SegmentId,
                SegmentVersion = completed.SegmentVersion,
                GeometryHash = completed.GeometryHash,
                Outcome = outcome,
                Reason = reason,
                DurationSeconds = (float)completed.ActiveSeconds,
                ProgressX = position.x,
                ProgressY = position.y
            });
        }
        attemptTelemetryRecorded = false;
        attemptTelemetrySuppressed = false;
    }

    public void SuppressCurrentTelemetry()
    {
        if (state.AttemptId != null) attemptTelemetrySuppressed = true;
    }

    public void ResetScene()
    {
        player = null;
        stats = null;
        requirements = null;
        definitions = Array.Empty<AnalyticsSegmentDefinition>();
        routeChoices = Array.Empty<AnalyticsSegmentDefinition>();
        routeStartReason = null;
        definition = null;
        snapshot = null;
        pendingStart = false;
        pendingReason = null;
    }

    public void CancelPendingStart()
    {
        pendingStart = false;
        pendingReason = null;
    }

    private bool Start(string reason, bool isGauntlet, string runId)
    {
        if (!isGauntlet || snapshot == null || string.IsNullOrEmpty(runId)
            || !state.Begin(snapshot.segment_id, snapshot.segment_version, snapshot.geometry_hash)) return false;
        pendingStart = false;
        attemptRunId = runId;
        attemptSceneName = player.gameObject.scene.name;
        ResearchTelemetryContext context = ResearchTelemetryEvent.CaptureContext(
            ResearchPlayerState.OfferVariant,
            attemptSceneName, ResearchPlayerState.PlaythroughId, attemptRunId,
            GauntletRunTracker.Instance == null ? string.Empty : GauntletRunTracker.Instance.CurrentGauntletSessionId);
        attemptTelemetryRecorded = recordStarted(new SegmentAttemptStartedEvent(context)
        {
            AttemptId = state.AttemptId,
            SegmentId = snapshot.segment_id,
            SegmentVersion = snapshot.segment_version,
            GeometryHash = snapshot.geometry_hash,
            StartReason = reason ?? "unknown",
            AttemptNumber = ResearchPlayerState.AdvanceSegmentAttemptCount(snapshot.segment_id, snapshot.segment_version),
            Health = stats.health,
            Defense = stats.defense,
            AttackDamage = stats.attackDamage,
            MoveSpeed = player.AnalyticsMoveSpeed,
            JumpHeight = player.AnalyticsJumpHeight,
            DefeatedRequiredEnemies = requirements == null ? -1 : requirements.DefeatedCount,
            TotalRequiredEnemies = requirements == null ? -1 : requirements.RequiredCount,
            Gravity = player.AnalyticsGravity,
            WallDistance = definition == null || definition.Wall == null
                ? -1f : Mathf.Abs(definition.Wall.transform.position.x - player.transform.position.x),
            WallActive = definition != null && definition.Wall != null && definition.Wall.AnalyticsActive
        });
        return true;
    }
}
