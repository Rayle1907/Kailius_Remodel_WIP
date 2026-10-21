using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class GauntletProgressDebugWindow : EditorWindow
{
    [SerializeField] private int selectedGauntlet;
    private string[] gauntlets = Array.Empty<string>();

    [MenuItem("Tools/Gauntlets/Debug Progress")]
    private static void Open()
    {
        GetWindow<GauntletProgressDebugWindow>("Gauntlet Progress").Show();
    }

    private void OnEnable()
    {
        gauntlets = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(GauntletHealthDropRules.IsGauntletScene)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        selectedGauntlet = Mathf.Clamp(selectedGauntlet, 0, Mathf.Max(0, gauntlets.Length - 1));
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Changes are saved to the current Editor player progress and persist after Play Mode.", MessageType.Info);
        if (gauntlets.Length == 0)
        {
            EditorGUILayout.HelpBox("No gauntlet scenes found in Assets/Scenes.", MessageType.Warning);
            return;
        }

        selectedGauntlet = EditorGUILayout.Popup("Gauntlet", selectedGauntlet, gauntlets);
        string sceneName = gauntlets[selectedGauntlet];
        bool cleared;
        try
        {
            cleared = GauntletProgress.IsCleared(sceneName);
        }
        catch (InvalidOperationException exception)
        {
            EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Status", cleared ? "Cleared" : "Not cleared");
        using (new EditorGUI.DisabledScope(cleared))
        {
            if (GUILayout.Button("Mark Cleared"))
            {
                GauntletProgress.MarkCleared(sceneName);
                Repaint();
            }
        }
        using (new EditorGUI.DisabledScope(!cleared))
        {
            if (GUILayout.Button("Mark Uncleared"))
            {
                GauntletProgress.MarkUncleared(sceneName);
                Repaint();
            }
        }
    }

    private void OnFocus()
    {
        Repaint();
    }
}
