using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaClubEloPublicationTests
{
    [Test]
    public async Task V1_publication_round_trips_as_a_headed_snapshot_not_a_v2_family()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
            BundesligaClubEloSelectionDisposition.NetworkDisabled, ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        var metadata = BundesligaClubEloPublication.ParseMetadata(build.MetadataJson);
        await Assert.That(metadata.SelectedOrigin).IsEqualTo(BundesligaClubEloSnapshotOrigin.LaunchSeed);
        await Assert.That(build.MetadataJson).Contains("\"schema_version\":\"club-elo-publication-v1\"");
        await Assert.That(build.MetadataJson).DoesNotContain("sourceDescriptor");
        await Assert.That(build.MetadataJson).IsEqualTo("{\"schema_version\":\"club-elo-publication-v1\",\"rated_at\":\"2026-08-14\",\"collected_at\":\"2026-08-16T10:44:16Z\",\"source_url\":\"https://clubelo.com/GER\",\"selected_origin\":\"LaunchSeed\",\"selection_disposition\":\"NetworkDisabled\",\"selection_diagnostics\":[\"UNATTENDED_NETWORK_USE_NOT_APPROVED\"],\"manifest_team_count\":18,\"rank_policy\":\"elo-desc-global-rank-asc-manifest-slug-ordinal-sequential\"}");
    }

    [Test]
    public async Task Csv_v2_preserves_a_fractional_evidence_token_but_refuses_to_bind_it_to_headed_documents()
    {
        var observedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var cycle = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var snapshot = NetworkSnapshot(new Uri("https://example.test/elo.csv"), new DateOnly(2026, 9, 4), observedAt);
        var selection = new BundesligaClubEloSelection(snapshot, BundesligaClubEloSelectionDisposition.NetworkAccepted, []);
        var bytes = System.Text.Encoding.UTF8.GetBytes("csv");
        var observation = CsvObservation(cycle, snapshot, observedAt, bytes, fractional: true);

        var evidence = BundesligaClubEloPublication.CreateHistoricalCsvV2Evidence(selection, cycle, observation, bytes);
        var parsed = BundesligaClubEloPublication.ParseMetadata(evidence);

        await Assert.That(evidence).Contains("\"elo\":1500.25");
        await Assert.That(parsed.CollectedAt).IsEqualTo(observedAt);
        await Assert.That(() => BundesligaClubEloPublication.BuildSourceBacked(selection, cycle, observation, bytes)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Csv_v2_public_apis_reject_mismatched_selected_provenance_and_retained_v1_selection()
    {
        var observedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var cycle = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var selected = NetworkSnapshot(new Uri("https://example.test/elo.csv"), new DateOnly(2026, 9, 4), observedAt);
        var observation = CsvObservation(cycle, selected, observedAt, System.Text.Encoding.UTF8.GetBytes("csv"), fractional: false);
        var invalid = new[]
        {
            new BundesligaClubEloSelection(NetworkSnapshot(new Uri("https://other.test/elo.csv"), new DateOnly(2026, 9, 4), observedAt), BundesligaClubEloSelectionDisposition.NetworkAccepted, []),
            new BundesligaClubEloSelection(NetworkSnapshot(new Uri("https://example.test/elo.csv"), new DateOnly(2026, 9, 3), observedAt), BundesligaClubEloSelectionDisposition.NetworkAccepted, []),
            new BundesligaClubEloSelection(NetworkSnapshot(new Uri("https://example.test/elo.csv"), new DateOnly(2026, 9, 4), observedAt.AddSeconds(1)), BundesligaClubEloSelectionDisposition.NetworkAccepted, []),
            new BundesligaClubEloSelection(NetworkSnapshot(new Uri("https://example.test/elo.csv"), new DateOnly(2026, 9, 4), observedAt, BundesligaClubEloSnapshotOrigin.LaunchSeed), BundesligaClubEloSelectionDisposition.NetworkDisabled, ["UNATTENDED_NETWORK_USE_NOT_APPROVED"])
        };

        foreach (var selection in invalid)
        {
            await Assert.That(() => BundesligaClubEloPublication.CreateHistoricalCsvV2Evidence(selection, cycle, observation, System.Text.Encoding.UTF8.GetBytes("csv"))).Throws<InvalidDataException>();
            await Assert.That(() => BundesligaClubEloPublication.BuildSourceBacked(selection, cycle, observation, System.Text.Encoding.UTF8.GetBytes("csv"))).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Integral_csv_v2_binds_to_exact_headed_lkg_and_rejects_mixed_or_reordered_families()
    {
        var observedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var cycle = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var snapshot = NetworkSnapshot(new Uri("https://example.test/elo.csv"), new DateOnly(2026, 9, 4), observedAt);
        var selection = new BundesligaClubEloSelection(snapshot, BundesligaClubEloSelectionDisposition.NetworkAccepted, []);
        var bytes = System.Text.Encoding.UTF8.GetBytes("csv");
        var build = BundesligaClubEloPublication.BuildSourceBacked(selection, cycle, CsvObservation(cycle, snapshot, observedAt, bytes, fractional: false), bytes);

        var reconstructed = BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build));
        var rejectionCases = new[]
        {
            ("mixed-shape", RequireChanged("mixed-shape", build.MetadataJson, build.MetadataJson.Replace("\"source_rows\":", "\"sourceDescriptor\":{},\"source_rows\":", StringComparison.Ordinal))),
            ("reordered-cycle-attempt", RequireChanged("reordered-cycle-attempt", build.MetadataJson, ReorderCycleAndAttempt(build.MetadataJson))),
            ("origin", ReplaceRequired("origin", build.MetadataJson, "\"NetworkCandidate\"", "\"LaunchSeed\"")),
            ("disposition", ReplaceRequired("disposition", build.MetadataJson, "\"NetworkAccepted\"", "\"NetworkDisabled\"")),
            ("timestamp", ReplaceRequired("timestamp", build.MetadataJson, "\"collected_at\":\"2026-09-04T12:00:00Z\"", "\"collected_at\":\"2026-09-04T12:00:01Z\"")),
            ("url", ReplaceRequired("url", build.MetadataJson, "\"source_url\":\"https://example.test/elo.csv\"", "\"source_url\":\"http://example.test/elo.csv\"")),
            ("evidence-kind", ReplaceRequired("evidence-kind", build.MetadataJson, "\"kind\":\"ProviderCsvField\"", "\"kind\":\"Unknown\"")),
            ("evidence-field", ReplaceRequired("evidence-field", build.MetadataJson, "\"field\":\"From\"", "\"field\":null")),
            ("evidence-recipe", ReplaceRequired("evidence-recipe", build.MetadataJson, "\"recipe_id\":\"recipe/v1\"", "\"recipe_id\":\"\"")),
            ("provider-name-mismatch", ReplaceRequired("provider-name-mismatch", build.MetadataJson, "\"provider_name\":\"Leverkusen\"", "\"provider_name\":\"Gladbach\"")),
            ("retained-v1-tuple", ReplaceRequired("retained-v1-origin", ReplaceRequired("retained-v1-disposition", ReplaceRequired("retained-v1-diagnostics", build.MetadataJson, "\"selection_diagnostics\":[]", "\"selection_diagnostics\":[\"UNATTENDED_NETWORK_USE_NOT_APPROVED\"]"), "\"NetworkAccepted\"", "\"NetworkDisabled\""), "\"NetworkCandidate\"", "\"LaunchSeed\""))
        };
        var numericCases = new[]
        {
            ("noncanonical-1500.0", ReplaceRequired("noncanonical-1500.0", build.MetadataJson, "\"elo\":1500}", "\"elo\":1500.0}"), false),
            ("decimal-int64-overflow", ReplaceRequired("decimal-int64-overflow", build.MetadataJson, "\"elo\":1500}", "\"elo\":9223372036854775808}"), false),
            ("int64-max-headed-non-int32", ReplaceRequired("int64-max-headed-non-int32", build.MetadataJson, "\"elo\":1500}", "\"elo\":9223372036854775807}"), true),
            ("canonical-finite-double-headed-binding", ReplaceRequired("canonical-finite-double-headed-binding", build.MetadataJson, "\"elo\":1500}", "\"elo\":1E+20}"), true)
        };

        await Assert.That(reconstructed.Entries.Count).IsEqualTo(BundesligaTeamManifest.ExpectedTeamCount);
        foreach (var rejection in rejectionCases) RejectMetadata(rejection.Item1, rejection.Item2);
        foreach (var numeric in numericCases)
        {
            if (!numeric.Item3)
            {
                RejectMetadata(numeric.Item1, numeric.Item2);
                continue;
            }

            await Assert.That(BundesligaClubEloPublication.ParseMetadata(numeric.Item2).RatedAt).IsEqualTo(snapshot.RatedAt);
            RejectHeadedReconstruction(numeric.Item1, build with { MetadataJson = numeric.Item2 });
        }
    }

    [Test]
    public async Task Html_v2_round_trips_only_the_complete_descriptor_and_selected_payload_identity()
    {
        var observedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var cycle = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var snapshot = NetworkSnapshot(new Uri("https://clubelo.com/GER"), new DateOnly(2026, 9, 4), observedAt);
        var selection = new BundesligaClubEloSelection(snapshot, BundesligaClubEloSelectionDisposition.NetworkAccepted, []);
        var bytes = System.Text.Encoding.UTF8.GetBytes("html");
        var build = BundesligaClubEloPublication.BuildSourceBacked(selection, cycle, HtmlObservation(cycle, snapshot, observedAt, bytes), bytes);
        var metadata = BundesligaClubEloPublication.ParseMetadata(build.MetadataJson);
        var hostile = new[]
        {
            build.MetadataJson.Replace("\"raw_byte_length\":4", "\"raw_byte_length\":4.0", StringComparison.Ordinal),
            build.MetadataJson.Replace("\"path\":\"club-elo/source.html\"", "\"path\":\"club-elo/source.csv\"", StringComparison.Ordinal),
            build.MetadataJson.Replace("\"cycle_id\":\"gha:1:2\"", "\"cycle_id\":\"gha:1:3\"", StringComparison.Ordinal),
            build.MetadataJson.Replace("\"sourceDescriptorSha256\":", "\"sourceDescriptorSha256X\":", StringComparison.Ordinal)
        };

        await Assert.That(metadata.RatedAt).IsEqualTo(snapshot.RatedAt);
        await Assert.That(BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build)).Entries.Count).IsEqualTo(18);
        foreach (var value in hostile) await Assert.That(() => BundesligaClubEloPublication.ParseMetadata(value)).Throws<InvalidDataException>();
    }
    [Test]
    public async Task Renderer_uses_exact_csv_contract_and_deterministic_elo_tie_order()
    {
        var entries = BundesligaClubEloSeed.Default.Entries
            .Select(entry => entry.Team.TeamSlug is "fcb" or "b04"
                ? entry with { Elo = 2000, GlobalRank = entry.Team.TeamSlug == "fcb" ? 4 : 2 }
                : entry)
            .OrderBy(entry => entry.Team.TeamSlug, StringComparer.Ordinal)
            .ToArray();
        var snapshot = BundesligaClubEloSnapshot.Create(
            entries, new DateOnly(2026, 8, 14), new DateTimeOffset(2026, 8, 16, 10, 44, 16, TimeSpan.Zero),
            new Uri("https://clubelo.com/GER"), BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            snapshot, BundesligaClubEloSelectionDisposition.NetworkDisabled, ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));

        var aggregate = build.Documents.Single(document => document.Kind == DocumentPublicationKind.Kpi).Content;
        var lines = aggregate.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        await Assert.That(lines[0]).IsEqualTo(BundesligaClubEloPublication.CsvHeader);
        await Assert.That(lines.Length).IsEqualTo(19);
        await Assert.That(lines[1]).StartsWith("2,1,Leverkusen,2000,2026-08-14", StringComparison.Ordinal);
        await Assert.That(lines[2]).StartsWith("4,2,Bayern,2000,2026-08-14", StringComparison.Ordinal);
        await Assert.That(aggregate).EndsWith("\r\n", StringComparison.Ordinal);
        await Assert.That(aggregate.Replace("\r\n", string.Empty, StringComparison.Ordinal)).DoesNotContain("\r").And.DoesNotContain("\n");
        await Assert.That(build.Documents.Count).IsEqualTo(19);
        await Assert.That(build.Documents.Count(document => document.Kind == DocumentPublicationKind.Context)).IsEqualTo(18);
    }

    [Test]
    public async Task Exact_headed_documents_and_metadata_reconstruct_last_known_good()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            BundesligaClubEloSeed.Default, BundesligaClubEloSelectionDisposition.NetworkDisabled,
            ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        var loaded = CreateLoaded(build);

        var reconstructed = BundesligaClubEloPublication.ReconstructLastKnownGood(loaded);

        await Assert.That(reconstructed.Origin).IsEqualTo(BundesligaClubEloSnapshotOrigin.LastKnownGood);
        await Assert.That(reconstructed.Entries).IsEquivalentTo(BundesligaClubEloSeed.Default.Entries);
        await Assert.That(reconstructed.RatedAt).IsEqualTo(BundesligaClubEloSeed.Default.RatedAt);
    }

    [Test]
    public async Task Reconstruction_rejects_lexically_noncanonical_headed_payloads()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            BundesligaClubEloSeed.Default, BundesligaClubEloSelectionDisposition.NetworkDisabled,
            ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        var leadingZero = build.Documents.Select(document => document.Name == "club-elo-b04.csv"
            ? document with { Content = document.Content.Replace("16,", "016,", StringComparison.Ordinal) }
            : document).ToArray();
        var lf = build.Documents.Select(document => document.Name == "club-elo-b04.csv"
            ? document with { Content = document.Content.Replace("\r\n", "\n", StringComparison.Ordinal) }
            : document).ToArray();
        var reorderedAggregate = build.Documents.Select(document => document.Kind == DocumentPublicationKind.Kpi
            ? document with { Content = SwapAggregateRows(document.Content) }
            : document).ToArray();

        var leadingZeroFailure = CaptureInvalid(() => BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build, leadingZero)));
        var lfFailure = CaptureInvalid(() => BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build, lf)));
        var aggregateFailure = CaptureInvalid(() => BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build, reorderedAggregate)));

        await Assert.That(leadingZeroFailure.Message).Contains("exact canonical single-row CSV");
        await Assert.That(lfFailure.Message).Contains("strict CSV line endings");
        await Assert.That(aggregateFailure.Message).Contains("exact canonical aggregate CSV");
    }

    [Test]
    public async Task Metadata_parser_rejects_noncanonical_enum_diagnostics_and_semantic_contradictions()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            BundesligaClubEloSeed.Default, BundesligaClubEloSelectionDisposition.NetworkDisabled,
            ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        var invalid = new[]
        {
            build.MetadataJson.Replace("\"LaunchSeed\"", "\"0\"", StringComparison.Ordinal),
            build.MetadataJson.Replace("\"LaunchSeed\"", "\"launchseed\"", StringComparison.Ordinal),
            build.MetadataJson.Replace("\"NetworkDisabled\"", "\"NetworkAccepted\"", StringComparison.Ordinal),
            build.MetadataJson.Replace("[\"UNATTENDED_NETWORK_USE_NOT_APPROVED\"]", "[\"B\",\"A\"]", StringComparison.Ordinal),
            build.MetadataJson.Replace("https://clubelo.com/GER", "http://clubelo.com/GER", StringComparison.Ordinal)
        };

        foreach (var metadata in invalid)
        {
            await Assert.That(() => BundesligaClubEloPublication.ParseMetadata(metadata)).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Build_rejects_selection_combinations_that_cannot_be_reconstructed()
    {
        var invalid = new[]
        {
            new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
                BundesligaClubEloSelectionDisposition.NetworkAccepted, []),
            new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
                BundesligaClubEloSelectionDisposition.NetworkDisabled, []),
            new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
                BundesligaClubEloSelectionDisposition.NetworkDisabled, ["OTHER"]),
            new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
                BundesligaClubEloSelectionDisposition.NetworkCandidateRejected, ["B", "A"])
        };

        foreach (var selection in invalid)
        {
            await Assert.That(() => BundesligaClubEloPublication.Build(selection)).Throws<InvalidDataException>();
        }
    }

    private static LoadedDocumentPublication CreateLoaded(
        BundesligaClubEloPublicationBuild build,
        IReadOnlyList<DocumentPublicationPayload>? documents = null)
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, "ehonda-dev-buli-2627", BundesligaDocumentPublication.ClubEloPublicationSet);
        var ordered = DocumentPublicationContract.ValidateAndOrder(documents ?? build.Documents);
        var snapshot = new DocumentPublicationSnapshot(
            scope.Competition, scope.CommunityContext, scope.PublicationSet,
            DocumentPublicationContract.ComputeSnapshotId(ordered), null, DateTimeOffset.UtcNow, build.MetadataJson,
            ordered.Select((document, version) => new DocumentPublicationEntry(
                document.Kind, document.Name, version, DocumentPublicationContract.ComputeContentSha256(document.Content))));
        return new LoadedDocumentPublication(snapshot, ordered.Select((document, version) => new PublishedDocument(
            scope.Competition, scope.CommunityContext, scope.PublicationSet, document.Kind, document.Name, version,
            document.Content, document.Description, DateTimeOffset.UtcNow)));
    }

    private static BundesligaClubEloSnapshot NetworkSnapshot(Uri sourceUrl, DateOnly ratedAt, DateTimeOffset observedAt,
        BundesligaClubEloSnapshotOrigin origin = BundesligaClubEloSnapshotOrigin.NetworkCandidate) =>
        BundesligaClubEloSnapshot.Create(BundesligaTeamManifest.Default.Entries
            .OrderBy(team => team.TeamSlug, StringComparer.Ordinal)
            .Select((team, index) => new BundesligaClubEloEntry(team, index + 1, 1500 + index)).ToArray(),
            ratedAt, observedAt, sourceUrl, origin);

    private static BundesligaContextSourceObservation CsvObservation(BundesligaContextSourceCycleIdentity cycle,
        BundesligaClubEloSnapshot snapshot, DateTimeOffset observedAt, byte[] bytes, bool fractional)
    {
        var rows = snapshot.Entries.OrderBy(entry => entry.Team.TeamSlug, StringComparer.Ordinal)
            .Select((entry, index) => new { teamSlug = entry.Team.TeamSlug, providerName = entry.Team.ClubEloName,
                globalRank = entry.GlobalRank, elo = fractional && index == 0 ? 1500.25 : entry.Elo }).ToArray();
        var descriptor = System.Text.Json.JsonSerializer.Serialize(new
        {
            contract = "club-elo-direct-csv-descriptor/v1", sourceUrl = snapshot.SourceUrl.AbsoluteUri,
            rawSha256 = BundesligaContextSourceHashing.Sha256(bytes), rawByteLength = (long)bytes.Length,
            csvHeader = "Rank,Club,Country,Level,Elo,From,To", providerRatedAt = snapshot.RatedAt.ToString("yyyy-MM-dd"),
            providerDateEvidence = new { kind = "ProviderCsvField", recipeId = "recipe/v1", field = "From", rawValue = snapshot.RatedAt.ToString("yyyy-MM-dd"), ratedAt = snapshot.RatedAt.ToString("yyyy-MM-dd") },
            nameMappingContract = "map/v1", nameMappingSha256 = new string('b', 64), sourceRows = rows, evaluation = "Eligible"
        });
        return new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo,
            BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), observedAt,
            BundesligaContextSourceDisposition.ArtifactCaptured, descriptor,
            new BundesligaContextSourcePayload("club-elo/source.csv", bytes.Length, BundesligaContextSourceHashing.Sha256(bytes)), []);
    }

    private static BundesligaContextSourceObservation HtmlObservation(BundesligaContextSourceCycleIdentity cycle,
        BundesligaClubEloSnapshot snapshot, DateTimeOffset observedAt, byte[] bytes)
    {
        var mapping = new[] { ("b04", "/Leverkusen", "Leverkusen"), ("bmg", "/Gladbach", "Gladbach"), ("bvb", "/Dortmund", "Dortmund"), ("fca", "/Augsburg", "Augsburg"), ("fcb", "/Bayern", "Bayern München"), ("fck", "/Koeln", "Köln"), ("fcu", "/UnionBerlin", "Union Berlin"), ("hsv", "/Hamburg", "Hamburg"), ("m05", "/Mainz", "Mainz"), ("rbl", "/RBLeipzig", "RB Leipzig"), ("s04", "/Schalke", "Schalke"), ("scf", "/Freiburg", "Freiburg"), ("scp", "/Paderborn", "Paderborn"), ("sge", "/Frankfurt", "Frankfurt"), ("sve", "/Elversberg", "Elversberg"), ("svw", "/Werder", "Werder"), ("tsg", "/Hoffenheim", "Hoffenheim"), ("vfb", "/Stuttgart", "Stuttgart") };
        var rows = snapshot.Entries.OrderBy(entry => entry.Team.TeamSlug, StringComparer.Ordinal).Zip(mapping)
            .Select(value => new { teamSlug = value.First.Team.TeamSlug, providerRoute = value.Second.Item2,
                providerDisplayName = value.Second.Item3, globalRank = value.First.GlobalRank, elo = value.First.Elo }).ToArray();
        var descriptor = System.Text.Json.JsonSerializer.Serialize(new
        {
            contract = "club-elo-official-html-descriptor/v1", sourceUrl = "https://clubelo.com/GER",
            response = new { statusCode = 200, finalUrl = "https://clubelo.com/GER", redirectCount = 0, redirectLocation = (string?)null, mediaType = "text/html", charset = "utf-8", contentEncodings = Array.Empty<string>(), declaredContentLength = (long)bytes.Length },
            rawSha256 = BundesligaContextSourceHashing.Sha256(bytes), rawByteLength = (long)bytes.Length,
            parserContract = "club-elo-official-html-parser/v1", displayedDate = snapshot.RatedAt.ToString("yyyy-MM-dd"),
            providerDateEvidence = new { kind = "OfficialHtmlHeadingLink", recipeId = "club-elo-official-html-displayed-date/v1", field = "h1>a[href]", rawValue = snapshot.RatedAt.ToString("yyyy-MM-dd"), ratedAt = snapshot.RatedAt.ToString("yyyy-MM-dd") },
            tableContract = "club-elo-official-html-table/v1", tableHeader = new[] { "Club", "Elo", "+/-", "Golo" },
            nameMappingContract = "bundesliga-2026-27-club-elo-name-map/v1", nameMappingSha256 = BundesligaContextSourceDescriptorContract.ClubEloHtmlNameMappingSha256,
            sourceRows = rows, evaluation = "Eligible"
        });
        return new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo,
            BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), observedAt,
            BundesligaContextSourceDisposition.ArtifactCaptured, descriptor,
            new BundesligaContextSourcePayload("club-elo/source.html", bytes.Length, BundesligaContextSourceHashing.Sha256(bytes)), []);
    }

    private static string SwapAggregateRows(string content)
    {
        var lines = content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        (lines[1], lines[2]) = (lines[2], lines[1]);
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string ReorderCycleAndAttempt(string json)
    {
        var cycleStart = json.IndexOf("\"cycle_id\":", StringComparison.Ordinal);
        var attemptStart = json.IndexOf("\"attempt_id\":", StringComparison.Ordinal);
        var attemptEnd = json.IndexOf(',', attemptStart) + 1;
        var cycle = json.Substring(cycleStart, attemptStart - cycleStart);
        var attempt = json.Substring(attemptStart, attemptEnd - attemptStart);
        return json.Remove(cycleStart, attemptEnd - cycleStart).Insert(cycleStart, attempt + cycle);
    }

    private static string ReplaceRequired(string caseName, string input, string oldValue, string newValue) =>
        RequireChanged(caseName, input, input.Replace(oldValue, newValue, StringComparison.Ordinal));

    private static string RequireChanged(string caseName, string input, string result)
    {
        if (input == result) throw new InvalidOperationException($"{caseName} replacement was a no-op.");
        return result;
    }

    private static void RejectMetadata(string caseName, string metadata)
    {
        try
        {
            _ = BundesligaClubEloPublication.ParseMetadata(metadata);
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException($"{caseName} was unexpectedly accepted by metadata parsing.");
    }

    private static void RejectHeadedReconstruction(string caseName, BundesligaClubEloPublicationBuild build)
    {
        try
        {
            _ = BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build));
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException($"{caseName} was unexpectedly accepted by headed LKG reconstruction.");
    }

    private static InvalidDataException CaptureInvalid(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected InvalidDataException.");
    }
}
