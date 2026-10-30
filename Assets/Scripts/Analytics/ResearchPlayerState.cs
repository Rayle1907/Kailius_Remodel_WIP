using System;
using UnityEngine;

public static class ResearchPlayerState
{
    private const string PlayerIdKey = "research_player_id";
    private const string VariantKey = "research_offer_variant";
    private const string BalanceKey = "research_currency_balance";
    private const string TotalDeathsKey = "research_total_deaths";
    private const string OnboardingKey = "research_revive_onboarding_completed";
    private const string OnboardingRevisionKey = "research_revive_onboarding_revision";
    public const int CurrentReviveOnboardingRevision = 2;

    private const string PlaythroughKey = "research_playthrough_id";
    private const string OfferCountPrefix = "research_gauntlet_offers_v1_";

    public static string PlaythroughId
    {
        get
        {
            if (!PlayerPrefs.HasKey(PlaythroughKey))
            {
                PlayerPrefs.SetString(PlaythroughKey, Guid.NewGuid().ToString("N"));
                PlayerPrefs.Save();
            }
            return PlayerPrefs.GetString(PlaythroughKey);
        }
    }

    public static int GetGauntletOfferCount(string gauntletId)
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(OfferCountPrefix + PlaythroughId + "_" + gauntletId, 0));
    }

    public static int AdvanceGauntletOfferCount(string gauntletId)
    {
        int count = GetGauntletOfferCount(gauntletId);
        if (count < int.MaxValue) count++;
        PlayerPrefs.SetInt(OfferCountPrefix + PlaythroughId + "_" + gauntletId, count);
        PlayerPrefs.Save();
        return count;
    }

    public static void ResetGauntletOfferCounts()
    {
        PlayerPrefs.SetString(PlaythroughKey, Guid.NewGuid().ToString("N"));
        PlayerPrefs.Save();
    }

    public const int InitialPremiumCurrency = 100;
    public const string PremiumCurrencyDisplayName = "Embers";

    public static event Action<int> PremiumCurrencyBalanceChanged;

    public static string PlayerId { get; private set; }
    public static string OfferVariant { get; private set; }
    public static int PremiumCurrencyBalance { get; private set; }
    public static bool ReviveOnboardingCompleted => PlayerPrefs.GetInt(OnboardingRevisionKey,
        PlayerPrefs.GetInt(OnboardingKey, 0) == 1 ? 1 : 0) >= CurrentReviveOnboardingRevision;

    public static void MarkReviveOnboardingCompleted()
    {
        if (ReviveOnboardingCompleted) return;
        PlayerPrefs.SetInt(OnboardingRevisionKey, CurrentReviveOnboardingRevision);
        PlayerPrefs.Save();
    }

    public static void ResetReviveOnboarding()
    {
        PlayerPrefs.DeleteKey(OnboardingKey);
        PlayerPrefs.DeleteKey(OnboardingRevisionKey);
        PlayerPrefs.Save();
    }

    public static void EnsureInitialized()
    {
        if (!PlayerPrefs.HasKey(PlayerIdKey))
        {
            PlayerPrefs.SetString(PlayerIdKey, Guid.NewGuid().ToString("N"));
        }

        if (!PlayerPrefs.HasKey(VariantKey))
        {
            PlayerPrefs.SetString(VariantKey, UnityEngine.Random.value < 0.5f ? "A" : "B");
        }

        if (!PlayerPrefs.HasKey(BalanceKey))
        {
            PlayerPrefs.SetInt(BalanceKey, InitialPremiumCurrency);
        }

        if (!PlayerPrefs.HasKey(TotalDeathsKey))
        {
            PlayerPrefs.SetInt(TotalDeathsKey, 0);
        }

        PlayerPrefs.Save();

        PlayerId = PlayerPrefs.GetString(PlayerIdKey);
        OfferVariant = PlayerPrefs.GetString(VariantKey);
        PremiumCurrencyBalance = PlayerPrefs.GetInt(BalanceKey, InitialPremiumCurrency);
    }

    public static int IncrementTotalDeaths()
    {
        int total = PlayerPrefs.GetInt(TotalDeathsKey, 0) + 1;
        PlayerPrefs.SetInt(TotalDeathsKey, total);
        PlayerPrefs.Save();
        return total;
    }

    public static bool TrySpendPremiumCurrency(int amount)
    {
        if (amount < 0 || PremiumCurrencyBalance < amount)
        {
            return false;
        }

        PremiumCurrencyBalance -= amount;
        PlayerPrefs.SetInt(BalanceKey, PremiumCurrencyBalance);
        PlayerPrefs.Save();
        PremiumCurrencyBalanceChanged?.Invoke(PremiumCurrencyBalance);
        return true;
    }
}
