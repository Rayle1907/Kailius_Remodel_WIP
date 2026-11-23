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
        SetTextOnlyRemaining(enemyType, Mathf.Max(0, total - defeated));
    }

    public void SetTextOnlyRemaining(string enemyType, int remaining)
    {
        portrait.gameObject.SetActive(false);
        countText.text = enemyType + ": " + remaining + " more";
        countText.alignment = TextAlignmentOptions.Center;
        RectTransform countRect = countText.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;
    }

    public void SetNoticeTextStyle(TMP_FontAsset font, float fontSizeMax, float fontSizeMin)
    {
        if (font != null) countText.font = font;
        countText.enableAutoSizing = true;
        countText.fontSizeMax = fontSizeMax;
        countText.fontSizeMin = fontSizeMin;
    }
}
