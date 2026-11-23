using UnityEngine;
using UnityEngine.UI;

public sealed class GauntletGateNotice : MonoBehaviour
{
    private GameplayOnboardingBannerView banner;
    private float hideAt;

    public static void Show(string message)
    {
        GauntletGateNotice notice = FindAnyObjectByType<GauntletGateNotice>();
        if (notice == null)
        {
            GameObject root = new GameObject("Gauntlet Gate Notice", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            MobileDialogLayout.Configure(root);

            PlayerController player = FindAnyObjectByType<PlayerController>();
            Stats stats = player != null ? player.GetComponent<Stats>() : null;
            notice = root.AddComponent<GauntletGateNotice>();
            notice.banner = GameplayOnboardingBannerView.Create(canvas,
                stats != null && stats.textDamage != null ? stats.textDamage.font : null);
        }

        notice.banner.Show(message);
        notice.hideAt = Time.unscaledTime + 5f;
    }

    private void Update()
    {
        if (Time.unscaledTime >= hideAt)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        banner.RefreshLayoutIfNeeded();
    }
}
