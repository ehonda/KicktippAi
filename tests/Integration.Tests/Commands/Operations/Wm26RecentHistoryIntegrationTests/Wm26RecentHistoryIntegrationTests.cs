using EHonda.KicktippAi.Core;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NodaTime;
using Orchestrator.Commands.Operations.Wm26RecentHistory;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using TestUtilities;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

namespace Integration.Tests.Commands.Operations.Wm26RecentHistoryIntegrationTests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.OrchestratorIntegrationParallelKey)]
public class Wm26RecentHistoryIntegrationTests(FirestoreFixture fixture)
{
    private const string CommunityContext = "ehonda-dev-wm26";
    private FirestoreFixture Fixture { get; } = fixture;

    [Before(Test)]
    public async Task ClearFirestoreAsync()
    {
        await Fixture.ClearOrchestratorIntegrationAsync();
    }

    [Test]
    public async Task Apply_date_map_resolves_cutoff_row_from_stored_prediction()
    {
        var factory = new TestFirebaseServiceFactory(Fixture.Db);
        var predictionRepository = factory.CreatePredictionRepository(CompetitionIds.FifaWorldCup2026);
        var contextRepository = factory.CreateContextRepository(CompetitionIds.FifaWorldCup2026);
        var match = CreateMatch(
            homeTeam: "Mexiko",
            awayTeam: "Südafrika",
            startsAt: Instant.FromUtc(2026, 6, 11, 19, 0).InUtc(),
            matchday: 1);
        var dateMapPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-mexiko.csv,WM,Mexiko,Südafrika,1:1,,2010-06-11,FIFA,https://example.test,2026-05-23,
            """);

        await predictionRepository.SavePredictionAsync(
            match,
            CreatePrediction(),
            model: "gpt-5-nano",
            tokenUsage: "{}",
            cost: 0.01,
            communityContext: CommunityContext,
            contextDocumentNames: []);
        await contextRepository.SaveContextDocumentAsync(
            "recent-history-mexiko.csv",
            "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nWM,2026-06-11,Mexiko,Südafrika,1:1,",
            CommunityContext);

        var (app, console) = CreateCommandApp(factory);
        var exitCode = await app.RunAsync([
            "apply-date-map",
            "--community-context",
            CommunityContext,
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            dateMapPath,
            "--apply-known-only",
            "--preserve-collected-on-or-after",
            "2026-06-11",
            "--verbose"]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("1 updated");
        var document = await contextRepository.GetLatestContextDocumentAsync(
            "recent-history-mexiko.csv",
            CommunityContext);
        await Assert.That(document).IsNotNull();
        await Assert.That(document!.Content).Contains("WM,2026-06-11T21:00:00+02:00,Mexiko,Südafrika,1:1,");
    }

    private static (CommandApp App, TestConsole Console) CreateCommandApp(IFirebaseServiceFactory firebaseServiceFactory)
    {
        var testConsole = new TestConsole();
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(firebaseServiceFactory);
        services.AddSingleton<ILogger<Wm26RecentHistoryApplyDateMapCommand>>(new FakeLogger<Wm26RecentHistoryApplyDateMapCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<Wm26RecentHistoryApplyDateMapCommand>("apply-date-map");
        });

        return (app, testConsole);
    }

    private static string CreateTempDateMap(string content)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "KicktippAi",
            "wm26-recent-history-integration-tests",
            $"{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.Replace("\r\n", "\n"));
        return path;
    }
}
