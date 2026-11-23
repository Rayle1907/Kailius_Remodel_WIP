using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GauntletRequirementsValidation
{
    private static readonly string[] ProductionGauntletScenes =
    {
        "1_1Gauntlet",
        "1_2Gauntlet",
        "1_3Gauntlet",
        "2_1Gauntlet",
        "2_2Gauntlet",
        "2_3Gauntlet"
    };

    private static readonly Dictionary<string, int[]> ExpectedEnemiesBehindEachCheckpoint =
        new Dictionary<string, int[]>
        {
            { "1_1Gauntlet", new[] { 1 } },
            { "1_2Gauntlet", new[] { 1, 1 } },
            { "1_3Gauntlet", new[] { 1, 3 } },
            { "2_1Gauntlet", new[] { 1, 4 } },
            { "2_2Gauntlet", new[] { 1, 1 } },
            { "2_3Gauntlet", new[] { 2, 2 } }
        };

    [MenuItem("Tools/Gauntlets/Assign Checkpoint Routes")]
    public static void AssignCheckpointRoutes()
    {
        foreach (string sceneName in ProductionGauntletScenes)
        {
            Scene existing = SceneManager.GetSceneByPath("Assets/Scenes/" + sceneName + ".unity");
            if (existing.IsValid() && existing.isLoaded && existing.isDirty)
                throw new InvalidOperationException(sceneName + " has unsaved scene changes. Save or discard them before assigning routes.");
        }

        foreach (string sceneName in ProductionGauntletScenes)
        {
            string path = "Assets/Scenes/" + sceneName + ".unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    opened = true;
                }

                AssignCheckpointRoutes(sceneName, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(sceneName + ": assigned explicit behind-enemy routes to its non-start checkpoints.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/Gauntlets/Validate Scenes")]
    public static void ValidateScenes()
    {
        var errors = new List<string>();
        foreach (string sceneName in ProductionGauntletScenes)
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

                ValidateScene(sceneName, scene);
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

        Debug.Log("Gauntlet scene validation passed for all six production scenes.");
    }

    private static void ValidateScene(string sceneName, Scene scene)
    {
        var enemies = new List<Enemy>();
        var required = new List<RequiredGauntletEnemy>();
        var respawns = new List<GameObject>();
        var checkpointGates = new List<CheckpointRequirements>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            enemies.AddRange(root.GetComponentsInChildren<Enemy>(true));
            required.AddRange(root.GetComponentsInChildren<RequiredGauntletEnemy>(true));
            checkpointGates.AddRange(root.GetComponentsInChildren<CheckpointRequirements>(true));
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.CompareTag("ReSpawn")) respawns.Add(child.gameObject);
        }

        if (enemies.Count == 0)
            throw new InvalidOperationException(sceneName + " has no enemy components.");

        var enemiesByType = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Enemy enemy in enemies)
        {
            if (string.IsNullOrWhiteSpace(enemy.EnemyTypeId))
                throw new InvalidOperationException(sceneName + " has an enemy without an explicit type ID: " + enemy.name);
            if (!enemiesByType.ContainsKey(enemy.EnemyTypeId)) enemiesByType.Add(enemy.EnemyTypeId, 0);
            enemiesByType[enemy.EnemyTypeId]++;
        }

        var quotas = new Dictionary<string, int>(StringComparer.Ordinal);
        var portraits = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        foreach (RequiredGauntletEnemy marker in required)
        {
            string typeId = marker.EnemyType;
            if (string.IsNullOrWhiteSpace(typeId) || marker.Portrait == null)
                throw new InvalidOperationException(sceneName + " has a required enemy marker without an enemy type ID or portrait.");
            if (!enemiesByType.ContainsKey(typeId))
                throw new InvalidOperationException(sceneName + " requires an enemy type with no eligible enemies: " + typeId);
            if (portraits.TryGetValue(typeId, out Sprite portrait) && portrait != marker.Portrait)
                throw new InvalidOperationException(sceneName + " has inconsistent portraits for required enemy type " + typeId + ".");
            portraits[typeId] = marker.Portrait;
            if (!quotas.ContainsKey(typeId)) quotas.Add(typeId, 0);
            quotas[typeId]++;
        }

        foreach (KeyValuePair<string, int> quota in quotas)
            if (quota.Value > enemiesByType[quota.Key])
                throw new InvalidOperationException(sceneName + " requires more " + quota.Key + " kills than there are eligible enemies.");

        ValidateSources(sceneName, enemies, scene);
        ValidateCheckpointRoutes(sceneName, enemies, respawns, checkpointGates);
        Debug.Log(sceneName + ": validated " + enemies.Count + " eligible enemies and " + required.Count + " quota markers.");
    }

    private static void ValidateSources(string sceneName, List<Enemy> enemies, Scene scene)
    {
        if (Array.IndexOf(GauntletProgress.RequiredScenes, sceneName) < 0) return;

        var ids = new HashSet<string>();
        foreach (Enemy enemy in enemies) ValidateSource(enemy, ids);
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Chest chest in root.GetComponentsInChildren<Chest>(true)) ValidateSource(chest, ids);
    }

    private static void ValidateSource(Component source, HashSet<string> ids)
    {
        string id = new SerializedObject(source).FindProperty("rewardSourceId").stringValue;
        if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            throw new InvalidOperationException(source.gameObject.scene.name + ": missing or duplicate reward ID on " + source.name);
    }

    private static void ValidateCheckpointRoutes(string sceneName, List<Enemy> enemies,
        List<GameObject> respawns, List<CheckpointRequirements> checkpointGates)
    {
        if (respawns.Count < 1)
            throw new InvalidOperationException(sceneName + " has no respawn checkpoints.");

        List<Enemy> routeEnemies = enemies
            .OrderBy(enemy => enemy.transform.position.x)
            .ThenBy(enemy => enemy.transform.position.y)
            .ToList();
        List<GameObject> routeCheckpoints = respawns
            .OrderBy(checkpoint => checkpoint.transform.position.x)
            .ThenBy(checkpoint => checkpoint.transform.position.y)
            .ToList();
        int[] expectedCounts = ExpectedEnemiesBehindEachCheckpoint[sceneName];
        if (expectedCounts.Length != routeCheckpoints.Count - 1)
            throw new InvalidOperationException(sceneName + " has an unexpected number of respawn checkpoints.");
        if (routeCheckpoints[0].GetComponent<CheckpointRequirements>() != null)
            throw new InvalidOperationException(sceneName + " gates its initial spawn checkpoint.");
        if (checkpointGates.Count != expectedCounts.Length)
            throw new InvalidOperationException(sceneName + " has an unexpected number of gated checkpoints.");

        for (int i = 1; i < routeCheckpoints.Count; i++)
        {
            GameObject checkpoint = routeCheckpoints[i];
            CheckpointRequirements gate = checkpoint.GetComponent<CheckpointRequirements>();
            if (gate == null)
                throw new InvalidOperationException(sceneName + " is missing a gate on " + checkpoint.name + ".");
            if (checkpoint.GetComponent<CheckpointActivationRegion>() == null)
                throw new InvalidOperationException(sceneName + " gates a checkpoint without an activation region: " + checkpoint.name);

            int expectedCount = expectedCounts[i - 1];
            float checkpointX = checkpoint.transform.position.x;
            int routeCountBehind = 0;
            while (routeCountBehind < routeEnemies.Count
                && routeEnemies[routeCountBehind].transform.position.x < checkpointX)
                routeCountBehind++;
            if (routeCountBehind != expectedCount)
                throw new InvalidOperationException(sceneName + " route order changed around " + checkpoint.name + ".");

            var expected = new HashSet<Enemy>();
            for (int enemyIndex = 0; enemyIndex < expectedCount; enemyIndex++)
                expected.Add(routeEnemies[enemyIndex]);
            var actual = new HashSet<Enemy>(gate.EnemiesBehind);
            if (actual.Count != gate.EnemiesBehind.Count || !expected.SetEquals(actual))
                throw new InvalidOperationException(sceneName + " has missing, later, duplicate, or unexpected behind-enemy assignments on " + checkpoint.name + ".");
        }
    }

    private static void AssignCheckpointRoutes(string sceneName, Scene scene)
    {
        var enemies = new List<Enemy>();
        var respawns = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            enemies.AddRange(root.GetComponentsInChildren<Enemy>(true));
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.CompareTag("ReSpawn")) respawns.Add(child.gameObject);
        }

        List<Enemy> routeEnemies = enemies
            .OrderBy(enemy => enemy.transform.position.x)
            .ThenBy(enemy => enemy.transform.position.y)
            .ToList();
        List<GameObject> routeCheckpoints = respawns
            .OrderBy(checkpoint => checkpoint.transform.position.x)
            .ThenBy(checkpoint => checkpoint.transform.position.y)
            .ToList();
        int[] expectedCounts = ExpectedEnemiesBehindEachCheckpoint[sceneName];
        if (routeCheckpoints.Count - 1 != expectedCounts.Length)
            throw new InvalidOperationException(sceneName + " does not match the audited checkpoint layout.");

        GameObject initialCheckpoint = routeCheckpoints[0];
        CheckpointRequirements initialGate = initialCheckpoint.GetComponent<CheckpointRequirements>();
        if (initialGate != null) Undo.DestroyObjectImmediate(initialGate);

        for (int i = 1; i < routeCheckpoints.Count; i++)
        {
            GameObject checkpoint = routeCheckpoints[i];
            CheckpointRequirements gate = checkpoint.GetComponent<CheckpointRequirements>();
            if (gate == null) gate = Undo.AddComponent<CheckpointRequirements>(checkpoint);
            Undo.RecordObject(gate, "Assign checkpoint route");

            int expectedCount = expectedCounts[i - 1];
            float checkpointX = checkpoint.transform.position.x;
            int routeCountBehind = 0;
            while (routeCountBehind < routeEnemies.Count
                && routeEnemies[routeCountBehind].transform.position.x < checkpointX)
                routeCountBehind++;
            if (routeCountBehind != expectedCount)
                throw new InvalidOperationException(sceneName + " route order changed around " + checkpoint.name + ".");

            SerializedObject serializedGate = new SerializedObject(gate);
            SerializedProperty behind = serializedGate.FindProperty("enemiesBehind");
            behind.arraySize = expectedCount;
            for (int enemyIndex = 0; enemyIndex < expectedCount; enemyIndex++)
                behind.GetArrayElementAtIndex(enemyIndex).objectReferenceValue = routeEnemies[enemyIndex];
            serializedGate.ApplyModifiedProperties();
            EditorUtility.SetDirty(gate);
        }
    }
}
