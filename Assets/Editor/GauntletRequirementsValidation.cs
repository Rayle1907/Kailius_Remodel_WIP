using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GauntletRequirementsValidation
{
    private static readonly Dictionary<string, int[]> ExpectedCheckpointPrerequisiteCounts =
        new Dictionary<string, int[]>
        {
            { "1_1Gauntlet", new[] { 1 } },
            { "1_3Gauntlet", new[] { 1, 3 } }
        };

    [MenuItem("Tools/Gauntlets/Validate Scenes")]
    public static void ValidateScenes()
    {
        int sources = 0;
        var errors = new List<string>();
        foreach (string sceneName in GauntletProgress.RequiredScenes)
        {
            string path = "Assets/Scenes/" + sceneName + ".unity";
            Scene scene = default;
            bool opened = false;
            try
            {
                if (!EditorBuildSettings.scenes.Any(buildScene => buildScene.enabled && buildScene.path == path))
                    throw new InvalidOperationException(sceneName + " is missing from enabled build scenes.");

                scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    opened = true;
                }

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
                ValidateCheckpointPrerequisiteOrder(sceneName, required, checkpointGates);
                if (required.Count == 0) Debug.LogWarning(sceneName + ": select required enemies by adding RequiredGauntletEnemy components.");
                sources += ids.Count;
                Debug.Log(sceneName + ": validated " + ids.Count + " reward IDs and " + required.Count + " required enemies.");
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Gauntlet scene validation failed:\n" + string.Join("\n", errors));

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

    private static void ValidateCheckpointPrerequisiteOrder(string sceneName,
        List<RequiredGauntletEnemy> required, List<CheckpointRequirements> checkpointGates)
    {
        if (!ExpectedCheckpointPrerequisiteCounts.TryGetValue(sceneName, out int[] expectedCounts)) return;
        if (checkpointGates.Count != expectedCounts.Length)
            throw new InvalidOperationException(sceneName + " has an unexpected number of gated checkpoints.");

        List<RequiredGauntletEnemy> routeEnemies = required
            .OrderBy(target => target.transform.position.x)
            .ToList();
        List<CheckpointRequirements> routeCheckpoints = checkpointGates
            .OrderBy(checkpoint => checkpoint.transform.position.x)
            .ToList();

        for (int i = 0; i < routeCheckpoints.Count; i++)
        {
            int expectedCount = expectedCounts[i];
            if (expectedCount < 0 || expectedCount > routeEnemies.Count)
                throw new InvalidOperationException(sceneName + " has an invalid audited checkpoint prerequisite count.");

            float checkpointX = routeCheckpoints[i].transform.position.x;
            if (expectedCount > 0 && routeEnemies[expectedCount - 1].transform.position.x >= checkpointX)
                throw new InvalidOperationException(sceneName + " has a checkpoint before one of its audited prerequisites.");
            if (expectedCount < routeEnemies.Count && routeEnemies[expectedCount].transform.position.x <= checkpointX)
                throw new InvalidOperationException(sceneName + " has an unlisted required enemy behind a checkpoint.");

            var expected = new HashSet<RequiredGauntletEnemy>(routeEnemies.Take(expectedCount));
            var actual = new HashSet<RequiredGauntletEnemy>(routeCheckpoints[i].Prerequisites);
            if (!expected.SetEquals(actual))
                throw new InvalidOperationException(sceneName + " has checkpoint prerequisites that do not match the required enemies behind it.");
        }
    }
}
