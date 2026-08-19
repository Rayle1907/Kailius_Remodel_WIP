using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Temporary runtime-built revive offer UI. Replace the visuals with a prefab once the layout is approved.
/// </summary>
public sealed class ReviveOfferPrototype : MonoBehaviour
{
    private const float DecisionTimeoutSeconds = 10f;

    private static ReviveOfferPrototype instance;
    private Stats playerStats;
    private GameObject overlay;
    private Button reviveButton;
    private TextMeshProUGUI affordabilityText;
    private Coroutine timeoutRoutine;

    public static bool TryShow(Stats stats)
    {
        if (stats == null || GauntletRunTracker.Instance == null)
        {
            return false;
        }

        string offerId = GauntletRunTracker.Instance.RecordOfferShown();
        if (string.IsNullOrEmpty(offerId))
        {
            return false;
        }

        if (instance == null)
        {
            GameObject host = new GameObject("Revive Offer Prototype");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ReviveOfferPrototype>();
        }

        instance.Show(stats);
        return true;
    }

    private void Show(Stats stats)
    {
        playerStats = stats;
        if (overlay != null)
        {
            Destroy(overlay);
        }

        int price = GauntletRunTracker.Instance.CurrentOfferPrice;
        int balance = ResearchPlayerState.PremiumCurrencyBalance;
        bool canAfford = balance >= price;

        GameObject canvasObject = new GameObject("Revive Offer Canvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        overlay = canvasObject;

        Image dimmer = CreateImage(canvasObject.transform, "Dimmer", new Color(0f, 0f, 0f, 0.68f));
        Stretch(dimmer.rectTransform);

        Image panel = CreateImage(canvasObject.transform, "Revive Panel", new Color(0.22f, 0.22f, 0.22f, 1f));
        SetSize(panel.rectTransform, 620f, 430f);

        TextMeshProUGUI heading = CreateText(panel.transform, "Heading", "REVIVE?", 46f, TextAlignmentOptions.Center);
        AnchorTop(heading.rectTransform, 24f, 54f);

        TextMeshProUGUI explanation = CreateText(panel.transform, "Explanation", "Continue this gauntlet from the last checkpoint.", 20f, TextAlignmentOptions.Center);
        AnchorTop(explanation.rectTransform, 94f, 70f);

        TextMeshProUGUI cost = CreateText(panel.transform, "Cost", $"Cost: {price} Embers\nYou have: {balance} Embers", 28f, TextAlignmentOptions.Center);
        AnchorTop(cost.rectTransform, 170f, 90f);

        affordabilityText = CreateText(panel.transform, "Affordability", canAfford ? "" : "Not enough Embers", 22f, TextAlignmentOptions.Center);
        affordabilityText.color = new Color(1f, 0.72f, 0.25f);
        AnchorTop(affordabilityText.rectTransform, 255f, 36f);

        reviveButton = CreateButton(panel.transform, "Revive", "REVIVE", new Vector2(-130f, -145f), 220f, AcceptRevive);
        reviveButton.interactable = canAfford;
        CreateButton(panel.transform, "Restart", "RESTART", new Vector2(130f, -145f), 220f, DeclineRevive);

        timeoutRoutine = StartCoroutine(ResolveTimeout());
    }

    private IEnumerator ResolveTimeout()
    {
        yield return new WaitForSecondsRealtime(DecisionTimeoutSeconds);
        if (overlay != null)
        {
            ResolveAndRestart("ignored");
        }
    }

    private void AcceptRevive()
    {
        if (reviveButton == null || !reviveButton.interactable)
        {
            return;
        }

        if (!GauntletRunTracker.Instance.ResolveOffer("accepted"))
        {
            return;
        }

        CloseOverlay();
        // The offer freezes gameplay; accepting it must unfreeze the player.
        Time.timeScale = 1f;
        playerStats.CompleteRevive();
        GauntletRunTracker.Instance.RecordPostDecisionOutcome("died_again");
    }

    private void DeclineRevive()
    {
        ResolveAndRestart("declined");
    }

    private void ResolveAndRestart(string response)
    {
        if (GauntletRunTracker.Instance != null)
        {
            GauntletRunTracker.Instance.ResolveOffer(response);
            GauntletRunTracker.Instance.RecordPostDecisionOutcome("run_restarted");
            GauntletRunTracker.Instance.SetNextRunStartReason("retry_button");
            GauntletRunTracker.Instance.RestartCurrentRun();
        }

        CloseOverlay();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void CloseOverlay()
    {
        if (timeoutRoutine != null)
        {
            StopCoroutine(timeoutRoutine);
            timeoutRoutine = null;
        }

        if (overlay != null)
        {
            Destroy(overlay);
            overlay = null;
        }
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject objectRoot = new GameObject(name);
        objectRoot.transform.SetParent(parent, false);
        Image image = objectRoot.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, TextAlignmentOptions alignment)
    {
        GameObject objectRoot = new GameObject(name);
        objectRoot.transform.SetParent(parent, false);
        TextMeshProUGUI label = objectRoot.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.enableWordWrapping = true;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, Vector2 position, float width, UnityEngine.Events.UnityAction action)
    {
        Image image = CreateImage(parent, name, new Color(0.34f, 0.34f, 0.34f, 1f));
        SetSize(image.rectTransform, width, 64f);
        image.rectTransform.anchoredPosition = position;
        Button button = image.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        TextMeshProUGUI label = CreateText(image.transform, "Label", labelText, 24f, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetSize(RectTransform rect, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;
    }

    private static void AnchorTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(560f, height);
        rect.anchoredPosition = new Vector2(0f, -y);
    }
}
