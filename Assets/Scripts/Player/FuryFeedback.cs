using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Stats), typeof(SpriteRenderer))]
public sealed class FuryFeedback : MonoBehaviour
{
    public const string HintSeenKey = "fury_introduction_seen";
    private const string HintRevisionKey = "fury_introduction_revision";
    public const int CurrentIntroductionRevision = 2;
    public const string Introduction = "Land 4 sword hits to charge Fury. Your next hit deals +50% damage.";
    public static bool IntroductionSeen => PlayerPrefs.GetInt(HintRevisionKey,
        PlayerPrefs.GetInt(HintSeenKey, 0) != 0 ? 1 : 0) >= CurrentIntroductionRevision;

    [SerializeField] private Material glowMaterial;
    [SerializeField, Range(0f, 1f)] private float swordTintStrength = 0.65f;
    [SerializeField] private Material swipeMaterial;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Color readyBarColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color swipeTintColor = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField, Range(0f, 1f)] private float swipeTintStrength = 0.3f;

    private Stats stats;
    private SpriteRenderer character;
    private MeshRenderer glow;
    private MeshFilter glowFilter;
    private Mesh[] frameMeshes;
    private MeshRenderer swipe;
    private MeshFilter swipeFilter;
    private Mesh[] swipeMeshes;
    private MaterialPropertyBlock properties;
    private MaterialPropertyBlock swipeProperties;
    private Image bar;
    private Color originalBarColor;
    private GameObject hint;
    private float hintVisibleSeconds;
    private bool hintWasReadable;
    private float empoweredUntil;
    private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
    private static readonly int TintColor = Shader.PropertyToID("_TintColor");
    private static readonly int TintStrength = Shader.PropertyToID("_TintStrength");

    private static readonly int[] SwordStartRows = { 28, 26, 5, 5, 4 };
    private static readonly Vector2Int[][] SwordRows = {
        new[] { new Vector2Int(13,15), new Vector2Int(12,15), new Vector2Int(11,14), new Vector2Int(10,13), new Vector2Int(9,12), new Vector2Int(8,11), new Vector2Int(7,10), new Vector2Int(7,9) },
        new[] { new Vector2Int(14,16), new Vector2Int(13,16), new Vector2Int(12,15), new Vector2Int(11,14), new Vector2Int(10,13), new Vector2Int(9,12), new Vector2Int(8,11), new Vector2Int(8,10) },
        new[] { new Vector2Int(25,28), new Vector2Int(26,29), new Vector2Int(27,30), new Vector2Int(28,31), new Vector2Int(29,30) },
        new[] { new Vector2Int(21,24), new Vector2Int(21,25), new Vector2Int(22,26), new Vector2Int(24,29), new Vector2Int(26,31), new Vector2Int(28,30) },
        new[] { new Vector2Int(20,21), new Vector2Int(18,21), new Vector2Int(18,20), new Vector2Int(17,19), new Vector2Int(17,19), new Vector2Int(17,19), new Vector2Int(16,19), new Vector2Int(16,21), new Vector2Int(16,27), new Vector2Int(16,26), new Vector2Int(16,21) }
    };
    private static readonly Vector2Int[][] SwipeRows = {
        null, null,
        new[] {
            new Vector2Int(29, 37), new Vector2Int(26, 40), new Vector2Int(23, 42), new Vector2Int(23, 43),
            new Vector2Int(22, 44), new Vector2Int(28, 45), new Vector2Int(30, 46), new Vector2Int(32, 47),
            new Vector2Int(33, 47), new Vector2Int(34, 48), new Vector2Int(35, 48), new Vector2Int(35, 48),
            new Vector2Int(36, 49), new Vector2Int(36, 49), new Vector2Int(37, 49), new Vector2Int(37, 49),
            new Vector2Int(37, 49), new Vector2Int(37, 49), new Vector2Int(37, 49), new Vector2Int(37, 49),
            new Vector2Int(37, 48), new Vector2Int(36, 48), new Vector2Int(36, 48), new Vector2Int(35, 47),
            new Vector2Int(35, 47), new Vector2Int(34, 46), new Vector2Int(33, 45), new Vector2Int(32, 44),
            new Vector2Int(30, 43), new Vector2Int(28, 42), new Vector2Int(26, 40), new Vector2Int(29, 37)
        },
        new[] {
            new Vector2Int(29, 37), new Vector2Int(26, 40), new Vector2Int(24, 42), new Vector2Int(23, 39),
            new Vector2Int(22, 32), new Vector2Int(24, 31), new Vector2Int(25, 28), new Vector2Int(26, 27)
        },
        null
    };

    private void Awake()
    {
        stats = GetComponent<Stats>();
        character = GetComponent<SpriteRenderer>();
        properties = new MaterialPropertyBlock();
        swipeProperties = new MaterialPropertyBlock();
        if (glowMaterial == null || swipeMaterial == null || attackFrames == null || attackFrames.Length != SwordRows.Length)
        {
            Debug.LogError("FuryFeedback needs its sword/swipe tint materials and five attack frames.", this);
            return;
        }

        var visual = new GameObject("Fury sword tint", typeof(MeshFilter), typeof(MeshRenderer));
        visual.transform.SetParent(transform, false);
        glow = visual.GetComponent<MeshRenderer>();
        glowFilter = visual.GetComponent<MeshFilter>();
        glow.sharedMaterial = glowMaterial;
        glow.enabled = false;
        glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glow.receiveShadows = false;
        var crescent = new GameObject("Fury swipe tint", typeof(MeshFilter), typeof(MeshRenderer));
        crescent.transform.SetParent(transform, false);
        swipe = crescent.GetComponent<MeshRenderer>();
        swipeFilter = crescent.GetComponent<MeshFilter>();
        swipe.sharedMaterial = swipeMaterial;
        swipe.enabled = false;
        swipe.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        swipe.receiveShadows = false;
        frameMeshes = new Mesh[attackFrames.Length];
        swipeMeshes = new Mesh[attackFrames.Length];
        for (int i = 0; i < attackFrames.Length; i++)
            if (attackFrames[i] != null)
            {
                frameMeshes[i] = BuildSwipeMesh(attackFrames[i], SwordRows[i], SwordStartRows[i]);
                if (SwipeRows[i] != null) swipeMeshes[i] = BuildSwipeMesh(attackFrames[i], SwipeRows[i]);
            }
    }

    private void OnEnable()
    {
        stats.FuryChargeChanged += OnChargeChanged;
        stats.FuryConsumed += OnConsumed;
    }

    private void Start()
    {
        BindBar();
        OnChargeChanged(stats.getPower());
    }

    private void OnChargeChanged(int charge)
    {
        BindBar();
        if (charge < Stats.FuryChargeLimit)
        {
            if (bar != null) bar.color = originalBarColor;
            if (stats.getHealth() <= 0)
            {
                empoweredUntil = 0f;
                if (hint != null) hint.SetActive(false);
                if (glow != null) glow.enabled = false;
                if (swipe != null) swipe.enabled = false;
            }
            return;
        }
        ShowIntroduction();
    }

    private void OnConsumed() => empoweredUntil = Time.time + 0.45f;

    private void BindBar()
    {
        if (bar == stats.powers) return;
        if (bar != null) bar.color = originalBarColor;
        bar = stats.powers;
        if (bar != null) originalBarColor = bar.color;
    }

    private void Update()
    {
        BindBar();
        if (bar != null && stats.IsFuryReady)
        {
            float pulse = 0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f);
            Color target = readyBarColor;
            target.a = originalBarColor.a;
            bar.color = Color.Lerp(originalBarColor, target, pulse);
        }
        if (!IntroductionSeen && stats.IsFuryReady && (hint == null || !hint.activeSelf)) ShowIntroduction();
        bool readable = hint != null && hint.activeInHierarchy && stats.getHealth() > 0 && Application.isFocused && Time.timeScale > 0f;
        if (readable && hintWasReadable) AdvanceIntroduction(Time.unscaledDeltaTime);
        hintWasReadable = readable;
    }

    private void OnApplicationFocus(bool focused) => hintWasReadable = false;
    private void OnApplicationPause(bool paused) => hintWasReadable = false;

    private void LateUpdate()
    {
        if (glow == null) return;
        bool empowered = Time.time < empoweredUntil;
        int frame = -1;
        if (stats.getHealth() > 0 && (stats.IsFuryReady || empowered))
            for (int i = 0; i < attackFrames.Length; i++)
                if (character.sprite == attackFrames[i]) { frame = i; break; }
        glow.enabled = frame >= 0 && frameMeshes[frame] != null && character.enabled;
        swipe.enabled = glow.enabled && empowered && swipeMeshes[frame] != null;
        if (!glow.enabled) return;
        glowFilter.sharedMesh = frameMeshes[frame];
        glow.sortingLayerID = character.sortingLayerID;
        glow.sortingOrder = character.sortingOrder + 2;
        glow.transform.localScale = new Vector3(character.flipX ? -1f : 1f, character.flipY ? -1f : 1f, 1f);
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.PI * 2f);
        properties.SetTexture(MainTexture, attackFrames[frame].texture);
        properties.SetColor(TintColor, new Color(1f, 0.15f, 0.15f, 1f));
        properties.SetFloat(TintStrength, swordTintStrength * (empowered ? 1f : pulse));
        glow.SetPropertyBlock(properties);
        if (!swipe.enabled) return;
        swipeFilter.sharedMesh = swipeMeshes[frame];
        swipe.sortingLayerID = character.sortingLayerID;
        swipe.sortingOrder = character.sortingOrder + 1;
        swipe.transform.localScale = glow.transform.localScale;
        swipeProperties.SetTexture(MainTexture, attackFrames[frame].texture);
        swipeProperties.SetColor(TintColor, swipeTintColor);
        swipeProperties.SetFloat(TintStrength, swipeTintStrength);
        swipe.SetPropertyBlock(swipeProperties);
    }

    private void ShowIntroduction()
    {
        if (IntroductionSeen || bar == null) return;
        Canvas canvas = bar.GetComponentInParent<Canvas>();
        if (canvas == null || !canvas.isActiveAndEnabled || !bar.gameObject.activeInHierarchy) return;
        if (hint != null)
        {
            hintVisibleSeconds = 0f;
            hintWasReadable = false;
            hint.SetActive(true);
            return;
        }
        canvas = canvas.rootCanvas;
        hint = new GameObject("Fury introduction", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform rect = hint.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        Rect safe = Screen.safeArea;
        float width = Mathf.Max(1f, Screen.width), height = Mathf.Max(1f, Screen.height);
        rect.anchorMin = new Vector2((safe.xMin + safe.width * 0.1f) / width, (safe.yMin + safe.height * 0.76f) / height);
        rect.anchorMax = new Vector2((safe.xMin + safe.width * 0.9f) / width, (safe.yMin + safe.height * 0.89f) / height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        hint.GetComponent<CanvasGroup>().blocksRaycasts = false;
        Image background = hint.GetComponent<Image>();
        background.color = new Color(0.12f, 0.025f, 0.025f, 0.9f);
        background.raycastTarget = false;

        var label = new GameObject("Explanation", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 6f);
        labelRect.offsetMax = new Vector2(-12f, -6f);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        if (stats.textDamage != null) text.font = stats.textDamage.font;
        text.text = Introduction;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMax = 54f * Mathf.Max(1f, safe.height) / 1080f / Mathf.Max(0.001f, canvas.scaleFactor);
        text.fontSizeMin = text.fontSizeMax * 0.75f;
        text.raycastTarget = false;
        hintVisibleSeconds = 0f;
        hintWasReadable = false;
    }

    private void AdvanceIntroduction(float visibleSeconds)
    {
        hintVisibleSeconds += Mathf.Max(0f, visibleSeconds);
        if (hintVisibleSeconds < 8f) return;
        hint.SetActive(false);
        PlayerPrefs.SetInt(HintRevisionKey, CurrentIntroductionRevision);
        PlayerPrefs.SetInt(HintSeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void ResetIntroduction()
    {
        PlayerPrefs.DeleteKey(HintSeenKey);
        PlayerPrefs.DeleteKey(HintRevisionKey);
        PlayerPrefs.Save();
    }

    private static Mesh BuildSwipeMesh(Sprite sprite, Vector2Int[] rows, int startRow = 0)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        Rect textureRect = sprite.textureRect;
        Vector2 textureOffset = sprite.textureRectOffset;
        for (int y = 0; y < rows.Length; y++)
        {
            int start = vertices.Count;
            Vector2[] corners = {
                new Vector2(rows[y].x, y + startRow), new Vector2(rows[y].y, y + startRow),
                new Vector2(rows[y].y, y + startRow + 1), new Vector2(rows[y].x, y + startRow + 1)
            };
            foreach (Vector2 point in corners)
            {
                vertices.Add(new Vector3((point.x - sprite.pivot.x) / sprite.pixelsPerUnit,
                    (sprite.rect.height - point.y - sprite.pivot.y) / sprite.pixelsPerUnit, 0f));
                uv.Add(new Vector2((textureRect.x + point.x - textureOffset.x) / sprite.texture.width,
                    (textureRect.y + sprite.rect.height - point.y - textureOffset.y) / sprite.texture.height));
            }
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        var mesh = new Mesh { name = "Fury original artwork mask" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDisable()
    {
        stats.FuryChargeChanged -= OnChargeChanged;
        stats.FuryConsumed -= OnConsumed;
        if (bar != null) bar.color = originalBarColor;
        if (glow != null) glow.enabled = false;
        if (swipe != null) swipe.enabled = false;
        if (hint != null) hint.SetActive(false);
        hintWasReadable = false;
        empoweredUntil = 0f;
    }

    private void OnDestroy()
    {
        if (hint != null) Release(hint);
        if (frameMeshes != null)
            foreach (Mesh mesh in frameMeshes) if (mesh != null) Release(mesh);
        if (swipeMeshes != null)
            foreach (Mesh mesh in swipeMeshes) if (mesh != null) Release(mesh);
    }

    private static void Release(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
