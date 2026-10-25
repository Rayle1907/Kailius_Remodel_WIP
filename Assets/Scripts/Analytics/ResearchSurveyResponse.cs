using System;

public sealed class ResearchSurveyResponse
{
    public const string Version = "pxi_selected_v1";
    public const int PxiItemCount = 12;
    public const int ItemCount = 15;

    private static readonly string[] parameterNames =
    {
        "pxi_autonomy_1", "pxi_autonomy_2", "pxi_autonomy_3",
        "pxi_mastery_1", "pxi_mastery_2", "pxi_mastery_3",
        "pxi_challenge_1", "pxi_challenge_2", "pxi_challenge_3",
        "pxi_progress_feedback_1", "pxi_progress_feedback_2", "pxi_progress_feedback_3",
        "background_experience_skill_1", "background_experience_skill_2", "background_experience_skill_3"
    };

    private static readonly string[] questions =
    {
        "I felt free to play the game in my own way.",
        "I felt like I had choices regarding how I wanted to play this game.",
        "I felt a sense of freedom about how I wanted to play this game.",
        "I felt I was good at playing this game.",
        "I felt capable while playing the game.",
        "I felt a sense of mastery playing this game.",
        "The game was not too easy and not too hard to play.",
        "The game was challenging but not too challenging.",
        "The challenges in the game were at the right level of difficulty for me.",
        "The game informed me of my progress in the game.",
        "I could easily assess how I was performing in the game.",
        "The game gave clear feedback on my progress towards the goals.",
        "I play video games regularly.",
        "I have experience playing games similar to this one.",
        "I consider myself skilled at video games."
    };

    private readonly int?[] answers = new int?[ItemCount];

    public string SurveyId { get; } = Guid.NewGuid().ToString("N");
    public string PlaythroughId { get; }
    public string SceneName { get; }
    public string OfferVariant { get; }
    public string ExperimentVersion { get; }
    public int AnsweredCount => CountAnswers(ItemCount);
    public int PxiAnsweredCount => CountAnswers(PxiItemCount);
    public bool Complete => AnsweredCount == ItemCount;

    public ResearchSurveyResponse(string playthroughId, string sceneName, string offerVariant, string experimentVersion)
    {
        PlaythroughId = playthroughId;
        SceneName = sceneName;
        OfferVariant = offerVariant;
        ExperimentVersion = experimentVersion;
    }

    public static string Question(int index) => questions[index];
    public static string ParameterName(int index) => parameterNames[index];
    public int? Answer(int index) => answers[index];

    public void SetAnswer(int index, int? value)
    {
        if (value.HasValue && (value.Value < -3 || value.Value > 3))
            throw new ArgumentOutOfRangeException(nameof(value));
        answers[index] = value;
    }

    private int CountAnswers(int count)
    {
        int answered = 0;
        for (int index = 0; index < count; index++)
            if (answers[index].HasValue) answered++;
        return answered;
    }
}
