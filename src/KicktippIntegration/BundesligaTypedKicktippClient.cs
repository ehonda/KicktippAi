using System.Globalization;
using System.Net;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using EHonda.KicktippAi.Core;
using NodaTime;
using NodaTime.Text;
using Microsoft.Extensions.Logging;

namespace KicktippIntegration;

public sealed class BundesligaTypedKicktippClient : IBundesligaTypedKicktippClient, IDisposable
{
    private static readonly DateTimeZone BerlinTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
    private readonly HttpClient _httpClient;
    private readonly ILogger<BundesligaTypedKicktippClient> _logger;
    private readonly IBrowsingContext _browsingContext;
    private readonly Func<Uri, bool> _finalAuthorityValidator;

    public BundesligaTypedKicktippClient(
        HttpClient httpClient,
        ILogger<BundesligaTypedKicktippClient> logger)
        : this(httpClient, logger, IsCanonicalKicktippAuthority)
    {
    }

    internal BundesligaTypedKicktippClient(
        HttpClient httpClient,
        ILogger<BundesligaTypedKicktippClient> logger,
        Func<Uri, bool> finalAuthorityValidator)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _finalAuthorityValidator = finalAuthorityValidator ?? throw new ArgumentNullException(nameof(finalAuthorityValidator));
        _browsingContext = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
    }

    public async Task<IReadOnlyList<TypedMatchSnapshot>> GetTypedOpenMatchSnapshotsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedMatchInventoryScope scope,
        CancellationToken cancellationToken = default)
    {
        ValidateTypedAuthority(authority);
        ArgumentNullException.ThrowIfNull(scope);
        ValidatePostingKeys(authority, scope.Items.Select(item => item.Key));
        var loaded = await LoadTypedMatchInventoryAsync(authority, scope.Items, cancellationToken);
        return loaded.Rows.Select(row => row.Snapshot).ToImmutableArray();
    }

    public async Task<IReadOnlyList<BundesligaTypedPlacedMatchPrediction>> GetTypedPlacedMatchPredictionsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedMatchReadScope scope,
        CancellationToken cancellationToken = default)
    {
        ValidateTypedAuthority(authority);
        ArgumentNullException.ThrowIfNull(scope);
        ValidatePostingKeys(authority, scope.Items.Select(item => item.Snapshot.Key));
        var loaded = await LoadTypedMatchInventoryAsync(
            authority, scope.Items.Select(item => item.SourceIdentity).ToArray(), cancellationToken);
        RequireExpectedMatchSnapshots(scope, loaded.Rows);
        return loaded.Rows.Select(row => new BundesligaTypedPlacedMatchPrediction(
            row.Snapshot, ParseExactMatchPrediction(row.HomeInput, row.AwayInput))).ToImmutableArray();
    }

    public async Task<IReadOnlyList<BundesligaTypedPlacedMatchPrediction>> PlaceTypedMatchPredictionsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedMatchPlacementBatch predictions,
        bool overrideExisting,
        CancellationToken cancellationToken = default)
    {
        ValidateTypedAuthority(authority);
        ArgumentNullException.ThrowIfNull(predictions);
        ValidatePostingKeys(authority, predictions.Scope.Items.Select(item => item.Snapshot.Key));
        var loaded = await LoadTypedMatchInventoryAsync(
            authority,
            predictions.Scope.Items.Select(item => item.SourceIdentity).ToArray(),
            cancellationToken);
        RequireExpectedMatchSnapshots(predictions.Scope, loaded.Rows);

        var submissions = predictions.Predictions.ToDictionary(
            item => item.Snapshot.Key.KicktippItemId, StringComparer.Ordinal);
        var expectedPostState = new Dictionary<string, BetPrediction?>(StringComparer.Ordinal);
        var formData = CopyExactHiddenInputs(loaded.Form);
        foreach (var row in loaded.Rows)
        {
            var existing = ParseExactMatchPrediction(row.HomeInput, row.AwayInput);
            if (submissions.TryGetValue(row.Snapshot.Key.KicktippItemId, out var submission))
            {
                if (!overrideExisting && existing is not null && existing != submission.Prediction)
                {
                    throw new KicktippTypedAuthorityException(
                        $"Fixture '{row.Snapshot.Key.KicktippItemId}' already has a different exact prediction.");
                }
                expectedPostState.Add(row.Snapshot.Key.KicktippItemId, submission.Prediction);
                AddExactFormValue(formData, row.HomeInput.Name, submission.Prediction.HomeGoals.ToString(CultureInfo.InvariantCulture));
                AddExactFormValue(formData, row.AwayInput.Name, submission.Prediction.AwayGoals.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                expectedPostState.Add(row.Snapshot.Key.KicktippItemId, existing);
                AddExactFormValue(formData, row.HomeInput.Name, row.HomeInput.Value ?? string.Empty);
                AddExactFormValue(formData, row.AwayInput.Name, row.AwayInput.Value ?? string.Empty);
            }
        }

        if (submissions.Count > 0)
        {
            AddExactSubmitControl(loaded.Form, formData);
            await PostExactTypedFormAsync(
                authority.PostingCommunity, loaded.Form, formData, cancellationToken);
        }

        var readback = await GetTypedPlacedMatchPredictionsAsync(
            authority, predictions.Scope, cancellationToken);
        foreach (var expected in expectedPostState)
        {
            var matches = readback.Where(item =>
                string.Equals(item.Snapshot.Key.KicktippItemId, expected.Key, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || matches[0].Prediction != expected.Value)
            {
                throw new KicktippTypedAuthorityException(
                    $"Exact match readback changed or omitted fixture '{expected.Key}'.");
            }
        }
        return readback;
    }

    public async Task<IReadOnlyList<TypedBonusSnapshot>> GetTypedOpenBonusSnapshotsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedBonusInventoryScope scope,
        CancellationToken cancellationToken = default)
    {
        ValidateTypedAuthority(authority);
        ArgumentNullException.ThrowIfNull(scope);
        ValidatePostingKeys(authority, scope.Items.Select(item => item.Key));
        var loaded = await LoadTypedBonusInventoryAsync(authority, scope.Items, cancellationToken);
        return loaded.Rows.Select(row => row.Snapshot).ToImmutableArray();
    }

    public async Task<IReadOnlyList<BundesligaTypedPlacedBonusPrediction>> GetTypedPlacedBonusPredictionsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedBonusReadScope scope,
        CancellationToken cancellationToken = default)
    {
        ValidateTypedAuthority(authority);
        ArgumentNullException.ThrowIfNull(scope);
        ValidatePostingKeys(authority, scope.Items.Select(item => item.Snapshot.Key));
        var loaded = await LoadTypedBonusInventoryAsync(
            authority, scope.Items.Select(item => item.SourceIdentity).ToArray(), cancellationToken);
        RequireExpectedBonusSnapshots(scope, loaded.Rows);
        return loaded.Rows.Select(row => new BundesligaTypedPlacedBonusPrediction(
            row.Snapshot, ParseExactBonusSelection(row))).ToImmutableArray();
    }

    public async Task<IReadOnlyList<BundesligaTypedPlacedBonusPrediction>> PlaceTypedBonusPredictionsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedBonusPlacementBatch predictions,
        bool overrideExisting,
        CancellationToken cancellationToken = default)
    {
        ValidateTypedAuthority(authority);
        ArgumentNullException.ThrowIfNull(predictions);
        ValidatePostingKeys(authority, predictions.Scope.Items.Select(item => item.Snapshot.Key));
        var loaded = await LoadTypedBonusInventoryAsync(
            authority,
            predictions.Scope.Items.Select(item => item.SourceIdentity).ToArray(),
            cancellationToken);
        RequireExpectedBonusSnapshots(predictions.Scope, loaded.Rows);

        var submissions = predictions.Predictions.ToDictionary(
            item => item.Snapshot.Key.KicktippItemId, StringComparer.Ordinal);
        var expectedPostState = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
        var expectedMixedMatchState = CaptureExactMixedMatchControlState(loaded.Form);
        var formData = CopyExactHiddenInputs(loaded.Form);
        foreach (var control in expectedMixedMatchState)
        {
            AddExactFormValue(formData, control.Key, control.Value);
        }
        foreach (var row in loaded.Rows)
        {
            var existing = ParseExactBonusSelection(row);
            if (submissions.TryGetValue(row.Snapshot.Key.KicktippItemId, out var submission))
            {
                if (!overrideExisting && existing.Count > 0
                    && !existing.SequenceEqual(submission.SelectedOptionIds, StringComparer.Ordinal))
                {
                    throw new KicktippTypedAuthorityException(
                        $"Bonus item '{row.Snapshot.Key.KicktippItemId}' already has a different exact prediction.");
                }
                expectedPostState.Add(
                    row.Snapshot.Key.KicktippItemId,
                    submission.SelectedOptionIds.ToImmutableArray());
                for (var index = 0; index < row.Selects.Count; index++)
                {
                    AddExactFormValue(
                        formData,
                        row.Selects[index].Name,
                        index < submission.SelectedOptionIds.Count
                            ? submission.SelectedOptionIds[index]
                            : "-1");
                }
            }
            else
            {
                expectedPostState.Add(row.Snapshot.Key.KicktippItemId, existing.ToImmutableArray());
                foreach (var select in row.Selects)
                {
                    AddExactFormValue(
                        formData,
                        select.Name,
                        select.Value ?? throw new KicktippTypedAuthorityException(
                            "Typed bonus select has no exact current value."));
                }
            }
        }

        if (submissions.Count > 0)
        {
            AddExactSubmitControl(loaded.Form, formData);
            await PostExactTypedFormAsync(
                authority.PostingCommunity, loaded.Form, formData, cancellationToken);
        }

        var readbackInventory = await LoadTypedBonusInventoryAsync(
            authority,
            predictions.Scope.Items.Select(item => item.SourceIdentity).ToArray(),
            cancellationToken);
        RequireExpectedBonusSnapshots(predictions.Scope, readbackInventory.Rows);
        RequireExactMixedMatchPostState(
            expectedMixedMatchState,
            CaptureExactMixedMatchControlState(readbackInventory.Form));
        var readback = readbackInventory.Rows.Select(row => new BundesligaTypedPlacedBonusPrediction(
            row.Snapshot, ParseExactBonusSelection(row))).ToImmutableArray();
        foreach (var expected in expectedPostState)
        {
            var matches = readback.Where(item =>
                string.Equals(item.Snapshot.Key.KicktippItemId, expected.Key, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || !matches[0].SelectedOptionIds.SequenceEqual(
                    expected.Value, StringComparer.Ordinal))
            {
                throw new KicktippTypedAuthorityException(
                    $"Exact bonus readback changed or omitted item '{expected.Key}'.");
            }
        }
        return readback;
    }

    private async Task<TypedMatchInventory> LoadTypedMatchInventoryAsync(
        BundesligaPredictionAuthority authority,
        IReadOnlyList<BundesligaTypedMatchSourceIdentity> expected,
        CancellationToken cancellationToken)
    {
        var document = await GetExactTypedDocumentAsync(
            authority.PostingCommunity, "/tippabgabe", [], cancellationToken);
        var form = RequireExactTypedForm(document, authority.PostingCommunity);
        var bodies = document.QuerySelectorAll("#tippabgabeSpiele > tbody").ToArray();
        if (bodies.Length != 1)
        {
            if (bodies.Length == 0 && expected.Count == 0)
            {
                return new TypedMatchInventory(form, []);
            }
            throw new KicktippTypedAuthorityException("Typed match source has no single exact match table.");
        }

        var sourceRows = ParseExactTypedMatchRows(bodies[0]);
        RequireExactItemSet(
            expected.Select(item => item.Key.KicktippItemId),
            sourceRows.Select(item => item.KicktippFixtureId),
            "match form");
        if (expected.Count == 0)
        {
            return new TypedMatchInventory(form, []);
        }

        var matchdays = expected.Select(item => item.Matchday).Distinct().ToArray();
        if (matchdays.Length != 1 || !TryExtractMatchdayFromPage(document, out var displayedMatchday)
            || displayedMatchday != matchdays[0])
        {
            throw new KicktippTypedAuthorityException("Typed match form has an ambiguous or drifted matchday.");
        }

        var outcomeDocument = await GetExactTypedDocumentAsync(
            authority.PostingCommunity,
            "/tippuebersicht",
            [("spieltagIndex", matchdays[0].ToString(CultureInfo.InvariantCulture))],
            cancellationToken);
        var outcomeReferences = ParseExactTypedOutcomeReferences(
            outcomeDocument, authority.PostingCommunity, matchdays[0]);
        var outcomeById = outcomeReferences.GroupBy(item => item.KicktippFixtureId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var expectedById = expected.ToDictionary(item => item.Key.KicktippItemId, StringComparer.Ordinal);
        var result = new List<TypedMatchRow>(expected.Count);
        foreach (var sourceRow in sourceRows.OrderBy(item => item.KicktippFixtureId, StringComparer.Ordinal))
        {
            var identity = expectedById[sourceRow.KicktippFixtureId];
            if (!string.Equals(sourceRow.HomeTeam, identity.HomeTeam, StringComparison.Ordinal)
                || !string.Equals(sourceRow.AwayTeam, identity.AwayTeam, StringComparison.Ordinal)
                || sourceRow.IsCancelled || sourceRow.IsInherited
                || sourceRow.ScheduledInstant is null)
            {
                throw new KicktippTypedAuthorityException(
                    $"Fixture '{sourceRow.KicktippFixtureId}' has cancelled, inherited, empty, or drifted source evidence.");
            }
            if (!outcomeById.TryGetValue(sourceRow.KicktippFixtureId, out var references)
                || references.Length != 1)
            {
                throw new KicktippTypedAuthorityException(
                    $"Fixture '{sourceRow.KicktippFixtureId}' has missing or duplicate ID-bearing detail evidence.");
            }

            var detail = await GetExactTypedFixtureDetailsAsync(
                authority.PostingCommunity, matchdays[0], references[0], cancellationToken);
            if (!string.Equals(detail.Competition, identity.SourceCompetitionLabel, StringComparison.Ordinal)
                || !string.Equals(detail.RoundName, identity.ExactRound, StringComparison.Ordinal))
            {
                throw new KicktippTypedAuthorityException(
                    $"Fixture '{sourceRow.KicktippFixtureId}' structured identity drifted.");
            }

            BundesligaResolvedScheduledInstant scheduled;
            try
            {
                scheduled = BundesligaScheduledInstantResolver.Resolve(
                    new BundesligaFixtureScheduleEvidence(
                        sourceRow.KicktippFixtureId,
                        sourceRow.IsCancelled,
                        sourceRow.ScheduledInstant is null
                            ? null
                            : InstantPattern.ExtendedIso.Format(sourceRow.ScheduledInstant.Value),
                        sourceRow.IsInherited),
                    detail.TerminValues.Select(value => new BundesligaFixtureDetailScheduleEvidence(
                        sourceRow.KicktippFixtureId, value)));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new KicktippTypedAuthorityException(
                    $"Fixture '{sourceRow.KicktippFixtureId}' scheduled-instant evidence is not authoritative.",
                    exception);
            }

            TypedMatchSnapshot snapshot;
            try
            {
                snapshot = TypedMatchSnapshot.Create(
                    identity.Key,
                    identity.Subcompetition,
                    identity.ExactRound,
                    identity.ResultBasis,
                    identity.HomeTeam,
                    identity.AwayTeam,
                    identity.Matchday,
                    scheduled);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new KicktippTypedAuthorityException(
                    $"Fixture '{sourceRow.KicktippFixtureId}' could not form a typed snapshot.", exception);
            }
            result.Add(new TypedMatchRow(snapshot, sourceRow.HomeInput, sourceRow.AwayInput));
        }
        return new TypedMatchInventory(form, result);
    }

    private async Task<TypedBonusInventory> LoadTypedBonusInventoryAsync(
        BundesligaPredictionAuthority authority,
        IReadOnlyList<BundesligaTypedBonusSourceIdentity> expected,
        CancellationToken cancellationToken)
    {
        var document = await GetExactTypedDocumentAsync(
            authority.PostingCommunity, "/tippabgabe", [("bonus", "true")], cancellationToken);
        var form = RequireExactTypedForm(document, authority.PostingCommunity);
        var bodies = document.QuerySelectorAll("#tippabgabeFragen > tbody").ToArray();
        if (bodies.Length != 1)
        {
            if (bodies.Length == 0 && expected.Count == 0)
            {
                return new TypedBonusInventory(form, []);
            }
            throw new KicktippTypedAuthorityException("Typed bonus source has no single exact question table.");
        }

        var parsed = ParseExactTypedBonusRows(bodies[0]);
        RequireExactItemSet(
            expected.Select(item => item.Key.KicktippItemId),
            parsed.Select(item => item.KicktippQuestionId),
            "bonus form");
        var expectedById = expected.ToDictionary(item => item.Key.KicktippItemId, StringComparer.Ordinal);
        var result = new List<TypedBonusRow>(parsed.Count);
        foreach (var row in parsed.OrderBy(item => item.KicktippQuestionId, StringComparer.Ordinal))
        {
            var identity = expectedById[row.KicktippQuestionId];
            TypedBonusSnapshot snapshot;
            try
            {
                snapshot = TypedBonusSnapshot.Create(
                    identity.Key,
                    identity.Subcompetition,
                    row.Text,
                    row.Deadline,
                    row.Selects.Count,
                    row.Options.Select(option => new TypedBonusSnapshotOption(option.Id, option.Text)));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new KicktippTypedAuthorityException(
                    $"Bonus item '{row.KicktippQuestionId}' could not form a typed snapshot.", exception);
            }
            result.Add(new TypedBonusRow(snapshot, row.Selects));
        }
        return new TypedBonusInventory(form, result);
    }

    private async Task<IDocument> GetExactTypedDocumentAsync(
        string community,
        string path,
        IReadOnlyList<(string Key, string Value)> query,
        CancellationToken cancellationToken)
    {
        var relative = community + path;
        if (query.Count > 0)
        {
            relative += "?" + string.Join("&", query.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        }
        var response = await _httpClient.GetAsync(relative, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK
            || !IsExpectedCommunityFinalUri(response.RequestMessage?.RequestUri, community, path)
            || !HasExactQuerySet(response.RequestMessage?.RequestUri, query.Select(item => (item.Key, (string?)item.Value)).ToArray()))
        {
            throw new KicktippTypedAuthorityException($"Typed Kicktipp GET '{path}' did not reach its exact authenticated target.");
        }
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await _browsingContext.OpenAsync(request => request.Content(content), cancellationToken);
        if (IsLoginDocument(document))
        {
            throw new KicktippTypedAuthorityException($"Typed Kicktipp GET '{path}' resolved to a login surface.");
        }
        return document;
    }

    private static IHtmlFormElement RequireExactTypedForm(IDocument document, string community)
    {
        var forms = document.QuerySelectorAll("form").OfType<IHtmlFormElement>()
            .Where(form => form.QuerySelector("#tippabgabeSpiele, #tippabgabeFragen") is not null)
            .ToArray();
        if (forms.Length != 1 || string.IsNullOrWhiteSpace(forms[0].Action))
        {
            throw new KicktippTypedAuthorityException("Typed Kicktipp page has no single exact prediction form.");
        }
        return forms[0];
    }

    private static List<ParsedTypedMatchRow> ParseExactTypedMatchRows(IElement body)
    {
        var result = new List<ParsedTypedMatchRow>();
        foreach (var row in body.Children)
        {
            if (!string.Equals(row.LocalName, "tr", StringComparison.Ordinal))
            {
                throw new KicktippTypedAuthorityException("Typed match table contains a non-row child.");
            }
            var cells = row.Children.ToArray();
            if (cells.Length != 4 || cells.Any(cell => !string.Equals(cell.LocalName, "td", StringComparison.Ordinal)))
            {
                throw new KicktippTypedAuthorityException("Typed match table contains a malformed row.");
            }
            var inputs = cells[3].QuerySelectorAll("input[name]").OfType<IHtmlInputElement>().ToArray();
            var predictionControls = cells[3].QuerySelectorAll("input[type='text'], input[type='number']")
                .OfType<IHtmlInputElement>().ToArray();
            var typedInputs = inputs.Select(input => new
            {
                Input = input,
                Match = System.Text.RegularExpressions.Regex.Match(
                    input.Name ?? string.Empty,
                    @"^spieltippForms\[(?<id>[^\]]+)\]\.(?<field>heimTipp|gastTipp)$")
            }).Where(item => item.Match.Success).ToArray();
            if (predictionControls.Length == 0)
            {
                continue;
            }
            if (typedInputs.Length != predictionControls.Length || predictionControls.Length != 2)
            {
                throw new KicktippTypedAuthorityException(
                    "Typed match row contains an unnamed or non-exact prediction control.");
            }
            var ids = typedInputs.Select(item => item.Match.Groups["id"].Value).Distinct(StringComparer.Ordinal).ToArray();
            if (ids.Length != 1)
            {
                throw new KicktippTypedAuthorityException("Typed match row contains conflicting fixture IDs.");
            }
            var home = typedInputs.Where(item => item.Match.Groups["field"].Value == "heimTipp").Select(item => item.Input).ToArray();
            var away = typedInputs.Where(item => item.Match.Groups["field"].Value == "gastTipp").Select(item => item.Input).ToArray();
            var timeText = NormalizeStructuredMetadata(cells[0].TextContent);
            var cancelled = IsCancelledTimeText(timeText);
            Instant? scheduled = null;
            if (!cancelled && !string.IsNullOrWhiteSpace(timeText) && TryParseStructuredDateTime(timeText, out var parsed))
            {
                scheduled = parsed;
            }
            var inherited = string.IsNullOrWhiteSpace(timeText);
            if (home.Length != 1 || away.Length != 1 || scheduled is null)
            {
                throw new KicktippTypedAuthorityException(
                    $"Fixture '{ids[0]}' has missing, duplicate, cancelled, inherited, or unparsable form evidence.");
            }
            result.Add(new ParsedTypedMatchRow(
                ids[0], NormalizeStructuredMetadata(cells[1].TextContent),
                NormalizeStructuredMetadata(cells[2].TextContent), cancelled, inherited,
                scheduled, home[0], away[0]));
        }
        if (result.GroupBy(item => item.KicktippFixtureId, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new KicktippTypedAuthorityException("Typed match form repeats a fixture ID.");
        }
        return result;
    }

    private List<TypedOutcomeReference> ParseExactTypedOutcomeReferences(
        IDocument document,
        string community,
        int matchday)
    {
        var bodies = document.QuerySelectorAll("#spielplanSpiele > tbody").ToArray();
        if (bodies.Length != 1)
        {
            throw new KicktippTypedAuthorityException("Typed outcome source has no single exact fixture table.");
        }
        var result = new List<TypedOutcomeReference>();
        foreach (var row in bodies[0].Children.Where(item => string.Equals(item.LocalName, "tr", StringComparison.Ordinal)))
        {
            var detailUrl = row.GetAttribute("data-url");
            var id = ExtractTippSpielId(detailUrl);
            if (string.IsNullOrWhiteSpace(id)
                || !TryCreateOutcomeDetailUri(detailUrl, community, matchday, id, out var uri)
                || !TryReadSingleQueryValue(uri, "tippsaisonId", out var seasonId))
            {
                throw new KicktippTypedAuthorityException("Typed outcome row has malformed exact fixture identity.");
            }
            result.Add(new TypedOutcomeReference(id, uri, seasonId));
        }
        return result;
    }

    private async Task<TypedFixtureDetails> GetExactTypedFixtureDetailsAsync(
        string community,
        int matchday,
        TypedOutcomeReference reference,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(reference.Uri, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK
            || !IsExpectedCommunityFinalUri(response.RequestMessage?.RequestUri, community, "/tippuebersicht/spiel")
            || !HasExactQuerySet(
                response.RequestMessage?.RequestUri,
                ("tippspielId", reference.KicktippFixtureId),
                ("tippsaisonId", reference.SeasonId),
                ("spieltagIndex", matchday.ToString(CultureInfo.InvariantCulture))))
        {
            throw new KicktippTypedAuthorityException(
                $"Fixture '{reference.KicktippFixtureId}' detail did not reach its exact ID target.");
        }
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await _browsingContext.OpenAsync(request => request.Content(content), cancellationToken);
        if (IsLoginDocument(document))
        {
            throw new KicktippTypedAuthorityException("Typed fixture detail resolved to a login surface.");
        }
        var competition = ExactStructuredValues(document, "Wettbewerb");
        var round = ExactStructuredValues(document, "Spieltag");
        var termini = ExactStructuredValues(document, "Termin");
        if (competition.Length != 1 || round.Length != 1 || termini.Length != 1)
        {
            throw new KicktippTypedAuthorityException(
                $"Fixture '{reference.KicktippFixtureId}' detail metadata is missing or ambiguous.");
        }
        var canonicalTermini = termini.Select(value =>
        {
            if (!TryParseStructuredDateTime(value, out var instant))
            {
                throw new KicktippTypedAuthorityException(
                    $"Fixture '{reference.KicktippFixtureId}' has an unparsable Termin.");
            }
            return InstantPattern.ExtendedIso.Format(instant);
        }).ToArray();
        return new TypedFixtureDetails(competition[0], round[0], canonicalTermini);
    }

    private static string[] ExactStructuredValues(IDocument document, string label)
    {
        var labels = document.QuerySelectorAll(".spieldaten-infos-label")
            .Where(item => string.Equals(NormalizeStructuredMetadata(item.TextContent), label, StringComparison.Ordinal))
            .ToArray();
        if (labels.Length != 1
            || labels[0].NextElementSibling is not { } value
            || !value.ClassList.Contains("spieldaten-infos-value"))
        {
            return [];
        }
        var normalized = NormalizeStructuredMetadata(value.TextContent);
        return string.IsNullOrWhiteSpace(normalized) ? [] : [normalized];
    }

    private static List<ParsedTypedBonusRow> ParseExactTypedBonusRows(IElement body)
    {
        var result = new List<ParsedTypedBonusRow>();
        foreach (var row in body.Children)
        {
            if (!string.Equals(row.LocalName, "tr", StringComparison.Ordinal))
            {
                throw new KicktippTypedAuthorityException("Typed bonus table contains a non-row child.");
            }
            var cells = row.Children.ToArray();
            if (cells.Length != 3 || cells.Any(cell => !string.Equals(cell.LocalName, "td", StringComparison.Ordinal)))
            {
                throw new KicktippTypedAuthorityException("Typed bonus table contains a malformed row.");
            }
            if (!TryParseStructuredDateTime(NormalizeStructuredMetadata(cells[0].TextContent), out var deadline))
            {
                throw new KicktippTypedAuthorityException("Typed bonus row has a missing or unparsable deadline.");
            }
            var text = NormalizeStructuredMetadata(cells[1].TextContent);
            var selects = cells[2].QuerySelectorAll("select").OfType<IHtmlSelectElement>().ToArray();
            if (string.IsNullOrWhiteSpace(text) || selects.Length == 0)
            {
                throw new KicktippTypedAuthorityException("Typed bonus row has no exact text or selection controls.");
            }
            string? id = null;
            var indices = new List<int>();
            List<BonusQuestionOption>? options = null;
            foreach (var select in selects)
            {
                if (!TryExtractExactKicktippQuestionSelectIdentity(select.Name, out var currentId, out var indexText)
                    || !int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    throw new KicktippTypedAuthorityException("Typed bonus select has a malformed stable identity.");
                }
                if (id is not null && !string.Equals(id, currentId, StringComparison.Ordinal))
                {
                    throw new KicktippTypedAuthorityException("Typed bonus row contains conflicting question IDs.");
                }
                id = currentId;
                indices.Add(index);
                List<BonusQuestionOption> currentOptions;
                try
                {
                    currentOptions = ExtractExactQuestionOptions(select);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw new KicktippTypedAuthorityException(
                        "Typed bonus select has missing or duplicate option identity.", exception);
                }
                if (options is not null && !currentOptions.SequenceEqual(options))
                {
                    throw new KicktippTypedAuthorityException("Typed bonus row contains conflicting option sets.");
                }
                options = currentOptions;
                var sentinel = select.QuerySelectorAll("option").OfType<IHtmlOptionElement>()
                    .Count(option => string.Equals(option.Value, "-1", StringComparison.Ordinal));
                if (sentinel != 1)
                {
                    throw new KicktippTypedAuthorityException("Typed bonus select has no single exact empty sentinel.");
                }
            }
            if (!indices.SequenceEqual(Enumerable.Range(0, selects.Length)) || options is null || options.Count == 0)
            {
                throw new KicktippTypedAuthorityException("Typed bonus selection indices or options are incomplete.");
            }
            result.Add(new ParsedTypedBonusRow(id!, text, deadline, options, selects));
        }
        if (result.GroupBy(item => item.KicktippQuestionId, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new KicktippTypedAuthorityException("Typed bonus form repeats a question ID.");
        }
        return result;
    }

    private static BetPrediction? ParseExactMatchPrediction(
        IHtmlInputElement homeInput,
        IHtmlInputElement awayInput)
    {
        var home = homeInput.Value?.Trim() ?? string.Empty;
        var away = awayInput.Value?.Trim() ?? string.Empty;
        if (home.Length == 0 && away.Length == 0)
        {
            return null;
        }
        if (!int.TryParse(home, NumberStyles.None, CultureInfo.InvariantCulture, out var homeGoals)
            || !int.TryParse(away, NumberStyles.None, CultureInfo.InvariantCulture, out var awayGoals)
            || homeGoals < 0 || awayGoals < 0)
        {
            throw new KicktippTypedAuthorityException("Typed match prediction is partial, negative, or unparsable.");
        }
        return new BetPrediction(homeGoals, awayGoals);
    }

    private static IReadOnlyList<string> ParseExactBonusSelection(TypedBonusRow row)
    {
        var selected = new List<string>();
        var sawEmpty = false;
        foreach (var select in row.Selects)
        {
            var value = select.Value ?? throw new KicktippTypedAuthorityException(
                "Typed bonus select has no exact current value.");
            if (string.Equals(value, "-1", StringComparison.Ordinal))
            {
                sawEmpty = true;
                continue;
            }
            if (sawEmpty || row.Snapshot.Options.All(option => !string.Equals(option.Id, value, StringComparison.Ordinal))
                || selected.Contains(value, StringComparer.Ordinal))
            {
                throw new KicktippTypedAuthorityException("Typed bonus prediction is sparse, duplicated, or outside the snapshot.");
            }
            selected.Add(value);
        }
        return selected;
    }

    private async Task PostExactTypedFormAsync(
        string community,
        IHtmlFormElement form,
        IReadOnlyList<KeyValuePair<string, string>> formData,
        CancellationToken cancellationToken)
    {
        if (_httpClient.BaseAddress is null || !Uri.TryCreate(_httpClient.BaseAddress, form.Action, out var action)
            || !IsExpectedCommunityFinalUri(action, community, "/tippabgabe")
            || !HasExactQuerySet(action))
        {
            throw new KicktippTypedAuthorityException("Typed prediction form action is not the exact posting target.");
        }
        using var content = new FormUrlEncodedContent(formData);
        var response = await _httpClient.PostAsync(action, content, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK
            || !IsExpectedCommunityFinalUri(response.RequestMessage?.RequestUri, community, "/tippabgabe")
            || !HasExactQuerySet(response.RequestMessage?.RequestUri))
        {
            throw new KicktippTypedAuthorityException("Typed prediction POST did not complete at the exact target.");
        }
    }

    private static List<KeyValuePair<string, string>> CopyExactHiddenInputs(IHtmlFormElement form)
    {
        var result = new List<KeyValuePair<string, string>>();
        foreach (var input in form.QuerySelectorAll("input[type='hidden']").OfType<IHtmlInputElement>())
        {
            AddExactFormValue(result, input.Name, input.Value ?? string.Empty);
        }
        return result;
    }

    private static ImmutableSortedDictionary<string, string> CaptureExactMixedMatchControlState(
        IHtmlFormElement form)
    {
        var tables = form.QuerySelectorAll("#tippabgabeSpiele").ToArray();
        if (tables.Length > 1)
        {
            throw new KicktippTypedAuthorityException(
                "Typed bonus form has multiple mixed match-control tables.");
        }
        if (tables.Length == 0)
        {
            return ImmutableSortedDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);
        }

        var state = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var fixtureFields = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var input in tables[0].QuerySelectorAll("input").OfType<IHtmlInputElement>())
        {
            var name = input.Name ?? string.Empty;
            var match = Regex.Match(
                name,
                @"^spieltippForms\[(?<id>[^\]]+)\]\.(?<field>heimTipp|gastTipp)$",
                RegexOptions.CultureInvariant);
            var isPredictionControl = string.Equals(input.Type, "text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input.Type, "number", StringComparison.OrdinalIgnoreCase);
            if (!match.Success)
            {
                if (isPredictionControl)
                {
                    throw new KicktippTypedAuthorityException(
                        "Typed bonus form has a renamed or non-exact mixed match control.");
                }
                continue;
            }
            if (!isPredictionControl || state.ContainsKey(name))
            {
                throw new KicktippTypedAuthorityException(
                    "Typed bonus form has a duplicate or malformed mixed match control.");
            }
            state.Add(name, input.Value ?? string.Empty);

            var id = match.Groups["id"].Value;
            var field = match.Groups["field"].Value;
            if (!fixtureFields.TryGetValue(id, out var fields))
            {
                fields = new HashSet<string>(StringComparer.Ordinal);
                fixtureFields.Add(id, fields);
            }
            if (!fields.Add(field))
            {
                throw new KicktippTypedAuthorityException(
                    $"Typed bonus form repeats mixed match control '{name}'.");
            }
        }

        if (fixtureFields.Values.Any(fields =>
                fields.Count != 2 || !fields.Contains("heimTipp") || !fields.Contains("gastTipp")))
        {
            throw new KicktippTypedAuthorityException(
                "Typed bonus form has an incomplete mixed match-control pair.");
        }
        return state.ToImmutable();
    }

    private static void RequireExactMixedMatchPostState(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        if (actual.Count != expected.Count || expected.Any(item =>
                !actual.TryGetValue(item.Key, out var value)
                || !string.Equals(value, item.Value, StringComparison.Ordinal)))
        {
            throw new KicktippTypedAuthorityException(
                "Typed bonus readback changed the exact mixed match-control set or values.");
        }
    }

    private static void AddExactSubmitControl(
        IHtmlFormElement form,
        ICollection<KeyValuePair<string, string>> formData)
    {
        var controls = form.QuerySelectorAll("input[type='submit'], button[type='submit']").OfType<IHtmlElement>().ToArray();
        if (controls.Length != 1)
        {
            throw new KicktippTypedAuthorityException("Typed prediction form has no single exact submit control.");
        }
        switch (controls[0])
        {
            case IHtmlInputElement input:
                AddExactFormValue(formData, input.Name, input.Value ?? string.Empty);
                break;
            case IHtmlButtonElement button:
                AddExactFormValue(formData, button.Name, button.Value ?? string.Empty);
                break;
            default:
                throw new KicktippTypedAuthorityException("Typed prediction submit control is malformed.");
        }
    }

    private static void AddExactFormValue(
        ICollection<KeyValuePair<string, string>> formData,
        string? name,
        string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new KicktippTypedAuthorityException("Typed prediction form control has no exact name.");
        }
        formData.Add(new KeyValuePair<string, string>(name, value));
    }

    private static void RequireExpectedMatchSnapshots(
        BundesligaTypedMatchReadScope scope,
        IReadOnlyList<TypedMatchRow> rows)
    {
        var expected = scope.Items.ToDictionary(item => item.Snapshot.Key.KicktippItemId, StringComparer.Ordinal);
        RequireExactItemSet(expected.Keys, rows.Select(item => item.Snapshot.Key.KicktippItemId), "match snapshot readback");
        foreach (var row in rows)
        {
            if (!row.Snapshot.Equals(expected[row.Snapshot.Key.KicktippItemId].Snapshot))
            {
                throw new KicktippTypedAuthorityException(
                    $"Fixture '{row.Snapshot.Key.KicktippItemId}' snapshot drifted from the exact read scope.");
            }
        }
    }

    private static void RequireExpectedBonusSnapshots(
        BundesligaTypedBonusReadScope scope,
        IReadOnlyList<TypedBonusRow> rows)
    {
        var expected = scope.Items.ToDictionary(item => item.Snapshot.Key.KicktippItemId, StringComparer.Ordinal);
        RequireExactItemSet(expected.Keys, rows.Select(item => item.Snapshot.Key.KicktippItemId), "bonus snapshot readback");
        foreach (var row in rows)
        {
            if (!row.Snapshot.Equals(expected[row.Snapshot.Key.KicktippItemId].Snapshot))
            {
                throw new KicktippTypedAuthorityException(
                    $"Bonus item '{row.Snapshot.Key.KicktippItemId}' snapshot drifted from the exact read scope.");
            }
        }
    }

    private static void RequireExactItemSet(
        IEnumerable<string> expected,
        IEnumerable<string> actual,
        string description)
    {
        var expectedItems = expected.Order(StringComparer.Ordinal).ToArray();
        var actualItems = actual.Order(StringComparer.Ordinal).ToArray();
        if (!actualItems.SequenceEqual(expectedItems, StringComparer.Ordinal))
        {
            throw new KicktippTypedAuthorityException(
                $"Typed {description} has missing, extra, duplicate, or changed IDs.");
        }
    }

    private static void ValidateTypedAuthority(BundesligaPredictionAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if (!string.Equals(authority.SeasonPartition, BundesligaPredictionAuthority.SeasonPartitionValue, StringComparison.Ordinal)
            || !string.Equals(authority.AuthorityEpoch, BundesligaPredictionAuthority.AuthorityEpochValue, StringComparison.Ordinal))
        {
            throw new KicktippTypedAuthorityException("Typed Kicktipp authority is outside the frozen season or epoch.");
        }
    }

    private static void ValidatePostingKeys(
        BundesligaPredictionAuthority authority,
        IEnumerable<StableLocalItemKey> keys)
    {
        if (keys.Any(key => key is null
            || !string.Equals(key.SeasonPartition, authority.SeasonPartition, StringComparison.Ordinal)
            || !string.Equals(key.PostingCommunity, authority.PostingCommunity, StringComparison.Ordinal)))
        {
            throw new KicktippTypedAuthorityException("Typed Kicktipp scope contains a key from another authority.");
        }
    }

    private bool IsExpectedCommunityFinalUri(Uri? uri, string community, string expectedPath) =>
        uri is not null && _finalAuthorityValidator(uri)
        && string.Equals(uri.AbsolutePath, $"/{community}{expectedPath}", StringComparison.Ordinal)
        && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo);

    private static bool IsCanonicalKicktippAuthority(Uri uri) =>
        uri.IsAbsoluteUri
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        && string.Equals(uri.Host, "www.kicktipp.de", StringComparison.OrdinalIgnoreCase)
        && uri.Port == 443 && string.IsNullOrEmpty(uri.UserInfo);

    private static bool HasExactQuerySet(Uri? uri, params (string Key, string? Value)[] expected)
    {
        if (uri is null)
        {
            return false;
        }
        if (string.IsNullOrEmpty(uri.Query))
        {
            return expected.Length == 0;
        }
        if (expected.Length == 0 || ContainsMalformedPercentEncoding(uri.Query[1..]))
        {
            return false;
        }
        var actual = uri.Query[1..].Split('&', StringSplitOptions.None)
            .Select(part => part.Split('=', 2)).ToArray();
        return actual.Length == expected.Length && actual.All(parts => parts.Length == 2)
            && expected.All(item => TryReadSingleQueryValue(uri, item.Key, out var value)
                && (item.Value is null ? !string.IsNullOrWhiteSpace(value)
                    : string.Equals(value, item.Value, StringComparison.Ordinal)));
    }

    private static bool TryReadSingleQueryValue(Uri? uri, string key, out string value)
    {
        value = string.Empty;
        if (uri is null || string.IsNullOrEmpty(uri.Query)
            || ContainsMalformedPercentEncoding(uri.Query[1..]))
        {
            return false;
        }
        var values = uri.Query[1..].Split('&', StringSplitOptions.None)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2
                && string.Equals(DecodeQuery(parts[0]), key, StringComparison.Ordinal))
            .Select(parts => DecodeQuery(parts[1])).ToArray();
        if (values.Length != 1)
        {
            return false;
        }
        value = values[0];
        return true;
    }

    private static string DecodeQuery(string value) =>
        Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));

    private static bool ContainsMalformedPercentEncoding(string query)
    {
        for (var index = 0; index < query.Length; index++)
        {
            if (query[index] != '%')
            {
                continue;
            }
            if (index + 2 >= query.Length || !Uri.IsHexDigit(query[index + 1]) || !Uri.IsHexDigit(query[index + 2]))
            {
                return true;
            }
            index += 2;
        }
        return false;
    }

    private static bool IsLoginDocument(IDocument document) =>
        document.QuerySelector("form#loginFormular") is not null
        || NormalizeStructuredMetadata(document.Title).Contains("Login", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeStructuredMetadata(string? value) =>
        NormalizeWhitespace(value).Normalize(NormalizationForm.FormC);

    private static string NormalizeWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : Regex.Replace(value.Trim(), @"\s+", " ");

    private static bool IsCancelledTimeText(string value) =>
        string.Equals(value, "Abgesagt", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseStructuredDateTime(string value, out Instant instant)
    {
        instant = default;
        if (!DateTime.TryParseExact(
                value,
                ["dd.MM.yy HH:mm", "dd.MM.yyyy HH:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }
        var local = LocalDateTime.FromDateTime(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified));
        var mapping = BerlinTimeZone.MapLocal(local);
        if (mapping.Count != 1)
        {
            return false;
        }
        instant = mapping.First().ToInstant();
        return instant != Instant.MinValue;
    }

    private static bool TryExtractMatchdayFromPage(IDocument document, out int matchday)
    {
        matchday = 0;
        var values = document.QuerySelectorAll("input[name]").OfType<IHtmlInputElement>()
            .Where(input => string.Equals(input.Name, "spieltagIndex", StringComparison.Ordinal))
            .Select(input => input.Value).ToArray();
        return values.Length == 1
            && int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out matchday)
            && matchday > 0;
    }

    private bool TryCreateOutcomeDetailUri(
        string? detailUrl,
        string community,
        int matchday,
        string expectedFixtureId,
        out Uri detailUri)
    {
        detailUri = null!;
        if (string.IsNullOrWhiteSpace(detailUrl) || _httpClient.BaseAddress is null
            || !Uri.TryCreate(_httpClient.BaseAddress, detailUrl, out var parsed)
            || !IsExpectedCommunityFinalUri(parsed, community, "/tippuebersicht/spiel")
            || !HasExactQuerySet(
                parsed,
                ("tippspielId", expectedFixtureId),
                ("tippsaisonId", null),
                ("spieltagIndex", matchday.ToString(CultureInfo.InvariantCulture))))
        {
            return false;
        }
        detailUri = parsed;
        return true;
    }

    private static string? ExtractTippSpielId(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return null;
        }
        var match = Regex.Match(dataUrl, @"(?:\?|&)tippspielId=(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool TryExtractExactKicktippQuestionSelectIdentity(
        string? formFieldName,
        out string questionId,
        out string selectionIndex)
    {
        questionId = string.Empty;
        selectionIndex = string.Empty;
        if (string.IsNullOrWhiteSpace(formFieldName))
        {
            return false;
        }
        var match = Regex.Match(
            formFieldName,
            @"^fragetippForms\[(?<id>\d+)\]\.antwortIds\[(?<index>\d+)\]$");
        if (!match.Success)
        {
            return false;
        }
        questionId = match.Groups["id"].Value;
        selectionIndex = match.Groups["index"].Value;
        return true;
    }

    private static List<BonusQuestionOption> ExtractExactQuestionOptions(IHtmlSelectElement select)
    {
        var options = new List<BonusQuestionOption>();
        foreach (var option in select.QuerySelectorAll("option"))
        {
            if (option is not IHtmlOptionElement typedOption)
            {
                throw new KicktippTypedAuthorityException("Typed bonus select contains a malformed option.");
            }
            if (string.Equals(typedOption.Value, "-1", StringComparison.Ordinal))
            {
                continue;
            }
            var text = NormalizeStructuredMetadata(typedOption.Text);
            if (!typedOption.HasAttribute("value") || string.IsNullOrWhiteSpace(typedOption.Value)
                || string.IsNullOrWhiteSpace(text))
            {
                throw new KicktippTypedAuthorityException("Typed bonus select contains a missing option identity.");
            }
            options.Add(new BonusQuestionOption(typedOption.Value, text));
        }
        if (options.Count == 0
            || options.GroupBy(item => item.Id, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || options.GroupBy(item => item.Text, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new KicktippTypedAuthorityException("Typed bonus select contains duplicate or empty option identity.");
        }
        return options;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _browsingContext.Dispose();
    }

    private sealed record ParsedTypedMatchRow(
        string KicktippFixtureId,
        string HomeTeam,
        string AwayTeam,
        bool IsCancelled,
        bool IsInherited,
        Instant? ScheduledInstant,
        IHtmlInputElement HomeInput,
        IHtmlInputElement AwayInput);
    private sealed record TypedOutcomeReference(string KicktippFixtureId, Uri Uri, string SeasonId);
    private sealed record TypedFixtureDetails(string Competition, string RoundName, IReadOnlyList<string> TerminValues);
    private sealed record TypedMatchRow(
        TypedMatchSnapshot Snapshot,
        IHtmlInputElement HomeInput,
        IHtmlInputElement AwayInput);
    private sealed record TypedMatchInventory(IHtmlFormElement Form, IReadOnlyList<TypedMatchRow> Rows);
    private sealed record ParsedTypedBonusRow(
        string KicktippQuestionId,
        string Text,
        Instant Deadline,
        IReadOnlyList<BonusQuestionOption> Options,
        IReadOnlyList<IHtmlSelectElement> Selects);
    private sealed record TypedBonusRow(TypedBonusSnapshot Snapshot, IReadOnlyList<IHtmlSelectElement> Selects);
    private sealed record TypedBonusInventory(IHtmlFormElement Form, IReadOnlyList<TypedBonusRow> Rows);
}
