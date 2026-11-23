using TMPro;
using UnityEngine;

[RequireComponent(typeof(OptionalLootProgress))]
public sealed class OptionalLootHud : MonoBehaviour
{
    [SerializeField] private GameObject section;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text details;
    [SerializeField] private GameObject[] currencyRows;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text gemsText;
    private OptionalLootProgress progress;
    private bool gauntlet;

    private void Awake()
    {
        progress = GetComponent<OptionalLootProgress>();
        gauntlet = GauntletHealthDropRules.IsGauntletScene(gameObject.scene.name);
        foreach (GameObject row in currencyRows) row.SetActive(!gauntlet);
        details.gameObject.SetActive(gauntlet);
        section.SetActive(gauntlet || OpenWorldChestProgress.IsOpenWorldScene(gameObject.scene.name));
    }

    private void OnEnable()
    {
        progress.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (progress != null) progress.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (gauntlet)
        {
            details.text = "Chests opened " + progress.OpenedCount + "/" + progress.ChestCount;
            if (progress.AllOpened) details.text = "<color=#9FE59F>[x] All chests opened</color>";
        }
        else
        {
            coinsText.text = "Coins +" + progress.Coins;
            gemsText.text = "Gems +" + progress.Gems;
        }
    }
}
