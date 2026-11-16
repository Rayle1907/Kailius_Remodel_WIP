using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GauntletGateNotice
{
    public static void Show(Scene sourceScene, string message,
        IReadOnlyList<KeyValuePair<string, int>> enemyRows = null)
    {
        if (!TryGetSceneCanvasAndFont(sourceScene, out Canvas canvas, out TMP_FontAsset font,
            out GauntletRequirementsHud hud)) return;
        string requestKey = "GauntletGateNotice:" + sourceScene.handle;
        GameplayBannerToken token = GameplayBannerPresenter.Show(canvas, font, message, 5f, requestKey);
        if (token == null) return;
        if (hud != null) GameplayBannerPresenter.SetEnemyRows(token, hud, enemyRows);
        else GameplayBannerPresenter.ClearEnemyRows(token);
    }

    private static bool TryGetSceneCanvasAndFont(Scene scene, out Canvas canvas, out TMP_FontAsset font,
        out GauntletRequirementsHud requirementsHud)
    {
        canvas = null;
        font = null;
        requirementsHud = null;
        if (!scene.IsValid() || !scene.isLoaded) return false;

        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            FuryFeedback[] feedback = root.GetComponentsInChildren<FuryFeedback>(true);
            foreach (FuryFeedback playerFeedback in feedback)
            {
                Stats stats = playerFeedback.GetComponent<Stats>();
                if (stats == null || stats.textDamage == null || stats.textDamage.font == null) continue;
                font = stats.textDamage.font;
                break;
            }
            if (font != null) break;
        }

        foreach (GameObject root in roots)
        {
            GauntletRequirementsHud[] huds = root.GetComponentsInChildren<GauntletRequirementsHud>(true);
            foreach (GauntletRequirementsHud hud in huds)
            {
                if (hud == null || hud.gameObject.scene != scene || hud.RootCanvas == null
                    || !hud.RootCanvas.isActiveAndEnabled) continue;
                canvas = hud.RootCanvas;
                requirementsHud = hud;
                if (font == null) font = hud.NoticeFont;
                break;
            }
            if (canvas != null) break;
        }

        if (canvas == null)
        {
            foreach (GameObject root in roots)
            {
                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                foreach (Canvas candidate in canvases)
                {
                    if (candidate == null || !candidate.isActiveAndEnabled) continue;
                    canvas = candidate.rootCanvas;
                    break;
                }
                if (canvas != null) break;
            }
        }

        if (canvas == null) return false;
        if (font == null)
        {
            TextMeshProUGUI referenceText = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
            if (referenceText != null) font = referenceText.font;
        }
        return true;
    }
}
