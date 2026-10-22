using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GauntletProgressData
{
    public int version = 1;
    public List<string> clearedGauntlets = new List<string>();
    public List<string> spentUpgradeSources = new List<string>();

    public bool TrySpendUpgrade(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || spentUpgradeSources.Contains(sourceId)) return false;
        spentUpgradeSources.Add(sourceId);
        return true;
    }
}

public static class GauntletProgress
{
    public const string SaveKey = "GauntletProgress.v1";
    public static readonly string[] RequiredScenes = { "1_1Gauntlet", "1_2Gauntlet", "1_3Gauntlet" };

    public static GauntletProgressData Read()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return new GauntletProgressData();
        try
        {
            GauntletProgressData data = JsonUtility.FromJson<GauntletProgressData>(json);
            if (data != null && data.version == 1 && data.clearedGauntlets != null
                && data.spentUpgradeSources != null) return data;
        }
        catch (ArgumentException)
        {
        }
        throw new InvalidOperationException("Gauntlet progress could not be loaded. The save was preserved.");
    }

    public static bool IsCleared(string sceneName) => Read().clearedGauntlets.Contains(sceneName);

    public static string[] GetUnfinishedScenes()
    {
        GauntletProgressData data = Read();
        return Array.FindAll(RequiredScenes, scene => !data.clearedGauntlets.Contains(scene));
    }

    public static void MarkCleared(string sceneName)
    {
        if (!GauntletHealthDropRules.IsGauntletScene(sceneName)) return;
        GauntletProgressData data = Read();
        if (data.clearedGauntlets.Contains(sceneName)) return;
        data.clearedGauntlets.Add(sceneName);
        Write(data);
    }

    public static void MarkUncleared(string sceneName)
    {
        if (!GauntletHealthDropRules.IsGauntletScene(sceneName)) return;
        GauntletProgressData data = Read();
        if (!data.clearedGauntlets.Remove(sceneName)) return;
        Write(data);
    }

    public static bool TrySpendUpgrade(string sceneName, string sourceId)
    {
        if (!GauntletHealthDropRules.IsGauntletScene(sceneName)) return true;
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            Debug.LogError("A gauntlet upgrade source has no stable ID. Use Tools/Gauntlets/Assign Reward IDs in Open Scenes.");
            return false;
        }
        GauntletProgressData data = Read();
        if (!data.TrySpendUpgrade(sceneName + ":" + sourceId)) return false;
        Write(data);
        return true;
    }

    public static void StartNewGame()
    {
        ResearchPlayerState.ResetGauntletOfferCounts();
        OpenWorldChestProgress.Reset();
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    private static void Write(GauntletProgressData data)
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}

public sealed class GauntletVisit
{
    private readonly HashSet<int> required = new HashSet<int>();
    private readonly HashSet<int> defeated = new HashSet<int>();
    public bool IsConfigured { get; private set; }
    public int DefeatedCount => defeated.Count;
    public int RequiredCount => required.Count;
    public bool IsComplete => IsConfigured && required.Count > 0 && defeated.Count == required.Count;

    public GauntletVisit(IEnumerable<int> enemyIds)
    {
        IsConfigured = true;
        foreach (int id in enemyIds)
            if (!required.Add(id)) IsConfigured = false;
    }

    public bool RecordDefeat(int enemyId) => required.Contains(enemyId) && defeated.Add(enemyId);
    public bool IsDefeated(int enemyId) => defeated.Contains(enemyId);
}
