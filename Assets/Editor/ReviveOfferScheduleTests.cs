#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class ReviveOfferScheduleTests
{
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    [TestCase(5, false)]
    [TestCase(6, true)]
    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(9, false)]
    [TestCase(10, true)]
    public void ShouldShowOffer_MatchesRequiredFirstTenDeaths(
        int deathNumberInGauntlet,
        bool expected)
    {
        Assert.That(
            GauntletRunTracker.ShouldShowOffer(deathNumberInGauntlet),
            Is.EqualTo(expected));
    }

    [Test]
    public void ShouldShowOffer_AfterDeathSeven_OnlyAllowsEvenDeaths()
    {
        for (int death = 8; death <= 100; death++)
        {
            Assert.That(
                GauntletRunTracker.ShouldShowOffer(death),
                Is.EqualTo(death % 2 == 0),
                $"Unexpected eligibility for gauntlet death {death}.");
        }
    }

    [TestCase(-1)]
    [TestCase(0)]
    public void ShouldShowOffer_RejectsNonPositiveDeathNumbers(int deathNumberInGauntlet)
    {
        Assert.That(
            GauntletRunTracker.ShouldShowOffer(deathNumberInGauntlet),
            Is.False);
    }
}
#endif
