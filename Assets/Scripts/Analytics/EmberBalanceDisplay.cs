using TMPro;
using UnityEngine;

public sealed class EmberBalanceDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text balanceText;

    private void Awake()
    {
        ResearchPlayerState.EnsureInitialized();
        Refresh(ResearchPlayerState.PremiumCurrencyBalance);
    }

    private void OnEnable()
    {
        ResearchPlayerState.PremiumCurrencyBalanceChanged += Refresh;
    }

    private void OnDisable()
    {
        ResearchPlayerState.PremiumCurrencyBalanceChanged -= Refresh;
    }

    private void Refresh(int balance)
    {
        if (balanceText != null)
        {
            balanceText.text = balance.ToString();
        }
    }
}
