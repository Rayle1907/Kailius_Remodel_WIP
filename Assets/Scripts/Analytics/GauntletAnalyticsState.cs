using System;

// Small, Unity-independent model of the analytics state transitions.
// GauntletRunTracker owns the live Unity/UGS integration; this class captures
// the rules so they can be tested without loading a scene.
public sealed class GauntletAnalyticsState
{
    public int RunDeaths { get; private set; }
    public int SegmentDeaths { get; private set; }
    public double RunStartedAt { get; private set; }
    public double? LastDeathAt { get; private set; }
    public bool ReviveAccepted { get; private set; }
    public string CurrentCheckpointId { get; private set; }
    public int CurrentCheckpointIndex { get; private set; }
    public string OutcomeObserved { get; private set; }

    public void StartRun(double now)
    {
        RunDeaths = 0;
        SegmentDeaths = 0;
        RunStartedAt = now;
        LastDeathAt = null;
        ReviveAccepted = false;
        CurrentCheckpointId = null;
        CurrentCheckpointIndex = -1;
        OutcomeObserved = null;
    }

    public float RecordDeath(double now, out string outcome)
    {
        outcome = null;
        if (ReviveAccepted)
        {
            outcome = "died_again";
            ReviveAccepted = false;
            SetObservedOutcome(outcome);
        }

        double previousEventAt = LastDeathAt.HasValue
            ? LastDeathAt.Value
            : RunStartedAt;

        RunDeaths++;
        SegmentDeaths++;
        LastDeathAt = now;
        return (float)(now - previousEventAt);
    }

    public void StartNextSegment()
    {
        SegmentDeaths = 0;
    }

    public void AcceptRevive()
    {
        ReviveAccepted = true;
    }
    public void ObserveOutcome(string outcome)
    {
        if (!string.IsNullOrWhiteSpace(outcome))
        {
            OutcomeObserved = outcome;
        }
    }

    public string ReachCheckpoint(string checkpointId, int checkpointIndex)
    {
        if (string.IsNullOrWhiteSpace(checkpointId)
            || checkpointIndex <= CurrentCheckpointIndex)
        {
            return null;
        }

        CurrentCheckpointId = checkpointId;
        CurrentCheckpointIndex = checkpointIndex;
        if (!ReviveAccepted)
        {
            return null;
        }

        ReviveAccepted = false;
        return SetObservedOutcome("next_checkpoint_reached");
    }

    public string CompleteLevel()
    {
        if (!ReviveAccepted)
        {
            return null;
        }

        ReviveAccepted = false;
        return SetObservedOutcome("level_completed");
    }

    private string SetObservedOutcome(string outcome)
    {
        ObserveOutcome(outcome);
        return outcome;
    }
}
