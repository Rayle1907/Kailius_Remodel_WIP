using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GauntletRequirements : MonoBehaviour
{
    public event Action Changed;
    public IReadOnlyList<RequiredGauntletEnemy> Targets => targets;
    public int DefeatedCount => visit == null ? 0 : visit.DefeatedCount;
    public int RequiredCount => visit == null ? 0 : visit.RequiredCount;
    public bool IsConfigured { get; private set; }
    public bool IsCleared => GauntletProgress.IsCleared(gameObject.scene.name);
    [SerializeField] private List<RequiredGauntletEnemy> targets = new List<RequiredGauntletEnemy>();
    [SerializeField] private List<int> defeatedIds = new List<int>();
    [SerializeField] private bool initialized;
    [SerializeField] private bool configured;
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
        root.AddComponent<GauntletEarnings>();
        root.AddComponent<GauntletRequirements>();
    }

    private void OnEnable()
    {
        if (!initialized) return;
        var ids = new List<int>();
        for (int i = 0; i < targets.Count; i++) ids.Add(i);
        visit = new GauntletVisit(ids);
        foreach (int id in defeatedIds) visit.RecordDefeat(id);
        IsConfigured = configured;
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
        configured = IsConfigured;
        initialized = true;
        if (targets.Count == 0) Debug.LogError("No required enemies configured in " + gameObject.scene.name + ".", this);
        GauntletRequirementsHud hud = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (GauntletRequirementsHud candidate in root.GetComponentsInChildren<GauntletRequirementsHud>(true))
            {
                if (hud != null)
                {
                    Debug.LogError("Gauntlet scene must contain exactly one authored GauntletRequirementsHud.", this);
                    return;
                }
                hud = candidate;
            }
        }
        if (hud == null)
        {
            Debug.LogError("Gauntlet scene is missing its authored GauntletRequirementsHud.", this);
            return;
        }
        hud.Initialize(this);
    }

    public void RecordDefeat(RequiredGauntletEnemy target)
    {
        if (visit == null || !targets.Contains(target) || !visit.RecordDefeat(target.VisitId)) return;
        defeatedIds.Add(target.VisitId);
        if (IsConfigured && visit.IsComplete) GauntletProgress.MarkCleared(gameObject.scene.name);
        Changed?.Invoke();
    }

    public bool IsDefeated(int enemyId) => visit != null && visit.IsDefeated(enemyId);
}
