using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaRosterPublicationContractTests
{
    [Test]
    public async Task Default_contract_requires_18_rosters_and_two_aggregates()
    {
        var required = BundesligaRosterPublicationContract.GetRequiredDocuments();

        await Assert.That(required.Count).IsEqualTo(20);
        await Assert.That(required.Count(document => document.Name.StartsWith("roster-", StringComparison.Ordinal))).IsEqualTo(18);
        await Assert.That(required).Contains((
            BundesligaRosterPublicationDocumentKind.Context,
            BundesligaRosterPublicationContract.AggregateRosterDocumentName));
        await Assert.That(required).Contains((
            BundesligaRosterPublicationDocumentKind.Kpi,
            BundesligaRosterPublicationContract.SquadSummaryDocumentName));
    }

    [Test]
    public async Task Snapshot_hash_is_stable_across_input_order_and_changes_with_exact_bytes()
    {
        var teams = Teams;
        var documents = CreateCompleteDocuments(teams);

        var first = BundesligaRosterPublicationContract.ComputeSnapshotId(documents, teams);
        var reversed = BundesligaRosterPublicationContract.ComputeSnapshotId(documents.Reverse().ToArray(), teams);
        var modified = documents
            .Select(document => document.Name == "roster-b04"
                ? document with
                {
                    Content = Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(document.Content).Replace(
                            "Coach Alpha",
                            "Coach Bravo",
                            StringComparison.Ordinal))
                }
                : document)
            .ToArray();
        var changed = BundesligaRosterPublicationContract.ComputeSnapshotId(modified, teams);

        await Assert.That(first.Length).IsEqualTo(64);
        await Assert.That(reversed).IsEqualTo(first);
        await Assert.That(changed).IsNotEqualTo(first);
    }

    [Test]
    public async Task Snapshot_hash_matches_the_hard_coded_adr_0011_compatibility_vector()
    {
        var roster = "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n" +
                     "Bayer 04 Leverkusen,2026-08-16,Coach,Coach Alpha,N/A,Coach,N/A\r\n";
        var summary = "Team_Slug,Team,Data_Collected_At,Membership_Source,Coach,Squad_Size,Known_Age_Count,Average_Age,Valued_Player_Count,Total_Market_Value_EUR,Median_Market_Value_EUR\r\n" +
                      "b04,Bayer 04 Leverkusen,2026-08-16,FallbackSeed,Coach Alpha,20,0,N/A,0,N/A,N/A\r\n";
        var documents = new[]
        {
            new BundesligaRosterPublicationDocument(BundesligaRosterPublicationDocumentKind.Kpi, "team-squad-summary", Encoding.UTF8.GetBytes(summary)),
            new BundesligaRosterPublicationDocument(BundesligaRosterPublicationDocumentKind.Context, "team-rosters", Encoding.UTF8.GetBytes(roster)),
            new BundesligaRosterPublicationDocument(BundesligaRosterPublicationDocumentKind.Context, "roster-bmg", Encoding.UTF8.GetBytes(roster)),
            new BundesligaRosterPublicationDocument(BundesligaRosterPublicationDocumentKind.Context, "roster-b04", Encoding.UTF8.GetBytes(roster))
        };

        await Assert.That(BundesligaRosterPublicationContract.ComputeSnapshotId(documents, Teams))
            .IsEqualTo("57d9a3f707a82f49054a81d3bde2db8f4186204bf8a6744404e6f8e986b1f90e");
    }

    [Test]
    public async Task Publication_contract_rejects_partial_header_only_bom_and_lf_documents()
    {
        var teams = Teams;
        var complete = CreateCompleteDocuments(teams);
        var partial = complete.Skip(1).ToArray();
        var headerOnly = ReplaceContent(
            complete,
            "roster-b04",
            Encoding.UTF8.GetBytes(string.Join(',', BundesligaRosterCsv.RosterHeaders) + "\r\n"));
        var bom = ReplaceContent(
            complete,
            "roster-b04",
            Encoding.UTF8.Preamble.ToArray().Concat(complete.Single(document => document.Name == "roster-b04").Content).ToArray());
        var lfOnly = ReplaceContent(
            complete,
            "roster-b04",
            Encoding.UTF8.GetBytes(CreateRosterCsv().Replace("\r\n", "\n", StringComparison.Ordinal)));

        await Assert.That(() => BundesligaRosterPublicationContract.ValidateAndOrder(partial, teams))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterPublicationContract.ValidateAndOrder(headerOnly, teams))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterPublicationContract.ValidateAndOrder(bom, teams))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterPublicationContract.ValidateAndOrder(lfOnly, teams))
            .Throws<InvalidDataException>();
    }

    private static IReadOnlyList<BundesligaTeamManifestEntry> Teams =>
    [
        BundesligaTeamManifest.Default.GetByTeamSlug("b04"),
        BundesligaTeamManifest.Default.GetByTeamSlug("bmg")
    ];

    private static BundesligaRosterPublicationDocument[] CreateCompleteDocuments(
        IReadOnlyList<BundesligaTeamManifestEntry> teams)
    {
        return BundesligaRosterPublicationContract.GetRequiredDocuments(teams)
            .Select(required => new BundesligaRosterPublicationDocument(
                required.Kind,
                required.Name,
                Encoding.UTF8.GetBytes(required.Name == BundesligaRosterPublicationContract.SquadSummaryDocumentName
                    ? CreateSummaryCsv()
                    : CreateRosterCsv())))
            .ToArray();
    }

    private static BundesligaRosterPublicationDocument[] ReplaceContent(
        IEnumerable<BundesligaRosterPublicationDocument> documents,
        string name,
        byte[] content)
    {
        return documents.Select(document => document.Name == name ? document with { Content = content } : document).ToArray();
    }

    private static string CreateRosterCsv()
    {
        return string.Join(',', BundesligaRosterCsv.RosterHeaders) + "\r\n" +
               "Bayer 04 Leverkusen,2026-08-16,Coach,Coach Alpha,N/A,Coach,N/A\r\n";
    }

    private static string CreateSummaryCsv()
    {
        return string.Join(',', BundesligaRosterCsv.SummaryHeaders) + "\r\n" +
               "b04,Bayer 04 Leverkusen,2026-08-16,FallbackSeed,Coach Alpha,20,0,N/A,0,N/A,N/A\r\n";
    }
}
