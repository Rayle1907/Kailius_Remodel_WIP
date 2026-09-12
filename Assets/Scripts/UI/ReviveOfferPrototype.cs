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
    private readonly System.Collections.Generic.List<GameObject> hiddenSceneCanvases = new System.Collections.Generic.List<GameObject>();

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
            CloseOverlay();
        }

        int price = GauntletRunTracker.Instance.CurrentOfferPrice;
        int balance = ResearchPlayerState.PremiumCurrencyBalance;
        bool canAfford = balance >= price;

        GameObject canvasObject = FindSceneCanvasRoot();
        if (canvasObject == null)
        {
            Debug.LogError("ReviveCanvas was not found in the active scene.");
            return;
        }

        HideOtherSceneCanvases(canvasObject);
        canvasObject.SetActive(true);
        CanvasScaler sceneScaler = canvasObject.GetComponent<CanvasScaler>();
        if (sceneScaler != null)
        {
            sceneScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sceneScaler.referenceResolution = new Vector2(1920f, 1080f);
            sceneScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sceneScaler.matchWidthOrHeight = 0.5f;
        }
        Transform safeArea = FindChild(canvasObject.transform, "SafeArea");
        if (safeArea != null)
        {
            safeArea.gameObject.SetActive(true);
        }

        overlay = canvasObject;

        Transform panelTransform = FindChild(canvasObject.transform, "Panel");
        if (panelTransform == null)
        {
            Debug.LogError("ReviveCanvas is missing a child named Panel.");
            canvasObject.SetActive(false);
            overlay = null;
            return;
        }

        Image panel = panelTransform.GetComponent<Image>();
        if (panel == null)
        {
            panel = panelTransform.gameObject.AddComponent<Image>();
        }

        RemoveGeneratedChildren(panel.transform);

        TextMeshProUGUI heading = CreateText(panel.transform, "ReviveHeading", "REVIVE?", 46f, TextAlignmentOptions.Center);
        AnchorTop(heading.rectTransform, 35f, 70f, 1000f);

        TextMeshProUGUI explanation = CreateText(panel.transform, "Explanation", "Continue this gauntlet from the last checkpoint.", 20f, TextAlignmentOptions.Center);
        AnchorTop(explanation.rectTransform, 140f, 90f, 1000f);

        TextMeshProUGUI cost = CreateText(panel.transform, "Cost", $"Cost: {price} Embers\nYou have: {balance} Embers", 28f, TextAlignmentOptions.Center);
        AnchorTop(cost.rectTransform, 260f, 110f, 1000f);

        affordabilityText = CreateText(panel.transform, "Affordability", canAfford ? "" : "Not enough Embers", 22f, TextAlignmentOptions.Center);
        affordabilityText.color = new Color(1f, 0.72f, 0.25f);
        AnchorTop(affordabilityText.rectTransform, 385f, 45f, 1000f);

        reviveButton = CreateButton(panel.transform, "Revive", "REVIVE", new Vector2(-260f, -250f), 420f, AcceptRevive);
        reviveButton.interactable = canAfford;
        CreateButton(panel.transform, "Restart", "RESTART", new Vector2(260f, -250f), 420f, DeclineRevive);

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
            overlay.SetActive(false);
            overlay = null;
        }

        RestoreOtherSceneCanvases();
    }

    private void HideOtherSceneCanvases(GameObject reviveCanvas)
    {
        hiddenSceneCanvases.Clear();

        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            GameObject canvasObject = canvas.gameObject;
            if (canvasObject == reviveCanvas || !canvasObject.scene.IsValid() || !canvasObject.activeSelf)
            {
                continue;
            }

            hiddenSceneCanvases.Add(canvasObject);
            canvasObject.SetActive(false);
        }
    }

    private void RestoreOtherSceneCanvases()
    {
        foreach (GameObject canvasObject in hiddenSceneCanvases)
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(true);
            }
        }

        hiddenSceneCanvases.Clear();
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject objectRoot = new GameObject(name);
        objectRoot.transform.SetParent(parent, false);
        Image image = objectRoot.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static GameObject FindSceneCanvasRoot()
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            if (canvas.name == "ReviveCanvas" && canvas.gameObject.scene.IsValid())
            {
                return canvas.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void RemoveGeneratedChildren(Transform panel)
    {
        string[] generatedNames =
        {
            "ReviveHeading",
            "Explanation",
            "Cost",
            "Affordability",
            "Revive",
            "Restart"
        };

        foreach (string generatedName in generatedNames)
        {
            Transform child = panel.Find(generatedName);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
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
        label.textWrappingMode = TextWrappingModes.Normal;
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

    private static void AnchorTop(RectTransform rect, float y, float height, float width)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, -y);
    }
}
