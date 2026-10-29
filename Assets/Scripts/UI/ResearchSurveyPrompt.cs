using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ResearchSurveyPrompt : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.11f, 1f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.18f, 0.24f, 1f);
    private static readonly Color AnswerColor = new Color(0.48f, 0.32f, 0.08f, 1f);
    private static readonly string[] ResponseLabels =
    {
        "-3\nStrongly\ndisagree", "-2\nDisagree", "-1\nSlightly\ndisagree",
        "0\nNeither agree\nnor disagree", "+1\nSlightly\nagree", "+2\nAgree", "+3\nStrongly\nagree"
    };

    private static ResearchSurveyPrompt active;
    private ResearchSurveyResponse response;
    private Action onQuit;
    private TMP_FontAsset font;
    private Transform content;
    private TMP_Text heading;
    private TMP_Text question;
    private TMP_Text status;
    private Button back;
    private Button next;
    private Button clear;
    private Button submit;
    private readonly Button[] choices = new Button[7];
    private readonly List<Button> buttons = new List<Button>();
    private GameObject previousSelection;
    private float previousTimeScale;
    private int questionIndex;
    private bool questionnaire;
    private bool closed;
    private GameObject leaveConfirmation;
    private Action cancelLeave;

    public static bool IsOpen => active != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => active = null;

    private void Update()
    {
        if (closed || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (leaveConfirmation != null) cancelLeave?.Invoke();
        else if (questionnaire) ShowLeaveConfirmation();
        else ResolveInvitation(false);
    }

    public static bool Show(ResearchSurveyResponse response, Action onQuit, TMP_FontAsset font)
    {
        if (IsOpen || EventSystem.current == null || font == null) return false;
        GameObject root = new GameObject("Player Experience Survey", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        ResearchSurveyPrompt prompt = root.AddComponent<ResearchSurveyPrompt>();
        active = prompt;
        prompt.response = response;
        prompt.onQuit = onQuit;
        prompt.font = font;
        prompt.previousTimeScale = Time.timeScale;
        prompt.previousSelection = EventSystem.current.currentSelectedGameObject;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        MobileDialogLayout.Configure(root);
        GameObject blocker = new GameObject("Survey Input Blocker", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(root.transform, false);
        blocker.GetComponent<Image>().color = PanelColor;
        blocker.AddComponent<MobileSafeAreaFullScreen>();
        Stretch((RectTransform)blocker.transform, Vector2.zero, Vector2.one);
        RectTransform safeArea = root.AddComponent<MobileSafeArea>().ContentRoot;
        RectTransform panel = (RectTransform)new GameObject("Survey Content", typeof(RectTransform)).transform;
        panel.SetParent(safeArea, false);
        MobileDialogLayout.Panel(panel, true);
        prompt.content = panel;
        Time.timeScale = 0f;
        prompt.ShowInvitation();
        return true;
    }

    private void ShowInvitation()
    {
        Text("Survey invitation", "Would you like to answer a short survey?",
            new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.94f), MobileDialogLayout.HeadingSize);
        Text("Survey description", "15 questions about your experience. About 3-5 minutes.\nYou may leave questions unanswered and submit the answers you have given.",
            new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.69f), MobileDialogLayout.BodySize);
        Button accept = Button("Answer survey", new Vector2(0.08f, 0.12f), new Vector2(0.48f, 0.29f), () => ResolveInvitation(true));
        Button("No thanks", new Vector2(0.52f, 0.12f), new Vector2(0.92f, 0.29f), () => ResolveInvitation(false));
        ConfigureNavigation(buttons);
        EventSystem.current.SetSelectedGameObject(accept.gameObject);
    }

    private void ResolveInvitation(bool accepted)
    {
        if (closed || questionnaire) return;
        ResearchAnalytics.RecordSurveyInvitationResolved(new SurveyInvitationResolvedEvent(response, accepted));
        if (!accepted)
        {
            Close();
            return;
        }
        ShowQuestionnaire();
    }

    private void ShowQuestionnaire()
    {
        questionnaire = true;
        buttons.Clear();
        foreach (Transform child in content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        heading = Text("Survey progress", "", new Vector2(0.02f, 0.92f), new Vector2(0.98f, 0.99f), MobileDialogLayout.SecondarySize);
        question = Text("Survey question", "", new Vector2(0.02f, 0.80f), new Vector2(0.98f, 0.91f), MobileDialogLayout.BodySize);
        for (int index = 0; index < choices.Length; index++)
        {
            int value = index - 3;
            bool firstRow = index < 4;
            float left = firstRow ? 0.035f + index * 0.235f : 0.1525f + (index - 4) * 0.235f;
            choices[index] = Button(ResponseLabels[index], new Vector2(left, firstRow ? 0.51f : 0.285f),
                new Vector2(left + 0.225f, firstRow ? 0.72f : 0.495f), () => Choose(value));
        }
        status = Text("Survey status", "", new Vector2(0.02f, 0.735f), new Vector2(0.98f, 0.795f), MobileDialogLayout.SecondarySize);
        back = Button("Back", new Vector2(0.035f, 0.15f), new Vector2(0.26f, 0.275f), () => Move(-1));
        clear = Button("Clear answer", new Vector2(0.30f, 0.15f), new Vector2(0.70f, 0.275f), () =>
        {
            response.SetAnswer(questionIndex, null);
            Refresh();
        });
        next = Button("Next", new Vector2(0.74f, 0.15f), new Vector2(0.965f, 0.275f), () => Move(1));
        Button("Leave without submitting", new Vector2(0.035f, 0.015f), new Vector2(0.48f, 0.14f), ShowLeaveConfirmation);
        submit = Button("Submit answers so far", new Vector2(0.52f, 0.015f), new Vector2(0.965f, 0.14f), Submit);
        Refresh();
        EventSystem.current.SetSelectedGameObject(choices[0].gameObject);
    }

    private void Choose(int value)
    {
        response.SetAnswer(questionIndex, value);
        Refresh();
    }

    private void Move(int direction)
    {
        questionIndex = Mathf.Clamp(questionIndex + direction, 0, ResearchSurveyResponse.ItemCount - 1);
        Refresh();
        int selected = response.Answer(questionIndex).HasValue ? response.Answer(questionIndex).Value + 3 : 0;
        EventSystem.current.SetSelectedGameObject(choices[selected].gameObject);
    }

    private void Refresh()
    {
        heading.text = $"Player experience survey  |  Question {questionIndex + 1} of {ResearchSurveyResponse.ItemCount}";
        question.text = ResearchSurveyResponse.Question(questionIndex);
        status.text = $"{response.AnsweredCount} of {ResearchSurveyResponse.ItemCount} answered. You may skip questions using Next.";
        back.interactable = questionIndex > 0;
        next.interactable = questionIndex < ResearchSurveyResponse.ItemCount - 1;
        clear.interactable = response.Answer(questionIndex).HasValue;
        submit.interactable = response.AnsweredCount > 0;
        submit.GetComponentInChildren<TMP_Text>().text = response.Complete ? "Submit all answers" : "Submit answers so far";
        for (int index = 0; index < choices.Length; index++)
        {
            bool selected = response.Answer(questionIndex) == index - 3;
            choices[index].GetComponent<Image>().color = selected ? AnswerColor : ButtonColor;
            choices[index].GetComponentInChildren<TMP_Text>().text = (selected ? "Selected\n" : "") + ResponseLabels[index];
        }
        ConfigureNavigation(buttons);
    }

    private void Submit()
    {
        if (closed) return;
        submit.interactable = false;
        if (ResearchSurvey.Submit(response))
        {
            Close();
            return;
        }
        status.text = ResearchAnalyticsBootstrap.HasAnalyticsConsent
            ? "Answers could not be queued. Please try submitting again, or leave without submitting."
            : "Research analytics is off. Answers cannot be submitted; you can leave without submitting.";
        submit.interactable = true;
    }

    private void ShowLeaveConfirmation()
    {
        if (closed || leaveConfirmation != null) return;
        GameObject overlay = new GameObject("Leave survey confirmation", typeof(RectTransform), typeof(Image));
        leaveConfirmation = overlay;
        overlay.transform.SetParent(content, false);
        overlay.GetComponent<Image>().color = PanelColor;
        Stretch((RectTransform)overlay.transform, Vector2.zero, Vector2.one);
        Transform originalContent = content;
        content = overlay.transform;
        int previousButtonCount = buttons.Count;
        Text("Leave survey", "Leave without submitting your answers?", new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.85f), MobileDialogLayout.BodySize);
        cancelLeave = () =>
        {
            leaveConfirmation = null;
            cancelLeave = null;
            overlay.SetActive(false);
            Destroy(overlay);
            buttons.RemoveRange(previousButtonCount, buttons.Count - previousButtonCount);
            Refresh();
            EventSystem.current.SetSelectedGameObject(submit.interactable ? submit.gameObject : choices[0].gameObject);
        };
        Button keep = Button("Keep answering", new Vector2(0.08f, 0.22f), new Vector2(0.48f, 0.39f), () => cancelLeave?.Invoke());
        Button leave = Button("Leave without submitting", new Vector2(0.52f, 0.22f), new Vector2(0.92f, 0.39f), Close);
        ConfigureNavigation(new[] { keep, leave });
        content = originalContent;
        EventSystem.current.SetSelectedGameObject(keep.gameObject);
    }

    private void Close()
    {
        if (closed) return;
        Action callback = onQuit;
        Release();
        gameObject.SetActive(false);
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void Release()
    {
        if (closed) return;
        closed = true;
        onQuit = null;
        cancelLeave = null;
        if (active != this) return;
        active = null;
        Time.timeScale = previousTimeScale;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
    }

    private void OnDisable() => Release();

    private TMP_Text Text(string name, string value, Vector2 min, Vector2 max, float size)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(content, false);
        Stretch((RectTransform)root.transform, min, max);
        TMP_Text text = root.GetComponent<TMP_Text>();
        text.font = font;
        text.text = value;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        MobileDialogLayout.Text(text, size);
        if (name != "Label")
        {
            MobileDialogLayout.ReadingArea(text, content, min, max);
            MobileDialogLayout.Text(text, size);
        }
        text.raycastTarget = false;
        return text;
    }

    private Button Button(string caption, Vector2 min, Vector2 max, UnityAction action, float size = MobileDialogLayout.ButtonSize)
    {
        GameObject root = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(content, false);
        Stretch((RectTransform)root.transform, min, max);
        root.GetComponent<Image>().color = ButtonColor;
        Button button = root.GetComponent<Button>();
        buttons.Add(button);
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        Transform originalContent = content;
        content = root.transform;
        Text("Label", caption, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), size);
        content = originalContent;
        return button;
    }

    private static void ConfigureNavigation(IEnumerable<Button> candidates)
    {
        List<Button> available = new List<Button>();
        foreach (Button button in candidates)
            if (button != null && button.interactable && button.gameObject.activeInHierarchy) available.Add(button);
        for (int index = 0; index < available.Count; index++)
        {
            Button previous = available[(index + available.Count - 1) % available.Count];
            Button nextButton = available[(index + 1) % available.Count];
            available[index].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = previous,
                selectOnUp = previous,
                selectOnRight = nextButton,
                selectOnDown = nextButton
            };
        }
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
