using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        ReviveButtonAssets buttonAssets = canvasObject.GetComponent<ReviveButtonAssets>();
        if (buttonAssets == null || buttonAssets.ButtonPrefab == null || buttonAssets.PressMeButtonPrefab == null || buttonAssets.UIFont == null)
        {
            Debug.LogError($"ReviveCanvas configuration is incomplete: component={buttonAssets != null}, regular={buttonAssets != null && buttonAssets.ButtonPrefab != null}, PressMe={buttonAssets != null && buttonAssets.PressMeButtonPrefab != null}, font={buttonAssets != null && buttonAssets.UIFont != null}.");
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
            RestoreOtherSceneCanvases();
            return;
        }

        Image panel = panelTransform.GetComponent<Image>();
        if (panel == null)
        {
            panel = panelTransform.gameObject.AddComponent<Image>();
        }

        RemoveGeneratedChildren(panel.transform);

        TextMeshProUGUI heading = CreateText(panel.transform, "ReviveHeading", "REVIVE?", 46f, TextAlignmentOptions.Center, buttonAssets.UIFont);
        AnchorTop(heading.rectTransform, 350f, -110f, 1000f);

        TextMeshProUGUI explanation = CreateText(panel.transform, "Explanation", "Continue this gauntlet from the last checkpoint.", 20f, TextAlignmentOptions.Center, buttonAssets.UIFont);
        AnchorTop(explanation.rectTransform, 425f, -130f, 1000f);

        TextMeshProUGUI cost = CreateText(panel.transform, "Cost", $"Cost: {price} Embers\nYou have: {balance} Embers", 28f, TextAlignmentOptions.Center, buttonAssets.UIFont);
        AnchorTop(cost.rectTransform, 475f, -150f, 1000f);

        affordabilityText = CreateText(panel.transform, "Affordability", canAfford ? "" : "Not enough Embers", 22f, TextAlignmentOptions.Center, buttonAssets.UIFont);
        affordabilityText.color = new Color(1f, 0.72f, 0.25f);
        AnchorTop(affordabilityText.rectTransform, 385f, 45f, 1000f);

        reviveButton = CreateButton(buttonAssets.PressMeButtonPrefab, panel.transform, "Revive", "REVIVE", new Vector2(-260f, -190f), 420f, AcceptRevive);
        reviveButton.interactable = canAfford;
        Button restartButton = CreateButton(buttonAssets.ButtonPrefab, panel.transform, "Restart", "RESTART", new Vector2(260f, -190f), 420f, DeclineRevive);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(canAfford ? reviveButton.gameObject : restartButton.gameObject);
        }

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
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        GameObject objectRoot = new GameObject(name);
        objectRoot.transform.SetParent(parent, false);
        TextMeshProUGUI label = objectRoot.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSharedMaterial = font.material;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private static Button CreateButton(LayeredButton prefab, Transform parent, string name, string labelText, Vector2 position, float width, UnityEngine.Events.UnityAction action)
    {
        LayeredButton button = Instantiate(prefab, parent, false);
        button.name = name;
        RectTransform rect = (RectTransform)button.transform;
        SetSize(rect, width, 64f);
        rect.anchoredPosition = position;
        button.SetLabel(labelText);
        button.onClick.AddListener(action);
        return button;
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
