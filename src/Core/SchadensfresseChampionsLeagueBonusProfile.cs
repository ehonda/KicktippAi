using NodaTime;
using NodaTime.Text;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Immutable admission contract for the one deadline-bound Schadensfresse
/// Champions-League bonus route.  This is deliberately not a competition
/// classifier or a general context-free profile facility.
/// </summary>
public static class SchadensfresseChampionsLeagueBonusProfile
{
    public const string ProfileId = "schadensfresse-champions-league-bonus-context-free-v1";
    public const string Community = "schadensfresse";
    public const string Competition = CompetitionIds.Bundesliga2026_27;
    public const string PromptName = "kicktippai/bundesliga-2026-27/champions-league/predict-bonus";
    public const int PromptVersion = 1;
    public const string PromptLabel = "production";
    public const string PromptNormalizedSha256 = "70819641df57c8979f1c11dfe4e3df920bca96defdbef29646fd22247dfd0ee2";
    public const string SourceSnapshotSha256 = "4299e240f7909f24c2b7f4d2eeeaef564beaea4a3539fe87984867fa890205b0";
    public const string QuestionSetSha256 = "378921172c307e81ab3d839cd229299ed5590e86bc4687d50818d88a46256eea";
    public const string HistoricalEvidenceQuestionSetSha256 = "80def7b217a382ed95450c2a8f8db227ba13a2f55ca72513a8897f86fa511ef9";
    public const string ServicePolicyId = "flex-first-standard-fallback-once-per-question-v1";
    public const string Model = "gpt-5.6-sol";
    public const string ReasoningEffort = "xhigh";
    public const int MaxOutputTokens = 10_000;
    public const string DeadlineUtc = "2026-09-08T16:45:00Z";

    private static readonly Instant Deadline = InstantPattern.ExtendedIso.Parse(DeadlineUtc).Value;

    public static IReadOnlyList<string> OrderedQuestionIds =>
        SchadensfresseChampionsLeagueBonusSeed.Default.Questions
            .Select(question => question.KicktippQuestionId)
            .ToArray();

    public static bool IsExactInvocation(
        string? profileId,
        string competition,
        string community,
        string communityContext,
        string? promptSource,
        string? promptName,
        string? promptLabel,
        int? promptVersion,
        string? model,
        string? reasoningEffort,
        int? maxOutputTokens,
        int? documentBudget,
        int? tokenBudget,
        string? deadlineAtOrBefore) =>
        string.Equals(profileId, ProfileId, StringComparison.Ordinal)
        && string.Equals(competition, Competition, StringComparison.Ordinal)
        && string.Equals(community, Community, StringComparison.Ordinal)
        && string.Equals(communityContext, Community, StringComparison.Ordinal)
        && string.Equals(promptSource, "langfuse", StringComparison.OrdinalIgnoreCase)
        && string.Equals(promptName, PromptName, StringComparison.Ordinal)
        && string.Equals(promptLabel, PromptLabel, StringComparison.Ordinal)
        && promptVersion == PromptVersion
        && string.Equals(model, Model, StringComparison.Ordinal)
        && string.Equals(reasoningEffort, ReasoningEffort, StringComparison.Ordinal)
        && maxOutputTokens == MaxOutputTokens
        && documentBudget == 0
        && tokenBudget == 0
        && string.Equals(deadlineAtOrBefore, DeadlineUtc, StringComparison.Ordinal);

    public static bool IsPotentialInvocation(
        string? profileId,
        string? community,
        string? communityContext,
        string? promptName) =>
        string.Equals(profileId, ProfileId, StringComparison.Ordinal)
        || string.Equals(promptName, PromptName, StringComparison.Ordinal)
        || profileId?.StartsWith("schadensfresse-champions-league-bonus", StringComparison.Ordinal) == true
        || promptName?.Contains("/champions-league/", StringComparison.Ordinal) == true
        || string.Equals(community, Community, StringComparison.Ordinal)
           && string.Equals(communityContext, Community, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(profileId);

    public static PredictionModelConfig CreateModelConfig() => PredictionModelConfig.Create(
        Model,
        ReasoningEffort,
        MaxOutputTokens,
        PromptName,
        PromptVersion);

    /// <summary>Fails closed unless the generic DTO represents all three frozen identities.</summary>
    public static void ValidateQuestions(IReadOnlyList<BonusQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        var seed = SchadensfresseChampionsLeagueBonusSeed.Default;
        if (questions.Count != seed.Questions.Count)
        {
            throw new InvalidDataException("The Champions-League route requires exactly the three frozen questions.");
        }

        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];
            var expected = seed.Questions[index];
            ValidateQuestionAgainst(question, expected);
        }
    }

    public static void ValidateQuestion(BonusQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var questionId = ExtractQuestionId(question.FormFieldName
            ?? throw new InvalidDataException("A frozen CL question has no form key."));
        var seed = SchadensfresseChampionsLeagueBonusSeed.Default.GetQuestion(questionId);
        ValidateQuestionAgainst(question, seed);
    }

    public static string GetQuestionId(BonusQuestion question) => ExtractQuestionId(
        question.FormFieldName ?? throw new InvalidDataException("A frozen CL question has no form key."));

    public static SchadensfresseChampionsLeagueBonusSeedQuestion GetSeedQuestion(string questionId) =>
        SchadensfresseChampionsLeagueBonusSeed.Default.GetQuestion(questionId);

    public static void ValidatePrediction(BonusQuestion question, BonusPrediction prediction)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(prediction);
        if (prediction.SelectedOptionIds.Count != question.MaxSelections
            || prediction.SelectedOptionIds.Distinct(StringComparer.Ordinal).Count() != question.MaxSelections
            || prediction.SelectedOptionIds.Any(id => !question.Options.Any(option => string.Equals(option.Id, id, StringComparison.Ordinal))))
        {
            throw new InvalidDataException("The CL bonus prediction does not select the exact allowed number of frozen option IDs.");
        }
    }

    private static string ExtractQuestionId(string formKey)
    {
        const string prefix = "fragetippForms[";
        const string marker = "].antwortIds[";
        if (!formKey.StartsWith(prefix, StringComparison.Ordinal)
            || formKey.IndexOf(marker, StringComparison.Ordinal) <= prefix.Length
            || !formKey.EndsWith(']'))
        {
            throw new InvalidDataException("The CL bonus form key is not an exact Kicktipp answer slot.");
        }

        return formKey[prefix.Length..formKey.IndexOf(marker, StringComparison.Ordinal)];
    }

    private static void ValidateOptions(BonusQuestion question)
    {
        if (question.Options.Any(option => string.IsNullOrWhiteSpace(option.Id)
                                            || string.IsNullOrWhiteSpace(option.Text))
            || question.Options.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count() != 36
            || question.Options.Select(option => option.Text).Distinct(StringComparer.Ordinal).Count() != 36)
        {
            throw new InvalidDataException("The CL bonus option array is malformed or ambiguous.");
        }
    }

    private static void ValidateQuestionAgainst(
        BonusQuestion question,
        SchadensfresseChampionsLeagueBonusSeedQuestion expected)
    {
        var formKey = question.FormFieldName ?? throw new InvalidDataException("A frozen CL question has no form key.");
        var questionId = ExtractQuestionId(formKey);
        if (!string.Equals(questionId, expected.KicktippQuestionId, StringComparison.Ordinal)
            || !string.Equals(question.Text, expected.Text, StringComparison.Ordinal)
            || question.Deadline.ToInstant() != Deadline
            || question.MaxSelections != expected.MaxSelections
            || !string.Equals(formKey, expected.FormKeys[0], StringComparison.Ordinal)
            || !question.Options.Select(option => new SchadensfresseChampionsLeagueBonusSeedOption(option.Id, option.Text))
                .SequenceEqual(expected.Options))
        {
            throw new InvalidDataException("The live CL bonus form does not match the frozen profile.");
        }

        ValidateOptions(question);
    }

}
