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
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Enemy enemy in root.GetComponentsInChildren<Enemy>(true)) ValidateSource(enemy, ids);
                    foreach (Chest chest in root.GetComponentsInChildren<Chest>(true)) ValidateSource(chest, ids);
                    required.AddRange(root.GetComponentsInChildren<RequiredGauntletEnemy>(true));
                }
                foreach (RequiredGauntletEnemy target in required)
                    if (string.IsNullOrWhiteSpace(target.EnemyType) || target.Portrait == null)
                        throw new InvalidOperationException(sceneName + " has a required enemy without a type or portrait.");
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

    private static void ValidateSource(Component source, HashSet<string> ids)
    {
        string id = new SerializedObject(source).FindProperty("rewardSourceId").stringValue;
        if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            throw new InvalidOperationException(source.gameObject.scene.name + ": missing or duplicate reward ID on " + source.name);
    }
}
