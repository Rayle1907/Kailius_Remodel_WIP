using NUnit.Framework;

public sealed class CheckpointActivationRegionTests
{
    [TestCase(0f, true)]
    [TestCase(1.92f, true)]
    [TestCase(1.9201f, false)]
    [TestCase(-0.001f, false)]
    public void UpwardActivationRangeIncludesOnlyCheckpointLevelThroughHeight(float playerBottomOffset, bool expected)
    {
        bool isInRange = CheckpointActivationRegion.IsPlayerInActivationRange(
            playerBottomOffset,
            0f,
            1.92f);

        Assert.That(isInRange, Is.EqualTo(expected));
    }

    [Test]
    public void DirectTouchActivatesEvenWhenPlayerFeetAreBelowCheckpoint()
    {
        bool isInRange = CheckpointActivationRegion.IsPlayerInActivationRange(
            -0.5f,
            0f,
            1.92f,
            directTouch: true);

        Assert.That(isInRange, Is.True);
    }
}
