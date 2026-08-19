#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;

public sealed class ReviveOfferPricingTests
{
    [TestCase(0, 1)]
    [TestCase(1, 2)]
    [TestCase(2, 4)]
    [TestCase(3, 8)]
    [TestCase(4, 16)]
    [TestCase(5, 32)]
    [TestCase(6, 64)]
    [TestCase(7, 128)]
    public void GetRevivePrice_IsExponentialByShownOfferIndex(
        int zeroBasedOfferIndex,
        int expectedPrice)
    {
        Assert.That(
            GauntletRunTracker.GetRevivePrice(zeroBasedOfferIndex),
            Is.EqualTo(expectedPrice));
    }

    [Test]
    public void GetRevivePrice_RejectsNegativeOfferIndex()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GauntletRunTracker.GetRevivePrice(-1));
    }

    [Test]
    public void GetRevivePrice_SaturatesBeforeSignedIntegerOverflow()
    {
        int maximumPrice = 1 << 30;

        Assert.That(GauntletRunTracker.GetRevivePrice(30), Is.EqualTo(maximumPrice));
        Assert.That(GauntletRunTracker.GetRevivePrice(31), Is.EqualTo(maximumPrice));
        Assert.That(GauntletRunTracker.GetRevivePrice(100), Is.EqualTo(maximumPrice));
    }

    [TestCase(100, 1, true)]
    [TestCase(1, 1, true)]
    [TestCase(0, 1, false)]
    [TestCase(3, 4, false)]
    [TestCase(4, 4, true)]
    [TestCase(-1, 1, false)]
    [TestCase(100, 0, false)]
    public void CanAffordRevive_RequiresBalanceAtLeastPrice(
        int balance,
        int revivePrice,
        bool expected)
    {
        Assert.That(
            GauntletRunTracker.CanAffordRevive(balance, revivePrice),
            Is.EqualTo(expected));
    }
}
#endif
