using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GauntletRequirements : MonoBehaviour
{
    public event Action Changed;
    public IReadOnlyList<RequiredGauntletEnemy> Targets => targets;
    public bool IsConfigured { get; private set; }
    public bool IsCleared => GauntletProgress.IsCleared(gameObject.scene.name);
    private readonly List<RequiredGauntletEnemy> targets = new List<RequiredGauntletEnemy>();
    private GauntletVisit visit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!GauntletHealthDropRules.IsGauntletScene(scene.name)) return;
        GameObject root = new GameObject("Gauntlet Requirements");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<GauntletRequirements>();
    }

    private void Start()
    {
        var ids = new List<int>();
        IsConfigured = true;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (RequiredGauntletEnemy target in root.GetComponentsInChildren<RequiredGauntletEnemy>(true))
            {
                targets.Add(target);
                ids.Add(targets.Count - 1);
                target.Bind(this, targets.Count - 1);
                if (string.IsNullOrWhiteSpace(target.EnemyType) || target.Portrait == null)
                {
                    IsConfigured = false;
                    Debug.LogError("Required enemies need an enemy type and HUD portrait.", target);
                }
            }
        }
        visit = new GauntletVisit(ids);
        IsConfigured &= visit.IsConfigured && visit.RequiredCount > 0;
        if (targets.Count == 0) Debug.LogError("No required enemies configured in " + gameObject.scene.name + ".", this);
        GauntletRequirementsHud hud = gameObject.AddComponent<GauntletRequirementsHud>();
        hud.Initialize(this);
    }

    public void RecordDefeat(RequiredGauntletEnemy target)
    {
        if (visit == null || !targets.Contains(target) || !visit.RecordDefeat(target.VisitId)) return;
        if (IsConfigured && visit.IsComplete) GauntletProgress.MarkCleared(gameObject.scene.name);
        Changed?.Invoke();
    }

    public bool IsDefeated(int enemyId) => visit != null && visit.IsDefeated(enemyId);
}
