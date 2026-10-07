using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ReviveButtonAssets : MonoBehaviour
{
    [SerializeField] private LayeredButton buttonPrefab;
    [SerializeField] private LayeredButton pressMeButtonPrefab;
    [SerializeField] private TMP_FontAsset uiFont;

    public LayeredButton ButtonPrefab => buttonPrefab;
    public LayeredButton PressMeButtonPrefab => pressMeButtonPrefab;
    public TMP_FontAsset UIFont => uiFont;
}
