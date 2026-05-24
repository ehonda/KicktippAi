using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp.Tests;

public class WorldCup2026ContextDocumentsFileProviderTests
{
    [Test]
    public async Task Create_roots_provider_at_wm26_context_documents_data_directory()
    {
        var provider = WorldCup2026ContextDocumentsFileProvider.Create();

        var fileInfo = provider.GetFileInfo("fifa-ranking-mexiko.csv");

        await Assert.That(fileInfo.Exists).IsTrue();
        await using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        await Assert.That(content).Contains("Mexiko,2026-05-24,15,1681.03");
    }

    [Test]
    public async Task Wm26_fifa_ranking_csvs_include_collection_date_column()
    {
        var solutionRoot = SolutionPathUtility.FindSolutionRoot();
        var contextDirectory = Path.Combine(solutionRoot, "data", "wm26", "context-documents");
        var kpiFile = Path.Combine(solutionRoot, "data", "wm26", "kpi-documents", "fifa-rankings.csv");
        var files = Directory
            .GetFiles(contextDirectory, "fifa-ranking-*.csv", SearchOption.TopDirectoryOnly)
            .Concat([kpiFile])
            .ToList();

        await Assert.That(files.Count).IsGreaterThan(0);

        foreach (var file in files)
        {
            var rows = await File.ReadAllLinesAsync(file);

            await Assert.That(rows[0]).IsEqualTo("team,Data_Collected_At,rank,ELO");
            foreach (var row in rows.Skip(1).Where(row => !string.IsNullOrWhiteSpace(row)))
            {
                var fields = row.Split(',');
                await Assert.That(fields).Count().IsEqualTo(4);
                await Assert.That(fields[1]).IsEqualTo("2026-05-24");
            }
        }
    }
}
