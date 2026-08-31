using System.Net;
using System.Text;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging.Testing;

namespace KicktippIntegration.Tests.KicktippClientTests;

public class KicktippClient_BundesligaTypedAuthority_Tests : KicktippClientTests_Base
{
    private const string Community = "test-community";
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Test]
    public async Task Typed_match_inventory_joins_exact_form_ID_to_same_ID_Termin_and_is_deterministic()
    {
        var handler = MatchHandler();
        var client = CreateTypedClient(handler);

        var snapshots = await client.GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory());

        await Assert.That(snapshots.Select(item => item.Key.KicktippItemId)).IsEquivalentTo(["101", "102"]);
        await Assert.That(snapshots[0].Key.PostingCommunity).IsEqualTo(Community);
        await Assert.That(snapshots[0].ScheduledInstant.ToDateTimeOffset())
            .IsEqualTo(DateTimeOffset.Parse("2026-08-30T13:30:00Z"));
        await Assert.That(snapshots.All(item => item.SnapshotHash.SchemaVersion == TypedMatchSnapshot.SchemaVersionValue)).IsTrue();
    }

    [Test]
    public async Task Same_ID_reschedule_preserves_stable_key_and_rotates_snapshot_hash()
    {
        var first = await CreateTypedClient(MatchHandler()).GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory());
        var moved = await CreateTypedClient(MatchHandler(firstTime: "31.08.26 15:30"))
            .GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory());

        await Assert.That(moved[0].Key).IsEqualTo(first[0].Key);
        await Assert.That(moved[0].ScheduledInstant).IsNotEqualTo(first[0].ScheduledInstant);
        await Assert.That(moved[0].SnapshotHash).IsNotEqualTo(first[0].SnapshotHash);
    }

    [Test]
    [Arguments("cancelled-first")]
    [Arguments("cancelled-after-valid")]
    [Arguments("empty-first")]
    [Arguments("unparsable-first")]
    [Arguments("duplicate-form-id")]
    [Arguments("unknown-extra-id")]
    [Arguments("team-drift")]
    [Arguments("competition-drift")]
    [Arguments("round-drift")]
    [Arguments("missing-outcome-id")]
    [Arguments("duplicate-outcome-id")]
    [Arguments("detail-id-conflict")]
    [Arguments("missing-detail")]
    [Arguments("duplicate-termin")]
    [Arguments("duplicate-termin-malformed-sibling")]
    [Arguments("unparsable-termin")]
    [Arguments("dst-overlap-termin")]
    [Arguments("dst-gap-termin")]
    [Arguments("fixture-detail-conflict")]
    public async Task Typed_match_inventory_rejects_hostile_identity_and_schedule_evidence_atomically(string mutation)
    {
        var handler = MatchHandler(mutation: mutation);
        var client = CreateTypedClient(handler);

        await Assert.That(async () =>
                await client.GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory()))
            .Throws<KicktippTypedAuthorityException>();
        await Assert.That(handler.Requests.Any(request => request.Method == HttpMethod.Post)).IsFalse();
    }

    [Test]
    [Arguments("25.10.26 02:30")]
    [Arguments("28.03.27 02:30")]
    public async Task Typed_match_fixture_time_rejects_Berlin_DST_overlap_and_gap(string localTime)
    {
        await Assert.That(async () => await CreateTypedClient(MatchHandler(firstTime: localTime))
                .GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory()))
            .Throws<KicktippTypedAuthorityException>();
    }

    [Test]
    public async Task Typed_match_read_rejects_snapshot_drift_instead_of_using_team_or_time_fallback()
    {
        var initialClient = CreateTypedClient(MatchHandler());
        var initial = await initialClient.GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory());
        var driftedClient = CreateTypedClient(MatchHandler(firstTime: "31.08.26 15:30"));

        await Assert.That(async () => await driftedClient.GetTypedPlacedMatchPredictionsAsync(
                Authority(), MatchRead(initial)))
            .Throws<KicktippTypedAuthorityException>();
    }

    [Test]
    public async Task Typed_match_scope_rejects_the_same_numeric_ID_from_another_posting_community_before_HTTP()
    {
        var handler = MatchHandler();
        var other = MatchIdentity("101", "other-community", "Team A", "Team B", "1. Spieltag");
        var scope = BundesligaTypedMatchInventoryScope.Create([other]);

        await Assert.That(async () => await CreateTypedClient(handler)
                .GetTypedOpenMatchSnapshotsAsync(Authority(), scope))
            .Throws<KicktippTypedAuthorityException>();
        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task Typed_match_POST_uses_exact_ID_fields_and_requires_exact_value_readback()
    {
        var handler = MatchHandler(statefulPost: true);
        var client = CreateTypedClient(handler);
        var snapshots = await client.GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory());
        var readScope = MatchRead(snapshots);
        var batch = BundesligaTypedMatchPlacementBatch.Create(
            readScope,
            [BundesligaTypedMatchSubmission.Create(snapshots[0], new BetPrediction(2, 1))]);

        var readback = await client.PlaceTypedMatchPredictionsAsync(Authority(), batch, overrideExisting: false);

        var post = handler.Requests.Single(request => request.Method == HttpMethod.Post);
        await Assert.That(post.Body).Contains("spieltippForms%5B101%5D.heimTipp=2");
        await Assert.That(post.Body).Contains("spieltippForms%5B101%5D.gastTipp=1");
        await Assert.That(post.Body).Contains("spieltippForms%5B102%5D.heimTipp=3");
        await Assert.That(post.Body).Contains("spieltippForms%5B102%5D.gastTipp=2");
        await Assert.That(post.Body).DoesNotContain("Team+A");
        await Assert.That(readback.Single(item => item.Snapshot.Key.KicktippItemId == "101").Prediction)
            .IsEqualTo(new BetPrediction(2, 1));
    }

    [Test]
    [Arguments("readback-missing-id")]
    [Arguments("readback-extra-id")]
    [Arguments("readback-duplicate-id")]
    [Arguments("readback-changed-value")]
    [Arguments("readback-changed-snapshot")]
    [Arguments("readback-non-target-changed-value")]
    public async Task Typed_match_POST_rejects_missing_extra_duplicate_or_changed_exact_readback(string mutation)
    {
        var handler = MatchHandler(mutation: mutation, statefulPost: true);
        var client = CreateTypedClient(handler);
        var snapshots = await client.GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory());
        var batch = BundesligaTypedMatchPlacementBatch.Create(
            MatchRead(snapshots),
            [BundesligaTypedMatchSubmission.Create(snapshots[0], new BetPrediction(2, 1))]);

        await Assert.That(async () => await client.PlaceTypedMatchPredictionsAsync(
                Authority(), batch, overrideExisting: true))
            .Throws<KicktippTypedAuthorityException>();
        await Assert.That(handler.Requests.Count(request => request.Method == HttpMethod.Post)).IsEqualTo(1);
    }

    [Test]
    public async Task Typed_bonus_inventory_preserves_complete_ordered_option_IDs()
    {
        var client = CreateTypedClient(BonusHandler());

        var snapshots = await client.GetTypedOpenBonusSnapshotsAsync(Authority(), BonusInventory());

        await Assert.That(snapshots.Select(item => item.Key.KicktippItemId)).IsEquivalentTo(["201", "202"]);
        await Assert.That(snapshots[0].Options.SequenceEqual([
            new TypedBonusSnapshotOption("a", "Alpha"),
            new TypedBonusSnapshotOption("b", "Beta")])).IsTrue();
        await Assert.That(snapshots[0].MaxSelections).IsEqualTo(1);
        await Assert.That(snapshots[1].MaxSelections).IsEqualTo(2);
    }

    [Test]
    public async Task Typed_bonus_option_reorder_preserves_key_and_rotates_snapshot_hash()
    {
        var first = await CreateTypedClient(BonusHandler())
            .GetTypedOpenBonusSnapshotsAsync(Authority(), BonusInventory());
        var reordered = await CreateTypedClient(BonusHandler("reordered-options"))
            .GetTypedOpenBonusSnapshotsAsync(Authority(), BonusInventory());

        await Assert.That(reordered[0].Key).IsEqualTo(first[0].Key);
        await Assert.That(reordered[0].Options.Select(option => option.Id).SequenceEqual(["b", "a"])).IsTrue();
        await Assert.That(reordered[0].SnapshotHash).IsNotEqualTo(first[0].SnapshotHash);
    }

    [Test]
    [Arguments("duplicate-question-id")]
    [Arguments("missing-question-id")]
    [Arguments("extra-question-id")]
    [Arguments("duplicate-option-id")]
    [Arguments("partial-select-options")]
    [Arguments("sparse-select-indices")]
    [Arguments("unparsable-deadline")]
    [Arguments("dst-overlap-deadline")]
    [Arguments("dst-gap-deadline")]
    public async Task Typed_bonus_inventory_rejects_ambiguous_or_drifted_exact_identity(string mutation)
    {
        var handler = BonusHandler(mutation);

        await Assert.That(async () => await CreateTypedClient(handler)
                .GetTypedOpenBonusSnapshotsAsync(Authority(), BonusInventory()))
            .Throws<KicktippTypedAuthorityException>();
        await Assert.That(handler.Requests.Any(request => request.Method == HttpMethod.Post)).IsFalse();
    }

    [Test]
    public async Task Typed_bonus_POST_uses_exact_question_and_option_IDs_and_requires_readback()
    {
        var handler = BonusHandler(statefulPost: true);
        var client = CreateTypedClient(handler);
        var snapshots = await client.GetTypedOpenBonusSnapshotsAsync(Authority(), BonusInventory());
        var scope = BonusRead(snapshots);
        var batch = BundesligaTypedBonusPlacementBatch.Create(
            scope,
            [BundesligaTypedBonusSubmission.Create(snapshots[1], ["c", "d"])]);

        var readback = await client.PlaceTypedBonusPredictionsAsync(Authority(), batch, overrideExisting: false);

        var post = handler.Requests.Single(request => request.Method == HttpMethod.Post);
        await Assert.That(post.Body).Contains("fragetippForms%5B202%5D.antwortIds%5B0%5D=c");
        await Assert.That(post.Body).Contains("fragetippForms%5B202%5D.antwortIds%5B1%5D=d");
        await Assert.That(post.Body).Contains("fragetippForms%5B201%5D.antwortIds%5B0%5D=a");
        await Assert.That(post.Body).Contains("spieltippForms%5B501%5D.heimTipp=4");
        await Assert.That(post.Body).Contains("spieltippForms%5B501%5D.gastTipp=2");
        await Assert.That(post.Body).DoesNotContain("Question+Two");
        await Assert.That(readback.Single(item => item.Snapshot.Key.KicktippItemId == "202").SelectedOptionIds)
            .IsEquivalentTo(["c", "d"]);
    }

    [Test]
    [Arguments("readback-missing-id")]
    [Arguments("readback-extra-id")]
    [Arguments("readback-duplicate-id")]
    [Arguments("readback-changed-value")]
    [Arguments("readback-changed-snapshot")]
    [Arguments("readback-non-target-changed-value")]
    public async Task Typed_bonus_POST_rejects_missing_extra_duplicate_or_changed_exact_readback(string mutation)
    {
        var handler = BonusHandler(mutation, statefulPost: true);
        var client = CreateTypedClient(handler);
        var snapshots = await client.GetTypedOpenBonusSnapshotsAsync(Authority(), BonusInventory());
        var batch = BundesligaTypedBonusPlacementBatch.Create(
            BonusRead(snapshots),
            [BundesligaTypedBonusSubmission.Create(snapshots[1], ["c", "d"])]);

        await Assert.That(async () => await client.PlaceTypedBonusPredictionsAsync(
                Authority(), batch, overrideExisting: true))
            .Throws<KicktippTypedAuthorityException>();
    }

    [Test]
    public async Task Typed_contracts_defensively_copy_scopes_and_bonus_selections()
    {
        var identities = new List<BundesligaTypedBonusSourceIdentity>
        {
            BonusIdentity("201")
        };
        var scope = BundesligaTypedBonusInventoryScope.Create(identities);
        identities.Clear();
        var snapshots = await CreateTypedClient(BonusHandler(singleQuestion: true))
            .GetTypedOpenBonusSnapshotsAsync(Authority(), scope);
        var selections = new List<string> { "a" };
        var submission = BundesligaTypedBonusSubmission.Create(snapshots[0], selections);
        selections[0] = "b";

        await Assert.That(scope.Items.Count).IsEqualTo(1);
        await Assert.That(submission.SelectedOptionIds).IsEquivalentTo(["a"]);
    }

    [Test]
    public async Task Typed_inventory_and_placed_results_are_runtime_immutable()
    {
        var matchClient = CreateTypedClient(MatchHandler());
        var matches = await matchClient.GetTypedOpenMatchSnapshotsAsync(Authority(), MatchInventory());
        var placedMatches = await matchClient.GetTypedPlacedMatchPredictionsAsync(Authority(), MatchRead(matches));
        var bonusClient = CreateTypedClient(BonusHandler());
        var bonuses = await bonusClient.GetTypedOpenBonusSnapshotsAsync(Authority(), BonusInventory());
        var placedBonuses = await bonusClient.GetTypedPlacedBonusPredictionsAsync(Authority(), BonusRead(bonuses));

        await Assert.That(matches.GetType().IsArray).IsFalse();
        await Assert.That(bonuses.GetType().IsArray).IsFalse();
        await Assert.That(placedMatches.GetType().IsArray).IsFalse();
        await Assert.That(placedBonuses.GetType().IsArray).IsFalse();
        await Assert.That(() => ((IList<TypedMatchSnapshot>)matches).Add(matches[0]))
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<TypedBonusSnapshot>)bonuses).Clear())
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<BundesligaTypedPlacedMatchPrediction>)placedMatches).RemoveAt(0))
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<BundesligaTypedPlacedBonusPrediction>)placedBonuses)[0] = placedBonuses[0])
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task Typed_client_interface_exposes_only_the_six_exact_authority_operations()
    {
        var methods = typeof(IBundesligaTypedKicktippClient).GetMethods()
            .Select(method => method.Name).Order(StringComparer.Ordinal).ToArray();

        await Assert.That(methods).IsEquivalentTo(new[]
        {
            "GetTypedOpenBonusSnapshotsAsync",
            "GetTypedOpenMatchSnapshotsAsync",
            "GetTypedPlacedBonusPredictionsAsync",
            "GetTypedPlacedMatchPredictionsAsync",
            "PlaceTypedBonusPredictionsAsync",
            "PlaceTypedMatchPredictionsAsync"
        });
    }

    private BundesligaTypedKicktippClient CreateTypedClient(SyntheticHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ServerUrl) };
        var testAuthority = new Uri(ServerUrl);
        return new BundesligaTypedKicktippClient(
            httpClient,
            new FakeLogger<BundesligaTypedKicktippClient>(),
            uri => string.Equals(uri.Scheme, testAuthority.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri.Host, testAuthority.Host, StringComparison.OrdinalIgnoreCase)
                && uri.Port == testAuthority.Port);
    }

    private static BundesligaPredictionAuthority Authority() =>
        BundesligaPredictionAuthority.CreateDirect(
            BundesligaPredictionAuthority.SeasonPartitionValue,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            Community,
            Community,
            Community,
            BundesligaIdentitySeedReference.Create(1, Sha),
            BundesligaIdentitySeedReference.Create(1, Sha));

    private static BundesligaTypedMatchInventoryScope MatchInventory() =>
        BundesligaTypedMatchInventoryScope.Create([
            MatchIdentity("102", Community, "Team C", "Team D", "1. Spieltag"),
            MatchIdentity("101", Community, "Team A", "Team B", "1. Spieltag")
        ]);

    private static BundesligaTypedMatchSourceIdentity MatchIdentity(
        string id,
        string community,
        string home,
        string away,
        string round) =>
        BundesligaTypedMatchSourceIdentity.Create(
            StableLocalItemKey.Create(
                BundesligaPredictionAuthority.SeasonPartitionValue,
                community,
                BundesligaPredictionItemKind.Match,
                id),
            "1. Bundesliga 2026/27",
            BundesligaSeasonSubcompetition.Bundesliga,
            round,
            ResultBasis.RegularTime90Minutes,
            home,
            away,
            1);

    private static BundesligaTypedMatchReadScope MatchRead(IReadOnlyList<TypedMatchSnapshot> snapshots)
    {
        var identities = MatchInventory().Items.ToDictionary(item => item.Key.KicktippItemId, StringComparer.Ordinal);
        return BundesligaTypedMatchReadScope.Create(snapshots.Select(snapshot =>
            BundesligaTypedMatchSnapshotBinding.Create(identities[snapshot.Key.KicktippItemId], snapshot)));
    }

    private static BundesligaTypedBonusInventoryScope BonusInventory() =>
        BundesligaTypedBonusInventoryScope.Create([BonusIdentity("202"), BonusIdentity("201")]);

    private static BundesligaTypedBonusSourceIdentity BonusIdentity(string id) =>
        BundesligaTypedBonusSourceIdentity.Create(
            StableLocalItemKey.Create(
                BundesligaPredictionAuthority.SeasonPartitionValue,
                Community,
                BundesligaPredictionItemKind.Bonus,
                id),
            BundesligaSeasonSubcompetition.Bundesliga);

    private static BundesligaTypedBonusReadScope BonusRead(IReadOnlyList<TypedBonusSnapshot> snapshots)
    {
        var identities = BonusInventory().Items.ToDictionary(item => item.Key.KicktippItemId, StringComparer.Ordinal);
        return BundesligaTypedBonusReadScope.Create(snapshots.Select(snapshot =>
            BundesligaTypedBonusSnapshotBinding.Create(identities[snapshot.Key.KicktippItemId], snapshot)));
    }

    private static SyntheticHandler MatchHandler(
        string? mutation = null,
        string firstTime = "30.08.26 15:30",
        bool statefulPost = false)
    {
        var posted = false;
        return new SyntheticHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                posted = true;
                return Html(request, "<html><body>saved</body></html>");
            }
            if (path.EndsWith("/tippabgabe", StringComparison.Ordinal))
            {
                var effectiveMutation = posted && mutation?.StartsWith("readback-", StringComparison.Ordinal) == true
                    ? mutation : mutation is not null && !mutation.StartsWith("readback-", StringComparison.Ordinal)
                        ? mutation : null;
                var postedValues = statefulPost && posted ? ("2", "1") : ("", "");
                return Html(request, MatchForm(firstTime, effectiveMutation, postedValues));
            }
            if (path.EndsWith("/tippuebersicht", StringComparison.Ordinal))
            {
                return Html(request, MatchOverview(mutation));
            }
            if (path.EndsWith("/tippuebersicht/spiel", StringComparison.Ordinal))
            {
                var id = ReadQueryValue(request.RequestUri, "tippspielId");
                var time = id == "101" ? firstTime : "30.08.26 18:30";
                var effectiveMutation = mutation is "missing-detail" or "duplicate-termin"
                    or "duplicate-termin-malformed-sibling" or "unparsable-termin"
                    or "dst-overlap-termin" or "dst-gap-termin"
                    or "fixture-detail-conflict" or "competition-drift" or "round-drift"
                    ? mutation : posted && mutation == "readback-changed-snapshot" ? mutation : null;
                var response = Html(request, MatchDetail(id, time, effectiveMutation));
                if (mutation == "detail-id-conflict" && id == "101")
                {
                    response.RequestMessage = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"{request.RequestUri!.GetLeftPart(UriPartial.Authority)}/{Community}/tippuebersicht/spiel?tippspielId=999&tippsaisonId=77&spieltagIndex=1");
                }
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
        });
    }

    private static SyntheticHandler BonusHandler(
        string? mutation = null,
        bool statefulPost = false,
        bool singleQuestion = false)
    {
        var posted = false;
        return new SyntheticHandler(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                posted = true;
                return Html(request, "<html><body>saved</body></html>");
            }
            var effectiveMutation = posted && mutation?.StartsWith("readback-", StringComparison.Ordinal) == true
                ? mutation : mutation is not null && !mutation.StartsWith("readback-", StringComparison.Ordinal)
                    ? mutation : null;
            return Html(request, BonusForm(effectiveMutation, statefulPost && posted, singleQuestion));
        });
    }

    private static string MatchForm(string firstTime, string? mutation, (string Home, string Away) posted)
    {
        var firstDate = mutation switch
        {
            "cancelled-first" => "Abgesagt",
            "empty-first" => "",
            "unparsable-first" => "not-a-date",
            "readback-changed-snapshot" => "31.08.26 15:30",
            _ => firstTime
        };
        var secondDate = mutation == "cancelled-after-valid" ? "Abgesagt" : "30.08.26 18:30";
        var secondId = mutation == "duplicate-form-id" || mutation == "readback-duplicate-id" ? "101" : "102";
        var rows = new StringBuilder();
        if (mutation != "readback-missing-id")
        {
            rows.Append(MatchRow("101", firstDate, "Team A", "Team B",
                mutation == "readback-changed-value" ? "9" : posted.Home,
                mutation == "readback-changed-value" ? "9" : posted.Away));
        }
        rows.Append(MatchRow(
            secondId,
            secondDate,
            mutation == "team-drift" ? "Changed Team" : "Team C",
            "Team D",
            mutation == "readback-non-target-changed-value" ? "8" : "3",
            mutation == "readback-non-target-changed-value" ? "8" : "2"));
        if (mutation is "unknown-extra-id" or "readback-extra-id")
        {
            rows.Append(MatchRow("999", "30.08.26 20:30", "Team X", "Team Y", "", ""));
        }
        return $"""
            <html><body><form action="/{Community}/tippabgabe" method="post">
            <input type="hidden" name="spieltagIndex" value="1" />
            <div class="prevnextTitle">1. Spieltag</div>
            <table id="tippabgabeSpiele"><tbody>{rows}</tbody></table>
            <button type="submit" name="submitbutton" value="save">save</button>
            </form></body></html>
            """;
    }

    private static string MatchRow(string id, string time, string home, string away, string homeValue, string awayValue) => $"""
        <tr><td>{time}</td><td>{home}</td><td>{away}</td><td>
        <input name="spieltippForms[{id}].heimTipp" type="text" value="{homeValue}" />
        <input name="spieltippForms[{id}].gastTipp" type="text" value="{awayValue}" />
        </td></tr>
        """;

    private static string MatchOverview(string? mutation)
    {
        var first = mutation == "missing-outcome-id" ? string.Empty :
            $"<tr data-url=\"/{Community}/tippuebersicht/spiel?tippspielId=101&amp;tippsaisonId=77&amp;spieltagIndex=1\"><td>30.08.26 15:30</td></tr>";
        var duplicate = mutation == "duplicate-outcome-id" ? first : string.Empty;
        return $"""
        <html><body><table id="spielplanSpiele"><tbody>
        {first}{duplicate}
        <tr data-url="/{Community}/tippuebersicht/spiel?tippspielId=102&amp;tippsaisonId=77&amp;spieltagIndex=1"><td>30.08.26 18:30</td></tr>
        </tbody></table></body></html>
        """;
    }

    private static string MatchDetail(string id, string time, string? mutation)
    {
        var termin = mutation switch
        {
            "missing-detail" => "",
            "unparsable-termin" => Detail("Termin", "not-a-date"),
            "fixture-detail-conflict" => Detail("Termin", "31.08.26 15:30"),
            "readback-changed-snapshot" => Detail("Termin", "31.08.26 15:30"),
            "duplicate-termin" => Detail("Termin", time) + Detail("Termin", time),
            "duplicate-termin-malformed-sibling" =>
                Detail("Termin", time) + "<div><span class=\"spieldaten-infos-label\">Termin</span><em>broken</em></div>",
            "dst-overlap-termin" => Detail("Termin", "25.10.26 02:30"),
            "dst-gap-termin" => Detail("Termin", "28.03.27 02:30"),
            _ => Detail("Termin", time)
        };
        var competition = mutation == "competition-drift" ? "DFB-Pokal 2026/27" : "1. Bundesliga 2026/27";
        var round = mutation == "round-drift" ? "2. Spieltag" : "1. Spieltag";
        return $"<html><body>{Detail("Wettbewerb", competition)}{Detail("Spieltag", round)}{termin}{Detail("Tipptermin", time)}</body></html>";
    }

    private static string Detail(string label, string value) =>
        $"<div><span class=\"spieldaten-infos-label\">{label}</span><span class=\"spieldaten-infos-value\">{value}</span></div>";

    private static string BonusForm(string? mutation, bool posted, bool singleQuestion)
    {
        var firstId = mutation is "missing-question-id" ? "bad" : "201";
        var secondId = mutation is "duplicate-question-id" or "readback-duplicate-id" ? "201" : "202";
        var firstSelected = posted && mutation == "readback-non-target-changed-value" ? "b" : "a";
        var firstOptions = mutation == "duplicate-option-id"
            ? Options(("a", "Alpha"), ("a", "Beta"), selected: "a")
            : mutation == "reordered-options"
                ? Options(("b", "Beta"), ("a", "Alpha"), selected: "a")
            : Options(("a", "Alpha"), ("b", "Beta"), selected: firstSelected);
        var secondFirstOptions = Options(("c", "Gamma"), ("d", "Delta"),
            posted ? (mutation == "readback-changed-value" ? "d" : "c") : null);
        var secondSecondOptions = mutation == "partial-select-options"
            ? Options(("c", "Gamma"), selected: posted ? "c" : null)
            : Options(("c", "Gamma"), ("d", "Delta"), selected: posted ? "d" : null);
        var secondIndex = mutation == "sparse-select-indices" ? 2 : 1;
        var rows = new StringBuilder();
        if (mutation != "readback-missing-id" || singleQuestion)
        {
            var deadline = mutation switch
            {
                "unparsable-deadline" => "never",
                "dst-overlap-deadline" => "25.10.26 02:30",
                "dst-gap-deadline" => "28.03.27 02:30",
                _ => "30.08.26 12:00"
            };
            rows.Append(BonusRow(firstId, mutation == "readback-changed-snapshot" ? "Changed Question" : "Question One",
                deadline,
                $"<select name=\"fragetippForms[{firstId}].antwortIds[0]\">{firstOptions}</select>"));
        }
        if (!singleQuestion)
        {
            rows.Append(BonusRow(secondId, "Question Two", "30.08.26 12:30",
                $"<select name=\"fragetippForms[{secondId}].antwortIds[0]\">{secondFirstOptions}</select>" +
                $"<select name=\"fragetippForms[{secondId}].antwortIds[{secondIndex}]\">{secondSecondOptions}</select>"));
        }
        if (mutation is "extra-question-id" or "readback-extra-id")
        {
            rows.Append(BonusRow("999", "Extra", "30.08.26 13:00",
                $"<select name=\"fragetippForms[999].antwortIds[0]\">{Options(("x", "Extra"), selected: null)}</select>"));
        }
        return $"""
            <html><body><form action="/{Community}/tippabgabe" method="post">
            <input type="hidden" name="bonus" value="true" />
            <table id="tippabgabeSpiele"><tbody>
            <tr><td>30.08.26 14:00</td><td>Mixed A</td><td>Mixed B</td><td>
            <input name="spieltippForms[501].heimTipp" type="text" value="4" />
            <input name="spieltippForms[501].gastTipp" type="text" value="2" />
            </td></tr></tbody></table>
            <table id="tippabgabeFragen"><tbody>{rows}</tbody></table>
            <button type="submit" name="submitbutton" value="save">save</button>
            </form></body></html>
            """;
    }

    private static string BonusRow(string id, string text, string deadline, string selects) =>
        $"<tr><td>{deadline}</td><td>{text}</td><td>{selects}</td></tr>";

    private static string Options(
        (string Id, string Text) first,
        (string Id, string Text)? second = null,
        string? selected = null)
    {
        var options = new[] { first }.Concat(second is null ? [] : [second.Value]);
        return "<option value=\"-1\"" + (selected is null ? " selected=\"selected\"" : "") + ">none</option>" +
            string.Concat(options.Select(option =>
                $"<option value=\"{option.Id}\"{(selected == option.Id ? " selected=\"selected\"" : "")}>{option.Text}</option>"));
    }

    private static HttpResponseMessage Html(HttpRequestMessage request, string body) =>
        new(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(body, Encoding.UTF8, "text/html")
        };

    private static string ReadQueryValue(Uri uri, string key)
    {
        var pair = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split('=', 2))
            .Single(item => string.Equals(Uri.UnescapeDataString(item[0]), key, StringComparison.Ordinal));
        return Uri.UnescapeDataString(pair[1]);
    }

    private sealed class SyntheticHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
            return await response(request);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);
}
