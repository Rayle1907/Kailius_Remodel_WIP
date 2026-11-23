using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class OpenWorldChestProgressData
{
    public int version = 1;
    public List<string> openedChests = new List<string>();
    public List<OpenWorldBonusLoot> bonusLoot = new List<OpenWorldBonusLoot>();
}

[Serializable]
public sealed class OpenWorldBonusLoot
{
    public string scenePath;
    public int coins;
    public int gems;
}

public static class OpenWorldChestProgress
{
    public const string SaveKey = "OpenWorldChestProgress.v1";

    public static bool IsOpenWorldScene(string sceneName)
    {
        return sceneName == "Scene_1" || sceneName == "Scene_2"
            || sceneName == "Scene_3" || sceneName == "Scene_Final_1";
    }

    public static bool IsOpened(string scenePath, string sourceId)
    {
        return Read().openedChests.Contains(Key(scenePath, sourceId));
    }

    public static bool TryOpen(string scenePath, string sourceId)
    {
        string key = Key(scenePath, sourceId);
        OpenWorldChestProgressData data = Read();
        if (data.openedChests.Contains(key)) return false;
        data.openedChests.Add(key);
        Save(data);
        return true;
    }

    public static (int coins, int gems) GetBonusLoot(string scenePath)
    {
        OpenWorldBonusLoot loot = Read().bonusLoot?.Find(entry => entry.scenePath == scenePath);
        return (loot?.coins ?? 0, loot?.gems ?? 0);
    }

    public static (int coins, int gems) RecordBonusLoot(string scenePath, int coins, int gems)
    {
        OpenWorldChestProgressData data = Read();
        data.bonusLoot ??= new List<OpenWorldBonusLoot>();
        OpenWorldBonusLoot loot = data.bonusLoot.Find(entry => entry.scenePath == scenePath);
        if (loot == null)
        {
            loot = new OpenWorldBonusLoot { scenePath = scenePath };
            data.bonusLoot.Add(loot);
        }
        loot.coins += coins;
        loot.gems += gems;
        Save(data);
        return (loot.coins, loot.gems);
    }

    private static void Save(OpenWorldChestProgressData data)
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    private static string Key(string scenePath, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(scenePath) || string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Open-world chests require a scene path and stable reward ID.");
        return scenePath + ":" + sourceId;
    }

    private static OpenWorldChestProgressData Read()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return new OpenWorldChestProgressData();
        try
        {
            OpenWorldChestProgressData data = JsonUtility.FromJson<OpenWorldChestProgressData>(json);
            if (data != null && data.version == 1 && data.openedChests != null) return data;
        }
        catch (ArgumentException)
        {
        }
        throw new InvalidOperationException("Open-world chest progress could not be loaded. The save was preserved.");
    }
}
