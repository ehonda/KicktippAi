using EHonda.KicktippAi.Core;
using Moq;
using NodaTime;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.ExportExperimentDatasetTests;

public abstract class ExportExperimentDatasetCommandTests_Base
{
    protected const string CommunityContext = "test-community";

    protected static PersistedMatchOutcome CreateOutcome(
        int matchday,
        string homeTeam,
        string awayTeam,
        string tippSpielId,
        MatchOutcomeAvailability availability = MatchOutcomeAvailability.Completed,
        int? homeGoals = 2,
        int? awayGoals = 1)
    {
        return new PersistedMatchOutcome(
            CommunityContext,
            "bundesliga-2025-26",
            homeTeam,
            awayTeam,
            Instant.FromUtc(2025, 3, 15, 15, 30).InUtc(),
            matchday,
            homeGoals,
            awayGoals,
            availability,
            tippSpielId,
            new DateTimeOffset(2025, 3, 15, 15, 35, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 3, 15, 16, 0, 0, TimeSpan.Zero));
    }

    protected static (
        CommandApp App,
        TestConsole Console,
        Mock<IMatchOutcomeRepository> MatchOutcomeRepository)
        CreateCommandApp(Dictionary<int, IReadOnlyList<PersistedMatchOutcome>> outcomesByMatchday)
    {
        var matchOutcomeRepository = new Mock<IMatchOutcomeRepository>();
        matchOutcomeRepository.Setup(repository => repository.GetMatchdayOutcomesAsync(
                It.IsAny<int>(),
                CommunityContext,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int matchday, string _, CancellationToken _) =>
                outcomesByMatchday.TryGetValue(matchday, out var outcomes) ? outcomes : []);

        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(matchOutcomeRepository: matchOutcomeRepository);
        var (app, console) = CreateCommandApp<ExportExperimentDatasetCommand>(
            "export-dataset",
            firebaseServiceFactory: firebaseFactory);

        return (app, console, matchOutcomeRepository);
    }

    protected static Task<(int ExitCode, string Output)> RunAsync(
        CommandApp app,
        TestConsole console,
        string outputPath,
        params string[] extraArgs)
    {
        var args = new List<string>
        {
            "export-dataset",
            "--community-context", CommunityContext,
            "--output", outputPath
        };
        args.AddRange(extraArgs);

        return RunCommandAsync(app, console, args.ToArray());
    }
}
