using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ReviveButtonAssets : MonoBehaviour
{
    [SerializeField] private LayeredButton buttonPrefab;
    [SerializeField] private LayeredButton pressMeButtonPrefab;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private ReviveOnboardingPresenter onboardingPrefab;
    [SerializeField] private ReviveOfferView offerPrefab;

    public LayeredButton ButtonPrefab => buttonPrefab;
    public LayeredButton PressMeButtonPrefab => pressMeButtonPrefab;
    public TMP_FontAsset UIFont => uiFont;
    public ReviveOnboardingPresenter OnboardingPrefab => onboardingPrefab;
    public ReviveOfferView OfferPrefab => offerPrefab;
}
