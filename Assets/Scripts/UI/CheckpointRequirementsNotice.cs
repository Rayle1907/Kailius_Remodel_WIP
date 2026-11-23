using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class CheckpointRequirementsNotice : MonoBehaviour
{
    private const string Message = "Defeat all required enemies behind this checkpoint to activate it.";
    private static CheckpointRequirementsNotice active;

    private readonly List<CheckpointRequirements.Shortfall> remaining = new List<CheckpointRequirements.Shortfall>();
    private readonly List<KeyValuePair<string, int>> rowValues = new List<KeyValuePair<string, int>>();
    private PlayerController player;
    private CheckpointRequirements checkpointRequirements;
    private GauntletRequirements requirements;
    private GauntletRequirementsHud hud;
    private Canvas rootCanvas;
    private TMP_FontAsset requestFont;
    private GameplayBannerToken bannerToken;
    private object requestKey;
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

        if (active == null || active.hud == null || active.hud.RootCanvas != hud.RootCanvas
            || active.gameObject.scene != player.gameObject.scene)
        {
            if (active != null) active.Close();
            GameObject root = new GameObject("Checkpoint Requirements Notice");
            active = root.AddComponent<CheckpointRequirementsNotice>();
            active.Build(hud, stats);
        }

        bool contextChanged = active.Bind(player, checkpointRequirements, requirements, hud);
        if (contextChanged || !active.hasState || active.lastState == CheckpointRequirements.GateState.WaitingForInitialization)
        {
            if (contextChanged) active.ResetPresentationForContext();
            active.Refresh();
        }
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
        rootCanvas = ownerHud.RootCanvas;
        if (ownerStats != null && ownerStats.textDamage != null) requestFont = ownerStats.textDamage.font;
        if (requestFont == null) requestFont = ownerHud.NoticeFont;
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
        if (changed) requestKey = new object();
        return changed;
    }

    private void Update()
    {
        if (bannerToken != null && !GameplayBannerPresenter.IsOwner(bannerToken))
        {
            Close();
            return;
        }
        if (player == null || !player.IsAlive || player.gameObject.scene != gameObject.scene)
        {
            Close();
            return;
        }

        if (bannerToken == null) return;
        GameplayBannerPresenter.RefreshLayoutIfNeeded(bannerToken);
        bool shouldShow = !IsSuppressed()
            && (lastState == CheckpointRequirements.GateState.Misconfigured
                || lastState == CheckpointRequirements.GateState.RequirementsRemain);
        GameplayBannerPresenter.SetVisible(bannerToken, shouldShow);
    }

    private void Refresh()
    {
        if (checkpointRequirements == null) return;
        if (bannerToken != null && !GameplayBannerPresenter.IsOwner(bannerToken))
        {
            Close();
            return;
        }

        CheckpointRequirements.GateState state = checkpointRequirements.Evaluate(requirements, remaining);
        lastState = state;
        hasState = true;
        if (state == CheckpointRequirements.GateState.Cleared)
        {
            Close();
            return;
        }
        if (state == CheckpointRequirements.GateState.WaitingForInitialization)
        {
            if (bannerToken != null) GameplayBannerPresenter.ClearEnemyRows(bannerToken);
            if (bannerToken != null) GameplayBannerPresenter.SetVisible(bannerToken, false);
            return;
        }

        if (bannerToken == null)
        {
            bannerToken = GameplayBannerPresenter.Show(rootCanvas, requestFont, Message, 0f, requestKey);
            if (bannerToken == null) return;
        }

        SetRows();
        GameplayBannerPresenter.SetVisible(bannerToken, !IsSuppressed());
    }

    private bool IsSuppressed()
    {
        return Time.timeScale <= 0f
            || (player != null && player.menu != null && player.menu.activeInHierarchy)
            || ResearchSurveyPrompt.IsOpen
            || MobilePrivacyControls.IsOpen
            || GauntletReplayPrompt.BlocksSettings;
    }

    private void SetRows()
    {
        rowValues.Clear();
        for (int i = 0; i < remaining.Count; i++)
        {
            CheckpointRequirements.Shortfall item = remaining[i];
            rowValues.Add(new KeyValuePair<string, int>(item.TypeId, item.Count));
        }
        GameplayBannerPresenter.SetEnemyRows(bannerToken, hud, rowValues);
    }

    private void ResetPresentationForContext()
    {
        if (bannerToken != null) GameplayBannerPresenter.Dismiss(bannerToken);
        bannerToken = null;
        rowValues.Clear();
    }

    private void Close()
    {
        if (requirements != null) requirements.Changed -= Refresh;
        requirements = null;
        if (bannerToken != null) GameplayBannerPresenter.Dismiss(bannerToken);
        bannerToken = null;
        rowValues.Clear();
        if (active == this) active = null;
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void OnDestroy()
    {
        if (requirements != null) requirements.Changed -= Refresh;
        if (bannerToken != null) GameplayBannerPresenter.Dismiss(bannerToken);
        if (active == this) active = null;
    }
}
