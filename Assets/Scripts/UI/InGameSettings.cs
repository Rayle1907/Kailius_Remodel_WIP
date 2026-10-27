using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class InGameSettings : MonoBehaviour
{
    private MoveByTouch movement;
    private GameObject menu;
    private Slider volume;
    private Button resume;
    private bool isOpen;
    private bool transitioning;
    private LayeredButton abandonTemplate;
    private GauntletReplayPrompt abandonPrompt;
    private bool statsWereActive;
    private float previousTimeScale;

    public void Initialize(MoveByTouch owner)
    {
        movement = owner;
        menu = owner.menu;
        CanvasScaler scaler = menu.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Transform panel = menu.transform.Find("Menu");
        Position(panel, menu.transform, 0f, new Vector2(320f, 340f));
        resume = panel.Find("BTNReanudar").GetComponent<Button>();
        Button quit = panel.Find("BTNSalir").GetComponent<Button>();
        Button world = panel.Find("BTNOpciones").GetComponent<Button>();
        Transform options = menu.transform.Find("Opciones");
        volume = options.GetComponentInChildren<Slider>(true);
        Transform volumeLabel = volume.transform.Find("Volumen");
        menu.SetActive(true);
        panel.gameObject.SetActive(true);
        options.gameObject.SetActive(true);

        Bind(resume, Resume, "Resume");
        abandonTemplate = world as LayeredButton;
        Bind(world, ReturnToOpenWorld, "Abandon run");
        Bind(quit, ReturnToMainMenu, "Quit");
        quit.gameObject.SetActive(true);
        world.gameObject.SetActive(GauntletHealthDropRules.IsGauntletScene(gameObject.scene.name));
        Position(resume.transform, panel, 140f, new Vector2(320f, 60f));
        Position(world.transform, panel, 65f, new Vector2(320f, 60f));
        Position(quit.transform, panel, -10f, new Vector2(320f, 60f));
        Position(volumeLabel, panel, -85f, new Vector2(300f, 52f));
        TMP_Text volumeText = volumeLabel.GetComponent<TMP_Text>();
        TMP_Text buttonText = resume.GetComponentInChildren<TMP_Text>(true);
        volumeText.font = buttonText.font;
        volumeText.enableAutoSizing = buttonText.enableAutoSizing;
        volumeText.fontSizeMin = buttonText.fontSizeMin;
        volumeText.fontSizeMax = buttonText.fontSizeMax;
        volumeText.fontSize = buttonText.fontSizeMax;
        volumeText.margin = Vector4.zero;
        volumeText.alignment = TextAlignmentOptions.Center;
        Position(volume.transform, panel, -130f, new Vector2(320f, 30f));
        options.gameObject.SetActive(false);
        panel.gameObject.SetActive(true);

        volume.onValueChanged = new Slider.SliderEvent();
        volume.minValue = 0f;
        volume.maxValue = 1f;
        volume.wholeNumbers = false;
        volume.SetValueWithoutNotify(VolumenController.Volume);
        volume.onValueChanged.AddListener(VolumenController.ApplyVolume);

        foreach (Button button in owner.ControlesMoviles.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "ButtonSettings") Bind(button, Toggle);
        }

        menu.GetComponent<Canvas>().sortingOrder = 100;
        menu.transform.localScale = Vector3.one;
        GameObject blocker = new GameObject("Settings Input Blocker", typeof(RectTransform), typeof(Image));
        RectTransform blockerRect = (RectTransform)blocker.transform;
        blockerRect.SetParent(menu.transform, false);
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        blockerRect.SetSiblingIndex(1);
        panel.SetAsLastSibling();
        menu.SetActive(false);
    }

    private void Update()
    {
        if (ResearchSurveyPrompt.IsOpen || MobilePrivacyControls.IsOpen) return;
        if (!GauntletReplayPrompt.BlocksSettings && menu != null && Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    public void Toggle()
    {
        if (ResearchSurveyPrompt.IsOpen || MobilePrivacyControls.IsOpen) return;
        if (GauntletReplayPrompt.BlocksSettings) return;
        if (isOpen)
        {
            Resume();
            return;
        }
        if (Time.timeScale <= 0f || GetComponent<Stats>().health <= 0) return;
        previousTimeScale = Time.timeScale;
        statsWereActive = movement.stats.activeSelf;
        movement.stats.SetActive(false);
        volume.SetValueWithoutNotify(VolumenController.Volume);
        isOpen = true;
        menu.SetActive(true);
        Time.timeScale = 0f;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resume.gameObject);
    }

    public void Resume()
    {
        if (!isOpen || abandonPrompt != null) return;
        isOpen = false;
        menu.SetActive(false);
        movement.stats.SetActive(statsWereActive);
        Time.timeScale = previousTimeScale;
        PlayerPrefs.Save();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void ReturnToOpenWorld()
    {
        if (!isOpen || transitioning || abandonPrompt != null) return;
        if (abandonTemplate == null)
        {
            foreach (ReviveButtonAssets assets in FindObjectsByType<ReviveButtonAssets>(FindObjectsInactive.Include))
            {
                if (assets.gameObject.scene != gameObject.scene || assets.ButtonPrefab == null) continue;
                abandonTemplate = assets.ButtonPrefab;
                break;
            }
        }
        abandonPrompt = GauntletReplayPrompt.ShowConfirmation(abandonTemplate,
            "Return to the open world. This attempt's progress will reset. Ember prices for this gauntlet will remain.",
            "Abandon run", "Keep playing",
            () => { RestoreSettingsAfterPrompt(); Leave("Scene_1", "abandoned"); },
            RestoreSettingsAfterPrompt, heading: "Abandon?");
        if (abandonPrompt != null) menu.transform.Find("Menu").gameObject.SetActive(false);
    }

    private void RestoreSettingsAfterPrompt()
    {
        abandonPrompt = null;
        Transform panel = menu.transform.Find("Menu");
        panel.gameObject.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(panel.Find("BTNOpciones").gameObject);
    }

    public void ReturnToMainMenu()
    {
        if (transitioning || ResearchSurveyPrompt.IsOpen) return;
        if (Application.CanStreamedLevelBeLoaded("Menu") && GauntletRunTracker.Instance != null
            && GauntletRunTracker.Instance.IsCurrentSceneGauntlet)
        {
            GauntletRunTracker.Instance.RecordPostDecisionOutcome("returned_to_menu");
            GauntletRunTracker.Instance.EndCurrentRun("returned_to_menu");
        }
        if (ResearchSurvey.TryShowInvitation(() => Leave("Menu", "returned_to_menu"),
            resume.GetComponentInChildren<TMP_Text>(true).font)) return;
        Leave("Menu", "returned_to_menu");
    }

    private void Leave(string destination, string reason)
    {
        if (transitioning) return;
        if (!Application.CanStreamedLevelBeLoaded(destination))
        {
            Debug.LogError("Settings destination is unavailable: " + destination, this);
            return;
        }
        transitioning = true;
        if (GauntletRunTracker.Instance != null && GauntletRunTracker.Instance.IsCurrentSceneGauntlet)
        {
            GauntletRunTracker.Instance.RecordPostDecisionOutcome(reason);
            GauntletRunTracker.Instance.EndCurrentRun(reason);
        }
        isOpen = false;
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene(destination);
    }

    private void OnDisable()
    {
        if (abandonPrompt != null) abandonPrompt.Dismiss();
        if (isOpen) Resume();
    }

    private static void Bind(Button button, UnityAction action, string label = null)
    {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        if (label == null) return;
        if (button is LayeredButton layered) layered.SetLabel(label);
        else
        {
            button.transition = Selectable.Transition.None;
            button.targetGraphic.color = Color.clear;
            button.targetGraphic.canvasRenderer.SetColor(Color.white);
            TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
            buttonLabel.text = label;
            buttonLabel.color = Color.white;
            button.targetGraphic = buttonLabel;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.82f, 0.35f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.8f, 0.65f, 0.28f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 30f;
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.Center;
        text.margin = Vector4.zero;
        text.rectTransform.localScale = Vector3.one;
        text.rectTransform.localRotation = Quaternion.identity;
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(10f, 4f);
        text.rectTransform.offsetMax = new Vector2(-10f, -4f);
    }

    private static void Position(Transform control, Transform parent, float y, Vector2 size)
    {
        RectTransform rect = (RectTransform)control;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
    }
}
