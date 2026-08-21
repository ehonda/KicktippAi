using EHonda.KicktippAi.Core;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Orchestrator.Commands.Operations.BundesligaHistory;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using TestUtilities;
using TUnit.Core;

namespace Integration.Tests.Commands.Operations.BundesligaHistoryIntegrationTests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.OrchestratorIntegrationParallelKey)]
public class BundesligaHistoryIntegrationTests(FirestoreFixture fixture)
{
    private const string Community = "ehonda-dev-buli-2627";
    private const string DocumentName = "recent-history-b04.csv";
    private FirestoreFixture Fixture { get; } = fixture;

    [Before(Test)]
    public async Task ClearFirestoreAsync() => await Fixture.ClearOrchestratorIntegrationAsync();

    [Test]
    public async Task Strict_apply_updates_only_the_Bundesliga_partition_and_leaves_WM26_bytes_unchanged()
    {
        const string undated = "Competition,Home_Team,Away_Team,Score,Annotation\n" +
                                 "1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,";
        const string wmBytes = "unrelated-wm26-bytes\n";
        var factory = new TestFirebaseServiceFactory(Fixture.Db);
        var bundesliga = factory.CreateContextRepository(CompetitionIds.Bundesliga2026_27);
        var worldCup = factory.CreateContextRepository(CompetitionIds.FifaWorldCup2026);
        await bundesliga.SaveContextDocumentAsync(DocumentName, undated, Community);
        await worldCup.SaveContextDocumentAsync(DocumentName, wmBytes, Community);

        var (app, console) = CreateCommandApp(factory);
        var exitCode = await app.RunAsync([
            "apply", "--community-context", Community, "--competition", CompetitionIds.Bundesliga2026_27,
            "--input", CreateMap()
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("saved 1 document");
        var updated = await bundesliga.GetLatestContextDocumentAsync(DocumentName, Community);
        var untouched = await worldCup.GetLatestContextDocumentAsync(DocumentName, Community);
        await Assert.That(updated!.Content).IsEqualTo(
            "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\n" +
            "1.BL,2026-05-09,Bayer 04 Leverkusen,VfB Stuttgart,3:1,\r\n");
        await Assert.That(untouched!.Content).IsEqualTo(wmBytes);
    }

    private static (CommandApp App, TestConsole Console) CreateCommandApp(IFirebaseServiceFactory factory)
    {
        var console = new TestConsole();
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton(factory);
        services.AddSingleton<IBundesligaHistoryPlayedDateCollector, BundesligaHistoryPlayedDateCollector>();
        services.AddSingleton<ILogger<BundesligaHistoryApplyCommand>>(new FakeLogger<BundesligaHistoryApplyCommand>());
        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(configuration =>
        {
            configuration.Settings.Console = console;
            configuration.AddCommand<BundesligaHistoryApplyCommand>("apply");
        });
        return (app, console);
    }

    private static string CreateMap()
    {
        var path = Path.Combine(Path.GetTempPath(), "KicktippAi", "bundesliga-history-integration-tests", $"{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, BundesligaHistoryPlayedDateMap.Write([
            new BundesligaHistoryPlayedDateMapEntry(
                DocumentName, 1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", string.Empty,
                "2026-05-09", BundesligaHistoryPlayedDateMap.TransfermarktDatasetSourceClass,
                BundesligaHistoryPlayedDateMap.TransfermarktDatasetSourceName,
                "https://www.transfermarkt.co.uk/example/index/spielbericht/4634534",
                BundesligaHistoryPlayedDateMap.TransfermarktDatasetRevision,
                "4634534", "2026-08-21T12:00:00+02:00")
        ]));
        return path;
    }
}
