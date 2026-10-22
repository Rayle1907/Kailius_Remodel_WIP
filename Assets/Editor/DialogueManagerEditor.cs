using TMPro;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueManager))]
public sealed class DialogueManagerEditor : Editor
{
    private GameObject preview;
    private bool showPreview = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        showPreview = EditorGUILayout.Toggle("Preview Bubble", showPreview);
        if (GUILayout.Button("Frame Instructor and Bubble"))
        {
            DialogueManager manager = (DialogueManager)target;
            SceneView.lastActiveSceneView?.LookAt(manager.BubbleAnchor, Quaternion.identity, 8f, true);
        }
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying || !showPreview)
        {
            ClearPreview();
            return;
        }

        DialogueManager manager = (DialogueManager)target;
        GameObject source = manager.dialoguePanel;
        if (source == null)
        {
            foreach (Canvas candidate in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (candidate.gameObject.scene != manager.gameObject.scene || candidate.name != "Dialogos") continue;
                source = candidate.transform.Find("Image")?.gameObject;
                if (source != null) break;
            }
        }
        if (source == null) return;
        Canvas canvas = source.GetComponentInParent<Canvas>(true);
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;

        if (preview == null)
        {
            preview = Instantiate(canvas.gameObject);
            preview.name = "Dialogue Placement Preview";
            preview.transform.position = canvas.transform.position;
            preview.transform.rotation = canvas.transform.rotation;
            preview.transform.localScale = canvas.transform.lossyScale;
            foreach (Transform child in preview.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            preview.SetActive(true);
        }

        Transform panel = preview.transform.Find("Image");
        if (panel == null) return;
        panel.gameObject.SetActive(true);

        TextMeshProUGUI text = preview.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = manager.dialogue != null && manager.dialogue.sentenceList != null && manager.dialogue.sentenceList.Length > 0
                ? manager.dialogue.sentenceList[0] : manager.fallbackSentence;
        }
        manager.PositionBubble((RectTransform)panel);
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(manager.BubbleAnchor, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(manager, "Move Dialogue Bubble");
            manager.bubbleOffset += (Vector2)(position - manager.BubbleAnchor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorUtility.SetDirty(manager);
        }
    }

    private void OnDisable() => ClearPreview();

    private void ClearPreview()
    {
        if (preview != null)
        {
            Transform panel = preview.transform.Find("Image");
            if (panel != null) DialogueManager.ForgetBubbleSize(panel as RectTransform);
            DestroyImmediate(preview);
        }
    }
}
