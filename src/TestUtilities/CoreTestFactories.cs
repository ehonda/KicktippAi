using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using NodaTime;

namespace TestUtilities;

/// <summary>
/// Factory methods for creating Core domain objects in tests.
/// Use these methods to create test instances with sensible defaults,
/// allowing tests to override only the properties relevant to their scenario.
/// </summary>
public static class CoreTestFactories
{
    /// <summary>
    /// Creates a test <see cref="Match"/> with default values.
    /// </summary>
    /// <param name="homeTeam">Home team name. Defaults to "Bayern München".</param>
    /// <param name="awayTeam">Away team name. Defaults to "Borussia Dortmund".</param>
    /// <param name="startsAt">Match start time. Defaults to 2025-03-15 15:30 UTC.</param>
    /// <param name="matchday">Matchday number. Defaults to 25.</param>
    public static Match CreateMatch(
        Option<string> homeTeam = default,
        Option<string> awayTeam = default,
        Option<ZonedDateTime> startsAt = default,
        Option<int> matchday = default)
    {
        return new Match(
            homeTeam.Or("Bayern München"),
            awayTeam.Or("Borussia Dortmund"),
            startsAt.Or(() => Instant.FromUtc(2025, 3, 15, 15, 30).InUtc()),
            matchday.Or(25));
    }

    /// <summary>
    /// Creates a test <see cref="Prediction"/> with default values.
    /// </summary>
    /// <param name="homeGoals">Home team goals. Defaults to 2.</param>
    /// <param name="awayGoals">Away team goals. Defaults to 1.</param>
    /// <param name="justification">Optional prediction justification. Defaults to null.</param>
    public static Prediction CreatePrediction(
        Option<int> homeGoals = default,
        Option<int> awayGoals = default,
        NullableOption<PredictionJustification> justification = default)
    {
        return new Prediction(
            homeGoals.Or(2),
            awayGoals.Or(1),
            justification.Or((PredictionJustification?)null));
    }

    /// <summary>
    /// Creates a test <see cref="PredictionJustification"/> with default values.
    /// </summary>
    /// <param name="keyReasoning">Key reasoning text. Defaults to "Default test reasoning".</param>
    /// <param name="contextSources">Context sources. Defaults to empty sources.</param>
    /// <param name="uncertainties">List of uncertainties. Defaults to empty list.</param>
    public static PredictionJustification CreatePredictionJustification(
        Option<string> keyReasoning = default,
        Option<PredictionJustificationContextSources> contextSources = default,
        Option<List<string>> uncertainties = default)
    {
        return new PredictionJustification(
            keyReasoning.Or("Default test reasoning"),
            contextSources.Or(() => CreatePredictionJustificationContextSources()),
            uncertainties.Or(() => []));
    }

    /// <summary>
    /// Creates a test <see cref="PredictionJustificationContextSources"/> with default values.
    /// </summary>
    /// <param name="mostValuable">Most valuable context sources. Defaults to empty list.</param>
    /// <param name="leastValuable">Least valuable context sources. Defaults to empty list.</param>
    public static PredictionJustificationContextSources CreatePredictionJustificationContextSources(
        Option<List<PredictionJustificationContextSource>> mostValuable = default,
        Option<List<PredictionJustificationContextSource>> leastValuable = default)
    {
        return new PredictionJustificationContextSources(
            mostValuable.Or(() => []),
            leastValuable.Or(() => []));
    }

    /// <summary>
    /// Creates a test <see cref="PredictionJustificationContextSource"/> with default values.
    /// </summary>
    /// <param name="documentName">Document name. Defaults to "test-document".</param>
    /// <param name="details">Details about the source. Defaults to "test details".</param>
    public static PredictionJustificationContextSource CreatePredictionJustificationContextSource(
        Option<string> documentName = default,
        Option<string> details = default)
    {
        return new PredictionJustificationContextSource(
            documentName.Or("test-document"),
            details.Or("test details"));
    }

    /// <summary>
    /// Creates a test <see cref="BonusQuestion"/> with default values.
    /// </summary>
    /// <param name="text">Question text. Defaults to "Who will win the league?".</param>
    /// <param name="deadline">Answer deadline. Defaults to 2025-05-15 18:00 UTC.</param>
    /// <param name="options">Available options. Defaults to 3 options: opt-1, opt-2, opt-3.</param>
    /// <param name="maxSelections">Maximum selections allowed. Defaults to 1.</param>
    /// <param name="formFieldName">Optional form field name. Defaults to null.</param>
    public static BonusQuestion CreateBonusQuestion(
        Option<string> text = default,
        Option<ZonedDateTime> deadline = default,
        Option<List<BonusQuestionOption>> options = default,
        Option<int> maxSelections = default,
        NullableOption<string> formFieldName = default)
    {
        var actualOptions = options.Or(() =>
        [
            new("opt-1", "Option 1"),
            new("opt-2", "Option 2"),
            new("opt-3", "Option 3")
        ]);

        return new BonusQuestion(
            text.Or("Who will win the league?"),
            deadline.Or(() => Instant.FromUtc(2025, 5, 15, 18, 0).InUtc()),
            actualOptions,
            maxSelections.Or(1),
            formFieldName.Or((string?)null));
    }

    /// <summary>
    /// Creates a test <see cref="BonusPrediction"/> with default values.
    /// </summary>
    /// <param name="selectedOptionIds">Selected option IDs. Defaults to ["opt-1"].</param>
    public static BonusPrediction CreateBonusPrediction(
        Option<List<string>> selectedOptionIds = default)
    {
        return new BonusPrediction(selectedOptionIds.Or(() => ["opt-1"]));
    }

    /// <summary>
    /// Creates a test <see cref="MatchResult"/> with default values.
    /// </summary>
    /// <param name="competition">Competition name. Defaults to "1.BL".</param>
    /// <param name="homeTeam">Home team name. Defaults to "Bayern München".</param>
    /// <param name="awayTeam">Away team name. Defaults to "Borussia Dortmund".</param>
    /// <param name="homeGoals">Home team goals. Defaults to 2.</param>
    /// <param name="awayGoals">Away team goals. Defaults to 1.</param>
    /// <param name="outcome">Match outcome. Defaults to Win.</param>
    /// <param name="annotation">Optional annotation. Defaults to null.</param>
    public static MatchResult CreateMatchResult(
        Option<string> competition = default,
        Option<string> homeTeam = default,
        Option<string> awayTeam = default,
        NullableOption<int> homeGoals = default,
        NullableOption<int> awayGoals = default,
        Option<MatchOutcome> outcome = default,
        NullableOption<string> annotation = default)
    {
        return new MatchResult(
            competition.Or("1.BL"),
            homeTeam.Or("Bayern München"),
            awayTeam.Or("Borussia Dortmund"),
            homeGoals.Or(2),
            awayGoals.Or(1),
            outcome.Or(MatchOutcome.Win),
            annotation.Or((string?)null));
    }

    /// <summary>
    /// Creates a test <see cref="MatchWithHistory"/> with default values.
    /// </summary>
    /// <param name="match">The match. Defaults to a new test match.</param>
    /// <param name="homeTeamHistory">Home team recent results. Defaults to empty list.</param>
    /// <param name="awayTeamHistory">Away team recent results. Defaults to empty list.</param>
    public static MatchWithHistory CreateMatchWithHistory(
        Option<Match> match = default,
        Option<List<MatchResult>> homeTeamHistory = default,
        Option<List<MatchResult>> awayTeamHistory = default)
    {
        return new MatchWithHistory(
            match.Or(() => CreateMatch()),
            homeTeamHistory.Or(() => []),
            awayTeamHistory.Or(() => []));
    }

    /// <summary>
    /// Creates a test <see cref="DocumentContext"/> with default values.
    /// </summary>
    /// <param name="name">Document name. Defaults to "test-document".</param>
    /// <param name="content">Document content. Defaults to "test content".</param>
    public static DocumentContext CreateDocumentContext(
        Option<string> name = default,
        Option<string> content = default)
    {
        return new DocumentContext(
            name.Or("test-document"),
            content.Or("test content"));
    }

    /// <summary>
    /// Creates a test <see cref="TeamStanding"/> with default values.
    /// </summary>
    /// <param name="position">League position. Defaults to 1.</param>
    /// <param name="teamName">Team name. Defaults to "Bayern München".</param>
    /// <param name="gamesPlayed">Games played. Defaults to 10.</param>
    /// <param name="points">Total points. Defaults to 25.</param>
    /// <param name="goalsFor">Goals scored. Defaults to 30.</param>
    /// <param name="goalsAgainst">Goals conceded. Defaults to 10.</param>
    /// <param name="goalDifference">Goal difference. Defaults to 20.</param>
    /// <param name="wins">Wins. Defaults to 8.</param>
    /// <param name="draws">Draws. Defaults to 1.</param>
    /// <param name="losses">Losses. Defaults to 1.</param>
    public static TeamStanding CreateTeamStanding(
        Option<int> position = default,
        Option<string> teamName = default,
        Option<int> gamesPlayed = default,
        Option<int> points = default,
        Option<int> goalsFor = default,
        Option<int> goalsAgainst = default,
        Option<int> goalDifference = default,
        Option<int> wins = default,
        Option<int> draws = default,
        Option<int> losses = default)
    {
        return new TeamStanding(
            position.Or(1),
            teamName.Or("Bayern München"),
            gamesPlayed.Or(10),
            points.Or(25),
            goalsFor.Or(30),
            goalsAgainst.Or(10),
            goalDifference.Or(20),
            wins.Or(8),
            draws.Or(1),
            losses.Or(1));
    }

    /// <summary>
    /// Creates a test <see cref="HeadToHeadResult"/> with default values.
    /// </summary>
    /// <param name="league">League name. Defaults to "1.BL 2024/25".</param>
    /// <param name="matchday">Matchday description. Defaults to "25. Spieltag".</param>
    /// <param name="playedAt">Date played. Defaults to "2025-03-15".</param>
    /// <param name="homeTeam">Home team name. Defaults to "Bayern München".</param>
    /// <param name="awayTeam">Away team name. Defaults to "Borussia Dortmund".</param>
    /// <param name="score">Match score. Defaults to "2:1".</param>
    /// <param name="annotation">Optional annotation. Defaults to null.</param>
    public static HeadToHeadResult CreateHeadToHeadResult(
        Option<string> league = default,
        Option<string> matchday = default,
        Option<string> playedAt = default,
        Option<string> homeTeam = default,
        Option<string> awayTeam = default,
        Option<string> score = default,
        NullableOption<string> annotation = default)
    {
        return new HeadToHeadResult(
            league.Or("1.BL 2024/25"),
            matchday.Or("25. Spieltag"),
            playedAt.Or("2025-03-15"),
            homeTeam.Or("Bayern München"),
            awayTeam.Or("Borussia Dortmund"),
            score.Or("2:1"),
            annotation.Or((string?)null));
    }

    /// <summary>
    /// Creates a test <see cref="PredictionMetadata"/> with default values.
    /// </summary>
    /// <param name="prediction">The prediction. Defaults to a new test prediction.</param>
    /// <param name="createdAt">Creation timestamp. Defaults to 2025-01-10 12:00 UTC.</param>
    /// <param name="contextDocumentNames">Context document names used. Defaults to empty list.</param>
    public static PredictionMetadata CreatePredictionMetadata(
        Option<Prediction> prediction = default,
        Option<DateTimeOffset> createdAt = default,
        Option<List<string>> contextDocumentNames = default)
    {
        return new PredictionMetadata(
            prediction.Or(() => CreatePrediction()),
            createdAt.Or(() => new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero)),
            contextDocumentNames.Or(() => []));
    }
}
