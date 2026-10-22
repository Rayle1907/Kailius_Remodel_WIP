using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class OpenWorldChestProgressData
{
    public int version = 1;
    public List<string> openedChests = new List<string>();
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
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        return true;
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
