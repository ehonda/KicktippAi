using System.Diagnostics;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionTelemetryMetadataTests;

public class PredictionTelemetryMetadata_Tests
{
    [Test]
    public async Task Applying_to_null_activity_does_nothing()
    {
        var metadata = new PredictionTelemetryMetadata("Bayern", "Dortmund", 2, "bundesliga-2026-27");

        metadata.ApplyToObservation(null);

        await Assert.That(metadata.RepredictionIndex).IsEqualTo(2);
    }

    [Test]
    public async Task Applying_to_activity_sets_expected_tags()
    {
        using var activity = new Activity("test");
        var rosterSnapshot = new string('a', 64);
        var eloSnapshot = new string('b', 64);
        var metadata = new PredictionTelemetryMetadata(
            HomeTeam: "Bayern",
            AwayTeam: "Dortmund",
            RepredictionIndex: 2,
            Competition: "bundesliga-2026-27",
            ContextDocumentNames: ["club-elo-rankings", "team-squad-summary", "roster-fcb"],
            RosterPublicationSnapshotId: rosterSnapshot,
            ClubEloPublicationSnapshotId: eloSnapshot,
            BonusContextCategory: "TopScorer",
            BonusContextSelectedDocuments: ["club-elo-rankings", "team-squad-summary", "roster-fcb"],
            BonusContextExcludedDocuments: ["team-rosters=ProhibitedAggregate", "roster-bvb=NoExactIdentity"],
            BonusContextEstimatedUtf8Bytes: 4_441,
            BonusContextEstimatedTokens: 1_111,
            BonusContextDocumentBudget: 20,
            BonusContextEstimatedTokenBudget: 32_000);

        metadata.ApplyToObservation(activity);

        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.homeTeam")).IsEqualTo("Bayern");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.awayTeam")).IsEqualTo("Dortmund");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.repredictionIndex")).IsEqualTo("2");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.competition")).IsEqualTo("bundesliga-2026-27");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.match")).IsEqualTo("Bayern vs Dortmund");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.contextDocuments"))
            .IsEqualTo("club-elo-rankings,team-squad-summary,roster-fcb");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.rosterPublicationSnapshotId"))
            .IsEqualTo(rosterSnapshot);
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.clubEloPublicationSnapshotId"))
            .IsEqualTo(eloSnapshot);
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.bonusContextCategory"))
            .IsEqualTo("TopScorer");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.bonusContextSelectedDocuments"))
            .IsEqualTo("club-elo-rankings,team-squad-summary,roster-fcb");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.bonusContextExcludedDocuments"))
            .IsEqualTo("team-rosters=ProhibitedAggregate,roster-bvb=NoExactIdentity");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.bonusContextEstimatedUtf8Bytes"))
            .IsEqualTo("4441");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.bonusContextEstimatedTokens"))
            .IsEqualTo("1111");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.bonusContextDocumentBudget"))
            .IsEqualTo("20");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.bonusContextEstimatedTokenBudget"))
            .IsEqualTo("32000");
    }

    [Test]
    public async Task Applying_to_activity_skips_blank_values()
    {
        using var activity = new Activity("test");
        var metadata = new PredictionTelemetryMetadata("Bayern", " ", null);

        metadata.ApplyToObservation(activity);

        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.homeTeam")).IsEqualTo("Bayern");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.awayTeam")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.repredictionIndex")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.match")).IsNull();
    }

    [Test]
    public async Task Building_delimited_filter_value_sorts_trims_and_deduplicates()
    {
        var value = PredictionTelemetryMetadata.BuildDelimitedFilterValue([" Dortmund ", "Bayern", "Bayern", "", " "]);

        await Assert.That(value).IsEqualTo("|Bayern|Dortmund|");
    }

    [Test]
    public async Task Building_delimited_filter_value_returns_empty_for_no_usable_values()
    {
        var value = PredictionTelemetryMetadata.BuildDelimitedFilterValue(["", " ", "\t"]);

        await Assert.That(value).IsEqualTo(string.Empty);
    }
}
