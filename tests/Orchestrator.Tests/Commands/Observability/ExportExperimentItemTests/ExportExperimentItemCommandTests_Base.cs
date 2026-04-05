using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using Orchestrator.Commands.Observability.ExportExperimentItem;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Observability.ExportExperimentItemTests;

public abstract class ExportExperimentItemCommandTests_Base
{
    protected const string CommunityContext = "test-community";
    protected const string Model = "gpt-5-nano";
    protected const string HomeTeam = "FC Bayern München";
    protected const string AwayTeam = "Borussia Dortmund";
    protected const int Matchday = 25;

    protected static Match CreateStoredMatch()
    {
        return new Match(
            HomeTeam,
            AwayTeam,
            Instant.FromUtc(2025, 3, 15, 15, 30).InUtc(),
            Matchday);
    }

    protected static PredictionMetadata CreatePredictionMetadata(DateTimeOffset createdAt)
    {
        return new PredictionMetadata(
            new Prediction(2, 1),
            createdAt,
            ["bundesliga-standings.csv", "community-rules-test-community.md"]);
    }

    protected static ContextDocument CreateContextDocument(string name, string content, DateTimeOffset createdAt)
    {
        return new ContextDocument(name, content, 3, createdAt);
    }

    protected static PersistedMatchOutcome CreateOutcome(
        MatchOutcomeAvailability availability = MatchOutcomeAvailability.Completed,
        int? homeGoals = 2,
        int? awayGoals = 1)
    {
        return new PersistedMatchOutcome(
            CommunityContext,
            "bundesliga-2025-26",
            HomeTeam,
            AwayTeam,
            Instant.FromUtc(2025, 3, 15, 15, 30).InUtc(),
            Matchday,
            homeGoals,
            awayGoals,
            availability,
            "tippspiel-25",
            new DateTimeOffset(2025, 3, 15, 15, 35, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 3, 15, 16, 0, 0, TimeSpan.Zero));
    }

    protected static (
        CommandApp App,
        TestConsole Console,
        Mock<IPredictionRepository> PredictionRepository,
        Mock<IContextRepository> ContextRepository,
        Mock<IMatchOutcomeRepository> MatchOutcomeRepository)
        CreateItemCommandApp(
            Match? storedMatch = null,
            PredictionMetadata? predictionMetadata = null,
            Dictionary<string, ContextDocument>? contextDocuments = null,
            IReadOnlyList<PersistedMatchOutcome>? outcomes = null,
            Exception? storedMatchException = null)
    {
        var match = storedMatch ?? CreateStoredMatch();
        var createdAt = predictionMetadata?.CreatedAt ?? new DateTimeOffset(2025, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var metadata = predictionMetadata ?? CreatePredictionMetadata(createdAt);
        var docs = contextDocuments ?? new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument("bundesliga-standings.csv", "standings", createdAt),
            ["community-rules-test-community.md"] = CreateContextDocument("community-rules-test-community.md", "rules", createdAt)
        };

        var predictionRepository = CreateMockPredictionRepository(getPredictionMetadataResult: metadata);
        if (storedMatchException is not null)
        {
            predictionRepository.Setup(repository => repository.GetStoredMatchAsync(
                    HomeTeam,
                    AwayTeam,
                    Matchday,
                    Model,
                    CommunityContext,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(storedMatchException);
        }
        else
        {
            predictionRepository.Setup(repository => repository.GetStoredMatchAsync(
                    HomeTeam,
                    AwayTeam,
                    Matchday,
                    Model,
                    CommunityContext,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedMatch);
        }

        var contextRepository = new Mock<IContextRepository>();
        contextRepository.Setup(repository => repository.GetContextDocumentByTimestampAsync(
                It.IsAny<string>(),
                createdAt,
                CommunityContext,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, DateTimeOffset _, string _, CancellationToken _) =>
                docs.TryGetValue(name, out var document) ? document : null);

        var matchOutcomeRepository = CreateMockMatchOutcomeRepository(
            matchdayOutcomes: EHonda.Optional.Core.Option.Some<IReadOnlyList<PersistedMatchOutcome>>(outcomes ?? [CreateOutcome()]));

        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: predictionRepository,
            contextRepository: contextRepository,
            matchOutcomeRepository: matchOutcomeRepository);

        var (app, console) = CreateCommandApp<ExportExperimentItemCommand>(
            "export-item",
            firebaseServiceFactory: firebaseFactory);

        return (app, console, predictionRepository, contextRepository, matchOutcomeRepository);
    }

    protected static Task<(int ExitCode, string Output)> RunAsync(
        CommandApp app,
        TestConsole console,
        string outputPath,
        params string[] extraArgs)
    {
        var args = new List<string>
        {
            "export-item",
            Model,
            "--community-context", CommunityContext,
            "--home", HomeTeam,
            "--away", AwayTeam,
            "--matchday", Matchday.ToString(),
            "--output", outputPath
        };
        args.AddRange(extraArgs);

        return RunCommandAsync(app, console, args.ToArray());
    }
}
