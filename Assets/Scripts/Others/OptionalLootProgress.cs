using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class OptionalLootProgress : MonoBehaviour
{
    public event Action Changed;
    public int Coins { get; private set; }
    public int Gems { get; private set; }
    public int OpenedCount => opened.Count;
    public int ChestCount => chests.Count;
    public bool AllOpened => ChestCount > 0 && OpenedCount == ChestCount;
    private readonly List<Chest> chests = new List<Chest>();
    private readonly HashSet<Chest> opened = new HashSet<Chest>();

    private bool initialized;

    private void Start() => Initialize();

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        foreach (Chest chest in root.GetComponentsInChildren<Chest>(true))
        {
            chests.Add(chest);
            chest.Opened += RecordOpened;
            if (chest.IsOpened) opened.Add(chest);
        }
        Changed?.Invoke();
    }

    private void OnDestroy()
    {
        foreach (Chest chest in chests)
            if (chest != null) chest.Opened -= RecordOpened;
    }

    private void RecordOpened(Chest chest)
    {
        if (chests.Contains(chest) && opened.Add(chest)) Changed?.Invoke();
    }

    public void RecordCollection(int coins, int gems)
    {
        Coins += Mathf.Max(0, coins);
        Gems += Mathf.Max(0, gems);
        Changed?.Invoke();
    }

    public static OptionalLootProgress ForScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            OptionalLootProgress progress = root.GetComponentInChildren<OptionalLootProgress>(true);
            if (progress != null) return progress;
        }
        return null;
    }
}
