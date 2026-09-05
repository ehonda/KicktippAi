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
    private static readonly IReadOnlyDictionary<string, ExpectedQuestion> Questions =
        new Dictionary<string, ExpectedQuestion>(StringComparer.Ordinal)
        {
            ["1662326752"] = new(
                "CL: Welche Mannschaft stellt den Spieler mit den meisten Toren?", 1,
                ["fragetippForms[1662326752].antwortIds[1795788]"],
                "642d2f1fa973fe8f32a5dfebcc8945615fa2dd27e24613b4552b99b47cc9e6d6"),
            ["1662326753"] = new(
                "CL: Wer erreicht das Halbfinale?", 4,
                ["fragetippForms[1662326753].antwortIds[1795789]", "fragetippForms[1662326753].antwortIds[1795790]", "fragetippForms[1662326753].antwortIds[1795791]", "fragetippForms[1662326753].antwortIds[1795792]"],
                "39492a824c1f894f1dda4b56efe68024d75d51d9909ce973a73c1029503fdd42"),
            ["1662326754"] = new(
                "CL: Wer gewinnt die Champions League?", 1,
                ["fragetippForms[1662326754].antwortIds[1795793]"],
                "7bbe70e0f9ad0a7f57fba6d4e27bfba4811452c3e2b03718b74fd09567bd725d")
        };

    public static bool IsExactInvocation(
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
        string.Equals(competition, Competition, StringComparison.Ordinal)
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

    /// <summary>Fails closed unless the generic DTO represents all three frozen identities.</summary>
    public static void ValidateQuestions(IReadOnlyList<BonusQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count != Questions.Count)
        {
            throw new InvalidDataException("The Champions-League route requires exactly the three frozen questions.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            var formKey = question.FormFieldName ?? throw new InvalidDataException("A frozen CL question has no form key.");
            var questionId = ExtractQuestionId(formKey);
            if (!seen.Add(questionId) || !Questions.TryGetValue(questionId, out var expected)
                || !string.Equals(question.Text, expected.Text, StringComparison.Ordinal)
                || question.Deadline.ToInstant() != Deadline
                || question.MaxSelections != expected.MaxSelections
                || question.Options.Count != 36
                || !string.Equals(formKey, expected.FormKeys[0], StringComparison.Ordinal))
            {
                throw new InvalidDataException("The live CL bonus form does not match the frozen profile.");
            }

            ValidateOptions(question);
        }

        if (!seen.SetEquals(Questions.Keys))
        {
            throw new InvalidDataException("The live CL bonus form omitted or replaced a frozen question.");
        }
    }

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

    private sealed record ExpectedQuestion(string Text, int MaxSelections, string[] FormKeys, string DefinitionSha256);
}
