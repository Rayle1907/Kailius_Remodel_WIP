using System.Collections.Generic;
using System.Linq;
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

    [SerializeField] private List<RequiredGauntletEnemy> prerequisites = new List<RequiredGauntletEnemy>();
    [SerializeField, Min(0f)] private float warningRadius = 10f;

    private readonly HashSet<RequiredGauntletEnemy> uniquePrerequisites = new HashSet<RequiredGauntletEnemy>();
    private GauntletRequirements validatedOwner;
    private bool configurationValidated;
    private bool configurationValid;
    private bool errorLogged;

    public IReadOnlyList<RequiredGauntletEnemy> Prerequisites => prerequisites;
    public float WarningRadius => warningRadius;

    public GateState Evaluate(GauntletRequirements owner, List<RequiredGauntletEnemy> remaining)
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

        if (validatedOwner != owner)
        {
            validatedOwner = owner;
            configurationValidated = false;
        }

        if (!configurationValidated)
        {
            configurationValidated = true;
            configurationValid = ValidateConfiguration(owner);
        }

        if (!configurationValid) return GateState.Misconfigured;

        foreach (RequiredGauntletEnemy target in prerequisites)
            if (!owner.IsDefeated(target.VisitId)) remaining.Add(target);

        return remaining.Count == 0 ? GateState.Cleared : GateState.RequirementsRemain;
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

        if (prerequisites == null || prerequisites.Count == 0)
        {
            error = "A gated checkpoint must reference at least one required enemy.";
            return false;
        }

        uniquePrerequisites.Clear();
        foreach (RequiredGauntletEnemy target in prerequisites)
        {
            if (target == null)
            {
                error = "A checkpoint requirement references a missing required enemy.";
                return false;
            }

            if (target.gameObject.scene != gameObject.scene)
            {
                error = "Checkpoint requirements must reference enemies in the same scene.";
                return false;
            }

            if (!uniquePrerequisites.Add(target))
            {
                error = "A checkpoint requirement references the same enemy more than once.";
                return false;
            }

            if (!owner.Targets.Contains(target))
            {
                error = "A checkpoint requirement references an enemy not registered with GauntletRequirements.";
                return false;
            }
        }

        if (!owner.IsConfigured)
        {
            error = "The gauntlet requirements owner is not configured.";
            return false;
        }

        error = null;
        return true;
    }

    private bool ValidateConfiguration(GauntletRequirements owner)
    {
        bool valid = ValidateConfiguration(owner, out string error);
        if (!valid) ReportConfigurationError(error);
        return valid;
    }

    internal void ReportConfigurationError(string message)
    {
        if (errorLogged) return;
        errorLogged = true;
        Debug.LogError(message, this);
    }
}
