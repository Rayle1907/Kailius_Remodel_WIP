using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GauntletEnemyRow : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text countText;

    public bool IsConfigured => portrait != null && countText != null;

    public void SetPortrait(Sprite sprite)
    {
        portrait.sprite = sprite;
    }

    public void SetCount(int defeated, int total)
    {
        countText.text = defeated + "/" + total;
    }
}
