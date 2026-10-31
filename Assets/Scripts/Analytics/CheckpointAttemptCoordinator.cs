using System;
using UnityEngine;

internal sealed class CheckpointAttemptCoordinator
{
    private readonly SegmentAttemptState state = new SegmentAttemptState();
    private readonly Action<SegmentAttemptStartedEvent> recordStarted;
    private readonly Action<SegmentAttemptEndedEvent> recordEnded;
    private AnalyticsSegmentDefinition[] definitions = Array.Empty<AnalyticsSegmentDefinition>();
    private AnalyticsSegmentDefinition definition;
    private SegmentSnapshot snapshot;
    private PlayerController player;
    private Stats stats;
    private GauntletRequirements requirements;
    private bool pendingStart;
    private string pendingReason;
    private int resumeFrame;
    private string attemptRunId;
    private string attemptSceneName;

    public string AttemptId => state.AttemptId;
    public string SegmentId => snapshot == null ? null : snapshot.segment_id;

    public CheckpointAttemptCoordinator(Action<SegmentAttemptStartedEvent> recordStarted = null,
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
        definition = Array.Find(definitions, item => item != null && item.IsSceneEntry);
        snapshot = definition == null ? null : definition.BakedSnapshot;
        if (snapshot != null && stats != null && stats.health > 0 && Time.timeScale > 0f)
            Start("scene_entry", isGauntlet,
                GauntletRunTracker.Instance == null ? null : GauntletRunTracker.Instance.CurrentRunId);
        else if (snapshot != null)
            Schedule("scene_entry", Time.frameCount + 1);
        return true;
    }

    public void Schedule(string reason, int startFrame)
    {
        if (state.AttemptId != null) return;
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
        definition = Array.Find(definitions, item => item != null && item.StartCheckpoint == checkpoint);
        snapshot = definition == null ? null : definition.BakedSnapshot;
        if (snapshot != null) Schedule("checkpoint_reached", frame + 1);
        return true;
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
        Vector3 position = player == null ? Vector3.zero : player.transform.position;
        ResearchTelemetryContext context = ResearchTelemetryEvent.CaptureContext(
            ResearchPlayerState.PlayerId, ResearchPlayerState.OfferVariant,
            attemptSceneName, ResearchPlayerState.PlaythroughId, attemptRunId);
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

    public void ResetScene()
    {
        player = null;
        stats = null;
        requirements = null;
        definitions = Array.Empty<AnalyticsSegmentDefinition>();
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
            ResearchPlayerState.PlayerId, ResearchPlayerState.OfferVariant,
            attemptSceneName, ResearchPlayerState.PlaythroughId, attemptRunId);
        recordStarted(new SegmentAttemptStartedEvent(context)
        {
            AttemptId = state.AttemptId,
            PlayerId = ResearchPlayerState.PlayerId,
            PlaythroughId = ResearchPlayerState.PlaythroughId,
            RunId = runId,
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
            InputMethod = player.AnalyticsInputMethod,
            WallDistance = definition == null || definition.Wall == null
                ? -1f : Mathf.Abs(definition.Wall.transform.position.x - player.transform.position.x),
            WallActive = definition != null && definition.Wall != null && definition.Wall.AnalyticsActive
        });
        return true;
    }
}
