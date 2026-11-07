using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GauntletRequirementsValidation
{
    [MenuItem("Tools/Gauntlets/Validate Scenes")]
    public static void ValidateScenes()
    {
        int sources = 0;
        foreach (string sceneName in GauntletProgress.RequiredScenes)
        {
            string path = "Assets/Scenes/" + sceneName + ".unity";
            if (!EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path))
                throw new InvalidOperationException(sceneName + " is missing from enabled build scenes.");
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var ids = new HashSet<string>();
                var required = new List<RequiredGauntletEnemy>();
                var respawns = new List<GameObject>();
                var checkpointGates = new List<CheckpointRequirements>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Enemy enemy in root.GetComponentsInChildren<Enemy>(true)) ValidateSource(enemy, ids);
                    foreach (Chest chest in root.GetComponentsInChildren<Chest>(true)) ValidateSource(chest, ids);
                    required.AddRange(root.GetComponentsInChildren<RequiredGauntletEnemy>(true));
                    checkpointGates.AddRange(root.GetComponentsInChildren<CheckpointRequirements>(true));
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (child.CompareTag("ReSpawn")) respawns.Add(child.gameObject);
                }
                foreach (RequiredGauntletEnemy target in required)
                    if (string.IsNullOrWhiteSpace(target.EnemyType) || target.Portrait == null)
                        throw new InvalidOperationException(sceneName + " has a required enemy without a type or portrait.");
                ValidateCheckpointRequirements(sceneName, required, respawns, checkpointGates);
                if (required.Count == 0) Debug.LogWarning(sceneName + ": select required enemies by adding RequiredGauntletEnemy components.");
                sources += ids.Count;
                Debug.Log(sceneName + ": validated " + ids.Count + " reward IDs and " + required.Count + " required enemies.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
        Debug.Log("Gauntlet scene validation passed: " + sources + " unique reward sources.");
    }

    private static void ValidateCheckpointRequirements(string sceneName,
        List<RequiredGauntletEnemy> required, List<GameObject> respawns,
        List<CheckpointRequirements> checkpointGates)
    {
        var requiredSet = new HashSet<RequiredGauntletEnemy>(required);
        foreach (GameObject checkpoint in respawns)
        {
            CheckpointRequirements gate = checkpoint.GetComponent<CheckpointRequirements>();
            if (checkpoint.name == "Respawn" && gate != null)
                throw new InvalidOperationException(sceneName + " gates its initial Respawn checkpoint.");
        }

        foreach (CheckpointRequirements gate in checkpointGates)
        {
            GameObject checkpoint = gate.gameObject;
            if (!checkpoint.CompareTag("ReSpawn"))
                throw new InvalidOperationException(sceneName + " has checkpoint requirements on a non-checkpoint object: " + checkpoint.name);
            if (checkpoint.GetComponent<CheckpointActivationRegion>() == null)
                throw new InvalidOperationException(sceneName + " gates a checkpoint without a CheckpointActivationRegion: " + checkpoint.name);
            if (gate.Prerequisites == null || gate.Prerequisites.Count == 0)
                throw new InvalidOperationException(sceneName + " has a gated checkpoint without required enemies: " + checkpoint.name);

            var seen = new HashSet<RequiredGauntletEnemy>();
            foreach (RequiredGauntletEnemy target in gate.Prerequisites)
            {
                if (target == null)
                    throw new InvalidOperationException(sceneName + " has a missing required enemy reference on " + checkpoint.name + ".");
                if (target.gameObject.scene != checkpoint.scene)
                    throw new InvalidOperationException(sceneName + " has a cross-scene required enemy reference on " + checkpoint.name + ".");
                if (!seen.Add(target))
                    throw new InvalidOperationException(sceneName + " has a duplicate required enemy reference on " + checkpoint.name + ".");
                if (!requiredSet.Contains(target))
                    throw new InvalidOperationException(sceneName + " references an enemy not registered with GauntletRequirements on " + checkpoint.name + ".");
            }
        }

        if (!respawns.Any(item => item.name == "Respawn" && item.GetComponent<CheckpointRequirements>() == null))
            throw new InvalidOperationException(sceneName + " needs an ungated initial Respawn checkpoint as its fallback.");
    }

    private static void ValidateSource(Component source, HashSet<string> ids)
    {
        string id = new SerializedObject(source).FindProperty("rewardSourceId").stringValue;
        if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            throw new InvalidOperationException(source.gameObject.scene.name + ": missing or duplicate reward ID on " + source.name);
    }
}
