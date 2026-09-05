using System.Net;
using System.Globalization;
using Regex = System.Text.RegularExpressions.Regex;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Extensions;
using KicktippIntegration.Transport;

namespace KicktippIntegration;

/// <summary>
/// Implementation of IKicktippClient for interacting with kicktipp.de website
/// Authentication is handled automatically via KicktippAuthenticationHandler
/// </summary>
public class KicktippClient : IKicktippClient, IDisposable
{
    private static readonly DateTimeZone BerlinTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];

    private readonly HttpClient _httpClient;
    private readonly ILogger<KicktippClient> _logger;
    private readonly IBrowsingContext _browsingContext;
    private readonly IMemoryCache _cache;
    private readonly ChampionsLeagueBonusStrictTransport? _championsLeagueBonusStrictTransport;

    public async Task<ChampionsLeagueBonusFormSnapshot> GetChampionsLeagueBonusFormSnapshotAsync(string community)
    {
        if (!string.Equals(community, SchadensfresseChampionsLeagueBonusProfile.Community, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The strict CL form route is scoped to schadensfresse only.");
        }

        using var response = await _httpClient.GetAsync($"{community}/tippabgabe?bonus=true");
        var snapshot = await ParseChampionsLeagueBonusSnapshotAsync(response);
        if (!HasExactUriComponents(snapshot.FinalUri, ExpectedChampionsLeagueBonusPage))
        {
            throw new InvalidDataException("The strict CL GET did not remain on the exact bonus page.");
        }
        return snapshot;
    }

    private async Task<ChampionsLeagueBonusFormSnapshot> ParseChampionsLeagueBonusSnapshotAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var finalUri = response.RequestMessage?.RequestUri
            ?? throw new InvalidDataException("The strict CL form read has no final URI.");
        var contentType = response.Content.Headers.ContentType;
        if (contentType is null
            || !string.Equals(contentType.MediaType, "text/html", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(contentType.CharSet?.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The strict CL form must be a single UTF-8 HTML response.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
        {
            throw new InvalidDataException("The strict CL form cannot contain a UTF-8 BOM.");
        }
        var content = new System.Text.UTF8Encoding(false, true).GetString(bytes);
        var document = await _browsingContext.OpenAsync(req => req.Content(content).Address(finalUri.ToString()));
        var forms = document.QuerySelectorAll("form").OfType<IHtmlFormElement>()
            .Where(candidate => candidate.QuerySelector("#tippabgabeFragen") is not null)
            .ToArray();
        if (forms.Length != 1)
        {
            throw new InvalidDataException("The strict CL page must contain exactly one bonus form.");
        }
        var form = forms[0];
        if (!string.Equals(form.Enctype, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The strict CL form does not use the expected URL-encoded submission format.");
        }
        ValidateChampionsLeagueAnswerControlKeys(form);
        var rows = document.QuerySelectorAll("#tippabgabeFragen tbody tr");
        var questions = new List<ChampionsLeagueBonusQuestionSnapshot>();
        var targetIds = SchadensfresseChampionsLeagueBonusProfile.OrderedQuestionIds.ToHashSet(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 3) continue;
            var selects = cells[2].QuerySelectorAll("select").OfType<IHtmlSelectElement>().ToArray();
            if (selects.Length == 0) continue;
            var parsedKeys = selects.Select(select => ParseChampionsLeagueTargetKey(select.Name)).ToArray();
            var matching = parsedKeys.Where(key => key is not null && targetIds.Contains(key.Value.QuestionId)).ToArray();
            if (matching.Length == 0) continue;
            if (matching.Length != selects.Length
                || matching.Select(key => key!.Value.QuestionId).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                throw new InvalidDataException("A strict CL target row mixes malformed, unrelated, or cross-question keys.");
            }

            var id = matching[0]!.Value.QuestionId;
            var key = selects[0].Name;
            var options = ParseStrictOptions(selects[0]);
            if (selects.Skip(1).Any(select => !ParseStrictOptions(select).SequenceEqual(options)))
            {
                throw new InvalidDataException("The strict CL multi-select slots expose different option arrays.");
            }
            var text = NormalizeWhitespace(cells[1].TextContent);
            if (!text.IsNormalized(System.Text.NormalizationForm.FormC))
            {
                throw new InvalidDataException("The strict CL question text is not Unicode NFC.");
            }
            questions.Add(new ChampionsLeagueBonusQuestionSnapshot(
                id,
                new BonusQuestion(text, ParseMatchDateTime(NormalizeWhitespace(cells[0].TextContent)), options, selects.Length, key),
                selects.Select(select => select.Name ?? throw new InvalidDataException("A strict CL slot has no form name.")).ToArray(),
                selects.Select(select => select.Value == "-1" ? null : select.Value).ToArray()));
        }

        var targetKeys = questions.SelectMany(question => question.FormKeys).ToHashSet(StringComparer.Ordinal);
        var submitters = form.Elements.OfType<IHtmlElement>()
            .Where(element => element is IHtmlButtonElement
                              || element is IHtmlInputElement input && input.Type is "submit" or "button")
            .Where(element => string.Equals(element.GetAttribute("name"), "submitbutton", StringComparison.Ordinal)
                              && !element.IsDisabled())
            .ToArray();
        if (submitters.Length != 1)
        {
            throw new InvalidDataException("The strict CL form has no unique intended submitter.");
        }
        var submitter = submitters[0];
        var submitterName = submitter.GetAttribute("name") ?? throw new InvalidDataException("The strict CL submitter has no name.");
        var submitterValue = submitter.GetAttribute("value") ?? string.Empty;
        var controls = ExtractSuccessfulNonTargetControls(form, targetKeys, submitter);
        Uri action;
        if (Uri.TryCreate(form.Action, UriKind.Absolute, out var absoluteAction))
        {
            action = absoluteAction;
        }
        else
        {
            if (!Uri.TryCreate(form.BaseUri, UriKind.Absolute, out var effectiveBase)
                || !Uri.TryCreate(effectiveBase, form.Action, out var resolvedAction)
                || resolvedAction is null)
            {
                throw new InvalidDataException(
                    "The strict CL form action cannot be resolved from the effective document base.");
            }
            action = resolvedAction;
        }
        var canPlace = questions.SelectMany(question => question.FormKeys)
            .All(targetKey => form.Elements.OfType<IHtmlSelectElement>()
                .Single(select => string.Equals(select.Name, targetKey, StringComparison.Ordinal))
                .IsDisabled() is false);
        var snapshot = new ChampionsLeagueBonusFormSnapshot(finalUri, action, form.Method.ToUpperInvariant(), questions, controls, submitterName, submitterValue, canPlace);
        ChampionsLeagueBonusRoute.ValidateSnapshot(ToCanonicalChampionsLeagueSnapshot(snapshot));
        return snapshot;
    }

    public async Task<ChampionsLeagueBonusFormSnapshot> PlaceChampionsLeagueBonusPredictionsAsync(string community, ChampionsLeagueBonusFormSnapshot initialSnapshot, IReadOnlyList<(string QuestionId, BonusPrediction Prediction)> predictions, bool overridePredictions)
    {
        var strictTransport = _championsLeagueBonusStrictTransport
            ?? throw new InvalidOperationException(
                "The strict Champions-League mutation transport is not configured for this Kicktipp client.");
        var current = await GetChampionsLeagueBonusFormSnapshotAsync(community);
        var payload = ChampionsLeagueBonusRoute.BuildPostPayload(
            ToCanonicalChampionsLeagueSnapshot(initialSnapshot),
            ToCanonicalChampionsLeagueSnapshot(current),
            predictions,
            overridePredictions);
        using var response = await strictTransport.PostAndResolveResponseOnceAsync(payload);
        var responseSnapshot = await ParseChampionsLeagueBonusSnapshotAsync(response);
        ChampionsLeagueBonusRoute.ValidatePlacedSelections(
            ToCanonicalChampionsLeagueSnapshot(responseSnapshot), predictions);
        using var finalResponse = await strictTransport.GetOnceAsync();
        var final = await ParseChampionsLeagueBonusSnapshotAsync(finalResponse);
        if (!HasExactUriComponents(final.FinalUri, strictTransport.PageUri))
        {
            throw new InvalidDataException("The strict CL final GET did not remain on the exact bonus page.");
        }
        ChampionsLeagueBonusRoute.ValidatePlacedSelections(ToCanonicalChampionsLeagueSnapshot(final), predictions);
        return final;
    }

    private Uri ExpectedChampionsLeagueBonusPage =>
        _championsLeagueBonusStrictTransport?.PageUri ?? ChampionsLeagueBonusRoute.ExpectedPage;

    private Uri ExpectedChampionsLeagueBonusAction =>
        _championsLeagueBonusStrictTransport?.ActionUri ?? ChampionsLeagueBonusRoute.ExpectedAction;

    private ChampionsLeagueBonusFormSnapshot ToCanonicalChampionsLeagueSnapshot(
        ChampionsLeagueBonusFormSnapshot snapshot)
    {
        if (!HasExactUriComponents(snapshot.Action, ExpectedChampionsLeagueBonusAction)
            || !HasExactUriComponents(snapshot.FinalUri, ExpectedChampionsLeagueBonusPage)
               && !HasExactUriComponents(snapshot.FinalUri, ExpectedChampionsLeagueBonusAction))
        {
            throw new InvalidDataException("The strict CL form is not bound to the configured exact route.");
        }

        return snapshot with
        {
            FinalUri = HasExactUriComponents(snapshot.FinalUri, ExpectedChampionsLeagueBonusPage)
                ? ChampionsLeagueBonusRoute.ExpectedPage
                : ChampionsLeagueBonusRoute.ExpectedAction,
            Action = ChampionsLeagueBonusRoute.ExpectedAction
        };
    }

    private static bool HasExactUriComponents(Uri actual, Uri expected) =>
        string.Equals(actual.Scheme, expected.Scheme, StringComparison.Ordinal)
        && string.Equals(actual.Host, expected.Host, StringComparison.Ordinal)
        && actual.Port == expected.Port
        && string.Equals(actual.UserInfo, expected.UserInfo, StringComparison.Ordinal)
        && string.Equals(actual.AbsolutePath, expected.AbsolutePath, StringComparison.Ordinal)
        && string.Equals(actual.Query, expected.Query, StringComparison.Ordinal)
        && string.Equals(actual.Fragment, expected.Fragment, StringComparison.Ordinal);

    private static (string QuestionId, string SlotId)? ParseChampionsLeagueTargetKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        var match = Regex.Match(key, @"^fragetippForms\[(?<questionId>[0-9]+)\]\.antwortIds\[(?<slotId>[0-9]+)\]$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success ? (match.Groups["questionId"].Value, match.Groups["slotId"].Value) : null;
    }

    private static void ValidateChampionsLeagueAnswerControlKeys(IHtmlFormElement form)
    {
        var seed = SchadensfresseChampionsLeagueBonusSeed.Default;
        var expectedKeys = seed.Questions.SelectMany(question => question.FormKeys).ToHashSet(StringComparer.Ordinal);
        var targetIds = seed.Questions.Select(question => question.KicktippQuestionId).ToHashSet(StringComparer.Ordinal);
        var expectedSubmissionStateKeys = targetIds
            .Select(questionId => $"fragetippForms[{questionId}].tippAbgegeben")
            .ToHashSet(StringComparer.Ordinal);
        var canonicalSlotIds = expectedKeys.Select(key => ParseChampionsLeagueTargetKey(key)!.Value.SlotId)
            .ToHashSet(StringComparer.Ordinal);
        var observedExpectedKeys = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var control in form.Elements.OfType<IHtmlElement>())
        {
            var name = control.GetAttribute("name");
            if (string.IsNullOrEmpty(name)) continue;
            var parsed = ParseChampionsLeagueTargetKey(name);
            if (parsed is { } answerKey)
            {
                if (targetIds.Contains(answerKey.QuestionId))
                {
                    if (!expectedKeys.Contains(name) || control is not IHtmlSelectElement)
                    {
                        throw new InvalidDataException("A canonical CL question has an extra or non-select answer control.");
                    }
                    observedExpectedKeys[name] = observedExpectedKeys.GetValueOrDefault(name) + 1;
                }
                else if (canonicalSlotIds.Contains(answerKey.SlotId))
                {
                    throw new InvalidDataException("A canonical CL slot identity is reused by another question.");
                }
                continue;
            }

            if (expectedSubmissionStateKeys.Contains(name))
            {
                if (control is not IHtmlInputElement submissionState
                    || !string.Equals(submissionState.Type, "hidden", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "A canonical CL submission-state control is not the expected hidden input.");
                }
                continue;
            }

            if (targetIds.Any(questionId => Regex.IsMatch(
                    name,
                    $@"^fragetippForms\[{Regex.Escape(questionId)}(?![0-9])",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
            {
                throw new InvalidDataException("A canonical CL question has a malformed answer key.");
            }
        }

        if (expectedKeys.Any(key => observedExpectedKeys.GetValueOrDefault(key) != 1))
        {
            throw new InvalidDataException("The canonical CL answer keys must each occur exactly once in the form.");
        }
    }

    private static List<BonusQuestionOption> ParseStrictOptions(IHtmlSelectElement select)
    {
        if (select.Options.Length == 0
            || select.Options[0].Value != "-1"
            || select.Options.Count(option => option.Value == "-1") != 1)
        {
            throw new InvalidDataException("A strict CL select must expose exactly one leading unselected sentinel.");
        }
        var options = select.Options.Where(option => option.Value != "-1").Select(option =>
        {
            var text = NormalizeWhitespace(option.TextContent);
            if (string.IsNullOrWhiteSpace(option.Value)
                || string.IsNullOrWhiteSpace(text)
                || !text.IsNormalized(System.Text.NormalizationForm.FormC))
            {
                throw new InvalidDataException("A strict CL option is blank or not Unicode NFC.");
            }
            return new BonusQuestionOption(option.Value, text);
        }).ToList();
        if (select.Options.Where(option => option.Value != "-1").Any(option => option.IsDisabled())
            || options.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count() != options.Count)
        {
            throw new InvalidDataException("A strict CL select contains duplicate or disabled option IDs.");
        }
        return options;
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ExtractSuccessfulNonTargetControls(
        IHtmlFormElement form,
        IReadOnlySet<string> targetKeys,
        IHtmlElement intendedSubmitter)
    {
        var result = new List<KeyValuePair<string, string>>();
        foreach (var element in form.Elements.OfType<IHtmlElement>())
        {
            if (ReferenceEquals(element, intendedSubmitter) || element.IsDisabled()) continue;
            var name = element.GetAttribute("name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (targetKeys.Contains(name))
            {
                if (element is not IHtmlSelectElement)
                {
                    throw new InvalidDataException($"A non-target control collides with frozen target key '{name}'.");
                }
                continue;
            }

            if (element is IHtmlButtonElement) continue;
            if (element is IHtmlInputElement input)
            {
                var type = (input.Type ?? "text").ToLowerInvariant();
                if (type == "file")
                {
                    throw new InvalidDataException($"Non-target file control '{name}' cannot be preserved by the strict URL-encoded route.");
                }
                if (type is "submit" or "button" or "reset" or "image") continue;
                if (type is "checkbox" or "radio" && !input.IsChecked) continue;
                result.Add(new KeyValuePair<string, string>(name, input.Value ?? (type is "checkbox" or "radio" ? "on" : string.Empty)));
                continue;
            }
            if (element is IHtmlTextAreaElement textArea)
            {
                result.Add(new KeyValuePair<string, string>(name, textArea.Value));
                continue;
            }
            if (element is IHtmlSelectElement select)
            {
                var selected = select.SelectedOptions.ToArray();
                if (selected.Length == 0 && !select.IsMultiple)
                {
                    throw new InvalidDataException($"Non-target select '{name}' has no preservable selected value.");
                }
                result.AddRange(selected.Select(option => new KeyValuePair<string, string>(name, option.Value)));
            }
        }
        return result;
    }

    public KicktippClient(HttpClient httpClient, ILogger<KicktippClient> logger, IMemoryCache cache)
        : this(httpClient, logger, cache, championsLeagueBonusStrictTransport: null, allowMissingStrictTransport: true)
    {
    }

    public KicktippClient(
        HttpClient httpClient,
        ILogger<KicktippClient> logger,
        IMemoryCache cache,
        ChampionsLeagueBonusStrictTransport championsLeagueBonusStrictTransport)
        : this(httpClient, logger, cache, championsLeagueBonusStrictTransport, allowMissingStrictTransport: false)
    {
    }

    private KicktippClient(
        HttpClient httpClient,
        ILogger<KicktippClient> logger,
        IMemoryCache cache,
        ChampionsLeagueBonusStrictTransport? championsLeagueBonusStrictTransport,
        bool allowMissingStrictTransport)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _championsLeagueBonusStrictTransport = allowMissingStrictTransport
            ? championsLeagueBonusStrictTransport
            : championsLeagueBonusStrictTransport
              ?? throw new ArgumentNullException(nameof(championsLeagueBonusStrictTransport));
        
        var config = Configuration.Default.WithDefaultLoader();
        _browsingContext = BrowsingContext.New(config);
    }

    private async Task<bool> IsKnownDrawNotAllowedSubmissionFailureAsync(
        HttpResponseMessage submitResponse,
        string htmlContent)
    {
        if (HasDrawNotAllowedQueryFlag(submitResponse.RequestMessage?.RequestUri))
        {
            _logger.LogWarning(
                "Detected Kicktipp draw-not-allowed query flag after submit at {ResponseUri}",
                submitResponse.RequestMessage?.RequestUri);
            return true;
        }

        if (await HasDrawNotAllowedBannerAsync(htmlContent))
        {
            _logger.LogWarning("Detected Kicktipp draw-not-allowed rejection banner after submit.");
            return true;
        }

        return false;
    }

    private async Task<bool> HasDrawNotAllowedBannerAsync(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent) ||
            !htmlContent.Contains("messagebox errors", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var document = await _browsingContext.OpenAsync(req => req.Content(htmlContent));
        var errorMessage = document.QuerySelector(".messagebox.errors .message");
        if (errorMessage == null)
        {
            return false;
        }

        var normalizedText = NormalizeWhitespace(errorMessage.TextContent);
        return normalizedText.Contains("Nicht alle gesendeten Tipps waren korrekt.", StringComparison.OrdinalIgnoreCase) &&
               normalizedText.Contains("Mindestens ein Spiel wurde Unentschieden getippt", StringComparison.OrdinalIgnoreCase) &&
               normalizedText.Contains("nicht möglich", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDrawNotAllowedQueryFlag(Uri? responseUri)
    {
        return responseUri != null &&
               responseUri.Query.Contains("unentschiedenNichtMoeglich=true", StringComparison.OrdinalIgnoreCase);
    }
    /// <inheritdoc />
    public Task<List<Match>> GetOpenPredictionsAsync(string community)
    {
        return GetOpenPredictionsInternalAsync(community, competition: null);
    }

    public Task<List<Match>> GetOpenPredictionsAsync(string community, string competition)
    {
        return GetOpenPredictionsInternalAsync(community, competition);
    }

    private async Task<List<Match>> GetOpenPredictionsInternalAsync(string community, string? competition)
    {
        try
        {
            var url = $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                return new List<Match>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            var matches = new List<Match>();
            
            // Extract matchday from the page
            var currentMatchday = ExtractMatchdayFromPage(document);
            _logger.LogDebug("Extracted matchday: {Matchday}", currentMatchday);
            var kicktippRoundName = ExtractKicktippRoundName(document);
            
            // Parse matches from the tippabgabe table
            var matchTable = document.QuerySelector("#tippabgabeSpiele tbody");
            if (matchTable == null)
            {
                _logger.LogWarning("Could not find tippabgabe table");
                return matches;
            }
            
            var matchRows = matchTable.QuerySelectorAll("tr");
            _logger.LogDebug("Found {MatchRowCount} potential match rows", matchRows.Length);
            
            string lastValidTimeText = "";  // Track the last valid date/time for inheritance
            
            foreach (var row in matchRows)
            {
                try
                {
                    var cells = row.QuerySelectorAll("td");
                    if (cells.Length >= 4)
                    {
                        // Extract match details from table cells
                        var timeText = cells[0].TextContent?.Trim() ?? "";
                        var homeTeam = cells[1].TextContent?.Trim() ?? "";
                        var awayTeam = cells[2].TextContent?.Trim() ?? "";
                        
                        // Check if match is cancelled ("Abgesagt" in German)
                        // Cancelled matches still accept predictions on Kicktipp, so we process them.
                        // See docs/features/cancelled-matches.md for design rationale.
                        var isCancelled = IsCancelledTimeText(timeText);
                        
                        // Handle date inheritance: if timeText is empty or cancelled, use the last valid time
                        // This preserves database key consistency (startsAt is part of the composite key)
                        if (string.IsNullOrWhiteSpace(timeText) || isCancelled)
                        {
                            if (!string.IsNullOrWhiteSpace(lastValidTimeText))
                            {
                                if (isCancelled)
                                {
                                    _logger.LogWarning(
                                        "Match {HomeTeam} vs {AwayTeam} is cancelled (Abgesagt). Using inherited time '{InheritedTime}' for database consistency. " +
                                        "Predictions can still be placed but may need to be re-evaluated when the match is rescheduled.",
                                        homeTeam, awayTeam, lastValidTimeText);
                                }
                                else
                                {
                                    _logger.LogDebug("Using inherited time for {HomeTeam} vs {AwayTeam}: '{InheritedTime}'", homeTeam, awayTeam, lastValidTimeText);
                                }
                                timeText = lastValidTimeText;
                            }
                            else
                            {
                                _logger.LogWarning("No previous valid time to inherit for {HomeTeam} vs {AwayTeam}{Cancelled}", 
                                    homeTeam, awayTeam, isCancelled ? " (cancelled match)" : "");
                            }
                        }
                        else
                        {
                            // Update the last valid time for future inheritance
                            lastValidTimeText = timeText;
                            _logger.LogDebug("Updated last valid time to: '{TimeText}'", timeText);
                        }
                        
                        // Check if this row has betting inputs (indicates open match)
                        var bettingInputs = cells[3].QuerySelectorAll("input[type='text']");
                        if (bettingInputs.Length >= 2)
                        {
                            _logger.LogDebug("Found open match: {HomeTeam} vs {AwayTeam} at {Time}{Cancelled}", 
                                homeTeam, awayTeam, timeText, isCancelled ? " (CANCELLED)" : "");
                            
                            // Parse the date/time - for now use a simple approach
                            // Format appears to be "08.07.25 21:00"
                            var startsAt = ParseMatchDateTime(timeText);
                            
                            matches.Add(CreateMatch(
                                homeTeam,
                                awayTeam,
                                startsAt,
                                currentMatchday,
                                isCancelled,
                                competition,
                                kicktippRoundName,
                                row));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing match row");
                    continue;
                }
            }

            matches = NormalizeWorldCupFinalRoundMatches(matches);

            _logger.LogInformation("Successfully parsed {MatchCount} open matches", matches.Count);
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetOpenPredictionsAsync");
            return new List<Match>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> PlaceBetAsync(string community, Match match, BetPrediction prediction, bool overrideBet = false)
    {
        try
        {
            var url = $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to access betting page. Status: {StatusCode}", response.StatusCode);
                return false;
            }
            
            var pageContent = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(pageContent));
            
            // Find the bet form
            var betForm = document.QuerySelector("form") as IHtmlFormElement;
            if (betForm == null)
            {
                _logger.LogWarning("Could not find betting form on the page");
                return false;
            }
            
            // Find the main content area
            var contentArea = document.QuerySelector("#kicktipp-content");
            if (contentArea == null)
            {
                _logger.LogWarning("Could not find content area on the betting page");
                return false;
            }
            
            // Find the table with predictions
            var tbody = contentArea.QuerySelector("tbody");
            if (tbody == null)
            {
                _logger.LogWarning("No betting table found");
                return false;
            }
            
            var rows = tbody.QuerySelectorAll("tr");
            var formData = new List<KeyValuePair<string, string>>();
            var matchFound = false;
            
            // Copy hidden inputs from the original form
            var hiddenInputs = betForm.QuerySelectorAll("input[type='hidden']");
            foreach (var hiddenInput in hiddenInputs.Cast<IHtmlInputElement>())
            {
                if (!string.IsNullOrEmpty(hiddenInput.Name) && hiddenInput.Value != null)
                {
                    formData.Add(new KeyValuePair<string, string>(hiddenInput.Name, hiddenInput.Value));
                }
            }
            
            // Find the specific match in the form and set its bet
            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 4) continue; // Need at least date, home team, road team, and bet inputs
                
                try
                {
                    var homeTeam = cells[1].TextContent?.Trim() ?? "";
                    var roadTeam = cells[2].TextContent?.Trim() ?? "";
                    
                    if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(roadTeam))
                        continue;
                    
                    // Check if this is the match we want to bet on
                    if (homeTeam == match.HomeTeam && roadTeam == match.AwayTeam)
                    {
                        // Find bet input fields in the row
                        var homeInput = cells[3].QuerySelector("input[id$='_heimTipp']") as IHtmlInputElement;
                        var awayInput = cells[3].QuerySelector("input[id$='_gastTipp']") as IHtmlInputElement;
                        
                        if (homeInput == null || awayInput == null)
                        {
                            _logger.LogWarning("No betting inputs found for {Match}, skipping", match);
                            continue;
                        }
                        
                        // Check if bets are already placed
                        var hasExistingHomeBet = !string.IsNullOrEmpty(homeInput.Value);
                        var hasExistingAwayBet = !string.IsNullOrEmpty(awayInput.Value);
                        
                        if ((hasExistingHomeBet || hasExistingAwayBet) && !overrideBet)
                        {
                            var existingBet = $"{homeInput.Value ?? ""}:{awayInput.Value ?? ""}";
                            _logger.LogInformation("{Match} - skipped, already placed {ExistingBet}", match, existingBet);
                            return true; // Consider this successful - bet already exists
                        }
                        
                        // Add bet to form data
                        if (!string.IsNullOrEmpty(homeInput.Name) && !string.IsNullOrEmpty(awayInput.Name))
                        {
                            formData.Add(new KeyValuePair<string, string>(homeInput.Name, prediction.HomeGoals.ToString()));
                            formData.Add(new KeyValuePair<string, string>(awayInput.Name, prediction.AwayGoals.ToString()));
                            matchFound = true;
                            _logger.LogInformation("{Match} - betting {Prediction}", match, prediction);
                        }
                        else
                        {
                            _logger.LogWarning("{Match} - input field names are missing, skipping", match);
                            continue;
                        }
                        
                        break; // Found our match, no need to continue
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing betting row");
                    continue;
                }
            }
            
            if (!matchFound)
            {
                _logger.LogWarning("Match {Match} not found in betting form", match);
                return false;
            }
            
            // Add other input fields that might have existing values
            var allInputs = betForm.QuerySelectorAll("input[type=text], input[type=number]").OfType<IHtmlInputElement>();
            foreach (var input in allInputs)
            {
                if (!string.IsNullOrEmpty(input.Name) && !string.IsNullOrEmpty(input.Value))
                {
                    // Only add if we haven't already added this field
                    if (!formData.Any(kv => kv.Key == input.Name))
                    {
                        formData.Add(new KeyValuePair<string, string>(input.Name, input.Value));
                    }
                }
            }
            
            // Find submit button
            var submitButton = betForm.QuerySelector("input[type=submit], button[type=submit]") as IHtmlElement;
            var submitName = "submitbutton"; // Default from Python
            
            if (submitButton != null)
            {
                if (submitButton is IHtmlInputElement inputSubmit && !string.IsNullOrEmpty(inputSubmit.Name))
                {
                    submitName = inputSubmit.Name;
                    formData.Add(new KeyValuePair<string, string>(submitName, inputSubmit.Value ?? "Submit"));
                }
                else if (submitButton is IHtmlButtonElement buttonSubmit && !string.IsNullOrEmpty(buttonSubmit.Name))
                {
                    submitName = buttonSubmit.Name;
                    formData.Add(new KeyValuePair<string, string>(submitName, buttonSubmit.Value ?? "Submit"));
                }
            }
            else
            {
                // Fallback to default submit button name
                formData.Add(new KeyValuePair<string, string>("submitbutton", "Submit"));
            }
            
            // Submit form
            var formActionUrl = string.IsNullOrEmpty(betForm.Action) ? url : 
                (betForm.Action.StartsWith("http") ? betForm.Action : 
                 betForm.Action.StartsWith("/") ? betForm.Action : 
                 $"{community}/{betForm.Action}");
            
            var formContent = new FormUrlEncodedContent(formData);
            var submitResponse = await _httpClient.PostAsync(formActionUrl, formContent);
            
            if (submitResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("✓ Successfully submitted bet for {Match}!", match);
                return true;
            }
            else
            {
                _logger.LogError("✗ Failed to submit bet. Status: {StatusCode}", submitResponse.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during bet placement");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> PlaceBetsAsync(string community, Dictionary<Match, BetPrediction> bets, bool overrideBets = false)
    {
        try
        {
            var url = $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to access betting page. Status: {StatusCode}", response.StatusCode);
                return false;
            }
            
            var pageContent = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(pageContent));
            
            // Find the bet form
            var betForm = document.QuerySelector("form") as IHtmlFormElement;
            if (betForm == null)
            {
                _logger.LogWarning("Could not find betting form on the page");
                return false;
            }
            
            // Find the main content area
            var contentArea = document.QuerySelector("#kicktipp-content");
            if (contentArea == null)
            {
                _logger.LogWarning("Could not find content area on the betting page");
                return false;
            }
            
            // Find the table with predictions
            var tbody = contentArea.QuerySelector("tbody");
            if (tbody == null)
            {
                _logger.LogWarning("No betting table found");
                return false;
            }
            
            var rows = tbody.QuerySelectorAll("tr");
            var formData = new List<KeyValuePair<string, string>>();
            var betsPlaced = 0;
            var betsSkipped = 0;
            
            // Add hidden fields from the form
            var hiddenInputs = betForm.QuerySelectorAll("input[type=hidden]").OfType<IHtmlInputElement>();
            foreach (var input in hiddenInputs)
            {
                if (!string.IsNullOrEmpty(input.Name) && input.Value != null)
                {
                    formData.Add(new KeyValuePair<string, string>(input.Name, input.Value));
                }
            }
            
            // Process all matches in the form
            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 4) continue; // Need at least date, home team, road team, and bet inputs
                
                try
                {
                    var homeTeam = cells[1].TextContent?.Trim() ?? "";
                    var roadTeam = cells[2].TextContent?.Trim() ?? "";
                    
                    if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(roadTeam))
                        continue;
                    
                    // Check if we have a bet for this match
                    var matchKey = bets.Keys.FirstOrDefault(m => m.HomeTeam == homeTeam && m.AwayTeam == roadTeam);
                    if (matchKey == null)
                    {
                        // Add existing bet values to maintain form state
                        var existingHomeInput = cells[3].QuerySelector("input[id$='_heimTipp']") as IHtmlInputElement;
                        var existingAwayInput = cells[3].QuerySelector("input[id$='_gastTipp']") as IHtmlInputElement;
                        
                        if (existingHomeInput != null && existingAwayInput != null && 
                            !string.IsNullOrEmpty(existingHomeInput.Name) && !string.IsNullOrEmpty(existingAwayInput.Name))
                        {
                            formData.Add(new KeyValuePair<string, string>(existingHomeInput.Name, existingHomeInput.Value ?? ""));
                            formData.Add(new KeyValuePair<string, string>(existingAwayInput.Name, existingAwayInput.Value ?? ""));
                        }
                        continue;
                    }
                    
                    var prediction = bets[matchKey];
                    
                    // Find bet input fields in the row
                    var homeInput = cells[3].QuerySelector("input[id$='_heimTipp']") as IHtmlInputElement;
                    var awayInput = cells[3].QuerySelector("input[id$='_gastTipp']") as IHtmlInputElement;
                    
                    if (homeInput == null || awayInput == null)
                    {
                        _logger.LogWarning("No betting inputs found for {MatchKey}, skipping", matchKey);
                        continue;
                    }
                    
                    // Check if bets are already placed
                    var hasExistingHomeBet = !string.IsNullOrEmpty(homeInput.Value);
                    var hasExistingAwayBet = !string.IsNullOrEmpty(awayInput.Value);
                    
                    if ((hasExistingHomeBet || hasExistingAwayBet) && !overrideBets)
                    {
                        var existingBet = $"{homeInput.Value ?? ""}:{awayInput.Value ?? ""}";
                        _logger.LogInformation("{MatchKey} - skipped, already placed {ExistingBet}", matchKey, existingBet);
                        betsSkipped++;
                        
                        // Keep existing values
                        if (!string.IsNullOrEmpty(homeInput.Name) && !string.IsNullOrEmpty(awayInput.Name))
                        {
                            formData.Add(new KeyValuePair<string, string>(homeInput.Name, homeInput.Value ?? ""));
                            formData.Add(new KeyValuePair<string, string>(awayInput.Name, awayInput.Value ?? ""));
                        }
                        continue;
                    }
                    
                    // Add bet to form data
                    if (!string.IsNullOrEmpty(homeInput.Name) && !string.IsNullOrEmpty(awayInput.Name))
                    {
                        formData.Add(new KeyValuePair<string, string>(homeInput.Name, prediction.HomeGoals.ToString()));
                        formData.Add(new KeyValuePair<string, string>(awayInput.Name, prediction.AwayGoals.ToString()));
                        betsPlaced++;
                        _logger.LogInformation("{MatchKey} - betting {Prediction}", matchKey, prediction);
                    }
                    else
                    {
                        _logger.LogWarning("{MatchKey} - input field names are missing, skipping", matchKey);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing betting row");
                    continue;
                }
            }
            
            _logger.LogInformation("Summary: {BetsPlaced} bets to place, {BetsSkipped} skipped", betsPlaced, betsSkipped);
            
            if (betsPlaced == 0)
            {
                _logger.LogInformation("No bets to place");
                return true;
            }
            
            // Find submit button
            var submitButton = betForm.QuerySelector("input[type=submit], button[type=submit]") as IHtmlElement;
            var submitName = "submitbutton"; // Default from Python
            
            if (submitButton != null)
            {
                if (submitButton is IHtmlInputElement inputSubmit && !string.IsNullOrEmpty(inputSubmit.Name))
                {
                    submitName = inputSubmit.Name;
                    formData.Add(new KeyValuePair<string, string>(submitName, inputSubmit.Value ?? "Submit"));
                }
                else if (submitButton is IHtmlButtonElement buttonSubmit && !string.IsNullOrEmpty(buttonSubmit.Name))
                {
                    submitName = buttonSubmit.Name;
                    formData.Add(new KeyValuePair<string, string>(submitName, buttonSubmit.Value ?? "Submit"));
                }
            }
            else
            {
                // Fallback to default submit button name
                formData.Add(new KeyValuePair<string, string>("submitbutton", "Submit"));
            }
            
            // Submit form
            var formActionUrl = string.IsNullOrEmpty(betForm.Action) ? url : 
                (betForm.Action.StartsWith("http") ? betForm.Action : 
                 betForm.Action.StartsWith("/") ? betForm.Action : 
                 $"{community}/{betForm.Action}");
            
            var formContent = new FormUrlEncodedContent(formData);
            var submitResponse = await _httpClient.PostAsync(formActionUrl, formContent);
            var submitContent = await submitResponse.Content.ReadAsStringAsync();
            
            if (!submitResponse.IsSuccessStatusCode)
            {
                _logger.LogError("✗ Failed to submit bets. Status: {StatusCode}", submitResponse.StatusCode);
                return false;
            }

            if (await IsKnownDrawNotAllowedSubmissionFailureAsync(submitResponse, submitContent))
            {
                _logger.LogWarning("Known Kicktipp draw-not-allowed rejection detected after bet submit. Valid rows may already be stored.");
                return false;
            }

            _logger.LogInformation("✓ Successfully submitted {BetsPlaced} bets!", betsPlaced);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during bet placement");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<List<TeamStanding>> GetStandingsAsync(string community)
    {
        // Create cache key based on community
        var cacheKey = $"standings_{community}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out List<TeamStanding>? cachedStandings))
        {
            _logger.LogDebug("Retrieved standings for {Community} from cache", community);
            return cachedStandings!;
        }

        try
        {
            var url = $"{community}/tabellen";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch standings page. Status: {StatusCode}", response.StatusCode);
                return new List<TeamStanding>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            var standings = new List<TeamStanding>();
            
            // Tournament pages can render one table per group; league pages render a single table.
            var standingsTables = document.QuerySelectorAll("table.sporttabelle");
            if (standingsTables.Length == 0)
            {
                _logger.LogWarning("Could not find standings table");
                return standings;
            }

            foreach (var standingsTable in standingsTables)
            {
                var groupName = ExtractStandingsGroupName(standingsTable);
                var tableBody = standingsTable.QuerySelector("tbody") ?? standingsTable;
                var rows = tableBody.QuerySelectorAll("tr");
                _logger.LogDebug("Found {RowCount} team rows in standings table for group {Group}", rows.Length, groupName ?? "(none)");

                foreach (var row in rows)
                {
                    try
                    {
                        var cells = row.QuerySelectorAll("td");
                        if (cells.Length >= 9) // Need at least 9 columns for all data
                        {
                            // Extract data from table cells
                            var positionText = cells[0].TextContent?.Trim().TrimEnd('.') ?? "";
                            var teamNameElement = cells[1].QuerySelector("div") ?? cells[1].QuerySelector("a");
                            var teamName = teamNameElement?.TextContent?.Trim() ?? cells[1].TextContent?.Trim() ?? "";
                            var gamesPlayedText = cells[2].TextContent?.Trim() ?? "";
                            var pointsText = cells[3].TextContent?.Trim() ?? "";
                            var goalsText = cells[4].TextContent?.Trim() ?? "";
                            var goalDifferenceText = cells[5].TextContent?.Trim() ?? "";
                            var winsText = cells[6].TextContent?.Trim() ?? "";
                            var drawsText = cells[7].TextContent?.Trim() ?? "";
                            var lossesText = cells[8].TextContent?.Trim() ?? "";

                            // Parse numeric values
                            if (int.TryParse(positionText, out var position) &&
                                int.TryParse(gamesPlayedText, out var gamesPlayed) &&
                                int.TryParse(pointsText, out var points) &&
                                int.TryParse(goalDifferenceText, out var goalDifference) &&
                                int.TryParse(winsText, out var wins) &&
                                int.TryParse(drawsText, out var draws) &&
                                int.TryParse(lossesText, out var losses))
                            {
                                // Parse goals (format: "15:8")
                                var goalsParts = goalsText.Split(':');
                                var goalsFor = 0;
                                var goalsAgainst = 0;

                                if (goalsParts.Length == 2)
                                {
                                    int.TryParse(goalsParts[0], out goalsFor);
                                    int.TryParse(goalsParts[1], out goalsAgainst);
                                }

                                var teamStanding = new TeamStanding(
                                    position,
                                    teamName,
                                    gamesPlayed,
                                    points,
                                    goalsFor,
                                    goalsAgainst,
                                    goalDifference,
                                    wins,
                                    draws,
                                    losses,
                                    groupName);

                                standings.Add(teamStanding);
                                _logger.LogDebug(
                                    "Parsed team standing: {Position}. {TeamName} - {Points} points (group {Group})",
                                    position,
                                    teamName,
                                    points,
                                    groupName ?? "(none)");
                            }
                            else
                            {
                                _logger.LogWarning("Failed to parse numeric values for team row");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing standings row");
                        continue;
                    }
                }
            }

            _logger.LogInformation("Successfully parsed {StandingsCount} team standings", standings.Count);
            
            // Cache the results for 20 minutes (standings change relatively infrequently)
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                SlidingExpiration = TimeSpan.FromMinutes(10) // Reset timer if accessed within 10 minutes
            };
            _cache.Set(cacheKey, standings, cacheOptions);
            _logger.LogDebug("Cached standings for {Community} for 20 minutes", community);
            
            return standings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetStandingsAsync");
            return new List<TeamStanding>();
        }
    }

    /// <inheritdoc />
    public Task<List<MatchWithHistory>> GetMatchesWithHistoryAsync(string community)
    {
        return GetMatchesWithHistoryInternalAsync(community, matchday: null, competition: null);
    }

    /// <inheritdoc />
    public Task<List<MatchWithHistory>> GetMatchesWithHistoryAsync(string community, string competition)
    {
        return GetMatchesWithHistoryInternalAsync(community, matchday: null, competition);
    }

    /// <inheritdoc />
    public Task<List<MatchWithHistory>> GetMatchesWithHistoryAsync(string community, int matchday)
    {
        return GetMatchesWithHistoryInternalAsync(community, matchday, competition: null);
    }

    /// <inheritdoc />
    public Task<List<MatchWithHistory>> GetMatchesWithHistoryAsync(string community, int matchday, string competition)
    {
        return GetMatchesWithHistoryInternalAsync(community, matchday, competition);
    }

    private async Task<List<MatchWithHistory>> GetMatchesWithHistoryInternalAsync(
        string community,
        int? matchday,
        string? competition)
    {
        // Create cache key based on community
        var competitionCacheKey = string.IsNullOrWhiteSpace(competition) ? "generic" : competition;
        var cacheKey = matchday.HasValue
            ? $"matches_history_{community}_{competitionCacheKey}_{matchday.Value}"
            : $"matches_history_{community}_{competitionCacheKey}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out List<MatchWithHistory>? cachedMatches))
        {
            _logger.LogDebug("Retrieved matches with history for {Community} from cache", community);
            return cachedMatches!;
        }

        try
        {
            var matches = new List<MatchWithHistory>();
            
            // First, get the tippabgabe page to find the link to spielinfos
            var tippabgabeUrl = matchday.HasValue
                ? $"{community}/tippabgabe?spieltagIndex={matchday.Value}"
                : $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(tippabgabeUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                return matches;
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            // Extract matchday from the tippabgabe page
            var currentMatchday = ExtractMatchdayFromPage(document);
            _logger.LogDebug("Extracted matchday for history extraction: {Matchday}", currentMatchday);
            var kicktippRoundName = ExtractKicktippRoundName(document);
            if (matchday.HasValue && currentMatchday != matchday.Value)
            {
                _logger.LogWarning("Requested history matchday {RequestedMatchday}, but page displayed {DisplayedMatchday}", matchday.Value, currentMatchday);
            }

            // Find the "Tippabgabe mit Spielinfos" link
            var spielinfoLink = document.QuerySelector("a[href*='spielinfo']");
            if (spielinfoLink == null)
            {
                _logger.LogWarning("Could not find Spielinfo link on tippabgabe page");
                return matches;
            }

            var spielinfoUrl = spielinfoLink.GetAttribute("href");
            if (string.IsNullOrEmpty(spielinfoUrl))
            {
                _logger.LogWarning("Spielinfo link has no href attribute");
                return matches;
            }

            // Make URL absolute if it's relative
            if (spielinfoUrl.StartsWith("/"))
            {
                spielinfoUrl = spielinfoUrl.Substring(1); // Remove leading slash
            }
            
            _logger.LogInformation("Starting to fetch match details from spielinfo pages...");

            // Navigate through all matches using the right arrow navigation
            var currentUrl = spielinfoUrl;
            var matchCount = 0;
            
            while (!string.IsNullOrEmpty(currentUrl))
            {
                try
                {
                    var spielinfoResponse = await _httpClient.GetAsync(currentUrl);
                    if (!spielinfoResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch spielinfo page: {Url}. Status: {StatusCode}", currentUrl, spielinfoResponse.StatusCode);
                        break;
                    }

                    var spielinfoContent = await spielinfoResponse.Content.ReadAsStringAsync();
                    var spielinfoDocument = await _browsingContext.OpenAsync(req => req.Content(spielinfoContent));

                    // Extract match information
                    var matchWithHistory = ExtractMatchWithHistoryFromSpielinfoPage(
                        spielinfoDocument,
                        currentMatchday,
                        competition,
                        kicktippRoundName);
                    if (matchWithHistory != null)
                    {
                        matches.Add(matchWithHistory);
                        matchCount++;
                        _logger.LogDebug("Extracted match {Count}: {Match}", matchCount, matchWithHistory.Match);
                    }

                    // Find the next match link (right arrow)
                    var nextLink = FindNextMatchLink(spielinfoDocument);
                    if (nextLink != null)
                    {
                        currentUrl = nextLink;
                        if (currentUrl.StartsWith("/"))
                        {
                            currentUrl = currentUrl.Substring(1); // Remove leading slash
                        }
                    }
                    else
                    {
                        // No more matches
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing spielinfo page: {Url}", currentUrl);
                    break;
                }
            }

            matches = NormalizeWorldCupFinalRoundMatches(matches);

            _logger.LogInformation("Successfully extracted {MatchCount} matches with history", matches.Count);
            
            // Cache the results for 15 minutes (match info changes less frequently than live scores)
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                SlidingExpiration = TimeSpan.FromMinutes(7) // Reset timer if accessed within 7 minutes
            };
            _cache.Set(cacheKey, matches, cacheOptions);
            _logger.LogDebug("Cached matches with history for {Community} for 15 minutes", community);
            
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetMatchesWithHistoryAsync");
            return new List<MatchWithHistory>();
        }
    }

    /// <inheritdoc />
    public async Task<int> GetCurrentTippuebersichtMatchdayAsync(string community)
    {
        var document = await GetTippuebersichtDocumentAsync(community, null);
        if (document == null)
        {
            return 1;
        }

        return ExtractMatchdayFromPage(document);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CollectedMatchOutcome>> GetMatchdayOutcomesAsync(string community, int matchday)
    {
        var cacheKey = $"tippuebersicht_outcomes_{community}_{matchday}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CollectedMatchOutcome>? cachedOutcomes))
        {
            _logger.LogDebug("Retrieved tippuebersicht outcomes for {Community} matchday {Matchday} from cache", community, matchday);
            return cachedOutcomes!;
        }

        var document = await GetTippuebersichtDocumentAsync(community, matchday);
        if (document == null)
        {
            return Array.Empty<CollectedMatchOutcome>();
        }

        var displayedMatchday = ExtractMatchdayFromPage(document);
        if (displayedMatchday != matchday)
        {
            _logger.LogWarning("Requested tippuebersicht matchday {RequestedMatchday}, but page displayed {DisplayedMatchday}", matchday, displayedMatchday);
        }

        var outcomes = ParseTippuebersichtMatchdayOutcomes(document, displayedMatchday)
            .AsReadOnly();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(5)
        };

        _cache.Set(cacheKey, outcomes, cacheOptions);
        return outcomes;
    }

    /// <inheritdoc />
    public async Task<KicktippCommunityMatchdaySnapshot?> GetCommunityMatchdaySnapshotAsync(string community, int matchday)
    {
        var cacheKey = $"tippuebersicht_snapshot_{community}_{matchday}";
        if (_cache.TryGetValue(cacheKey, out KicktippCommunityMatchdaySnapshot? cachedSnapshot))
        {
            _logger.LogDebug("Retrieved tippuebersicht snapshot for {Community} matchday {Matchday} from cache", community, matchday);
            return cachedSnapshot;
        }

        var document = await GetTippuebersichtDocumentAsync(community, matchday);
        if (document == null)
        {
            return null;
        }

        var displayedMatchday = ExtractMatchdayFromPage(document);
        if (displayedMatchday != matchday)
        {
            _logger.LogWarning("Requested tippuebersicht snapshot matchday {RequestedMatchday}, but page displayed {DisplayedMatchday}", matchday, displayedMatchday);
        }

        var outcomes = ParseTippuebersichtMatchdayOutcomes(document, displayedMatchday)
            .AsReadOnly();
        var participants = ParseTippuebersichtParticipantSnapshots(document, displayedMatchday, outcomes)
            .AsReadOnly();

        var snapshot = new KicktippCommunityMatchdaySnapshot(displayedMatchday, outcomes, participants);
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(5)
        };

        _cache.Set(cacheKey, snapshot, cacheOptions);
        return snapshot;
    }

    /// <inheritdoc />
    public Task<(List<MatchResult> homeTeamHomeHistory, List<MatchResult> awayTeamAwayHistory)> GetHomeAwayHistoryAsync(
        string community,
        string homeTeam,
        string awayTeam) =>
        GetHomeAwayHistoryInternalAsync(community, homeTeam, awayTeam, matchday: null);

    /// <inheritdoc />
    public Task<(List<MatchResult> homeTeamHomeHistory, List<MatchResult> awayTeamAwayHistory)> GetHomeAwayHistoryAsync(
        string community,
        string homeTeam,
        string awayTeam,
        int matchday)
    {
        if (matchday <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(matchday), matchday, "Matchday must be positive.");
        }

        return GetHomeAwayHistoryInternalAsync(community, homeTeam, awayTeam, matchday);
    }

    private async Task<(List<MatchResult> homeTeamHomeHistory, List<MatchResult> awayTeamAwayHistory)> GetHomeAwayHistoryInternalAsync(
        string community,
        string homeTeam,
        string awayTeam,
        int? matchday)
    {
        try
        {
            // First, get the tippabgabe page to find the link to spielinfos
            var tippabgabeUrl = matchday.HasValue
                ? $"{community}/tippabgabe?spieltagIndex={matchday.Value}"
                : $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(tippabgabeUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                return (new List<MatchResult>(), new List<MatchResult>());
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            // Find the "Tippabgabe mit Spielinfos" link
            var spielinfoLink = document.QuerySelector("a[href*='spielinfo']");
            if (spielinfoLink == null)
            {
                _logger.LogWarning("Could not find Spielinfo link on tippabgabe page");
                return (new List<MatchResult>(), new List<MatchResult>());
            }

            var spielinfoUrl = spielinfoLink.GetAttribute("href");
            if (string.IsNullOrEmpty(spielinfoUrl))
            {
                _logger.LogWarning("Spielinfo link has no href attribute");
                return (new List<MatchResult>(), new List<MatchResult>());
            }

            // Make URL absolute if it's relative
            if (spielinfoUrl.StartsWith("/"))
            {
                spielinfoUrl = spielinfoUrl.Substring(1); // Remove leading slash
            }

            // Navigate through all matches using the right arrow navigation
            var currentUrl = spielinfoUrl;
            
            while (!string.IsNullOrEmpty(currentUrl))
            {
                try
                {
                    // Add ansicht=2 parameter for home/away history
                    var homeAwayUrl = currentUrl.Contains('?') 
                        ? $"{currentUrl}&ansicht=2" 
                        : $"{currentUrl}?ansicht=2";
                    
                    var spielinfoResponse = await _httpClient.GetAsync(homeAwayUrl);
                    if (!spielinfoResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch spielinfo page: {Url}. Status: {StatusCode}", homeAwayUrl, spielinfoResponse.StatusCode);
                        break;
                    }

                    var spielinfoContent = await spielinfoResponse.Content.ReadAsStringAsync();
                    var spielinfoDocument = await _browsingContext.OpenAsync(req => req.Content(spielinfoContent));

                    // Check if this page contains our match
                    if (IsMatchOnPage(spielinfoDocument, homeTeam, awayTeam))
                    {
                        // Extract home team home history
                        var homeTeamHomeHistory = ExtractTeamHistory(spielinfoDocument, "spielinfoHeim");
                        
                        // Extract away team away history  
                        var awayTeamAwayHistory = ExtractTeamHistory(spielinfoDocument, "spielinfoGast");

                        return (homeTeamHomeHistory, awayTeamAwayHistory);
                    }

                    // Find the next match link (right arrow)
                    var nextLink = FindNextMatchLink(spielinfoDocument);
                    if (nextLink != null)
                    {
                        currentUrl = nextLink;
                        if (currentUrl.StartsWith("/"))
                        {
                            currentUrl = currentUrl.Substring(1); // Remove leading slash
                        }
                    }
                    else
                    {
                        // No more matches
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing spielinfo page for home/away history: {CurrentUrl}", currentUrl);
                    break;
                }
            }

            _logger.LogWarning("Could not find match {HomeTeam} vs {AwayTeam} in spielinfo pages", homeTeam, awayTeam);
            return (new List<MatchResult>(), new List<MatchResult>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetHomeAwayHistoryAsync for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
            return (new List<MatchResult>(), new List<MatchResult>());
        }
    }

    /// <inheritdoc />
    public async Task<List<MatchResult>> GetHeadToHeadHistoryAsync(string community, string homeTeam, string awayTeam)
    {
        try
        {
            // First, get the tippabgabe page to find the link to spielinfos
            var tippabgabeUrl = $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(tippabgabeUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                return new List<MatchResult>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            // Find the "Tippabgabe mit Spielinfos" link
            var spielinfoLink = document.QuerySelector("a[href*='spielinfo']");
            if (spielinfoLink == null)
            {
                _logger.LogWarning("Could not find Spielinfo link on tippabgabe page");
                return new List<MatchResult>();
            }

            var spielinfoUrl = spielinfoLink.GetAttribute("href");
            if (string.IsNullOrEmpty(spielinfoUrl))
            {
                _logger.LogWarning("Spielinfo link has no href attribute");
                return new List<MatchResult>();
            }

            // Make URL absolute if it's relative
            if (spielinfoUrl.StartsWith("/"))
            {
                spielinfoUrl = spielinfoUrl.Substring(1); // Remove leading slash
            }

            // Navigate through all matches using the right arrow navigation
            var currentUrl = spielinfoUrl;
            
            while (!string.IsNullOrEmpty(currentUrl))
            {
                try
                {
                    // Add ansicht=3 parameter for head-to-head history
                    var headToHeadUrl = currentUrl.Contains('?') 
                        ? $"{currentUrl}&ansicht=3" 
                        : $"{currentUrl}?ansicht=3";
                    
                    var spielinfoResponse = await _httpClient.GetAsync(headToHeadUrl);
                    if (!spielinfoResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch spielinfo page: {Url}. Status: {StatusCode}", headToHeadUrl, spielinfoResponse.StatusCode);
                        break;
                    }

                    var spielinfoContent = await spielinfoResponse.Content.ReadAsStringAsync();
                    var spielinfoDocument = await _browsingContext.OpenAsync(req => req.Content(spielinfoContent));

                    // Check if this page contains our match
                    if (IsMatchOnPage(spielinfoDocument, homeTeam, awayTeam))
                    {
                        // Extract head-to-head history
                        return ExtractTeamHistory(spielinfoDocument, "spielinfoDirekterVergleich");
                    }

                    // Find the next match link (right arrow)
                    var nextLink = FindNextMatchLink(spielinfoDocument);
                    if (nextLink != null)
                    {
                        currentUrl = nextLink;
                        if (currentUrl.StartsWith("/"))
                        {
                            currentUrl = currentUrl.Substring(1); // Remove leading slash
                        }
                    }
                    else
                    {
                        // No more matches
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing spielinfo page for head-to-head history: {CurrentUrl}", currentUrl);
                    break;
                }
            }

            _logger.LogWarning("Could not find match {HomeTeam} vs {AwayTeam} in spielinfo pages", homeTeam, awayTeam);
            return new List<MatchResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetHeadToHeadHistoryAsync for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
            return new List<MatchResult>();
        }
    }

    /// <inheritdoc />
    public Task<List<HeadToHeadResult>> GetHeadToHeadDetailedHistoryAsync(
        string community,
        string homeTeam,
        string awayTeam) =>
        GetHeadToHeadDetailedHistoryInternalAsync(community, homeTeam, awayTeam, matchday: null);

    /// <inheritdoc />
    public Task<List<HeadToHeadResult>> GetHeadToHeadDetailedHistoryAsync(
        string community,
        string homeTeam,
        string awayTeam,
        int matchday)
    {
        if (matchday <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(matchday), matchday, "Matchday must be positive.");
        }

        return GetHeadToHeadDetailedHistoryInternalAsync(community, homeTeam, awayTeam, matchday);
    }

    private async Task<List<HeadToHeadResult>> GetHeadToHeadDetailedHistoryInternalAsync(
        string community,
        string homeTeam,
        string awayTeam,
        int? matchday)
    {
        try
        {
            // First, get the tippabgabe page to find the link to spielinfos
            var tippabgabeUrl = matchday.HasValue
                ? $"{community}/tippabgabe?spieltagIndex={matchday.Value}"
                : $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(tippabgabeUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                return new List<HeadToHeadResult>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            // Find the "Tippabgabe mit Spielinfos" link
            var spielinfoLink = document.QuerySelector("a[href*='spielinfo']");
            if (spielinfoLink == null)
            {
                _logger.LogWarning("Could not find Spielinfo link on tippabgabe page");
                return new List<HeadToHeadResult>();
            }

            var spielinfoUrl = spielinfoLink.GetAttribute("href");
            if (string.IsNullOrEmpty(spielinfoUrl))
            {
                _logger.LogWarning("Spielinfo link has no href attribute");
                return new List<HeadToHeadResult>();
            }

            // Make URL absolute if it's relative
            if (spielinfoUrl.StartsWith("/"))
            {
                spielinfoUrl = spielinfoUrl.Substring(1); // Remove leading slash
            }

            // Navigate through all matches using the right arrow navigation
            var currentUrl = spielinfoUrl;
            
            while (!string.IsNullOrEmpty(currentUrl))
            {
                try
                {
                    // Append ansicht=3 to get head-to-head view
                    var urlWithAnsicht = currentUrl.Contains('?') ? $"{currentUrl}&ansicht=3" : $"{currentUrl}?ansicht=3";
                    var spielinfoResponse = await _httpClient.GetAsync(urlWithAnsicht);
                    
                    if (!spielinfoResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch spielinfo page: {Url}. Status: {StatusCode}", urlWithAnsicht, spielinfoResponse.StatusCode);
                        break;
                    }

                    var spielinfoContent = await spielinfoResponse.Content.ReadAsStringAsync();
                    var spielinfoDocument = await _browsingContext.OpenAsync(req => req.Content(spielinfoContent));

                    // Check if this page contains our match
                    if (IsMatchOnPage(spielinfoDocument, homeTeam, awayTeam))
                    {
                        // Extract head-to-head history from this page
                        return ExtractHeadToHeadHistory(spielinfoDocument);
                    }

                    // Find the next match link (right arrow)
                    var nextLink = FindNextMatchLink(spielinfoDocument);
                    if (nextLink != null)
                    {
                        currentUrl = nextLink;
                        if (currentUrl.StartsWith("/"))
                        {
                            currentUrl = currentUrl.Substring(1); // Remove leading slash
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing spielinfo page: {Url}", currentUrl);
                    break;
                }
            }

            _logger.LogWarning("Could not find match {HomeTeam} vs {AwayTeam} in spielinfo pages", homeTeam, awayTeam);
            return new List<HeadToHeadResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetHeadToHeadDetailedHistoryAsync for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
            return new List<HeadToHeadResult>();
        }
    }
    private bool IsMatchOnPage(IDocument document, string homeTeam, string awayTeam)
    {
        try
        {
            // Look for the match in the tippabgabe table
            var matchRows = document.QuerySelectorAll("table.tippabgabe tbody tr");
            
            foreach (var row in matchRows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length >= 3)
                {
                    var pageHomeTeam = cells[1].TextContent?.Trim() ?? "";
                    var pageAwayTeam = cells[2].TextContent?.Trim() ?? "";
                    
                    if (pageHomeTeam == homeTeam && pageAwayTeam == awayTeam)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking if match is on page");
            return false;
        }
    }

    private MatchWithHistory? ExtractMatchWithHistoryFromSpielinfoPage(
        IDocument document,
        int matchday,
        string? competition,
        string? kicktippRoundName)
    {
        try
        {
            // Extract match information from the tippabgabe table
            // Look for all rows in the table, not just the first one
            var matchRows = document.QuerySelectorAll("table.tippabgabe tbody tr");
            if (matchRows.Length == 0)
            {
                _logger.LogWarning("Could not find any match rows in tippabgabe table on spielinfo page");
                return null;
            }

            _logger.LogDebug("Found {RowCount} rows in tippabgabe table", matchRows.Length);

            // Find the row that contains match data (has input fields for betting)
            IElement? matchRow = null;
            foreach (var row in matchRows)
            {
                var rowCells = row.QuerySelectorAll("td");
                if (rowCells.Length >= 4)
                {
                    // Check if this row has betting inputs (indicates it's the match row)
                    var bettingInputs = rowCells[3].QuerySelectorAll("input[type='text']");
                    if (bettingInputs.Length >= 2)
                    {
                        matchRow = row;
                        break;
                    }
                }
            }

            if (matchRow == null)
            {
                _logger.LogWarning("Could not find match row with betting inputs in tippabgabe table");
                return null;
            }

            var cells = matchRow.QuerySelectorAll("td");
            if (cells.Length < 4)
            {
                _logger.LogWarning("Match row does not have enough cells");
                return null;
            }

            _logger.LogDebug("Found {CellCount} cells in match row", cells.Length);
            for (int i = 0; i < Math.Min(cells.Length, 5); i++)
            {
                _logger.LogDebug("Cell[{Index}]: '{Content}' (Class: '{Class}')", i, cells[i].TextContent?.Trim(), cells[i].ClassName);
            }

            var timeText = cells[0].TextContent?.Trim() ?? "";
            var homeTeam = cells[1].TextContent?.Trim() ?? "";
            var awayTeam = cells[2].TextContent?.Trim() ?? "";

            _logger.LogDebug("Extracted from spielinfo page - Time: '{TimeText}', Home: '{HomeTeam}', Away: '{AwayTeam}'", timeText, homeTeam, awayTeam);

            if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam))
            {
                _logger.LogWarning("Could not extract team names from match table");
                return null;
            }

            // Check if match is cancelled ("Abgesagt" in German)
            // Note: On spielinfo pages, cancelled matches may still show - process them with IsCancelled flag
            var isCancelled = IsCancelledTimeText(timeText);
            if (isCancelled)
            {
                _logger.LogWarning(
                    "Match {HomeTeam} vs {AwayTeam} is cancelled (Abgesagt) on spielinfo page. " +
                    "Using current time as fallback since spielinfo doesn't provide time inheritance context.",
                    homeTeam, awayTeam);
            }

            var startsAt = ParseMatchDateTime(timeText);
            var match = CreateMatch(
                homeTeam,
                awayTeam,
                startsAt,
                matchday,
                isCancelled,
                competition,
                kicktippRoundName,
                matchRow);

            // Extract home team history
            var homeTeamHistory = ExtractTeamHistory(document, "spielinfoHeim");
            
            // Extract away team history
            var awayTeamHistory = ExtractTeamHistory(document, "spielinfoGast");

            return new MatchWithHistory(match, homeTeamHistory, awayTeamHistory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting match with history from spielinfo page");
            return null;
        }
    }

    private List<MatchResult> ExtractTeamHistory(IDocument document, string tableClass)
    {
        var results = new List<MatchResult>();
        
        try
        {
            var table = document.QuerySelector($"table.{tableClass} tbody");
            if (table == null)
            {
                _logger.LogDebug("Could not find team history table with class: {TableClass}", tableClass);
                return results;
            }

            var rows = table.QuerySelectorAll("tr");
            foreach (var row in rows)
            {
                try
                {
                    var cells = row.QuerySelectorAll("td");
                    
                    // Handle different table formats
                    string competition, homeTeam, awayTeam;
                    var resultCell = cells.Last(); // Result is always in the last cell
                    var homeGoals = (int?)null;
                    var awayGoals = (int?)null;
                    var outcome = MatchOutcome.Pending;
                    string? annotation = null;

                    if (tableClass == "spielinfoDirekterVergleich")
                    {
                        // Direct comparison format: Season | Matchday | Date | Home | Away | Result
                        if (cells.Length < 6)
                            continue;
                            
                        competition = $"{cells[0].TextContent?.Trim()} {cells[1].TextContent?.Trim()}";
                        homeTeam = cells[3].TextContent?.Trim() ?? "";
                        awayTeam = cells[4].TextContent?.Trim() ?? "";
                    }
                    else
                    {
                        // Standard format: Competition | Home | Away | Result
                        if (cells.Length < 4)
                            continue;
                            
                        competition = cells[0].TextContent?.Trim() ?? "";
                        homeTeam = cells[1].TextContent?.Trim() ?? "";
                        awayTeam = cells[2].TextContent?.Trim() ?? "";
                    }
                    
                    // Parse the score from the result cell
                    var scoreElements = resultCell.QuerySelectorAll(".kicktipp-heim, .kicktipp-gast");
                    if (scoreElements.Length >= 2)
                    {
                        var homeScoreText = scoreElements[0].TextContent?.Trim() ?? "";
                        var awayScoreText = scoreElements[1].TextContent?.Trim() ?? "";
                        
                        if (homeScoreText != "-" && awayScoreText != "-")
                        {
                            if (int.TryParse(homeScoreText, out var homeScore) && int.TryParse(awayScoreText, out var awayScore))
                            {
                                homeGoals = homeScore;
                                awayGoals = awayScore;
                                
                                // Determine outcome from team's perspective based on CSS classes
                                var homeTeamCell = tableClass == "spielinfoDirekterVergleich" ? cells[3] : cells[1];
                                var awayTeamCell = tableClass == "spielinfoDirekterVergleich" ? cells[4] : cells[2];
                                
                                var isHomeTeam = homeTeamCell.ClassList.Contains("sieg") || homeTeamCell.ClassList.Contains("niederlage") || homeTeamCell.ClassList.Contains("remis");
                                var isAwayTeam = awayTeamCell.ClassList.Contains("sieg") || awayTeamCell.ClassList.Contains("niederlage") || awayTeamCell.ClassList.Contains("remis");
                                
                                if (isHomeTeam)
                                {
                                    outcome = homeScore > awayScore ? MatchOutcome.Win : 
                                             homeScore < awayScore ? MatchOutcome.Loss : MatchOutcome.Draw;
                                }
                                else if (isAwayTeam)
                                {
                                    outcome = awayScore > homeScore ? MatchOutcome.Win : 
                                             awayScore < homeScore ? MatchOutcome.Loss : MatchOutcome.Draw;
                                }
                                else
                                {
                                    // Fallback: determine from score (neutral perspective)
                                    outcome = homeScore == awayScore ? MatchOutcome.Draw : 
                                             homeScore > awayScore ? MatchOutcome.Win : MatchOutcome.Loss;
                                }
                            }
                        }
                    }

                    // Extract annotation if present (e.g., "n.E." for penalty shootout)
                    var annotationElement = resultCell.QuerySelector(".kicktipp-zusatz");
                    if (annotationElement != null)
                    {
                        annotation = ExpandAnnotation(annotationElement.TextContent?.Trim());
                    }

                    var matchResult = new MatchResult(competition, homeTeam, awayTeam, homeGoals, awayGoals, outcome, annotation);
                    results.Add(matchResult);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error parsing team history row");
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting team history for table class: {TableClass}", tableClass);
        }

        return results;
    }

    private List<HeadToHeadResult> ExtractHeadToHeadHistory(IDocument document)
    {
        var results = new List<HeadToHeadResult>();
        
        try
        {
            var table = document.QuerySelector("table.spielinfoDirekterVergleich tbody");
            if (table == null)
            {
                _logger.LogDebug("Could not find head-to-head table with class: spielinfoDirekterVergleich");
                return results;
            }

            var rows = table.QuerySelectorAll("tr");
            foreach (var row in rows)
            {
                try
                {
                    var cells = row.QuerySelectorAll("td");
                    
                    // Direct comparison format: Season | Matchday | Date | Home | Away | Result
                    if (cells.Length < 6)
                        continue;
                    
                    var league = cells[0].TextContent?.Trim() ?? "";
                    var matchday = cells[1].TextContent?.Trim() ?? "";
                    var playedAt = cells[2].TextContent?.Trim() ?? "";
                    var homeTeam = cells[3].TextContent?.Trim() ?? "";
                    var awayTeam = cells[4].TextContent?.Trim() ?? "";
                    
                    // Extract score from the result cell
                    var resultCell = cells[5];
                    var score = "";
                    string? annotation = null;
                    
                    var scoreElements = resultCell.QuerySelectorAll(".kicktipp-heim, .kicktipp-gast");
                    if (scoreElements.Length >= 2)
                    {
                        var homeScoreText = scoreElements[0].TextContent?.Trim() ?? "";
                        var awayScoreText = scoreElements[1].TextContent?.Trim() ?? "";
                        
                        if (homeScoreText != "-" && awayScoreText != "-")
                        {
                            score = $"{homeScoreText}:{awayScoreText}";
                        }
                    }

                    // Extract annotation if present (e.g., "n.E." for penalty shootout)
                    var annotationElement = resultCell.QuerySelector(".kicktipp-zusatz");
                    if (annotationElement != null)
                    {
                        annotation = ExpandAnnotation(annotationElement.TextContent?.Trim());
                    }

                    var headToHeadResult = new HeadToHeadResult(league, matchday, playedAt, homeTeam, awayTeam, score, annotation);
                    results.Add(headToHeadResult);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error parsing head-to-head row");
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting head-to-head history");
        }

        return results;
    }

    private string? FindNextMatchLink(IDocument document)
    {
        try
        {
            // Look for the right arrow button in the match navigation
            var nextButton = document.QuerySelector(".prevnextNext a");
            if (nextButton == null)
            {
                _logger.LogDebug("No next match button found");
                return null;
            }

            // Check if the button is disabled
            var parentDiv = nextButton.ParentElement;
            if (parentDiv?.ClassList.Contains("disabled") == true)
            {
                _logger.LogDebug("Next match button is disabled - reached end of matches");
                return null;
            }

            var href = nextButton.GetAttribute("href");
            if (string.IsNullOrEmpty(href))
            {
                _logger.LogDebug("Next match button has no href");
                return null;
            }

            _logger.LogDebug("Found next match link: {Href}", href);
            return href;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding next match link");
            return null;
        }
    }

    private ZonedDateTime ParseMatchDateTime(string timeText)
    {
        try
        {
            // Handle empty or null time text
            // Use MinValue to ensure database key consistency and prevent orphaned predictions
            // See docs/features/cancelled-matches.md for design rationale
            if (string.IsNullOrWhiteSpace(timeText))
            {
                _logger.LogWarning("Match time text is empty, using MinValue for database consistency");
                return DateTimeOffset.MinValue.ToZonedDateTime();
            }

            // Expected formats: "22.08.25 20:30" and "22.08.2026 20:30".
            _logger.LogDebug("Attempting to parse time: '{TimeText}'", timeText);
            var formats = new[] { "dd.MM.yy HH:mm", "dd.MM.yyyy HH:mm" };
            if (DateTime.TryParseExact(timeText, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            {
                _logger.LogDebug("Successfully parsed time: {DateTime}", dateTime);
                var localDateTime = LocalDateTime.FromDateTime(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified));
                return BerlinTimeZone.AtLeniently(localDateTime);
            }
            
            // Fallback to MinValue if parsing fails - ensures database key consistency
            // and prevents orphaned predictions from being created with varying timestamps
            // See docs/features/cancelled-matches.md for design rationale
            _logger.LogWarning("Could not parse match time: '{TimeText}', using MinValue for database consistency", timeText);
            return DateTimeOffset.MinValue.ToZonedDateTime();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing match time '{TimeText}'", timeText);
            return DateTimeOffset.MinValue.ToZonedDateTime();
        }
    }

    private static string? ExtractStandingsGroupName(IElement standingsTable)
    {
        var caption = ExtractGroupLabel(standingsTable.QuerySelector("caption")?.TextContent);
        if (!string.IsNullOrWhiteSpace(caption))
        {
            return caption;
        }

        foreach (var headerCell in standingsTable.QuerySelectorAll("thead th, tr th"))
        {
            var headerLabel = ExtractGroupLabel(headerCell.TextContent);
            if (!string.IsNullOrWhiteSpace(headerLabel))
            {
                return headerLabel;
            }
        }

        for (var current = standingsTable; current is not null; current = current.ParentElement)
        {
            var labelFromPreviousSibling = ExtractGroupLabelFromPreviousSiblings(current);
            if (!string.IsNullOrWhiteSpace(labelFromPreviousSibling))
            {
                return labelFromPreviousSibling;
            }

            if (current != standingsTable && ContainsOnlyCurrentStandingsTable(current, standingsTable))
            {
                var labelFromWrapper = ExtractGroupLabel(current.TextContent);
                if (!string.IsNullOrWhiteSpace(labelFromWrapper))
                {
                    return labelFromWrapper;
                }
            }

            if (current.TagName.Equals("BODY", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return null;
    }

    private static string? ExtractGroupLabelFromPreviousSiblings(IElement element)
    {
        for (var sibling = element.PreviousElementSibling; sibling is not null; sibling = sibling.PreviousElementSibling)
        {
            if (IsStandingsTableContainer(sibling))
            {
                foreach (var previousHeading in sibling.QuerySelectorAll("h1,h2,h3,h4,h5,h6").Reverse())
                {
                    var headingLabel = ExtractGroupLabel(previousHeading.TextContent);
                    if (!string.IsNullOrWhiteSpace(headingLabel))
                    {
                        return headingLabel;
                    }
                }

                break;
            }

            var heading = IsHeading(sibling)
                ? sibling
                : sibling.QuerySelector("h1,h2,h3,h4,h5,h6");
            var label = ExtractGroupLabel(heading?.TextContent);
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            label = ExtractGroupLabel(sibling.TextContent);
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }
        }

        return null;
    }

    private static bool ContainsOnlyCurrentStandingsTable(IElement candidate, IElement standingsTable)
    {
        var nestedStandingsTables = candidate.QuerySelectorAll("table.sporttabelle");
        return nestedStandingsTables.Length == 1 && ReferenceEquals(nestedStandingsTables[0], standingsTable);
    }

    private static bool IsStandingsTableContainer(IElement element)
    {
        return element.Matches("table.sporttabelle") || element.QuerySelector("table.sporttabelle") is not null;
    }

    private static bool IsHeading(IElement element)
    {
        return element.TagName.ToUpperInvariant() is "H1" or "H2" or "H3" or "H4" or "H5" or "H6";
    }

    private static string? ExtractGroupLabel(string? text)
    {
        var normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var match = Regex.Match(
            normalized,
            @"\b(?<prefix>Gruppe|Group)\s+(?<group>[A-Z])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var prefix = match.Groups["prefix"].Value.Equals("group", StringComparison.OrdinalIgnoreCase)
            ? "Group"
            : "Gruppe";
        return $"{prefix} {match.Groups["group"].Value.ToUpperInvariant()}";
    }

    private static string NormalizeWhitespace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), @"\s+", " ");
    }

    /// <summary>
    /// Determines if the given time text indicates a cancelled match.
    /// </summary>
    /// <param name="timeText">The time text from the Kicktipp page.</param>
    /// <returns>True if the match is cancelled ("Abgesagt" in German), false otherwise.</returns>
    /// <remarks>
    /// <para>
    /// Cancelled matches on Kicktipp display "Abgesagt" instead of a date/time in the schedule.
    /// These matches can still receive predictions, so we continue processing them rather than skipping.
    /// </para>
    /// <para>
    /// <b>Design Decision:</b> We treat "Abgesagt" similar to an empty time cell and inherit the
    /// previous valid time. This preserves database key consistency since the composite key
    /// (HomeTeam, AwayTeam, StartsAt, ...) must remain stable across prediction operations.
    /// </para>
    /// <para>
    /// See <c>docs/features/cancelled-matches.md</c> for complete design rationale.
    /// </para>
    /// </remarks>
    private static bool IsCancelledTimeText(string timeText)
    {
        return string.Equals(timeText, "Abgesagt", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IDocument?> GetTippuebersichtDocumentAsync(string community, int? matchday)
    {
        try
        {
            var url = matchday.HasValue
                ? $"{community}/tippuebersicht?spieltagIndex={matchday.Value}"
                : $"{community}/tippuebersicht";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippuebersicht page {Url}. Status: {StatusCode}", url, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var responseUrl = response.RequestMessage?.RequestUri?.ToString();
            return await _browsingContext.OpenAsync(req => req.Content(content).Address(responseUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tippuebersicht page for {Community} matchday {Matchday}", community, matchday);
            return null;
        }
    }

    private List<CollectedMatchOutcome> ParseTippuebersichtMatchdayOutcomes(IDocument document, int matchday)
    {
        var outcomes = new List<CollectedMatchOutcome>();

        var matchTable = document.QuerySelector("#spielplanSpiele tbody");
        if (matchTable == null)
        {
            _logger.LogWarning("Could not find tippuebersicht match table for matchday {Matchday}", matchday);
            return outcomes;
        }

        var matchRows = matchTable.QuerySelectorAll("tr");
        string lastValidTimeText = string.Empty;

        foreach (var row in matchRows)
        {
            try
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 4)
                {
                    continue;
                }

                var timeText = cells[0].TextContent?.Trim() ?? string.Empty;
                var homeTeam = cells[1].TextContent?.Trim() ?? string.Empty;
                var awayTeam = cells[2].TextContent?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
                {
                    continue;
                }

                var isCancelled = IsCancelledTimeText(timeText);
                if (string.IsNullOrWhiteSpace(timeText) || isCancelled)
                {
                    if (!string.IsNullOrWhiteSpace(lastValidTimeText))
                    {
                        timeText = lastValidTimeText;
                    }
                }
                else
                {
                    lastValidTimeText = timeText;
                }

                var startsAt = ParseMatchDateTime(timeText);
                var (homeGoals, awayGoals, availability) = ParseMatchOutcome(cells[3]);
                var tippSpielId = ExtractTippSpielId(row.GetAttribute("data-url"));

                outcomes.Add(new CollectedMatchOutcome(
                    homeTeam,
                    awayTeam,
                    startsAt,
                    matchday,
                    homeGoals,
                    awayGoals,
                    availability,
                    tippSpielId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing tippuebersicht row for matchday {Matchday}", matchday);
            }
        }

        _logger.LogInformation("Parsed {MatchCount} tippuebersicht matches for matchday {Matchday}", outcomes.Count, matchday);
        return outcomes;
    }

    private List<KicktippCommunityParticipantSnapshot> ParseTippuebersichtParticipantSnapshots(
        IDocument document,
        int matchday,
        IReadOnlyList<CollectedMatchOutcome> outcomes)
    {
        var rankingTable = document.QuerySelector("#ranking");
        if (rankingTable == null)
        {
            _logger.LogWarning("Could not find tippuebersicht ranking table for matchday {Matchday}", matchday);
            return [];
        }

        var completedMappings = BuildCompletedRankingEventMappings(rankingTable, outcomes);
        if (completedMappings.Count == 0)
        {
            _logger.LogInformation("No completed ranking event mappings found for matchday {Matchday}", matchday);
            return [];
        }

        var participantRows = rankingTable.QuerySelectorAll("tbody tr.teilnehmer");
        var participants = new List<KicktippCommunityParticipantSnapshot>();

        foreach (var row in participantRows)
        {
            try
            {
                var participantId = row.GetAttribute("data-teilnehmer-id")?.Trim() ?? string.Empty;
                var displayName = row.QuerySelector(".mg_name")?.TextContent?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                var predictions = new List<KicktippCommunityMatchPrediction>();
                foreach (var mapping in completedMappings.OrderBy(candidate => candidate.EventIndex))
                {
                    var predictionCell = row.QuerySelector($"td.ereignis{mapping.EventIndex}");
                    if (predictionCell == null)
                    {
                        continue;
                    }

                    predictions.Add(ParseParticipantPredictionCell(predictionCell, mapping));
                }

                participants.Add(new KicktippCommunityParticipantSnapshot(
                    participantId,
                    displayName,
                    predictions,
                    ParseIntegerCell(row.QuerySelector("td.spieltagspunkte")),
                    ParseIntegerCell(row.QuerySelector("td.gesamtpunkte"))));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing tippuebersicht participant row for matchday {Matchday}", matchday);
            }
        }

        _logger.LogInformation("Parsed {ParticipantCount} tippuebersicht participants for matchday {Matchday}", participants.Count, matchday);
        return participants;
    }

    private static (int? homeGoals, int? awayGoals, MatchOutcomeAvailability availability) ParseMatchOutcome(IElement resultCell)
    {
        var homeGoalText = resultCell.QuerySelector(".kicktipp-heim")?.TextContent?.Trim();
        var awayGoalText = resultCell.QuerySelector(".kicktipp-gast")?.TextContent?.Trim();

        if (int.TryParse(homeGoalText, out var homeGoals) && int.TryParse(awayGoalText, out var awayGoals))
        {
            return (homeGoals, awayGoals, MatchOutcomeAvailability.Completed);
        }

        return (null, null, MatchOutcomeAvailability.Pending);
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

    private List<CompletedRankingEventMapping> BuildCompletedRankingEventMappings(
        IElement rankingTable,
        IReadOnlyList<CollectedMatchOutcome> outcomes)
    {
        var outcomesByTippSpielId = outcomes
            .Where(outcome => !string.IsNullOrWhiteSpace(outcome.TippSpielId))
            .ToDictionary(outcome => outcome.TippSpielId!, StringComparer.Ordinal);
        var outcomesByEventIndex = outcomes
            .Select((outcome, index) => new { outcome, index })
            .ToDictionary(pair => pair.index, pair => pair.outcome);

        var mappings = new List<CompletedRankingEventMapping>();
        foreach (var header in rankingTable.QuerySelectorAll("thead th.ereignis[data-spiel='true']"))
        {
            if (!int.TryParse(header.GetAttribute("data-index"), out var eventIndex))
            {
                continue;
            }

            var headerTippSpielId = ExtractTippSpielId(header.QuerySelector("a")?.GetAttribute("href"));
            CollectedMatchOutcome? mappedOutcome = null;

            if (!string.IsNullOrWhiteSpace(headerTippSpielId)
                && outcomesByTippSpielId.TryGetValue(headerTippSpielId, out var byTippSpielId))
            {
                mappedOutcome = byTippSpielId;
            }
            else if (outcomesByEventIndex.TryGetValue(eventIndex, out var byEventIndex))
            {
                mappedOutcome = byEventIndex;
            }

            if (mappedOutcome is null || !mappedOutcome.HasOutcome)
            {
                continue;
            }

            var sourceMatchId = mappedOutcome.TippSpielId
                ?? string.Join("|", mappedOutcome.Matchday, mappedOutcome.HomeTeam, mappedOutcome.AwayTeam);
            mappings.Add(new CompletedRankingEventMapping(eventIndex, sourceMatchId, mappedOutcome.TippSpielId));
        }

        return mappings;
    }

    private static KicktippCommunityMatchPrediction ParseParticipantPredictionCell(
        IElement predictionCell,
        CompletedRankingEventMapping mapping)
    {
        var awardedPoints = ParseIntegerCell(predictionCell.QuerySelector("sub.p"));
        var rawText = ExtractPredictionCellScoreText(predictionCell);
        if (TryParseBetPrediction(rawText, out var prediction))
        {
            return new KicktippCommunityMatchPrediction(
                mapping.EventIndex,
                mapping.SourceMatchId,
                mapping.TippSpielId,
                KicktippCommunityPredictionStatus.Placed,
                prediction,
                awardedPoints);
        }

        return new KicktippCommunityMatchPrediction(
            mapping.EventIndex,
            mapping.SourceMatchId,
            mapping.TippSpielId,
            KicktippCommunityPredictionStatus.Missed,
            null,
            0);
    }

    private static string ExtractPredictionCellScoreText(IElement predictionCell)
    {
        return string.Concat(predictionCell.ChildNodes.Select(ExtractNodeText)).Trim();

        static string ExtractNodeText(INode node)
        {
            if (node is IElement element && element.Matches("sub.p"))
            {
                return string.Empty;
            }

            return node.ChildNodes.Length == 0
                ? node.TextContent ?? string.Empty
                : string.Concat(node.ChildNodes.Select(ExtractNodeText));
        }
    }

    private static bool TryParseBetPrediction(string? value, out BetPrediction? prediction)
    {
        prediction = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var sanitized = Regex.Replace(value, @"\s+", string.Empty);
        var match = Regex.Match(sanitized, @"^(\d+):(\d+)$");
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out var homeGoals)
            || !int.TryParse(match.Groups[2].Value, out var awayGoals))
        {
            return false;
        }

        prediction = new BetPrediction(homeGoals, awayGoals);
        return true;
    }

    private static int ParseIntegerCell(IElement? element)
    {
        if (element == null)
        {
            return 0;
        }

        var raw = element.TextContent?.Trim() ?? string.Empty;
        return int.TryParse(raw, out var value) ? value : 0;
    }

    private sealed record CompletedRankingEventMapping(
        int EventIndex,
        string SourceMatchId,
        string? TippSpielId);

    /// <inheritdoc />
    public Task<Dictionary<Match, BetPrediction?>> GetPlacedPredictionsAsync(string community)
    {
        return GetPlacedPredictionsInternalAsync(community, competition: null);
    }

    public Task<Dictionary<Match, BetPrediction?>> GetPlacedPredictionsAsync(string community, string competition)
    {
        return GetPlacedPredictionsInternalAsync(community, competition);
    }

    private async Task<Dictionary<Match, BetPrediction?>> GetPlacedPredictionsInternalAsync(
        string community,
        string? competition)
    {
        try
        {
            var url = $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                return new Dictionary<Match, BetPrediction?>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            if (document.QuerySelector("#tippabgabeSpiele tbody") == null)
            {
                document = await GetExplicitCurrentTippabgabeDocumentAsync(community) ?? document;
            }

            var placedPredictions = new Dictionary<Match, BetPrediction?>();
            
            // Extract matchday from the page
            var currentMatchday = ExtractMatchdayFromPage(document);
            _logger.LogDebug("Extracted matchday for placed predictions: {Matchday}", currentMatchday);
            var kicktippRoundName = ExtractKicktippRoundName(document);
            
            // Parse matches from the tippabgabe table
            var matchTable = document.QuerySelector("#tippabgabeSpiele tbody");
            if (matchTable == null)
            {
                _logger.LogWarning("Could not find tippabgabe table");
                return placedPredictions;
            }
            
            var matchRows = matchTable.QuerySelectorAll("tr");
            _logger.LogDebug("Found {MatchRowCount} potential match rows", matchRows.Length);
            
            string lastValidTimeText = "";  // Track the last valid date/time for inheritance
            
            foreach (var row in matchRows)
            {
                try
                {
                    var cells = row.QuerySelectorAll("td");
                    if (cells.Length >= 4)
                    {
                        // Extract match details from table cells
                        var timeText = cells[0].TextContent?.Trim() ?? "";
                        var homeTeam = cells[1].TextContent?.Trim() ?? "";
                        var awayTeam = cells[2].TextContent?.Trim() ?? "";
                        
                        _logger.LogDebug("Raw time text for {HomeTeam} vs {AwayTeam}: '{TimeText}'", homeTeam, awayTeam, timeText);
                        
                        // Check if match is cancelled ("Abgesagt" in German)
                        // Cancelled matches still accept predictions on Kicktipp, so we process them.
                        // See docs/features/cancelled-matches.md for design rationale.
                        var isCancelled = IsCancelledTimeText(timeText);
                        
                        // Handle date inheritance: if timeText is empty or cancelled, use the last valid time
                        // This preserves database key consistency (startsAt is part of the composite key)
                        if (string.IsNullOrWhiteSpace(timeText) || isCancelled)
                        {
                            if (!string.IsNullOrWhiteSpace(lastValidTimeText))
                            {
                                if (isCancelled)
                                {
                                    _logger.LogWarning(
                                        "Match {HomeTeam} vs {AwayTeam} is cancelled (Abgesagt). Using inherited time '{InheritedTime}' for database consistency. " +
                                        "Predictions can still be placed but may need to be re-evaluated when the match is rescheduled.",
                                        homeTeam, awayTeam, lastValidTimeText);
                                }
                                else
                                {
                                    _logger.LogDebug("Using inherited time for {HomeTeam} vs {AwayTeam}: '{InheritedTime}'", homeTeam, awayTeam, lastValidTimeText);
                                }
                                timeText = lastValidTimeText;
                            }
                            else
                            {
                                _logger.LogWarning("No previous valid time to inherit for {HomeTeam} vs {AwayTeam}{Cancelled}", 
                                    homeTeam, awayTeam, isCancelled ? " (cancelled match)" : "");
                            }
                        }
                        else
                        {
                            // Update the last valid time for future inheritance
                            lastValidTimeText = timeText;
                            _logger.LogDebug("Updated last valid time to: '{TimeText}'", timeText);
                        }
                        
                        // Look for betting inputs to get placed predictions
                        var bettingInputs = cells[3].QuerySelectorAll("input[type='text']");
                        if (bettingInputs.Length >= 2)
                        {
                            var homeInput = bettingInputs[0] as IHtmlInputElement;
                            var awayInput = bettingInputs[1] as IHtmlInputElement;
                            
                            // Parse the date/time
                            var startsAt = ParseMatchDateTime(timeText);
                            var match = CreateMatch(
                                homeTeam,
                                awayTeam,
                                startsAt,
                                currentMatchday,
                                isCancelled,
                                competition,
                                kicktippRoundName,
                                row);
                            
                            // Check if predictions are placed (inputs have values)
                            var homeValue = homeInput?.Value?.Trim();
                            var awayValue = awayInput?.Value?.Trim();
                            
                            BetPrediction? prediction = null;
                            if (!string.IsNullOrEmpty(homeValue) && !string.IsNullOrEmpty(awayValue))
                            {
                                if (int.TryParse(homeValue, out var homeGoals) && int.TryParse(awayValue, out var awayGoals))
                                {
                                    prediction = new BetPrediction(homeGoals, awayGoals);
                                    _logger.LogDebug("Found placed prediction: {HomeTeam} vs {AwayTeam} = {Prediction}", homeTeam, awayTeam, prediction);
                                }
                                else
                                {
                                    _logger.LogWarning("Could not parse prediction values for {HomeTeam} vs {AwayTeam}: '{HomeValue}':'{AwayValue}'", homeTeam, awayTeam, homeValue, awayValue);
                                }
                            }
                            else
                            {
                                _logger.LogDebug("No prediction placed for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
                            }
                            
                            placedPredictions[match] = prediction;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing match row");
                    continue;
                }
            }

            placedPredictions = NormalizeWorldCupFinalRoundMatches(placedPredictions);

            _logger.LogInformation("Successfully parsed {MatchCount} matches with {PlacedCount} placed predictions",
                placedPredictions.Count, placedPredictions.Values.Count(p => p != null));
            return placedPredictions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetPlacedPredictionsAsync");
            return new Dictionary<Match, BetPrediction?>();
        }
    }

    private async Task<IDocument?> GetExplicitCurrentTippabgabeDocumentAsync(string community)
    {
        _logger.LogWarning(
            "The default tippabgabe page for {Community} did not contain a match table; resolving the current season and matchday from tippuebersicht",
            community);

        var overviewDocument = await GetTippuebersichtDocumentAsync(community, matchday: null);
        if (overviewDocument == null)
        {
            return null;
        }

        if (!TryExtractMatchdayFromPage(overviewDocument, out var matchday, preferFinalUrl: true))
        {
            _logger.LogWarning(
                "Could not resolve the current spieltagIndex from tippuebersicht for {Community}; refusing an ambiguous tippabgabe fallback",
                community);
            return null;
        }

        var seasonId = ExtractTippsaisonIdFromPage(overviewDocument);
        if (string.IsNullOrWhiteSpace(seasonId))
        {
            _logger.LogWarning(
                "Could not resolve the current tippsaisonId from tippuebersicht for {Community}; refusing an ambiguous tippabgabe fallback",
                community);
            return null;
        }

        var explicitUrl = $"{community}/tippabgabe?spieltagIndex={matchday.ToString(CultureInfo.InvariantCulture)}&tippsaisonId={Uri.EscapeDataString(seasonId)}";
        var response = await _httpClient.GetAsync(explicitUrl);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Failed to fetch explicitly selected tippabgabe page for {Community}, season {SeasonId}, matchday {Matchday}. Status: {StatusCode}",
                community,
                seasonId,
                matchday,
                response.StatusCode);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var document = await _browsingContext.OpenAsync(req => req.Content(content));
        if (document.QuerySelector("#tippabgabeSpiele tbody") == null)
        {
            _logger.LogWarning(
                "The explicitly selected tippabgabe page for {Community}, season {SeasonId}, matchday {Matchday} did not contain a match table",
                community,
                seasonId,
                matchday);
        }

        return document;
    }

    private static string? ExtractTippsaisonIdFromPage(IDocument document)
    {
        // HttpClient exposes the final RequestUri after redirects. Prefer it
        // because it is Kicktipp's explicit current-season selection even when
        // the returned overview contains no season control.
        if (TryGetQueryParameter(document.Url, "tippsaisonId", out var redirectedSeasonId))
        {
            return redirectedSeasonId;
        }

        foreach (var input in document.QuerySelectorAll("input"))
        {
            if (string.Equals(input.GetAttribute("name"), "tippsaisonId", StringComparison.OrdinalIgnoreCase))
            {
                var value = input.GetAttribute("value")?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        foreach (var select in document.QuerySelectorAll("select"))
        {
            if (!string.Equals(select.GetAttribute("name"), "tippsaisonId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var selectedValue = (select as IHtmlSelectElement)?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(selectedValue))
            {
                return selectedValue;
            }
        }

        foreach (var element in document.QuerySelectorAll("a[href*='tippsaisonId='], form[action*='tippsaisonId=']"))
        {
            var target = element.GetAttribute("href") ?? element.GetAttribute("action");
            if (TryGetQueryParameter(target, "tippsaisonId", out var seasonId))
            {
                return seasonId;
            }
        }

        return null;
    }

    private static bool TryGetQueryParameter(string? target, string parameterName, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var fragmentStart = target.IndexOf('#');
        var queryStart = target.IndexOf('?');
        if (queryStart < 0 ||
            (fragmentStart >= 0 && queryStart > fragmentStart))
        {
            return false;
        }

        var queryEnd = fragmentStart >= 0 ? fragmentStart : target.Length;
        if (queryStart + 1 >= queryEnd)
        {
            return false;
        }

        var query = target[(queryStart + 1)..queryEnd];
        if (ContainsMalformedPercentEncoding(query))
        {
            return false;
        }

        // URLSearchParams follows application/x-www-form-urlencoded rules:
        // percent escapes are decoded once and '+' represents a space.
        // Invalid escapes were rejected above so they cannot become an
        // accidental identifier or throw through Uri.UnescapeDataString.
        var formEncodedQuery = query.Replace("+", "%20", StringComparison.Ordinal);
        var decodedValue = new UrlSearchParams(formEncodedQuery).Get(parameterName)?.Trim();
        if (string.IsNullOrWhiteSpace(decodedValue))
        {
            return false;
        }

        value = decodedValue;
        return true;
    }

    private static bool ContainsMalformedPercentEncoding(string query)
    {
        for (var index = 0; index < query.Length; index++)
        {
            if (query[index] != '%')
            {
                continue;
            }

            if (index + 2 >= query.Length ||
                !Uri.IsHexDigit(query[index + 1]) ||
                !Uri.IsHexDigit(query[index + 2]))
            {
                return true;
            }

            index += 2;
        }

        return false;
    }

    private static string? ExtractKicktippRoundName(IDocument document)
    {
        var roundElement = document.QuerySelector(
            ".spieltagsauswahl .prevnextTitle a, .spieltagsauswahl .prevnextTitle");

        var roundName = NormalizeWhitespace(roundElement?.TextContent);
        return string.IsNullOrWhiteSpace(roundName) ? null : roundName;
    }

    private static List<Match> NormalizeWorldCupFinalRoundMatches(List<Match> matches)
    {
        var finalRoundMatches = matches
            .Select((match, index) => new { Match = match, Index = index })
            .Where(item => item.Match.CompetitionSpecificData is FifaWorldCup2026MatchData
            {
                Stage: FifaWorldCup2026KnockoutStage.Final,
                KicktippRoundName: not null
            } data && data.KicktippRoundName.Equals("Finale", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Kicktipp groups the third-place playoff and final under the shared round name "Finale".
        // When both are present, the earlier match is the third-place playoff.
        if (finalRoundMatches.Count != 2)
        {
            return matches;
        }

        var thirdPlaceMatch = finalRoundMatches
            .OrderBy(item => item.Match.StartsAt.ToInstant())
            .ThenBy(item => item.Index)
            .First();
        var worldCupData = (FifaWorldCup2026MatchData)thirdPlaceMatch.Match.CompetitionSpecificData!;

        var normalizedMatches = matches.ToList();
        normalizedMatches[thirdPlaceMatch.Index] = thirdPlaceMatch.Match with
        {
            CompetitionSpecificData = worldCupData with
            {
                Stage = FifaWorldCup2026KnockoutStage.ThirdPlacePlayoff
            }
        };

        return normalizedMatches;
    }

    private static List<MatchWithHistory> NormalizeWorldCupFinalRoundMatches(List<MatchWithHistory> matches)
    {
        var normalizedMatches = NormalizeWorldCupFinalRoundMatches(matches.Select(item => item.Match).ToList());
        return matches
            .Select((item, index) => item with { Match = normalizedMatches[index] })
            .ToList();
    }

    private static Dictionary<Match, BetPrediction?> NormalizeWorldCupFinalRoundMatches(
        Dictionary<Match, BetPrediction?> predictions)
    {
        var entries = predictions.ToList();
        var normalizedMatches = NormalizeWorldCupFinalRoundMatches(entries.Select(entry => entry.Key).ToList());

        return entries
            .Select((entry, index) => new { Match = normalizedMatches[index], Prediction = entry.Value })
            .ToDictionary(
                entry => entry.Match,
                entry => entry.Prediction);
    }


    private static Match CreateMatch(
        string homeTeam,
        string awayTeam,
        ZonedDateTime startsAt,
        int matchday,
        bool isCancelled,
        string? competition,
        string? kicktippRoundName,
        IElement matchRow)
    {
        var match = new Match(homeTeam, awayTeam, startsAt, matchday, isCancelled);
        if (!string.Equals(competition, CompetitionIds.FifaWorldCup2026, StringComparison.OrdinalIgnoreCase))
        {
            return match;
        }

        var hasPenaltyShootoutMarker = HasPenaltyShootoutMarker(matchRow);
        if (TryMapWorldCupKnockoutStage(kicktippRoundName, out var stage))
        {
            return match with
            {
                CompetitionSpecificData = new FifaWorldCup2026MatchData(
                    kicktippRoundName,
                    stage,
                    FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
            };
        }

        if (!hasPenaltyShootoutMarker)
        {
            return match;
        }

        return match with
        {
            CompetitionSpecificData = new FifaWorldCup2026MatchData(
                kicktippRoundName,
                FifaWorldCup2026KnockoutStage.Unknown,
                FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
        };
    }

    private static bool HasPenaltyShootoutMarker(IElement matchRow)
    {
        var marker = NormalizeWhitespace(
            matchRow.QuerySelector(".kicktipp-spielabschnitt-markierung")?.TextContent);
        return string.Equals(marker, "n.E.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMapWorldCupKnockoutStage(
        string? kicktippRoundName,
        out FifaWorldCup2026KnockoutStage stage)
    {
        stage = kicktippRoundName?.Trim() switch
        {
            string value when value.Equals("Sechzehntelfinale", StringComparison.OrdinalIgnoreCase) =>
                FifaWorldCup2026KnockoutStage.RoundOf32,
            string value when value.Equals("Achtelfinale", StringComparison.OrdinalIgnoreCase) =>
                FifaWorldCup2026KnockoutStage.RoundOf16,
            string value when value.Equals("Viertelfinale", StringComparison.OrdinalIgnoreCase) =>
                FifaWorldCup2026KnockoutStage.Quarterfinal,
            string value when value.Equals("Halbfinale", StringComparison.OrdinalIgnoreCase) =>
                FifaWorldCup2026KnockoutStage.Semifinal,
            string value when value.Equals("Spiel um Platz 3", StringComparison.OrdinalIgnoreCase) ||
                              value.Equals("Spiel um den 3. Platz", StringComparison.OrdinalIgnoreCase) =>
                FifaWorldCup2026KnockoutStage.ThirdPlacePlayoff,
            string value when value.Equals("Finale", StringComparison.OrdinalIgnoreCase) =>
                FifaWorldCup2026KnockoutStage.Final,
            _ => FifaWorldCup2026KnockoutStage.Unknown
        };

        return stage != FifaWorldCup2026KnockoutStage.Unknown;
    }

    private int ExtractMatchdayFromPage(IDocument document)
    {
        if (TryExtractMatchdayFromPage(document, out var matchday))
        {
            return matchday;
        }

        _logger.LogWarning("Could not extract matchday from page, defaulting to 1");
        return 1;
    }

    private bool TryExtractMatchdayFromPage(
        IDocument document,
        out int matchday,
        bool preferFinalUrl = false)
    {
        matchday = 0;

        try
        {
            // The explicit-current fallback must prefer the final redirected
            // overview URI because it is Kicktipp's current selection. Other
            // callers retain DOM-first behavior so a displayed round can
            // correct a stale or mismatched requested URL.
            if (preferFinalUrl &&
                TryGetQueryParameter(document.Url, "spieltagIndex", out var redirectedMatchday) &&
                TryParsePositiveInteger(redirectedMatchday, out matchday))
            {
                _logger.LogDebug("Extracted matchday from final page URL: {Matchday}", matchday);
                return true;
            }

            // Hidden fields are the most stable source across league and tournament pages.
            foreach (var input in document.QuerySelectorAll("input"))
            {
                var name = input.GetAttribute("name") ?? string.Empty;
                var value = input.GetAttribute("value") ?? string.Empty;
                if (name.Contains("spieltag", StringComparison.OrdinalIgnoreCase) &&
                    TryParsePositiveInteger(value, out var matchdayFromHiddenInput))
                {
                    _logger.LogDebug("Extracted matchday from hidden input {InputName}: {Matchday}", name, matchdayFromHiddenInput);
                    matchday = matchdayFromHiddenInput;
                    return true;
                }
            }

            foreach (var select in document.QuerySelectorAll("select"))
            {
                var name = select.GetAttribute("name") ?? string.Empty;
                if (!name.Contains("spieltag", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var selectedRoundOption = select.QuerySelector("option[selected]");
                var selectedRoundValue = selectedRoundOption?.GetAttribute("value");
                if (TryParsePositiveInteger(selectedRoundValue, out var matchdayFromSelectedOption))
                {
                    _logger.LogDebug("Extracted matchday from selected round option: {Matchday}", matchdayFromSelectedOption);
                    matchday = matchdayFromSelectedOption;
                    return true;
                }
            }

            // Fallback: extract any numeric round marker from common navigation elements.
            foreach (var element in document.QuerySelectorAll(".prevnextTitle a, .prevnextTitle, .pagination .active, .pagination .selected, .nav .active, .active"))
            {
                var text = NormalizeWhitespace(element.TextContent);
                if (TryExtractFirstPositiveInteger(text, out var matchdayFromNavigation))
                {
                    _logger.LogDebug("Extracted matchday from navigation text '{NavigationText}': {Matchday}", text, matchdayFromNavigation);
                    matchday = matchdayFromNavigation;
                    return true;
                }
            }

            if (!preferFinalUrl &&
                TryGetQueryParameter(document.Url, "spieltagIndex", out var requestedMatchday) &&
                TryParsePositiveInteger(requestedMatchday, out matchday))
            {
                _logger.LogDebug("Extracted matchday from final page URL: {Matchday}", matchday);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting matchday from page");
            matchday = 0;
            return false;
        }
    }

    private static bool TryExtractFirstPositiveInteger(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Regex.Match(text, @"\b(\d+)\b");
        return match.Success && TryParsePositiveInteger(match.Groups[1].Value, out value);
    }

    private static bool TryParsePositiveInteger(string? text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    /// <inheritdoc />
    public async Task<List<BonusQuestion>> GetOpenBonusQuestionsAsync(string community)
    {
        try
        {
            var url = $"{community}/tippabgabe?bonus=true";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page for bonus questions. Status: {StatusCode}", response.StatusCode);
                return new List<BonusQuestion>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            var bonusQuestions = new List<BonusQuestion>();
            
            // Parse bonus questions from the tippabgabeFragen table
            var bonusTable = document.QuerySelector("#tippabgabeFragen tbody");
            if (bonusTable == null)
            {
                _logger.LogDebug("No bonus questions table found - this is normal if no bonus questions are available");
                return bonusQuestions;
            }
            
            var questionRows = bonusTable.QuerySelectorAll("tr");
            _logger.LogDebug("Found {QuestionRowCount} potential bonus question rows", questionRows.Length);
            
            foreach (var row in questionRows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 3) continue;
                
                // Extract deadline and question text
                var deadlineText = cells[0]?.TextContent?.Trim();
                var questionText = cells[1]?.TextContent?.Trim();
                
                if (string.IsNullOrEmpty(questionText)) continue;
                
                // Parse deadline
                var deadline = ParseMatchDateTime(deadlineText ?? "");
                
                // Extract options from select elements
                var tipCell = cells[2];
                var selectElements = tipCell?.QuerySelectorAll("select");
                var options = new List<BonusQuestionOption>();
                string? formFieldName = null;
                int maxSelections = 1; // Default to single selection
                
                if (selectElements != null && selectElements.Length > 0)
                {
                    // The number of select elements indicates how many selections are allowed
                    maxSelections = selectElements.Length;
                    
                    // Use the first select element to get the available options
                    var firstSelect = selectElements[0] as IHtmlSelectElement;
                    formFieldName = firstSelect?.Name;
                    
                    var optionElements = firstSelect?.QuerySelectorAll("option");
                    if (optionElements != null)
                    {
                        foreach (var option in optionElements.Cast<IHtmlOptionElement>())
                        {
                            if (option.Value != "-1" && !string.IsNullOrEmpty(option.Text))
                            {
                                options.Add(new BonusQuestionOption(option.Value, option.Text.Trim()));
                            }
                        }
                    }
                }
                
                if (options.Any())
                {
                    bonusQuestions.Add(new BonusQuestion(
                        Text: questionText,
                        Deadline: deadline,
                        Options: options,
                        MaxSelections: maxSelections,
                        FormFieldName: formFieldName
                    ));
                }
            }

            _logger.LogInformation("Successfully parsed {QuestionCount} bonus questions", bonusQuestions.Count);
            return bonusQuestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetOpenBonusQuestionsAsync");
            return new List<BonusQuestion>();
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, BonusPrediction?>> GetPlacedBonusPredictionsAsync(string community)
    {
        try
        {
            var url = $"{community}/tippabgabe?bonus=true";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page for placed bonus predictions. Status: {StatusCode}", response.StatusCode);
                return new Dictionary<string, BonusPrediction?>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            var placedPredictions = new Dictionary<string, BonusPrediction?>();
            
            // Parse bonus questions from the tippabgabeFragen table
            var bonusTable = document.QuerySelector("#tippabgabeFragen tbody");
            if (bonusTable == null)
            {
                _logger.LogDebug("No bonus questions table found - this is normal if no bonus questions are available");
                return placedPredictions;
            }
            
            var questionRows = bonusTable.QuerySelectorAll("tr");
            _logger.LogDebug("Found {QuestionRowCount} potential bonus question rows for placed predictions", questionRows.Length);
            
            foreach (var row in questionRows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 3) continue;
                
                // Extract question text
                var questionText = cells[1]?.TextContent?.Trim();
                if (string.IsNullOrEmpty(questionText)) continue;
                
                // Extract current selections from select elements
                var tipCell = cells[2];
                var selectElements = tipCell?.QuerySelectorAll("select");
                
                if (selectElements != null && selectElements.Length > 0)
                {
                    // Extract form field name from the first select element
                    var firstSelect = selectElements[0] as IHtmlSelectElement;
                    var formFieldName = firstSelect?.Name;
                    
                    var selectedOptionIds = new List<string>();
                    
                    // Check each select element for its current selection
                    foreach (var selectElement in selectElements.Cast<IHtmlSelectElement>())
                    {
                        var selectedOption = selectElement.SelectedOptions.FirstOrDefault();
                        if (selectedOption != null && selectedOption.Value != "-1" && !string.IsNullOrEmpty(selectedOption.Value))
                        {
                            selectedOptionIds.Add(selectedOption.Value);
                        }
                    }
                    
                    // Use form field name as key, fall back to question text
                    var dictionaryKey = formFieldName ?? questionText;
                    
                    // Only create a prediction if there are actual selections
                    if (selectedOptionIds.Any())
                    {
                        placedPredictions[dictionaryKey] = new BonusPrediction(selectedOptionIds);
                    }
                    else
                    {
                        placedPredictions[dictionaryKey] = null; // No prediction placed
                    }
                }
            }

            _logger.LogInformation("Successfully retrieved placed predictions for {QuestionCount} bonus questions", placedPredictions.Count);
            return placedPredictions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetPlacedBonusPredictionsAsync");
            return new Dictionary<string, BonusPrediction?>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> PlaceBonusPredictionsAsync(string community, Dictionary<string, BonusPrediction> predictions, bool overridePredictions = false)
    {
        try
        {
            if (!predictions.Any())
            {
                _logger.LogInformation("No bonus predictions to place");
                return true;
            }

            var url = $"{community}/tippabgabe?bonus=true";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to access betting page for bonus predictions. Status: {StatusCode}", response.StatusCode);
                return false;
            }
            
            var pageContent = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(pageContent));
            
            // Find the bet form
            var betForm = document.QuerySelector("form") as IHtmlFormElement;
            if (betForm == null)
            {
                _logger.LogWarning("Could not find betting form on the page");
                return false;
            }
            
            var formData = new List<KeyValuePair<string, string>>();
            
            // Copy hidden inputs from the original form
            var hiddenInputs = betForm.QuerySelectorAll("input[type='hidden']");
            foreach (var hiddenInput in hiddenInputs.Cast<IHtmlInputElement>())
            {
                if (!string.IsNullOrEmpty(hiddenInput.Name) && hiddenInput.Value != null)
                {
                    formData.Add(new KeyValuePair<string, string>(hiddenInput.Name, hiddenInput.Value));
                }
            }
            
            // Copy existing match predictions to avoid overwriting them
            var allInputs = betForm.QuerySelectorAll("input[type=text], input[type=number]").OfType<IHtmlInputElement>();
            foreach (var input in allInputs)
            {
                if (!string.IsNullOrEmpty(input.Name) && !string.IsNullOrEmpty(input.Value))
                {
                    formData.Add(new KeyValuePair<string, string>(input.Name, input.Value));
                }
            }
            
            // Add bonus predictions
            var bonusTable = document.QuerySelector("#tippabgabeFragen tbody");
            if (bonusTable != null)
            {
                var questionRows = bonusTable.QuerySelectorAll("tr");
                
                foreach (var row in questionRows)
                {
                    var cells = row.QuerySelectorAll("td");
                    if (cells.Length < 3) continue;
                    
                    var tipCell = cells[2];
                    var selectElements = tipCell?.QuerySelectorAll("select");
                    
                    if (selectElements != null)
                    {
                        var selectArray = selectElements.Cast<IHtmlSelectElement>().ToArray();
                        
                        // Check if we have a prediction for this question based on form field name match
                        var matchingPrediction = predictions.FirstOrDefault(p =>
                            selectArray.Any(sel => sel.Name == p.Key) ||
                            selectArray.Any(sel => sel.Name?.Contains(p.Key) == true));

                        if (matchingPrediction.Value != null && matchingPrediction.Value.SelectedOptionIds.Any())
                        {
                            var selectedOptions = matchingPrediction.Value.SelectedOptionIds;
                            
                            // For multi-selection questions, we need to fill multiple select elements
                            for (int i = 0; i < Math.Min(selectArray.Length, selectedOptions.Count); i++)
                            {
                                var selectElement = selectArray[i];
                                var fieldName = selectElement.Name;
                                if (string.IsNullOrEmpty(fieldName)) continue;
                                
                                var selectedOptionId = selectedOptions[i];
                                
                                // Check if this option exists in the select element
                                var optionExists = selectElement.QuerySelectorAll("option")
                                    .Cast<IHtmlOptionElement>()
                                    .Any(opt => opt.Value == selectedOptionId);
                                
                                if (optionExists)
                                {
                                    formData.Add(new KeyValuePair<string, string>(fieldName, selectedOptionId));
                                    _logger.LogDebug("Added bonus prediction for field {FieldName}: {OptionId} (selection {Index})", 
                                        fieldName, selectedOptionId, i + 1);
                                }
                                else
                                {
                                    _logger.LogWarning("Option {OptionId} not found for field {FieldName}", selectedOptionId, fieldName);
                                }
                            }
                        }
                        else
                        {
                            // A bonus submission posts the complete form. Preserve every
                            // current select value outside this invocation's target scope so
                            // a deadline-filtered run cannot clear later questions.
                            foreach (var selectElement in selectArray)
                            {
                                var fieldName = selectElement.Name;
                                var selectedOptionId = selectElement.Value;
                                if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(selectedOptionId))
                                {
                                    continue;
                                }

                                var optionExists = selectElement.QuerySelectorAll("option")
                                    .Cast<IHtmlOptionElement>()
                                    .Any(option => option.Value == selectedOptionId);
                                if (!optionExists)
                                {
                                    _logger.LogWarning(
                                        "Existing bonus option {OptionId} not found for non-target field {FieldName}",
                                        selectedOptionId,
                                        fieldName);
                                    continue;
                                }

                                formData.Add(new KeyValuePair<string, string>(fieldName, selectedOptionId));
                                _logger.LogDebug(
                                    "Preserved existing bonus selection for non-target field {FieldName}: {OptionId}",
                                    fieldName,
                                    selectedOptionId);
                            }
                        }
                    }
                }
            }
            
            // Find submit button
            var submitButton = betForm.QuerySelector("input[type=submit], button[type=submit]") as IHtmlElement;
            if (submitButton != null)
            {
                if (submitButton is IHtmlInputElement inputSubmit && !string.IsNullOrEmpty(inputSubmit.Name))
                {
                    formData.Add(new KeyValuePair<string, string>(inputSubmit.Name, inputSubmit.Value ?? "Submit"));
                }
                else if (submitButton is IHtmlButtonElement buttonSubmit && !string.IsNullOrEmpty(buttonSubmit.Name))
                {
                    formData.Add(new KeyValuePair<string, string>(buttonSubmit.Name, buttonSubmit.Value ?? "Submit"));
                }
            }
            else
            {
                // Fallback to default submit button name
                formData.Add(new KeyValuePair<string, string>("submitbutton", "Submit"));
            }
            
            // Submit form
            var formActionUrl = string.IsNullOrEmpty(betForm.Action) ? url : 
                (betForm.Action.StartsWith("http") ? betForm.Action : 
                 betForm.Action.StartsWith("/") ? betForm.Action : 
                 $"{community}/{betForm.Action}");
            
            var formContent = new FormUrlEncodedContent(formData);
            var submitResponse = await _httpClient.PostAsync(formActionUrl, formContent);
            
            if (submitResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("✓ Successfully submitted {PredictionCount} bonus predictions!", predictions.Count);
                return true;
            }
            else
            {
                _logger.LogError("✗ Failed to submit bonus predictions. Status: {StatusCode}", submitResponse.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during bonus prediction placement");
            return false;
        }
    }

    /// <summary>
    /// Expands match annotation abbreviations to their full text.
    /// </summary>
    /// <param name="annotation">The abbreviated annotation (e.g., "n.E.", "n.V.")</param>
    /// <returns>The expanded annotation or null if empty</returns>
    private static string? ExpandAnnotation(string? annotation)
    {
        if (string.IsNullOrWhiteSpace(annotation))
            return null;

        return annotation.Trim() switch
        {
            "n.E." => "nach Elfmeterschießen",
            "n.V." => "nach Verlängerung",
            _ => annotation.Trim() // Return as-is if not recognized
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _championsLeagueBonusStrictTransport?.Dispose();
        _browsingContext?.Dispose();
    }
}
