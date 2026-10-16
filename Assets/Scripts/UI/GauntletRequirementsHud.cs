using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GauntletRequirementsHud : MonoBehaviour
{
    private sealed class Row
    {
        public TMP_Text Text;
        public string Type;
        public readonly List<int> EnemyIds = new List<int>();
    }

    private readonly List<Row> rows = new List<Row>();
    private GauntletRequirements requirements;
    private GameObject panel;

    public void Initialize(GauntletRequirements owner)
    {
        requirements = owner;
        ScoreManager scores = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            scores = root.GetComponentInChildren<ScoreManager>(true);
            if (scores != null) break;
        }
        TMP_Text template = null;
        TMP_Text gemsText = null;
        TMP_Text styleTemplate = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Coins Text (TMP)") template = text;
                if (text.name == "Gems Text (TMP)") gemsText = text;
                if (text.name == "Damage Text (TMP)") styleTemplate = text;
            }
        }
        if (template == null && scores != null) template = scores.textStars;
        if (template == null && scores != null) template = scores.textScore;
        if (template == null && scores != null) template = scores.Score;
        Canvas canvas = template != null ? template.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            Debug.LogError("Gauntlet requirements HUD could not find the gameplay Canvas.", this);
            return;
        }
        if (styleTemplate != null)
        {
            template.font = styleTemplate.font;
            template.fontSharedMaterial = styleTemplate.fontSharedMaterial;
            template.fontStyle = styleTemplate.fontStyle;
            if (gemsText != null)
            {
                gemsText.font = styleTemplate.font;
                gemsText.fontSharedMaterial = styleTemplate.fontSharedMaterial;
                gemsText.fontStyle = styleTemplate.fontStyle;
            }
        }
        RectTransform starsPosition = null;
        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Stars Image") starsPosition = child as RectTransform;
            if (child.name == "Coins Image" || child.name == "Coins Text (TMP)"
                || child.name == "Gems Image" || child.name == "Gems Text (TMP)")
                child.gameObject.SetActive(true);
            if (child.name == "Stars Image" || child.name == "Stars Text (TMP)")
                child.gameObject.SetActive(false);
        }
        if (scores != null && template.name == "Coins Text (TMP)")
        {
            scores.textCoins = template as TextMeshProUGUI;
            template.text = "150/" + PlayerPrefs.GetInt("ScoreCoins", 0);
            scores.enabled = true;
        }
        if (scores != null && gemsText != null)
        {
            scores.textGems = gemsText as TextMeshProUGUI;
            gemsText.text = "60/" + PlayerPrefs.GetInt("ScoreGems", 0);
        }
        if (scores != null && scores.textStars != null) scores.textStars.gameObject.SetActive(false);

        panel = new GameObject("Required Enemies", typeof(RectTransform));
        RectTransform rect = (RectTransform)panel.transform;
        MobileSafeArea safeArea = canvas.GetComponent<MobileSafeArea>();
        rect.SetParent(starsPosition != null ? starsPosition.parent
            : safeArea != null ? safeArea.ContentRoot : canvas.transform, false);
        rect.anchorMin = starsPosition != null ? starsPosition.anchorMin : new Vector2(0f, 1f);
        rect.anchorMax = starsPosition != null ? starsPosition.anchorMax : new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = starsPosition != null ? starsPosition.anchoredPosition
            + new Vector2(-starsPosition.sizeDelta.x * starsPosition.pivot.x,
                starsPosition.sizeDelta.y * (1f - starsPosition.pivot.y)) : new Vector2(24f, -260f);
        rect.sizeDelta = new Vector2(280f, 80f);

        foreach (RequiredGauntletEnemy target in owner.Targets)
        {
            Row row = rows.Find(item => item.Type == target.EnemyType);
            if (row == null)
            {
                row = new Row { Type = target.EnemyType };
                float y = -rows.Count * 52f;
                GameObject icon = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                RectTransform iconRect = (RectTransform)icon.transform;
                iconRect.SetParent(rect, false);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
                iconRect.pivot = new Vector2(0f, 1f);
                iconRect.anchoredPosition = new Vector2(0f, y);
                iconRect.sizeDelta = new Vector2(44f, 44f);
                Image image = icon.GetComponent<Image>();
                image.sprite = target.Portrait;
                image.preserveAspect = true;
                image.raycastTarget = false;
                row.Text = CreateText("Enemy Count", rect, template, new Vector2(56f, y), new Vector2(224f, 52f));
                rows.Add(row);
            }
            row.EnemyIds.Add(target.VisitId);
        }
        rect.sizeDelta = new Vector2(280f, rows.Count * 52f);
        requirements.Changed += Refresh;
        Refresh();
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_Text template, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TMP_Text text = obj.GetComponent<TMP_Text>();
        text.font = template.font;
        text.fontSharedMaterial = template.fontSharedMaterial;
        text.fontStyle = template.fontStyle;
        text.fontSize = template.fontSize;
        text.fontWeight = template.fontWeight;
        text.color = template.color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private void Refresh()
    {
        bool cleared = requirements.IsCleared;
        foreach (Row row in rows)
        {
            int count = cleared ? row.EnemyIds.Count : row.EnemyIds.FindAll(requirements.IsDefeated).Count;
            row.Text.text = count + " / " + row.EnemyIds.Count;
        }
    }

    private void OnDestroy()
    {
        if (requirements != null) requirements.Changed -= Refresh;
        if (panel != null) Destroy(panel);
    }
}
