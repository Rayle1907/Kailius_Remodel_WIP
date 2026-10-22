using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GauntletRequirementsHud : MonoBehaviour
{
    private sealed class Row
    {
        public string Type;
        public GauntletEnemyRow View;
        public readonly List<int> EnemyIds = new List<int>();
    }

    [SerializeField] private TMP_Text requiredHeading;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text gemsText;
    [SerializeField] private RectTransform enemyRows;
    [SerializeField] private GauntletEnemyRow rowTemplate;
    [SerializeField] private GameObject[] previewRows;

    private readonly List<Row> rows = new List<Row>();
    private GauntletRequirements requirements;
    private GauntletEarnings earnings;

    private void Awake()
    {
        HidePreview();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Initialize(GauntletRequirements owner)
    {
        Unsubscribe();
        requirements = null;
        earnings = null;
        ClearRows();
        HidePreview();

        if (owner == null || owner.gameObject.scene != gameObject.scene ||
            requiredHeading == null || coinsText == null || gemsText == null ||
            enemyRows == null || rowTemplate == null || !rowTemplate.IsConfigured)
        {
            Debug.LogError("Gauntlet HUD needs a scene-local requirements owner, heading, currency labels, enemy row container, and configured row template.", this);
            return;
        }

        earnings = owner.GetComponent<GauntletEarnings>();
        if (earnings == null)
        {
            Debug.LogError("Gauntlet HUD requirements owner is missing GauntletEarnings.", owner);
            return;
        }

        requirements = owner;
        foreach (RequiredGauntletEnemy target in owner.Targets)
        {
            Row row = rows.Find(item => item.Type == target.EnemyType);
            if (row == null)
            {
                GauntletEnemyRow view = Instantiate(rowTemplate, enemyRows, false);
                view.name = target.EnemyType + " Count";
                view.SetPortrait(target.Portrait);
                view.gameObject.SetActive(true);
                row = new Row { Type = target.EnemyType, View = view };
                rows.Add(row);
            }
            row.EnemyIds.Add(target.VisitId);
        }

        if (isActiveAndEnabled) Subscribe();
        Refresh();
    }

    private void HidePreview()
    {
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (previewRows == null) return;
        foreach (GameObject preview in previewRows)
            if (preview != null) preview.SetActive(false);
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (requirements != null) requirements.Changed += Refresh;
        if (earnings != null) earnings.Changed += RefreshEarnings;
    }

    private void Unsubscribe()
    {
        if (requirements != null) requirements.Changed -= Refresh;
        if (earnings != null) earnings.Changed -= RefreshEarnings;
    }

    private void RefreshEarnings()
    {
        if (earnings == null) return;
        coinsText.text = "+" + earnings.Coins;
        gemsText.text = "+" + earnings.Gems;
    }

    private void Refresh()
    {
        RefreshEarnings();
        if (requirements == null) return;
        bool cleared = requirements.IsCleared;
        requiredHeading.text = cleared ? "Gauntlet cleared" : "Required";
        foreach (Row row in rows)
        {
            int count = 0;
            foreach (int id in row.EnemyIds)
                if (cleared || requirements.IsDefeated(id)) count++;
            row.View.SetCount(count, row.EnemyIds.Count);
        }
    }

    private void ClearRows()
    {
        foreach (Row row in rows)
        {
            if (row.View == null) continue;
            row.View.gameObject.SetActive(false);
            Destroy(row.View.gameObject);
        }
        rows.Clear();
    }
}
