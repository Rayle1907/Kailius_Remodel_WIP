using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ReviveOnboardingPreview : EditorWindow
{
    private const string PrefabPath = "Assets/Prefabs/UI/ReviveOnboardingView.prefab";
    [SerializeField] private GameObject previewRoot;
    [SerializeField] private GameObject view;
    [SerializeField] private int previewPage;

    [MenuItem("Tools/Revive/Preview Onboarding")]
    private static void Open()
    {
        GetWindow<ReviveOnboardingPreview>("Onboarding Preview").Show();
    }

    [MenuItem("Tools/Revive/Preview Onboarding", true)]
    private static bool CanOpen() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private void OnEnable()
    {
        saveChangesMessage = "Save onboarding preview edits to the prefab before closing?";
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        Close();
        if (this != null) EditorApplication.isPlaying = false;
    }

    private void CreatePreview()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        previewRoot = new GameObject("Onboarding Preview (temporary)", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(previewRoot, scene);
        previewRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        previewRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)previewRoot.transform).sizeDelta = new Vector2(1920f, 1080f);
        view = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        view.transform.SetParent(previewRoot.transform, false);
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        SerializedObject presenter = new SerializedObject(view.GetComponent<ReviveOnboardingPresenter>());
        TextMeshProUGUI text = (TextMeshProUGUI)presenter.FindProperty("introductionText").objectReferenceValue;
        if (text != null)
        {
            text.text = PageText;
            text.maxVisibleCharacters = int.MaxValue;
        }
        Frame();
    }

    private void Update()
    {
        if (view == null) return;
        TextMeshProUGUI text = PreviewText;
        if (text != null) text.text = ReviveOnboardingPresenter.IntroductionPageOne;
        hasUnsavedChanges = PrefabUtility.HasPrefabInstanceAnyOverrides(view, false);
        if (text != null) text.text = PageText;
        Repaint();
    }

    private void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (view == null) CreatePreview();
        if (view == null)
        {
            EditorGUILayout.HelpBox("The onboarding prefab could not be loaded.", MessageType.Error);
            return;
        }
        EditorGUILayout.HelpBox("Edit in Scene view with the Rect tool (T). Resize Dialogue Box using Width and Height in the Inspector. Save to Prefab to keep edits.", MessageType.Info);
        SelectButton("Move Everything", "LayoutRoot");
        SelectButton("Instructor", "LayoutRoot/Content/Instructor");
        SelectButton("Dialogue Box", "LayoutRoot/Content/DialogueBox");
        if (GUILayout.Button("Frame Tutorial")) Frame();
        SerializedObject presenter = new SerializedObject(view.GetComponent<ReviveOnboardingPresenter>());
        TextMeshProUGUI text = (TextMeshProUGUI)presenter.FindProperty("introductionText").objectReferenceValue;
        Image box = (Image)presenter.FindProperty("dialogueBox").objectReferenceValue;
        Canvas.ForceUpdateCanvases();
        if (text != null && box != null)
        {
            previewPage = GUILayout.Toolbar(previewPage, new[] { "Page 1", "Page 2" });
            text.text = PageText;
            float width = Mathf.Max(1f, text.rectTransform.rect.width);
            Vector2 first = text.GetPreferredValues(ReviveOnboardingPresenter.IntroductionPageOne, width, Mathf.Infinity);
            Vector2 second = text.GetPreferredValues(ReviveOnboardingPresenter.IntroductionPageTwo, width, Mathf.Infinity);
            Vector2 preferred = Vector2.Max(first, second);
            if (preferred.y > text.rectTransform.rect.height || preferred.x > text.rectTransform.rect.width)
                EditorGUILayout.HelpBox("One or both pages do not fit. Increase the box width or fit its height below.", MessageType.Warning);
            if (GUILayout.Button("Fit Box Height to Text"))
            {
                Undo.RecordObject(box.rectTransform, "Fit onboarding dialogue height");
                box.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferred.y + 112f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(box.rectTransform);
            }
        }
        if (GUILayout.Button("Save to Prefab")) SaveChanges();
        if (GUILayout.Button("Close Preview")) Close();
    }

    private void SelectButton(string label, string path)
    {
        if (!GUILayout.Button(label)) return;
        Transform selected = view.transform.Find(path);
        if (selected == null) return;
        Selection.activeGameObject = selected.gameObject;
        Tools.current = Tool.Rect;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private void Frame()
    {
        SceneView sceneView = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
        sceneView.in2DMode = true;
        sceneView.Frame(new Bounds(previewRoot.transform.position, new Vector3(1920f, 1080f, 1f)), false);
    }

    public override void SaveChanges()
    {
        if (view != null)
        {
            TextMeshProUGUI text = PreviewText;
            if (text != null)
            {
                text.text = ReviveOnboardingPresenter.IntroductionPageOne;
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            try
            {
                PrefabUtility.ApplyPrefabInstance(view, InteractionMode.UserAction);
            }
            finally
            {
                if (text != null) text.text = PageText;
            }
        }
        base.SaveChanges();
    }

    private string PageText => previewPage == 0
        ? ReviveOnboardingPresenter.IntroductionPageOne
        : ReviveOnboardingPresenter.IntroductionPageTwo;

    private TextMeshProUGUI PreviewText => view == null ? null
        : (TextMeshProUGUI)new SerializedObject(view.GetComponent<ReviveOnboardingPresenter>())
            .FindProperty("introductionText").objectReferenceValue;

    private void OnDestroy()
    {
        if (previewRoot == null) return;
        Scene scene = previewRoot.scene;
        if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
    }
}
