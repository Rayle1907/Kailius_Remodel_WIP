using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GauntletRewardIds
{
    [MenuItem("Tools/Gauntlets/Assign Reward IDs in Open Scenes")]
    public static void AssignInOpenScenes()
    {
        if (Application.isPlaying) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !GauntletHealthDropRules.IsGauntletScene(scene.name)) continue;
            var used = new HashSet<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Enemy enemy in root.GetComponentsInChildren<Enemy>(true)) Assign(enemy, used);
                foreach (Chest chest in root.GetComponentsInChildren<Chest>(true)) Assign(chest, used);
            }
        }
    }

    [MenuItem("Tools/Open World/Assign Chest IDs in Open Scenes")]
    public static void AssignOpenWorldChests()
    {
        if (Application.isPlaying) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !OpenWorldChestProgress.IsOpenWorldScene(scene.name)) continue;
            var used = new HashSet<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Chest chest in root.GetComponentsInChildren<Chest>(true)) Assign(chest, used);
        }
    }

    private static void Assign(Component source, HashSet<string> used)
    {
        SerializedObject serialized = new SerializedObject(source);
        SerializedProperty property = serialized.FindProperty("rewardSourceId");
        if (!string.IsNullOrEmpty(property.stringValue) && used.Add(property.stringValue)) return;
        string id = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
        if (!used.Add(id))
        {
            Debug.LogError("Could not assign a unique reward ID. Save the scene first.", source);
            return;
        }
        property.stringValue = id;
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
    }
}
