using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CheckpointRequirements : MonoBehaviour
{
    public enum GateState
    {
        WaitingForInitialization,
        Misconfigured,
        RequirementsRemain,
        Cleared
    }

    public sealed class Shortfall
    {
        public string TypeId;
        public int Count;
    }

    [SerializeField] private List<Enemy> enemiesBehind = new List<Enemy>();
    [SerializeField, Min(0f)] private float warningRadius = 10f;

    private readonly HashSet<Enemy> uniqueEnemies = new HashSet<Enemy>();
    private readonly HashSet<int> enemiesBehindIds = new HashSet<int>();
    private GauntletRequirements validatedOwner;
    private bool configurationValidated;
    private bool configurationValid;
    private bool errorLogged;

    public IReadOnlyList<Enemy> EnemiesBehind => enemiesBehind;
    public float WarningRadius => warningRadius;

    public GateState Evaluate(GauntletRequirements owner, List<Shortfall> remaining)
    {
        remaining.Clear();
        if (owner == null)
        {
            ReportConfigurationError("Checkpoint requirements could not find the gauntlet requirements owner.");
            return GateState.Misconfigured;
        }

        if (owner.gameObject.scene != gameObject.scene)
        {
            ReportConfigurationError("Checkpoint requirements must use a scene-local gauntlet requirements owner.");
            return GateState.Misconfigured;
        }

        if (!owner.IsInitialized) return GateState.WaitingForInitialization;

        Initialize(owner);
        if (!configurationValid) return GateState.Misconfigured;
        if (owner.RequiredCount == 0) return GateState.Cleared;

        foreach (string typeId in owner.QuotaTypes)
        {
            int shortfall = GauntletVisit.CalculateBehindShortfall(
                owner.GetQuota(typeId),
                owner.GetDefeatedCount(typeId),
                owner.GetLivingAheadCount(typeId, enemiesBehindIds));
            if (shortfall <= 0) continue;
            remaining.Add(new Shortfall
            {
                TypeId = typeId,
                Count = shortfall
            });
        }

        if (remaining.Count == 0) return GateState.Cleared;
        return GateState.RequirementsRemain;
    }

    public void Initialize(GauntletRequirements owner)
    {
        if (owner == null || !owner.IsInitialized) return;
        if (configurationValidated && validatedOwner == owner) return;

        validatedOwner = owner;
        configurationValidated = true;
        configurationValid = ValidateConfiguration(owner, out string error);
        enemiesBehindIds.Clear();
        if (!configurationValid)
        {
            ReportConfigurationError(error);
            return;
        }

        foreach (Enemy enemy in enemiesBehind)
        {
            if (owner.TryGetVisitId(enemy, out int visitId)) enemiesBehindIds.Add(visitId);
        }
    }

    public bool ValidateConfiguration(GauntletRequirements owner, out string error)
    {
        if (owner == null)
        {
            error = "Checkpoint requirements could not find the gauntlet requirements owner.";
            return false;
        }

        if (owner.gameObject.scene != gameObject.scene)
        {
            error = "Checkpoint requirements must use a scene-local gauntlet requirements owner.";
            return false;
        }

        if (enemiesBehind == null)
        {
            error = "A checkpoint has a missing behind-enemy list.";
            return false;
        }

        if (!owner.IsConfigured)
        {
            error = "The gauntlet requirements owner is not configured.";
            return false;
        }

        uniqueEnemies.Clear();
        foreach (Enemy enemy in enemiesBehind)
        {
            if (enemy == null)
            {
                error = "A checkpoint references a missing enemy behind it.";
                return false;
            }

            if (enemy.gameObject.scene != gameObject.scene)
            {
                error = "Checkpoint route assignments must reference enemies in the same scene.";
                return false;
            }

            if (!uniqueEnemies.Add(enemy))
            {
                error = "A checkpoint references the same enemy behind it more than once.";
                return false;
            }

            if (!owner.TryGetVisitId(enemy, out _))
            {
                error = "A checkpoint references an enemy not registered with GauntletRequirements.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public void ReportConfigurationError(string message)
    {
        if (errorLogged) return;
        errorLogged = true;
        Debug.LogError(message, this);
    }
}
