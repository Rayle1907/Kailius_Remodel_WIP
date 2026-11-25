using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GauntletRequirements : MonoBehaviour
{
    [Serializable]
    private sealed class EnemyRecord
    {
        public Enemy Target;
        public int VisitId;
        public string TypeId;
    }

    [Serializable]
    private sealed class QuotaRecord
    {
        public string TypeId;
        public int Count;
        public Sprite Portrait;
    }

    public event Action Changed;
    public int DefeatedCount
    {
        get
        {
            if (visit == null) return 0;
            return visit.DefeatedCount;
        }
    }
    public int RequiredCount
    {
        get
        {
            if (visit == null) return 0;
            return visit.RequiredCount;
        }
    }
    public bool IsConfigured { get; private set; }
    public bool IsInitialized => initialized;
    public bool IsCurrentAttemptComplete
    {
        get
        {
            if (!initialized || !IsConfigured || visit == null || !visit.IsConfigured) return false;
            if (visit.RequiredCount == 0) return true;
            return visit.IsComplete;
        }
    }
    public bool IsCleared => GauntletProgress.IsCleared(gameObject.scene.name);
    public IReadOnlyList<string> QuotaTypes => quotaTypes;

    [SerializeField] private List<EnemyRecord> enemies = new List<EnemyRecord>();
    [SerializeField] private List<QuotaRecord> quotas = new List<QuotaRecord>();
    [SerializeField] private List<int> defeatedIds = new List<int>();
    [SerializeField] private bool initialized;
    [SerializeField] private bool configured;

    private readonly List<string> quotaTypes = new List<string>();
    private GauntletVisit visit;
    private GauntletRequirementsHud hud;

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
        RebuildVisit();
        IsConfigured = configured;
    }

    private void Start()
    {
        enemies.Clear();
        quotas.Clear();
        quotaTypes.Clear();
        defeatedIds.Clear();
        IsConfigured = true;

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (Enemy enemy in root.GetComponentsInChildren<Enemy>(true))
            {
                string typeId = enemy.EnemyTypeId;
                if (string.IsNullOrWhiteSpace(typeId))
                {
                    IsConfigured = false;
                    Debug.LogError("Every gauntlet enemy needs an enemy type ID.", enemy);
                    continue;
                }

                int visitId = enemies.Count;
                enemies.Add(new EnemyRecord { Target = enemy, VisitId = visitId, TypeId = typeId });
                enemy.BindGauntletRequirements(this, visitId);

                RequiredGauntletEnemy required = enemy.GetComponent<RequiredGauntletEnemy>();
                if (required == null) continue;
                if (required.Portrait == null)
                {
                    IsConfigured = false;
                    Debug.LogError("Required enemy quota markers need a HUD portrait.", required);
                    continue;
                }

                QuotaRecord quota = FindQuota(typeId);
                if (quota == null)
                {
                    quota = new QuotaRecord { TypeId = typeId, Portrait = required.Portrait };
                    quotas.Add(quota);
                    quotaTypes.Add(typeId);
                }
                else if (quota.Portrait != required.Portrait)
                {
                    IsConfigured = false;
                    Debug.LogError("Required enemy markers of the same type need the same HUD portrait.", required);
                }

                quota.Count++;
                required.Bind(this, visitId);
            }
        }

        RebuildVisit();
        IsConfigured &= visit.IsConfigured && enemies.Count > 0;
        configured = IsConfigured;
        initialized = true;

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (CheckpointRequirements checkpoint in root.GetComponentsInChildren<CheckpointRequirements>(true))
                checkpoint.Initialize(this);
        }

        hud = null;
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

    private QuotaRecord FindQuota(string typeId)
    {
        foreach (QuotaRecord quota in quotas)
            if (quota.TypeId == typeId) return quota;
        return null;
    }

    private void RebuildVisit()
    {
        var enemyTypes = new List<KeyValuePair<int, string>>(enemies.Count);
        foreach (EnemyRecord enemy in enemies)
            enemyTypes.Add(new KeyValuePair<int, string>(enemy.VisitId, enemy.TypeId));

        var quotaCounts = new List<KeyValuePair<string, int>>(quotas.Count);
        foreach (QuotaRecord quota in quotas)
            quotaCounts.Add(new KeyValuePair<string, int>(quota.TypeId, quota.Count));

        visit = new GauntletVisit(enemyTypes, quotaCounts);
        foreach (int id in defeatedIds)
            visit.RecordDefeat(id);

        foreach (EnemyRecord enemy in enemies)
            if (enemy.Target != null) enemy.Target.BindGauntletRequirements(this, enemy.VisitId);
    }

    public void RecordDefeat(int enemyId)
    {
        if (visit == null || !visit.RecordDefeat(enemyId)) return;
        defeatedIds.Add(enemyId);
        if (IsConfigured && visit.IsComplete)
            GauntletProgress.MarkCleared(gameObject.scene.name);
        Changed?.Invoke();
    }

    public bool IsDefeated(int enemyId)
    {
        return visit != null && visit.IsDefeated(enemyId);
    }

    public bool ContainsEnemy(int enemyId)
    {
        return visit != null && visit.ContainsEnemy(enemyId);
    }

    public bool TryGetVisitId(Enemy enemy, out int visitId)
    {
        visitId = -1;
        if (enemy == null || enemy.gameObject.scene != gameObject.scene) return false;
        visitId = enemy.GauntletVisitId;
        return visit != null && visit.ContainsEnemy(visitId);
    }

    public int GetQuota(string typeId)
    {
        if (visit == null) return 0;
        return visit.GetQuota(typeId);
    }

    public int GetDefeatedCount(string typeId)
    {
        if (visit == null) return 0;
        return visit.GetDefeatedCount(typeId);
    }

    public IReadOnlyList<KeyValuePair<string, int>> GetRemainingQuotaCounts()
    {
        List<KeyValuePair<string, int>> remaining = new List<KeyValuePair<string, int>>();
        if (visit == null) return remaining;
        foreach (string typeId in quotaTypes)
        {
            int quota = visit.GetQuota(typeId);
            int count = quota - Mathf.Min(quota, visit.GetDefeatedCount(typeId));
            if (count > 0) remaining.Add(new KeyValuePair<string, int>(typeId, count));
        }
        return remaining;
    }

    public int GetLivingAheadCount(string typeId, HashSet<int> enemiesBehind)
    {
        if (visit == null) return 0;
        return visit.GetLivingAheadCount(typeId, enemiesBehind);
    }

    public Sprite GetQuotaPortrait(string typeId)
    {
        QuotaRecord quota = FindQuota(typeId);
        if (quota == null) return null;
        return quota.Portrait;
    }
}
