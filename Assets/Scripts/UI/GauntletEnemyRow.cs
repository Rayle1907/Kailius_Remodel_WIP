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

    public void SetTextOnlyCount(string enemyType, int defeated, int total)
    {
        portrait.gameObject.SetActive(false);
        countText.text = enemyType + ": " + defeated + "/" + total;
        countText.alignment = TextAlignmentOptions.Center;
        RectTransform countRect = countText.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;
    }
}
