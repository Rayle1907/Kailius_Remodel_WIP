using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(LayeredButton)), CanEditMultipleObjects]
public sealed class LayeredButtonEditor : ButtonEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.Space();
        foreach (string field in new[]
        {
            "fillImage", "grassImage", "highlightImage", "lowlightImage", "outlineImage", "label",
            "visualGroup", "normalSprites", "pressedSprites", "focusLabelColor", "disabledOpacity"
        })
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
