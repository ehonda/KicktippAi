using EHonda.KicktippAi.Core;

namespace KicktippIntegration;

/// <summary>Route-only form representation; never use generic bonus reads/posts for this profile.</summary>
public sealed record ChampionsLeagueBonusFormSnapshot(
    Uri FinalUri,
    Uri Action,
    string Method,
    IReadOnlyList<ChampionsLeagueBonusQuestionSnapshot> Questions,
    IReadOnlyList<KeyValuePair<string, string>> NonTargetControls,
    string SubmitterName,
    string SubmitterValue,
    bool CanPlace);

public sealed record ChampionsLeagueBonusQuestionSnapshot(
    string QuestionId,
    BonusQuestion Question,
    IReadOnlyList<string> FormKeys,
    IReadOnlyList<string?> SelectedOptionIds);

public static class ChampionsLeagueBonusRoute
{
    private const string PagePathAndQuery = "/schadensfresse/tippabgabe?bonus=true";
    private const string ActionPath = "/schadensfresse/tippabgabeForm";
    private static readonly Uri ProductionOrigin = new("https://www.kicktipp.de/");
    private static readonly (Uri Page, Uri Action) ProductionRoute = CreateExactUrisForValidatedOrigin(ProductionOrigin);

    public static readonly Uri ExpectedPage = ProductionRoute.Page;
    public static readonly Uri ExpectedAction = ProductionRoute.Action;

    internal static (Uri Page, Uri Action) CreateExactUrisForValidatedOrigin(Uri validatedOrigin)
    {
        ArgumentNullException.ThrowIfNull(validatedOrigin);
        if (!validatedOrigin.IsAbsoluteUri
            || validatedOrigin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(validatedOrigin.Query)
            || !string.IsNullOrEmpty(validatedOrigin.Fragment)
            || !string.IsNullOrEmpty(validatedOrigin.UserInfo))
        {
            throw new ArgumentException(
                "The exact Champions-League route requires a validated authority-only origin.",
                nameof(validatedOrigin));
        }

        return (new Uri(validatedOrigin, PagePathAndQuery), new Uri(validatedOrigin, ActionPath));
    }

    public static void ValidateSnapshot(ChampionsLeagueBonusFormSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.CanPlace
            || snapshot.FinalUri != ExpectedPage && snapshot.FinalUri != ExpectedAction
            || snapshot.Action != ExpectedAction
            || !string.Equals(snapshot.Method, "POST", StringComparison.Ordinal)
            || !string.Equals(snapshot.SubmitterName, "submitbutton", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Champions-League bonus form is not the exact authenticated, writable HTTPS POST form.");
        }

        SchadensfresseChampionsLeagueBonusProfile.ValidateQuestions(snapshot.Questions.Select(question => question.Question).ToArray());
        if (!snapshot.Questions.Select(question => question.QuestionId)
                .SequenceEqual(SchadensfresseChampionsLeagueBonusProfile.OrderedQuestionIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The frozen CL form question IDs are missing, duplicated, or reordered.");
        }

        var allTargetKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Questions)
        {
            var seed = SchadensfresseChampionsLeagueBonusProfile.GetSeedQuestion(item.QuestionId);
            if (!item.FormKeys.SequenceEqual(seed.FormKeys, StringComparer.Ordinal)
                || item.SelectedOptionIds.Count != item.FormKeys.Count
                || item.SelectedOptionIds.Where(value => value is not null).Distinct(StringComparer.Ordinal).Count()
                    != item.SelectedOptionIds.Count(value => value is not null)
                || item.SelectedOptionIds.Where(value => value is not null)
                    .Any(value => !item.Question.Options.Any(option => string.Equals(option.Id, value, StringComparison.Ordinal)))
                || item.FormKeys.Any(key => !allTargetKeys.Add(key)))
            {
                throw new InvalidDataException("The frozen CL form has drifted slots, duplicate keys, or invalid current selections.");
            }
        }

        if (snapshot.NonTargetControls.Any(control => string.IsNullOrWhiteSpace(control.Key)
                                                      || allTargetKeys.Contains(control.Key)
                                                      || string.Equals(control.Key, snapshot.SubmitterName, StringComparison.Ordinal)
                                                      || !CanEncodeUtf8(control.Key)
                                                      || !CanEncodeUtf8(control.Value))
            || !CanEncodeUtf8(snapshot.SubmitterValue))
        {
            throw new InvalidDataException("The strict CL form contains an ambiguous or unpreservable non-target control.");
        }
    }

    public static IReadOnlyList<KeyValuePair<string, string>> BuildPostPayload(
        ChampionsLeagueBonusFormSnapshot initial,
        ChampionsLeagueBonusFormSnapshot current,
        IReadOnlyList<(string QuestionId, BonusPrediction Prediction)> predictions,
        bool overrideKicktipp)
    {
        ValidateSnapshot(initial);
        ValidateSnapshot(current);
        ValidateCompletePredictions(current, predictions);
        if (!SameDefinitions(initial, current)
            || !initial.Questions.Zip(current.Questions).All(pair =>
                pair.First.SelectedOptionIds.SequenceEqual(pair.Second.SelectedOptionIds, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The frozen CL form definition or target selections changed before POST.");
        }

        if (!overrideKicktipp && current.Questions.Any(question => question.SelectedOptionIds.Any(value => value is not null)))
        {
            throw new InvalidOperationException("Replacing any existing CL selection requires explicit Kicktipp override.");
        }

        var payload = current.NonTargetControls.ToList();
        foreach (var (questionId, prediction) in predictions)
        {
            var question = current.Questions.Single(item => string.Equals(item.QuestionId, questionId, StringComparison.Ordinal));
            for (var index = 0; index < question.FormKeys.Count; index++)
            {
                payload.Add(new KeyValuePair<string, string>(question.FormKeys[index], prediction.SelectedOptionIds[index]));
            }
        }

        payload.Add(new KeyValuePair<string, string>(current.SubmitterName, current.SubmitterValue));
        return payload;
    }

    public static void ValidateCompletePredictions(
        ChampionsLeagueBonusFormSnapshot snapshot,
        IReadOnlyList<(string QuestionId, BonusPrediction Prediction)> predictions)
    {
        ValidateSnapshot(snapshot);
        ArgumentNullException.ThrowIfNull(predictions);
        if (predictions.Count != 3
            || !predictions.Select(result => result.QuestionId)
                .SequenceEqual(SchadensfresseChampionsLeagueBonusProfile.OrderedQuestionIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The strict CL route requires exactly the ordered three prediction identities.");
        }

        foreach (var (questionId, prediction) in predictions)
        {
            var question = snapshot.Questions.Single(item => string.Equals(item.QuestionId, questionId, StringComparison.Ordinal));
            SchadensfresseChampionsLeagueBonusProfile.ValidatePrediction(question.Question, prediction);
        }
    }

    public static void ValidatePlacedSelections(
        ChampionsLeagueBonusFormSnapshot snapshot,
        IReadOnlyList<(string QuestionId, BonusPrediction Prediction)> predictions)
    {
        ValidateCompletePredictions(snapshot, predictions);
        foreach (var (questionId, prediction) in predictions)
        {
            var actual = snapshot.Questions.Single(question => question.QuestionId == questionId)
                .SelectedOptionIds.Where(value => value is not null).Cast<string>().Order(StringComparer.Ordinal);
            if (!actual.SequenceEqual(prediction.SelectedOptionIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new InvalidDataException("The strict CL readback does not match the complete requested selection set.");
            }
        }
    }

    private static bool SameDefinitions(ChampionsLeagueBonusFormSnapshot left, ChampionsLeagueBonusFormSnapshot right) =>
        left.Questions.Count == right.Questions.Count
        && left.Questions.Zip(right.Questions).All(pair =>
            pair.First.QuestionId == pair.Second.QuestionId
            && pair.First.FormKeys.SequenceEqual(pair.Second.FormKeys, StringComparer.Ordinal)
            && string.Equals(pair.First.Question.Text, pair.Second.Question.Text, StringComparison.Ordinal)
            && pair.First.Question.Deadline == pair.Second.Question.Deadline
            && pair.First.Question.MaxSelections == pair.Second.Question.MaxSelections
            && string.Equals(pair.First.Question.FormFieldName, pair.Second.Question.FormFieldName, StringComparison.Ordinal)
            && pair.First.Question.Options.SequenceEqual(pair.Second.Question.Options));

    private static bool CanEncodeUtf8(string value)
    {
        try
        {
            _ = new System.Text.UTF8Encoding(false, true).GetBytes(value);
            return true;
        }
        catch (System.Text.EncoderFallbackException)
        {
            return false;
        }
    }
}
