using System.Globalization;
using CsvHelper;
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
        const string wmBytes = "unrelated-wm26-bytes\n";
        var factory = new TestFirebaseServiceFactory(Fixture.Db);
        var bundesliga = factory.CreateContextRepository(CompetitionIds.Bundesliga2026_27);
        var worldCup = factory.CreateContextRepository(CompetitionIds.FifaWorldCup2026);
        var map = BundesligaHistoryPlayedDateMap.Default.Entries;
        var initialDocuments = map.GroupBy(entry => entry.DocumentName, StringComparer.Ordinal)
            .Select(group => new ContextDocumentWrite(
                group.Key,
                RenderHistory(group, includePlayedAt: group.Key != DocumentName)))
            .ToArray();
        await bundesliga.SaveContextDocumentsAtomicallyAsync(initialDocuments, Community);
        await worldCup.SaveContextDocumentAsync(DocumentName, wmBytes, Community);

        var (app, console) = CreateCommandApp(factory);
        var exitCode = await app.RunAsync([
            "apply", "--community-context", Community, "--competition", CompetitionIds.Bundesliga2026_27,
            "--input", CreateMap()
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("atomically saved 1 document");
        var updated = await bundesliga.GetLatestContextDocumentAsync(DocumentName, Community);
        var untouched = await worldCup.GetLatestContextDocumentAsync(DocumentName, Community);
        await Assert.That(updated!.Content).IsEqualTo(RenderHistory(
            map.Where(entry => entry.DocumentName == DocumentName), includePlayedAt: true));
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
        return Path.Combine(SolutionPathUtility.FindSolutionRoot(), "data", "bundesliga-2026-27", "history", "history-played-dates.csv");
    }

    private static string RenderHistory(
        IEnumerable<BundesligaHistoryPlayedDateMapEntry> entries,
        bool includePlayedAt)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var header in includePlayedAt
                     ? new[] { "Competition", "Played_At", "Home_Team", "Away_Team", "Score", "Annotation" }
                     : new[] { "Competition", "Home_Team", "Away_Team", "Score", "Annotation" })
        {
            csv.WriteField(header);
        }
        csv.NextRecord();

        foreach (var entry in entries.OrderBy(entry => entry.RowOrdinal))
        {
            csv.WriteField(entry.HistoryCompetition);
            if (includePlayedAt) csv.WriteField(entry.PlayedAt);
            csv.WriteField(entry.HomeTeam);
            csv.WriteField(entry.AwayTeam);
            csv.WriteField(entry.Score);
            csv.WriteField(entry.Annotation);
            csv.NextRecord();
        }
        return writer.ToString();
    }
}
