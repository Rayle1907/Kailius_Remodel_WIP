using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Development-only button for opening the revive offer without dying.
/// </summary>
public sealed class ReviveOfferDebugButton : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Revive/Reset Onboarding")]
    public static void ResetOnboarding()
    {
        PlayerPrefs.DeleteKey("research_revive_onboarding_completed");
        PlayerPrefs.Save();
        Debug.Log("Revive onboarding reset. It will appear on the next revive offer. If an offer is already open, close it first. Currency and research assignment are unchanged.");
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (FindAnyObjectByType<ReviveOfferDebugButton>() != null)
        {
            return;
        }

        GameObject host = new GameObject("Revive Offer Debug Button");
        DontDestroyOnLoad(host);
        host.AddComponent<ReviveOfferDebugButton>();
#endif
    }

    private void Start()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
        }

        GameObject buttonObject = new GameObject("Revive");
        buttonObject.transform.SetParent(transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -20f);
        rect.sizeDelta = new Vector2(160f, 40f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.55f, 0.9f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(OpenReviveOffer);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.text = "REVIVE";
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 14;
        label.color = Color.white;

        GameObject furyObject = Instantiate(buttonObject, transform, false);
        furyObject.name = "Full Fury";
        furyObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(20f, -70f);
        Button furyButton = furyObject.GetComponent<Button>();
        // The clone must only charge Fury, never invoke the REVIVE callback.
        furyButton.onClick = new Button.ButtonClickedEvent();
        furyButton.onClick.AddListener(FillPlayerFury);
        Text furyLabel = furyObject.GetComponentInChildren<Text>();
        furyLabel.text = "FULL FURY";
        furyLabel.raycastTarget = false;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool FillFury(Stats playerStats)
    {
        if (playerStats == null || playerStats.getHealth() <= 0) return false;
        playerStats.takePower(Stats.FuryChargeLimit - playerStats.getPower());
        return playerStats.IsFuryReady;
    }

    private static void FillPlayerFury()
    {
        // During a scene transition there may briefly be more than one player.
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Stats fallback = null;
        foreach (Stats playerStats in FindObjectsByType<Stats>())
        {
            if (playerStats.getHealth() <= 0) continue;
            if (playerStats.gameObject.scene == activeScene)
            {
                FillFury(playerStats);
                return;
            }
            if (fallback == null) fallback = playerStats;
        }
        FillFury(fallback);
    }
#endif

    private static void OpenReviveOffer()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Stats stats = FindAnyObjectByType<Stats>();
        if (stats == null)
        {
            Debug.LogWarning("Revive debug button could not find a Stats component in the active scene.");
            return;
        }

        if (!ReviveOfferPrototype.TryShow(stats))
        {
            Debug.LogWarning("Revive debug button could not open the revive offer. Check GauntletRunTracker and ReviveCanvas.");
        }
#endif
    }
}
