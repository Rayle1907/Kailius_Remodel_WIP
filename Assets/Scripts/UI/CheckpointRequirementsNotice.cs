using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CheckpointRequirementsNotice : MonoBehaviour
{
    private sealed class EnemyTypeProgress
    {
        public string Type;
        public int Defeated;
        public int Total;
    }

    private const string Message = "This checkpoint will not activate until you defeat the remaining required enemies in this section.";
    private static CheckpointRequirementsNotice active;

    private readonly List<GauntletEnemyRow> rows = new List<GauntletEnemyRow>();
    private readonly List<RequiredGauntletEnemy> remaining = new List<RequiredGauntletEnemy>();
    private readonly List<EnemyTypeProgress> progress = new List<EnemyTypeProgress>();
    private PlayerController player;
    private CheckpointRequirements checkpointRequirements;
    private GauntletRequirements requirements;
    private GauntletRequirementsHud hud;
    private GameObject panel;
    private RectTransform panelRect;
    private RectTransform rowsRoot;
    private float scaleRatio;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private Canvas rootCanvas;
    private TMP_Text messageText;
    private CheckpointRequirements.GateState lastState;
    private bool hasState;

    public static void Show(PlayerController player, CheckpointRequirements checkpointRequirements,
        GauntletRequirements requirements, GauntletRequirementsHud hud, Stats stats)
    {
        if (player == null || checkpointRequirements == null) return;
        if (hud == null || hud.RootCanvas == null)
        {
            checkpointRequirements.ReportConfigurationError("A checkpoint requirements notice needs the gauntlet requirements HUD and its Canvas.");
            return;
        }

        if (active == null || active.hud == null || active.hud.RootCanvas != hud.RootCanvas)
        {
            if (active != null) active.Close();
            GameObject root = new GameObject("Checkpoint Requirements Notice", typeof(RectTransform));
            root.transform.SetParent(hud.RootCanvas.transform, false);
            active = root.AddComponent<CheckpointRequirementsNotice>();
            active.Build(hud, stats);
        }

        bool contextChanged = active.Bind(player, checkpointRequirements, requirements, hud);
        if (contextChanged || !active.hasState || active.lastState == CheckpointRequirements.GateState.WaitingForInitialization)
            active.Refresh();
    }

    public static void HideFor(PlayerController player, CheckpointRequirements checkpointRequirements)
    {
        if (active == null || active.player != player) return;
        if (checkpointRequirements == null || active.checkpointRequirements != checkpointRequirements) return;
        active.Close();
    }

    public static void HideForPlayer(PlayerController player)
    {
        if (active == null || active.player != player) return;
        active.Close();
    }

    public static void HideForActivation(PlayerController player)
    {
        if (active == null || active.player != player) return;
        active.Close();
    }

    private void Build(GauntletRequirementsHud ownerHud, Stats ownerStats)
    {
        Canvas canvas = ownerHud.RootCanvas;
        rootCanvas = canvas;

        RectTransform rootRect = (RectTransform)transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        panel = new GameObject("Warning Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(transform, false);
        panelRect.pivot = new Vector2(0.5f, 1f);

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.12f, 0.025f, 0.025f, 0.63f);
        background.raycastTarget = false;

        GameObject messageObject = new GameObject("Explanation", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform messageRect = (RectTransform)messageObject.transform;
        messageRect.SetParent(panelRect, false);
        messageRect.anchorMin = new Vector2(0f, 0f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageText = messageObject.GetComponent<TMP_Text>();
        if (ownerStats != null && ownerStats.textDamage != null) messageText.font = ownerStats.textDamage.font;
        else messageText.font = ownerHud.NoticeFont;
        messageText.text = Message;
        messageText.color = Color.white;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.enableAutoSizing = true;
        messageText.raycastTarget = false;

        GameObject rowRoot = new GameObject("Outstanding Requirements", typeof(RectTransform));
        rowsRoot = (RectTransform)rowRoot.transform;
        rowsRoot.SetParent(panelRect, false);
        rowsRoot.anchorMin = new Vector2(0f, 1f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.pivot = new Vector2(0.5f, 1f);
        rowsRoot.anchoredPosition = Vector2.zero;
        rowsRoot.sizeDelta = new Vector2(-24f * scaleRatio, 1f);
        RefreshLayout();
        panel.SetActive(false);
    }

    private bool Bind(PlayerController ownerPlayer, CheckpointRequirements ownerCheckpoint,
        GauntletRequirements ownerRequirements, GauntletRequirementsHud ownerHud)
    {
        bool changed = player != ownerPlayer || checkpointRequirements != ownerCheckpoint
            || requirements != ownerRequirements || hud != ownerHud;
        if (requirements != ownerRequirements)
        {
            if (requirements != null) requirements.Changed -= Refresh;
            requirements = ownerRequirements;
            if (requirements != null) requirements.Changed += Refresh;
        }

        player = ownerPlayer;
        checkpointRequirements = ownerCheckpoint;
        hud = ownerHud;
        return changed;
    }

    private void Update()
    {
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            RefreshLayout();
        if (player == null || !player.IsAlive || player.gameObject.scene != gameObject.scene)
        {
            Close();
            return;
        }

        bool suppressed = Time.timeScale <= 0f
            || (player.menu != null && player.menu.activeInHierarchy)
            || ResearchSurveyPrompt.IsOpen
            || MobilePrivacyControls.IsOpen
            || GauntletReplayPrompt.BlocksSettings;
        bool shouldShow = !suppressed && (lastState == CheckpointRequirements.GateState.Misconfigured
            || lastState == CheckpointRequirements.GateState.RequirementsRemain);
        if (panel.activeSelf != shouldShow) panel.SetActive(shouldShow);
    }

    private void RefreshLayout()
    {
        if (rootCanvas == null || panelRect == null) return;
        Rect safe = Screen.safeArea;
        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);
        float canvasScale = Mathf.Max(0.001f, rootCanvas.scaleFactor);
        scaleRatio = Mathf.Max(1f, safe.height) / 1080f / canvasScale;
        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        panelRect.anchorMin = new Vector2(0f, (safe.yMin + safe.height * 0.61f) / screenHeight);
        panelRect.anchorMax = new Vector2(1f, (safe.yMin + safe.height * 0.74f) / screenHeight);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        messageText.fontSizeMax = 54f * Mathf.Max(1f, safe.height) / 1080f / canvasScale;
        messageText.fontSizeMin = messageText.fontSizeMax * 0.75f;
        RectTransform messageRect = messageText.rectTransform;
        messageRect.anchorMin = new Vector2(safe.xMin / screenWidth, 0f);
        messageRect.anchorMax = new Vector2(safe.xMax / screenWidth, 1f);
        messageRect.offsetMin = new Vector2(12f, 6f);
        messageRect.offsetMax = new Vector2(-12f, -6f);
        rowsRoot.anchorMin = new Vector2(safe.xMin / screenWidth, 1f);
        rowsRoot.anchorMax = new Vector2(safe.xMax / screenWidth, 1f);
        rowsRoot.sizeDelta = new Vector2(-24f * scaleRatio, 1f);
        for (int i = 0; i < rows.Count; i++)
        {
            RectTransform rowRect = (RectTransform)rows[i].transform;
            rowRect.anchoredPosition = new Vector2(0f, -(148f + i * 68f) * scaleRatio);
            rowRect.sizeDelta = new Vector2(-12f * scaleRatio, 60f * scaleRatio);
        }
    }

    private void Refresh()
    {
        if (checkpointRequirements == null) return;
        CheckpointRequirements.GateState state = checkpointRequirements.Evaluate(requirements, remaining);
        lastState = state;
        hasState = true;
        if (state == CheckpointRequirements.GateState.Cleared)
        {
            if (panel != null) panel.SetActive(false);
            return;
        }

        RebuildProgress();
        SetRows();
        RefreshLayout();
        bool suppressed = Time.timeScale <= 0f
            || (player != null && player.menu != null && player.menu.activeInHierarchy)
            || ResearchSurveyPrompt.IsOpen
            || MobilePrivacyControls.IsOpen
            || GauntletReplayPrompt.BlocksSettings;
        panel.SetActive(!suppressed && state != CheckpointRequirements.GateState.WaitingForInitialization);
    }

    private void RebuildProgress()
    {
        progress.Clear();
        if (checkpointRequirements.Prerequisites == null || requirements == null) return;
        foreach (RequiredGauntletEnemy target in checkpointRequirements.Prerequisites)
        {
            if (target == null) continue;
            EnemyTypeProgress item = progress.Find(entry => entry.Type == target.EnemyType);
            if (item == null)
            {
                item = new EnemyTypeProgress { Type = target.EnemyType };
                progress.Add(item);
            }
            item.Total++;
            if (requirements.IsDefeated(target.VisitId)) item.Defeated++;
        }
        progress.RemoveAll(item => item.Defeated >= item.Total);
    }

    private void SetRows()
    {
        foreach (GauntletEnemyRow row in rows)
            if (row != null) Destroy(row.gameObject);
        rows.Clear();

        for (int i = 0; i < progress.Count; i++)
        {
            EnemyTypeProgress item = progress[i];
            GauntletEnemyRow row = hud.CreateNoticeRow(rowsRoot);
            if (row == null) continue;
            row.name = item.Type + " Outstanding Count";
            row.SetTextOnlyCount(item.Type, item.Defeated, item.Total);
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -(148f + i * 68f) * scaleRatio);
            rowRect.sizeDelta = new Vector2(-12f * scaleRatio, 60f * scaleRatio);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }
    }

    private void Close()
    {
        if (requirements != null) requirements.Changed -= Refresh;
        requirements = null;
        if (panel != null) panel.SetActive(false);
        if (active == this) active = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (requirements != null) requirements.Changed -= Refresh;
        if (active == this) active = null;
    }
}
