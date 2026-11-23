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

    public static string[] GetUnfinishedScenes() => GetUnfinishedScenes(RequiredScenes);

    public static string[] GetUnfinishedScenes(string[] requiredScenes)
    {
        GauntletProgressData data = Read();
        return Array.FindAll(requiredScenes, scene => !data.clearedGauntlets.Contains(scene));
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
    private readonly Dictionary<int, string> enemyTypeById = new Dictionary<int, string>();
    private readonly Dictionary<string, int> quotaByType = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<int> defeated = new HashSet<int>();
    private readonly Dictionary<string, int> defeatedByType = new Dictionary<string, int>(StringComparer.Ordinal);
    public bool IsConfigured { get; private set; }
    public int DefeatedCount
    {
        get
        {
            int count = 0;
            foreach (KeyValuePair<string, int> quota in quotaByType)
                count += Mathf.Min(quota.Value, GetDefeatedCount(quota.Key));
            return count;
        }
    }
    public int RequiredCount
    {
        get
        {
            int count = 0;
            foreach (int quota in quotaByType.Values) count += quota;
            return count;
        }
    }
    public bool IsComplete
    {
        get
        {
            if (!IsConfigured || RequiredCount == 0) return false;
            foreach (KeyValuePair<string, int> quota in quotaByType)
                if (GetDefeatedCount(quota.Key) < quota.Value) return false;
            return true;
        }
    }

    public GauntletVisit(IEnumerable<KeyValuePair<int, string>> enemies,
        IEnumerable<KeyValuePair<string, int>> quotas)
    {
        IsConfigured = true;
        foreach (KeyValuePair<int, string> enemy in enemies)
        {
            if (enemy.Key < 0 || string.IsNullOrWhiteSpace(enemy.Value)
                || enemyTypeById.ContainsKey(enemy.Key))
            {
                IsConfigured = false;
                continue;
            }

            enemyTypeById.Add(enemy.Key, enemy.Value);
        }

        foreach (KeyValuePair<string, int> quota in quotas)
        {
            if (string.IsNullOrWhiteSpace(quota.Key) || quota.Value < 0
                || quotaByType.ContainsKey(quota.Key))
            {
                IsConfigured = false;
                continue;
            }

            quotaByType.Add(quota.Key, quota.Value);
        }

        foreach (KeyValuePair<string, int> quota in quotaByType)
        {
            int available = 0;
            foreach (string enemyType in enemyTypeById.Values)
                if (enemyType == quota.Key) available++;
            if (quota.Value > available) IsConfigured = false;
        }
    }

    public bool RecordDefeat(int enemyId)
    {
        if (!enemyTypeById.TryGetValue(enemyId, out string enemyType) || !defeated.Add(enemyId)) return false;
        if (!defeatedByType.ContainsKey(enemyType)) defeatedByType.Add(enemyType, 0);
        defeatedByType[enemyType]++;
        return true;
    }

    public bool IsDefeated(int enemyId) => defeated.Contains(enemyId);
    public bool ContainsEnemy(int enemyId) => enemyTypeById.ContainsKey(enemyId);
    public string GetEnemyType(int enemyId)
    {
        if (!enemyTypeById.TryGetValue(enemyId, out string enemyType)) return string.Empty;
        return enemyType;
    }

    public int GetQuota(string enemyType)
    {
        if (!quotaByType.TryGetValue(enemyType, out int quota)) return 0;
        return quota;
    }

    public int GetDefeatedCount(string enemyType)
    {
        if (!defeatedByType.TryGetValue(enemyType, out int count)) return 0;
        return count;
    }

    public int GetLivingAheadCount(string enemyType, HashSet<int> enemiesBehind)
    {
        int count = 0;
        foreach (KeyValuePair<int, string> enemy in enemyTypeById)
        {
            if (enemy.Value == enemyType && !enemiesBehind.Contains(enemy.Key) && !defeated.Contains(enemy.Key))
                count++;
        }
        return count;
    }

    public static int CalculateBehindShortfall(int quota, int creditedKills, int livingAhead)
    {
        return Math.Max(0, quota - creditedKills - livingAhead);
    }
}
