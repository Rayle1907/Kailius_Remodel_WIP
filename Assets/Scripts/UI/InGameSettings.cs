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
    private bool statsWereActive;
    private float previousTimeScale;

    public void Initialize(MoveByTouch owner)
    {
        movement = owner;
        menu = owner.menu;
        Transform panel = menu.transform.Find("Menu");
        resume = panel.Find("BTNReanudar").GetComponent<Button>();
        Button quit = panel.Find("BTNSalir").GetComponent<Button>();
        Button world = panel.Find("BTNOpciones").GetComponent<Button>();
        Transform options = menu.transform.Find("Opciones");
        volume = options.GetComponentInChildren<Slider>(true);
        Transform volumeLabel = volume.transform.Find("Volumen");

        Bind(resume, Resume, "Resume");
        Bind(world, ReturnToOpenWorld, "Return to Open World");
        Bind(quit, ReturnToMainMenu, "Quit");
        quit.gameObject.SetActive(true);
        world.gameObject.SetActive(GauntletHealthDropRules.IsGauntletScene(gameObject.scene.name));
        Position(resume.transform, panel, 140f, new Vector2(320f, 60f));
        Position(world.transform, panel, 65f, new Vector2(320f, 60f));
        Position(quit.transform, panel, -10f, new Vector2(320f, 60f));
        Position(volumeLabel, panel, -85f, new Vector2(320f, 40f));
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
        if (menu != null && Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    public void Toggle()
    {
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
        if (!isOpen) return;
        isOpen = false;
        menu.SetActive(false);
        movement.stats.SetActive(statsWereActive);
        Time.timeScale = previousTimeScale;
        PlayerPrefs.Save();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void ReturnToOpenWorld()
    {
        Leave("Scene_1", "returned_to_outer_world");
    }

    public void ReturnToMainMenu()
    {
        Leave("Menu", "returned_to_menu");
    }

    private void Leave(string destination, string reason)
    {
        if (!Application.CanStreamedLevelBeLoaded(destination))
        {
            Debug.LogError("Settings destination is unavailable: " + destination, this);
            return;
        }
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
        if (isOpen) Resume();
    }

    private static void Bind(Button button, UnityAction action, string label = null)
    {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        if (label == null) return;
        if (button is LayeredButton layered) layered.SetLabel(label);
        else button.GetComponentInChildren<TMP_Text>(true).text = label;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 30f;
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
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
    }
}
