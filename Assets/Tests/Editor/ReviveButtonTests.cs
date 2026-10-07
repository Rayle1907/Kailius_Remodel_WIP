using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ReviveButtonTests
{
    private const string PrefabPath = "Assets/Prefabs/UI/ReviveButton.prefab";
    private const string VariantPath = "Assets/Prefabs/UI/PressMeButton.prefab";
    private GameObject root;
    private EventSystem events;
    private StandaloneInputModule input;
    private int runtimeClicks;
    private int factoryClicks;

    [SetUp]
    public void SetUp()
    {
        if (Application.isPlaying) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        root = new GameObject("Button test canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventObject.transform.SetParent(root.transform);
        events = eventObject.GetComponent<EventSystem>();
        input = eventObject.GetComponent<StandaloneInputModule>();
        input.enabled = false;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (root != null) Object.DestroyImmediate(root);
    }

    [TestCase("ButtonFill")]
    [TestCase("Highlight")]
    [TestCase("Lowlight")]
    [TestCase("Outline")]
    [TestCase("PressedFill")]
    [TestCase("PressedHighlight")]
    [TestCase("PressedLowlight")]
    [TestCase("PressMeFill")]
    [TestCase("PressMeHighlight")]
    [TestCase("PressMeLowlight")]
    public void SpriteImportPreservesTransparencyAndMatchingSliceGeometry(string name)
    {
        string path = "Assets/Sprites/UI/Revive/Buttons/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(sprite, Is.Not.Null);
        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(384f, 96f)));
        Assert.That(sprite.border, Is.EqualTo(new Vector4(20f, 20f, 20f, 20f)));
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
        Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
    }

    [TestCase(PrefabPath, "ButtonFill")]
    [TestCase(VariantPath, "PressMeFill")]
    public void PrefabLayersStayAlignedAtAllThreeWidths(string path, string fillName)
    {
        LayeredButton button = InstantiateButton(path);
        Assert.That(button.GetComponent<Image>().sprite.name, Is.EqualTo(fillName));
        Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);
        if (path == VariantPath)
            Assert.That(PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)), Is.EqualTo(PrefabAssetType.Variant));
        foreach (float width in new[] { 320f, 420f, 520f })
        {
            RectTransform rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(width, 64f);
            Canvas.ForceUpdateCanvases();
            Image[] images = button.GetComponentsInChildren<Image>();
            Assert.That(images.Length, Is.EqualTo(4));
            foreach (Image image in images)
            {
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1f));
                Assert.That(image.rectTransform.rect.size, Is.EqualTo(rect.rect.size));
                Assert.That(image.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(image.raycastTarget, Is.EqualTo(image.gameObject == button.gameObject));
            }
            Assert.That(button.GetComponentInChildren<TextMeshProUGUI>().raycastTarget, Is.False);
        }
    }

    [TestCase(PrefabPath, "ButtonFill", "Highlight", "Lowlight")]
    [TestCase(VariantPath, "PressMeFill", "PressMeHighlight", "PressMeLowlight")]
    public void PointerFocusPressedDisabledAndReenableUseCompleteSpriteSets(string path, string fill, string highlight, string lowlight)
    {
        LayeredButton button = InstantiateButton(path);
        Sprite outline = button.transform.Find("Outline").GetComponent<Image>().sprite;
        PointerEventData pointer = Pointer(button);
        button.OnPointerEnter(pointer);
        Assert.That(button.GetComponentInChildren<TextMeshProUGUI>().color, Is.EqualTo(new Color(1f, 0.82f, 0.35f, 1f)));
        button.OnPointerDown(pointer);
        AssertSprites(button, "PressedFill", "PressedHighlight", "PressedLowlight");
        Assert.That(button.transform.Find("Outline").GetComponent<Image>().sprite, Is.SameAs(outline));
        button.OnPointerUp(pointer);
        button.OnPointerExit(pointer);
        events.SetSelectedGameObject(null);
        AssertSprites(button, fill, highlight, lowlight);
        button.interactable = false;
        Assert.That(button.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.45f));
        int clicks = 0;
        button.onClick.AddListener(() => clicks++);
        button.OnPointerClick(pointer);
        Assert.That(clicks, Is.Zero);
        button.interactable = true;
        button.OnPointerDown(pointer);
        button.gameObject.SetActive(false);
        button.gameObject.SetActive(true);
        AssertSprites(button, fill, highlight, lowlight);
        Assert.That(button.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
    }

    [Test]
    public void SavedGauntletCanvasHasPrefabAndLandscapeScaler()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/1_1Gauntlet.unity");
        ReviveButtonAssets assets = Object.FindAnyObjectByType<ReviveButtonAssets>(FindObjectsInactive.Include);
        Assert.That(assets, Is.Not.Null);
        Assert.That(assets.gameObject.name, Is.EqualTo("ReviveCanvas"));
        Assert.That(assets.ButtonPrefab, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<LayeredButton>()));
        CanvasScaler scaler = assets.GetComponent<CanvasScaler>();
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
        Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f));
    }

    [UnityTest]
    public IEnumerator PausedTouchSubmitAndRepeatedFactoryCleanupPreserveInteraction()
    {
        Object.DestroyImmediate(root);
        root = null;
        yield return new EnterPlayMode();
        root = new GameObject("Paused button canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject eventObject = new GameObject("Test EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventObject.transform.SetParent(root.transform);
        events = eventObject.GetComponent<EventSystem>();
        input = eventObject.GetComponent<StandaloneInputModule>();
        input.enabled = false;
        LayeredButton button = InstantiateButton(PrefabPath);
        runtimeClicks = 0;
        button.onClick.AddListener(CountRuntimeClick);
        Time.timeScale = 0f;
        PointerEventData pointer = Pointer(button);
        Touch(pointer, true, false);
        AssertSprites(button, "PressedFill", "PressedHighlight", "PressedLowlight");
        Touch(pointer, false, true);
        Assert.That(runtimeClicks, Is.EqualTo(1));
        pointer = Pointer(button);
        Touch(pointer, true, false);
        pointer.pointerCurrentRaycast = new RaycastResult();
        Touch(pointer, false, true);
        Assert.That(runtimeClicks, Is.EqualTo(1), "Release outside must cancel activation.");
        AssertSprites(button, "ButtonFill", "Highlight", "Lowlight");
        events.SetSelectedGameObject(button.gameObject);
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        Assert.That(runtimeClicks, Is.EqualTo(2));
        AssertSprites(button, "PressedFill", "PressedHighlight", "PressedLowlight");
        float end = Time.realtimeSinceStartup + 0.25f;
        while (Time.realtimeSinceStartup < end) yield return null;
        AssertSprites(button, "ButtonFill", "Highlight", "Lowlight");
        button.interactable = false;
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        Assert.That(runtimeClicks, Is.EqualTo(2));

        GameObject panel = new GameObject("Generated panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        MethodInfo factory = typeof(ReviveOfferPrototype).GetMethod("CreateButton", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo cleanup = typeof(ReviveOfferPrototype).GetMethod("RemoveGeneratedChildren", BindingFlags.Static | BindingFlags.NonPublic);
        LayeredButton prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<LayeredButton>();
        factoryClicks = 0;
        UnityEngine.Events.UnityAction action = CountFactoryClick;
        for (int i = 0; i < 3; i++)
        {
            LayeredButton revive = (LayeredButton)factory.Invoke(null, new object[] { prefab, panel.transform, "Revive", "REVIVE", new Vector2(-260f, -190f), 420f, action });
            LayeredButton restart = (LayeredButton)factory.Invoke(null, new object[] { prefab, panel.transform, "Restart", "RESTART", new Vector2(260f, -190f), 420f, action });
            Assert.That(panel.transform.childCount, Is.EqualTo(2));
            Assert.That(((RectTransform)revive.transform).sizeDelta, Is.EqualTo(new Vector2(420f, 64f)));
            Assert.That(((RectTransform)restart.transform).anchoredPosition, Is.EqualTo(new Vector2(260f, -190f)));
            revive.onClick.Invoke();
            Assert.That(factoryClicks, Is.EqualTo(i + 1));
            cleanup.Invoke(null, new object[] { panel.transform });
            Assert.That(panel.transform.childCount, Is.Zero, "Cleanup must detach immediately, before deferred destruction.");
            Assert.That(revive.gameObject.activeSelf, Is.False);
        }
        Time.timeScale = 1f;
        Object.Destroy(root);
        yield return null;
        yield return new ExitPlayMode();
    }

    private LayeredButton InstantiateButton(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        return Object.Instantiate(prefab, root.transform).GetComponent<LayeredButton>();
    }

    private void CountRuntimeClick() => runtimeClicks++;
    private void CountFactoryClick() => factoryClicks++;

    private PointerEventData Pointer(LayeredButton button) => new PointerEventData(events)
    {
        button = PointerEventData.InputButton.Left,
        pointerId = 0,
        pointerCurrentRaycast = new RaycastResult { gameObject = button.gameObject }
    };

    private void Touch(PointerEventData pointer, bool pressed, bool released)
    {
        typeof(StandaloneInputModule).GetMethod("ProcessTouchPress", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(input, new object[] { pointer, pressed, released });
    }

    private static void AssertSprites(LayeredButton button, string fill, string highlight, string lowlight)
    {
        Assert.That(button.GetComponent<Image>().sprite.name, Is.EqualTo(fill));
        Assert.That(button.transform.Find("Highlight").GetComponent<Image>().sprite.name, Is.EqualTo(highlight));
        Assert.That(button.transform.Find("Lowlight").GetComponent<Image>().sprite.name, Is.EqualTo(lowlight));
    }
}
