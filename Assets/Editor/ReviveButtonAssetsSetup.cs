using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ReviveButtonAssetsSetup
{
    static ReviveButtonAssetsSetup()
    {
        EditorApplication.delayCall += AssignMissingReferences;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorSceneManager.sceneOpened += (scene, mode) => AssignMissingReferences();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            AssignMissingReferences();
        }
    }

    private static void AssignMissingReferences()
    {
        if (EditorApplication.isPlaying)
        {
            return;
        }

        foreach (ReviveButtonAssets assets in Resources.FindObjectsOfTypeAll<ReviveButtonAssets>())
        {
            if (!assets.gameObject.scene.IsValid() || !assets.gameObject.scene.isLoaded)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(assets);
            AssignPrefab(serialized, "buttonPrefab", "b0b92fef0172a2d4c91c7c1885472d61");
            AssignPrefab(serialized, "pressMeButtonPrefab", "5ebed260254e783419452c0343f1513c");
            SerializedProperty font = serialized.FindProperty("uiFont");
            if (font.objectReferenceValue == null)
            {
                font.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    AssetDatabase.GUIDToAssetPath("d22c986e05eac53b8bc884c1372fa019"));
            }

            if (serialized.ApplyModifiedProperties())
            {
                EditorSceneManager.MarkSceneDirty(assets.gameObject.scene);
            }
        }
    }

    private static void AssignPrefab(SerializedObject serialized, string propertyName, string guid)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property.objectReferenceValue != null)
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        if (prefab != null)
        {
            property.objectReferenceValue = prefab.GetComponent<LayeredButton>();
        }
    }
}
