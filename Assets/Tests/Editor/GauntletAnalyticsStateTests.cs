using NUnit.Framework;

public sealed class GauntletAnalyticsStateTests
{
    private GauntletAnalyticsState state;

    [SetUp]
    public void SetUp()
    {
        state = new GauntletAnalyticsState();
        state.StartRun(100.0);
    }

    [Test]
    public void FirstDeath_UsesTimeSinceRunStart()
    {
        string outcome;
        float elapsed = state.RecordDeath(112.5, out outcome);

        Assert.That(elapsed, Is.EqualTo(12.5f).Within(0.001f));
        Assert.That(outcome, Is.Null);
    }

    [Test]
    public void LaterDeath_UsesTimeSincePreviousDeath()
    {
        string ignored;
        state.RecordDeath(110.0, out ignored);
        float elapsed = state.RecordDeath(127.5, out ignored);

        Assert.That(elapsed, Is.EqualTo(17.5f).Within(0.001f));
    }

    [Test]
    public void DeathCountPersistsAcrossGauntletScenes()
    {
        string ignored;
        state.RecordDeath(110.0, out ignored);
        state.StartNextSegment();
        state.RecordDeath(130.0, out ignored);

        Assert.That(state.RunDeaths, Is.EqualTo(2));
        Assert.That(state.SegmentDeaths, Is.EqualTo(1));
    }

    [Test]
    public void AcceptingReviveDoesNotCreateDiedAgain()
    {
        state.AcceptRevive();

        Assert.That(state.OutcomeObserved, Is.Null);
    }

    [Test]
    public void NextDeathAfterReviveCreatesDiedAgain()
    {
        state.AcceptRevive();

        string outcome;
        state.RecordDeath(140.0, out outcome);

        Assert.That(outcome, Is.EqualTo("died_again"));
    }

    [Test]
    public void SameOrEarlierCheckpointDoesNotCreateOutcome()
    {
        state.ReachCheckpoint("checkpoint_b", 2);
        state.AcceptRevive();

        string sameCheckpointOutcome =
            state.ReachCheckpoint("checkpoint_b", 2);

        string earlierCheckpointOutcome =
            state.ReachCheckpoint("checkpoint_a", 1);

        Assert.That(sameCheckpointOutcome, Is.Null);
        Assert.That(earlierCheckpointOutcome, Is.Null);
        Assert.That(state.OutcomeObserved, Is.Null);
        Assert.That(state.ReviveAccepted, Is.True);
    }

    [Test]
    public void NextCheckpointCreatesNextCheckpointOutcome()
    {
        state.ReachCheckpoint("checkpoint_a", 1);
        state.AcceptRevive();

        string outcome = state.ReachCheckpoint("checkpoint_b", 2);

        Assert.That(outcome, Is.EqualTo("next_checkpoint_reached"));
        Assert.That(state.ReviveAccepted, Is.False);
    }

    [Test]
    public void CompletingLevelCreatesLevelCompletedOutcome()
    {
        state.AcceptRevive();

        string outcome = state.CompleteLevel();

        Assert.That(outcome, Is.EqualTo("level_completed"));
    }
}

