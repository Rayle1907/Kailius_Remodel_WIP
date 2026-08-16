using System;
using UnityEngine;

public static class ResearchPlayerState
{
    private const string PlayerIdKey = "research_player_id";
    private const string VariantKey = "research_offer_variant";
    private const string BalanceKey = "research_currency_balance";
    private const string TotalDeathsKey = "research_total_deaths";

    public const int InitialPremiumCurrency = 100;

    public static string PlayerId { get; private set; }
    public static string OfferVariant { get; private set; }
    public static int PremiumCurrencyBalance { get; private set; }

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
        return true;
    }
}
