using System.Net;
using System.Globalization;
using System.Text;
using Regex = System.Text.RegularExpressions.Regex;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Dom.Events;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Parser.Tokens;
using AngleSharp.Text;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Extensions;

namespace KicktippIntegration;

/// <summary>
/// Signals that the authenticated schadensfresse Bundesliga fixture identity surfaces
/// cannot prove a complete exact seed-backed mapping. Callers must not treat this as
/// an empty matchday.
/// </summary>
public sealed class KicktippFixtureIdentityException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

/// <summary>
/// Signals that the target-owned schadensfresse bonus source cannot establish stable
/// question identities. This is never equivalent to a normal empty bonus page.
/// </summary>
public sealed class KicktippBonusQuestionIdentityException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

/// <summary>
/// Implementation of IKicktippClient for interacting with kicktipp.de website
/// Authentication is handled automatically via KicktippAuthenticationHandler
/// </summary>
public partial class KicktippClient : IKicktippClient, IBundesligaTypedKicktippClient, IDisposable
{
    private static readonly DateTimeZone BerlinTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
    private const int SchadensfresseBundesligaMaximumMatchdayFixtures = 9;

    private readonly HttpClient _httpClient;
    private readonly ILogger<KicktippClient> _logger;
    private readonly IBrowsingContext _browsingContext;
    private readonly IMemoryCache _cache;
    private readonly Func<Uri, bool> _finalAuthorityValidator;

    public KicktippClient(HttpClient httpClient, ILogger<KicktippClient> logger, IMemoryCache cache)
        : this(httpClient, logger, cache, IsCanonicalKicktippAuthority)
    {
    }

    internal KicktippClient(HttpClient httpClient, ILogger<KicktippClient> logger, IMemoryCache cache, Func<Uri, bool> finalAuthorityValidator)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _finalAuthorityValidator = finalAuthorityValidator ?? throw new ArgumentNullException(nameof(finalAuthorityValidator));
        
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
        return GetOpenPredictionsInternalAsync(community, competition: null, CancellationToken.None);
    }

    public Task<List<Match>> GetOpenPredictionsAsync(string community, string competition)
    {
        return GetOpenPredictionsInternalAsync(community, competition, CancellationToken.None);
    }

    public Task<List<Match>> GetOpenPredictionsAsync(
        string community,
        string competition,
        CancellationToken cancellationToken) =>
        GetOpenPredictionsInternalAsync(community, competition, cancellationToken);

    private async Task<List<Match>> GetOpenPredictionsInternalAsync(
        string community,
        string? competition,
        CancellationToken cancellationToken)
    {
        var requiresSchadensfresseBundesligaIdentity = IsSchadensfresseBundesligaRoute(community, competition);
        try
        {
            var url = $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode || (requiresSchadensfresseBundesligaIdentity && response.StatusCode != HttpStatusCode.OK))
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                if (requiresSchadensfresseBundesligaIdentity)
                {
                    throw new KicktippFixtureIdentityException("The authenticated schadensfresse tippabgabe surface did not return HTTP 200.");
                }
                return new List<Match>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            if (requiresSchadensfresseBundesligaIdentity &&
                (!IsExpectedCommunityFinalUri(response.RequestMessage?.RequestUri, community, "/tippabgabe") ||
                 !HasExactQuerySet(response.RequestMessage?.RequestUri) ||
                 IsLoginDocument(document)))
            {
                _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because tippabgabe did not reach the authenticated target.");
                throw new KicktippFixtureIdentityException("The authenticated schadensfresse tippabgabe surface did not reach its exact target.");
            }

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
                if (requiresSchadensfresseBundesligaIdentity)
                {
                    throw new KicktippFixtureIdentityException("The authenticated schadensfresse tippabgabe surface is missing its match table.");
                }
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

            if (requiresSchadensfresseBundesligaIdentity &&
                !await TryJoinSchadensfresseBundesligaFixtureIdentitiesAsync(
                    community,
                    currentMatchday,
                    document,
                    matches,
                    cancellationToken))
            {
                throw new KicktippFixtureIdentityException("The schadensfresse Bundesliga fixture identity join is incomplete, ambiguous, or drifted.");
            }

            _logger.LogInformation("Successfully parsed {MatchCount} open matches", matches.Count);
            return matches;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KicktippFixtureIdentityException)
        {
            throw;
        }
        catch (OperationCanceledException) when (requiresSchadensfresseBundesligaIdentity)
        {
            throw;
        }
        catch (Exception exception) when (requiresSchadensfresseBundesligaIdentity)
        {
            throw new KicktippFixtureIdentityException("The schadensfresse Bundesliga fixture identity retrieval failed.", exception);
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
        var requiresSchadensfresseBundesligaIdentity = IsSchadensfresseBundesligaRoute(community, competition);
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

            if (!response.IsSuccessStatusCode ||
                (requiresSchadensfresseBundesligaIdentity && response.StatusCode != HttpStatusCode.OK))
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                if (requiresSchadensfresseBundesligaIdentity)
                {
                    throw new KicktippFixtureIdentityException("The authenticated schadensfresse history tippabgabe surface did not return HTTP 200.");
                }
                return matches;
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            var expectedTippabgabeQuery = matchday.HasValue
                ? new[] { (Key: "spieltagIndex", Value: (string?)matchday.Value.ToString(CultureInfo.InvariantCulture)) }
                : Array.Empty<(string Key, string? Value)>();
            if (requiresSchadensfresseBundesligaIdentity &&
                (!IsExpectedCommunityFinalUri(response.RequestMessage?.RequestUri, community, "/tippabgabe") ||
                 !HasExactQuerySet(response.RequestMessage?.RequestUri, expectedTippabgabeQuery) ||
                 IsLoginDocument(document)))
            {
                throw new KicktippFixtureIdentityException("The authenticated schadensfresse history tippabgabe surface did not reach its exact target.");
            }

            HashSet<FixtureTuple>? openFixtureTuples = null;
            if (requiresSchadensfresseBundesligaIdentity)
            {
                if (!TryExtractSchadensfresseOpenFixtureTuples(document, out var extractedOpenFixtureTuples))
                {
                    throw new KicktippFixtureIdentityException("The authenticated schadensfresse history tippabgabe surface is missing or has malformed betting controls.");
                }

                if (extractedOpenFixtureTuples.Count == 0)
                {
                    return matches;
                }

                openFixtureTuples = extractedOpenFixtureTuples;
            }

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
                if (requiresSchadensfresseBundesligaIdentity)
                {
                    throw new KicktippFixtureIdentityException("The authenticated schadensfresse history tippabgabe surface is missing its Spielinfo link.");
                }
                return matches;
            }

            var spielinfoUrl = spielinfoLink.GetAttribute("href");
            if (string.IsNullOrEmpty(spielinfoUrl))
            {
                _logger.LogWarning("Spielinfo link has no href attribute");
                if (requiresSchadensfresseBundesligaIdentity)
                {
                    throw new KicktippFixtureIdentityException("The authenticated schadensfresse history Spielinfo link has no target.");
                }
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
            var seenSpielinfoFixtureIds = new HashSet<string>(StringComparer.Ordinal);
            var historyFixtureIdsByTuple = new Dictionary<FixtureTuple, string>();

            while (!string.IsNullOrEmpty(currentUrl))
            {
                try
                {
                    Uri? expectedSpielinfoUri = null;
                    var expectedSpielinfoFixtureId = string.Empty;
                    if (requiresSchadensfresseBundesligaIdentity &&
                        (!TryCreateSchadensfresseSpielinfoUri(currentUrl, community, out expectedSpielinfoUri, out expectedSpielinfoFixtureId) ||
                         !seenSpielinfoFixtureIds.Add(expectedSpielinfoFixtureId) ||
                         seenSpielinfoFixtureIds.Count > SchadensfresseBundesligaMaximumMatchdayFixtures))
                    {
                        throw new KicktippFixtureIdentityException("A schadensfresse history Spielinfo link is malformed, repeated, or exceeds the matchday bound.");
                    }

                    var spielinfoResponse = await _httpClient.GetAsync(expectedSpielinfoUri ?? new Uri(_httpClient.BaseAddress!, currentUrl));
                    if (!spielinfoResponse.IsSuccessStatusCode ||
                        (requiresSchadensfresseBundesligaIdentity &&
                         (spielinfoResponse.StatusCode != HttpStatusCode.OK ||
                          !IsExpectedSchadensfresseSpielinfoFinalUri(
                              spielinfoResponse.RequestMessage?.RequestUri,
                              community,
                              expectedSpielinfoFixtureId))))
                    {
                        _logger.LogWarning("Failed to fetch spielinfo page: {Url}. Status: {StatusCode}", currentUrl, spielinfoResponse.StatusCode);
                        if (requiresSchadensfresseBundesligaIdentity)
                        {
                            throw new KicktippFixtureIdentityException("A schadensfresse history Spielinfo surface did not return a success status.");
                        }
                        break;
                    }

                    var spielinfoContent = await spielinfoResponse.Content.ReadAsStringAsync();
                    var spielinfoDocument = await _browsingContext.OpenAsync(req => req.Content(spielinfoContent));
                    if (requiresSchadensfresseBundesligaIdentity && IsLoginDocument(spielinfoDocument))
                    {
                        throw new KicktippFixtureIdentityException("A schadensfresse history Spielinfo response reached a login surface.");
                    }

                    // Extract match information
                    var matchWithHistory = ExtractMatchWithHistoryFromSpielinfoPage(
                        spielinfoDocument,
                        currentMatchday,
                        competition,
                        kicktippRoundName);
                    if (matchWithHistory != null)
                    {
                        if (requiresSchadensfresseBundesligaIdentity)
                        {
                            var tuple = new FixtureTuple(
                                matchWithHistory.Match.StartsAt.ToInstant(),
                                NormalizeStructuredMetadata(matchWithHistory.Match.HomeTeam),
                                NormalizeStructuredMetadata(matchWithHistory.Match.AwayTeam));
                            if (openFixtureTuples is null || !openFixtureTuples.Contains(tuple))
                            {
                                // History navigation can expose closed rows. They never enter the open-item join.
                                matchWithHistory = null;
                            }
                            else if (!historyFixtureIdsByTuple.TryAdd(tuple, expectedSpielinfoFixtureId))
                            {
                                throw new KicktippFixtureIdentityException("Multiple Schadensfresse history pages describe one open fixture tuple.");
                            }
                            else
                            {
                                matchWithHistory = matchWithHistory with
                                {
                                    Match = matchWithHistory.Match with { KicktippFixtureId = expectedSpielinfoFixtureId }
                                };
                            }
                        }
                    }

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
                        if (requiresSchadensfresseBundesligaIdentity &&
                            !TryCreateSchadensfresseSpielinfoUri(currentUrl, community, out _, out _))
                        {
                            throw new KicktippFixtureIdentityException("A schadensfresse history next-page link is malformed or outside the exact target contract.");
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
                    if (requiresSchadensfresseBundesligaIdentity)
                    {
                        throw new KicktippFixtureIdentityException("A schadensfresse history Spielinfo surface could not be retrieved or parsed.", ex);
                    }
                    _logger.LogError(ex, "Error processing spielinfo page: {Url}", currentUrl);
                    break;
                }
            }

            matches = NormalizeWorldCupFinalRoundMatches(matches);

            if (requiresSchadensfresseBundesligaIdentity)
            {
                var joinedMatches = matches.Select(item => item.Match).ToList();
                if (!await TryJoinSchadensfresseBundesligaFixtureIdentitiesAsync(
                        community,
                        currentMatchday,
                        document,
                        joinedMatches,
                        CancellationToken.None))
                {
                    throw new KicktippFixtureIdentityException("The schadensfresse Bundesliga history fixture identity join is incomplete, ambiguous, or drifted.");
                }

                if (historyFixtureIdsByTuple.Count != joinedMatches.Count ||
                    joinedMatches.Any(item =>
                        !historyFixtureIdsByTuple.TryGetValue(
                            new FixtureTuple(item.StartsAt.ToInstant(), NormalizeStructuredMetadata(item.HomeTeam), NormalizeStructuredMetadata(item.AwayTeam)),
                            out var historyFixtureId) ||
                        !string.Equals(historyFixtureId, item.KicktippFixtureId, StringComparison.Ordinal)))
                {
                    throw new KicktippFixtureIdentityException("The Schadensfresse history-page fixture ID does not agree with the exact outcome/detail fixture ID.");
                }

                var joinedByTuple = joinedMatches.ToDictionary(
                    item => new FixtureTuple(item.StartsAt.ToInstant(), NormalizeStructuredMetadata(item.HomeTeam), NormalizeStructuredMetadata(item.AwayTeam)));
                matches = matches.Select(item => item with
                {
                    Match = joinedByTuple[new FixtureTuple(
                        item.Match.StartsAt.ToInstant(),
                        NormalizeStructuredMetadata(item.Match.HomeTeam),
                        NormalizeStructuredMetadata(item.Match.AwayTeam))]
                }).ToList();
            }

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
        catch (KicktippFixtureIdentityException)
        {
            throw;
        }
        catch (Exception ex) when (requiresSchadensfresseBundesligaIdentity)
        {
            throw new KicktippFixtureIdentityException("The schadensfresse Bundesliga history retrieval failed.", ex);
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

    private static bool IsSchadensfresseBundesligaRoute(string community, string? competition) =>
        string.Equals(community, "schadensfresse", StringComparison.Ordinal) &&
        string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal);

    /// <summary>
    /// Resolves the only two source surfaces that can establish a current schadensfresse
    /// Bundesliga match identity: the outcome table's stable ID and its ID-addressed detail.
    /// The bounded nine-detail maximum is the accepted Bundesliga matchday size; it avoids
    /// turning a table parser into an unbounded request-per-row crawler.
    /// </summary>
    private async Task<bool> TryJoinSchadensfresseBundesligaFixtureIdentitiesAsync(
        string community,
        int matchday,
        IDocument tippabgabeDocument,
        List<Match> matches,
        CancellationToken cancellationToken)
    {
        if (matches.Count == 0)
        {
            return tippabgabeDocument.QuerySelectorAll("#tippabgabeSpiele tbody input[type='text']").Length == 0;
        }

        if (!TryExtractOpenFixtureReferences(tippabgabeDocument, matches, out var openFixtures) || openFixtures.Count > 9)
        {
            _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because open rows are malformed or exceed the nine-fixture bound.");
            return false;
        }

        var outcomeDocument = await GetTippuebersichtDocumentForFixtureJoinAsync(community, matchday, cancellationToken);
        if (outcomeDocument is null ||
            !TryExtractOutcomeFixtureReferences(outcomeDocument, community, matchday, out var outcomeFixtures) ||
            outcomeFixtures.Count > 9)
        {
            _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because the outcome surface is incomplete or ambiguous.");
            return false;
        }

        var outcomesByTuple = outcomeFixtures.GroupBy(item => item.Tuple).ToDictionary(group => group.Key, group => group.ToArray());
        var outcomesById = outcomeFixtures.GroupBy(item => item.KicktippFixtureId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        if (outcomesById.Values.Any(group => group.Length != 1))
        {
            _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because the outcome surface repeats a fixture ID.");
            return false;
        }

        var matched = new List<(OpenFixtureReference Open, OutcomeFixtureReference Outcome)>();
        foreach (var openFixture in openFixtures)
        {
            if (!outcomesByTuple.TryGetValue(openFixture.Tuple, out var candidates) || candidates.Length != 1)
            {
                _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because an open row does not map one-to-one to an outcome row.");
                return false;
            }

            var outcome = candidates[0];
            if (openFixture.FormFixtureId is not null &&
                !string.Equals(openFixture.FormFixtureId, outcome.KicktippFixtureId, StringComparison.Ordinal))
            {
                _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because the open form fixture ID disagrees with the outcome ID.");
                return false;
            }

            matched.Add((openFixture, outcome));
        }

        if (matched.GroupBy(item => item.Outcome.KicktippFixtureId, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because multiple open rows map to one outcome ID.");
            return false;
        }

        var seed = BundesligaSeasonRoutingSeed.Default;
        foreach (var (_, outcome) in matched)
        {
            if (!seed.TryGetFixture(outcome.KicktippFixtureId, out _))
            {
                _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because outcome ID {FixtureId} is not in the routing seed.", outcome.KicktippFixtureId);
                return false;
            }
        }

        var detailsById = new Dictionary<string, StructuredFixtureDetail>(StringComparer.Ordinal);
        foreach (var (_, outcome) in matched.OrderBy(item => item.Outcome.KicktippFixtureId, StringComparer.Ordinal))
        {
            var detail = await GetStructuredFixtureDetailAsync(community, matchday, outcome, cancellationToken);
            if (detail is null || !detailsById.TryAdd(outcome.KicktippFixtureId, detail))
            {
                _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because structured detail is missing, invalid, or duplicated.");
                return false;
            }
        }

        for (var index = 0; index < matched.Count; index++)
        {
            var (openFixture, outcome) = matched[index];
            var detail = detailsById[outcome.KicktippFixtureId];
            if (!TryMapSchadensfresseStructuredCompetition(detail.Competition, out var subcompetition, out var resultBasis) ||
                !seed.TryGetFixture(outcome.KicktippFixtureId, out var expected) ||
                !string.Equals(detail.RoundName, expected.KicktippRoundName, StringComparison.Ordinal) ||
                subcompetition != expected.BundesligaSeasonSubcompetition ||
                resultBasis != expected.ResultBasis ||
                detail.StartsAt != openFixture.Tuple.StartsAt ||
                detail.Deadline != openFixture.Tuple.StartsAt)
            {
                _logger.LogWarning("Refusing schadensfresse Bundesliga fixture identity join because detail metadata drifts from the exact routing seed or open row.");
                return false;
            }

            matches[matches.IndexOf(openFixture.Match)] = openFixture.Match with
            {
                KicktippFixtureId = outcome.KicktippFixtureId,
                KicktippRoundName = detail.RoundName,
                BundesligaSeasonSubcompetition = subcompetition,
                ResultBasis = resultBasis
            };
        }

        return true;
    }

    private async Task<IDocument?> GetTippuebersichtDocumentForFixtureJoinAsync(
        string community,
        int matchday,
        CancellationToken cancellationToken)
    {
        var url = $"{community}/tippuebersicht?spieltagIndex={matchday.ToString(CultureInfo.InvariantCulture)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK ||
            !IsExpectedCommunityFinalUri(response.RequestMessage?.RequestUri, community, "/tippuebersicht") ||
            !HasExactQuerySet(response.RequestMessage?.RequestUri, ("spieltagIndex", matchday.ToString(CultureInfo.InvariantCulture))))
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await _browsingContext.OpenAsync(req => req.Content(content));
        return IsLoginDocument(document) ? null : document;
    }

    private async Task<StructuredFixtureDetail?> GetStructuredFixtureDetailAsync(
        string community,
        int matchday,
        OutcomeFixtureReference outcome,
        CancellationToken cancellationToken)
    {
        if (!TryCreateOutcomeDetailUri(outcome.DetailUrl, community, matchday, outcome.KicktippFixtureId, out var detailUri))
        {
            return null;
        }

        var response = await _httpClient.GetAsync(detailUri, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK ||
            !IsExpectedCommunityFinalUri(response.RequestMessage?.RequestUri, community, "/tippuebersicht/spiel") ||
            !HasExactQuerySet(response.RequestMessage?.RequestUri,
                ("tippspielId", outcome.KicktippFixtureId),
                ("tippsaisonId", outcome.TippsaisonId),
                ("spieltagIndex", matchday.ToString(CultureInfo.InvariantCulture))))
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await _browsingContext.OpenAsync(req => req.Content(content));
        if (IsLoginDocument(document) ||
            !TryGetExactStructuredDetailValue(document, "Wettbewerb", out var competition) ||
            !TryGetExactStructuredDetailValue(document, "Spieltag", out var roundName) ||
            !TryGetExactStructuredDetailValue(document, "Termin", out var termin) ||
            !TryGetExactStructuredDetailValue(document, "Tipptermin", out var deadline) ||
            !HasOnlyRequiredStructuredDetailLabels(document))
        {
            return null;
        }

        if (TryParseStructuredDateTime(termin, out var startsAt) && TryParseStructuredDateTime(deadline, out var deadlineAt))
        {
            return new StructuredFixtureDetail(competition, roundName, startsAt, deadlineAt);
        }
        return null;
    }

    private bool TryExtractSchadensfresseOpenFixtureTuples(
        IDocument document,
        out HashSet<FixtureTuple> fixtures)
    {
        fixtures = [];
        var table = document.QuerySelector("#tippabgabeSpiele tbody");
        if (table is null)
        {
            return false;
        }

        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 4)
            {
                continue;
            }

            var bettingControls = cells[3].QuerySelectorAll("input[type='text']");
            if (bettingControls.Length == 0)
            {
                continue;
            }
            if (bettingControls.Length < 2)
            {
                return false;
            }

            var timeText = NormalizeStructuredMetadata(cells[0].TextContent);
            var homeTeam = NormalizeStructuredMetadata(cells[1].TextContent);
            var awayTeam = NormalizeStructuredMetadata(cells[2].TextContent);
            if (string.IsNullOrWhiteSpace(homeTeam) ||
                string.IsNullOrWhiteSpace(awayTeam) ||
                !TryParseStructuredDateTime(timeText, out var startsAt) ||
                !fixtures.Add(new FixtureTuple(startsAt, homeTeam, awayTeam)))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryCreateSchadensfresseSpielinfoUri(
        string? sourceUrl,
        string community,
        out Uri spielinfoUri,
        out string fixtureId)
    {
        spielinfoUri = null!;
        fixtureId = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceUrl) ||
            _httpClient.BaseAddress is null ||
            !Uri.TryCreate(_httpClient.BaseAddress, sourceUrl, out var parsed) ||
            !IsExpectedCommunityFinalUri(parsed, community, "/spielinfo") ||
            !HasExactQuerySet(parsed, ("tippspielId", null)) ||
            !TryReadSingleQueryValue(parsed, "tippspielId", out fixtureId))
        {
            return false;
        }

        spielinfoUri = parsed;
        return true;
    }

    private bool IsExpectedSchadensfresseSpielinfoFinalUri(Uri? uri, string community, string fixtureId) =>
        IsExpectedCommunityFinalUri(uri, community, "/spielinfo") &&
        HasExactQuerySet(uri, ("tippspielId", fixtureId));

    private bool TryExtractOpenFixtureReferences(
        IDocument document,
        IReadOnlyList<Match> matches,
        out List<OpenFixtureReference> fixtures)
    {
        fixtures = [];
        var expected = matches.GroupBy(match => new FixtureTuple(
                match.StartsAt.ToInstant(),
                NormalizeStructuredMetadata(match.HomeTeam),
                NormalizeStructuredMetadata(match.AwayTeam)))
            .ToDictionary(group => group.Key, group => group.ToArray());
        if (expected.Values.Any(group => group.Length != 1))
        {
            return false;
        }

        var table = document.QuerySelector("#tippabgabeSpiele tbody");
        if (table is null)
        {
            return false;
        }

        var seen = new HashSet<FixtureTuple>();
        var lastValidTimeText = string.Empty;
        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 4 || cells[3].QuerySelectorAll("input[type='text']").Length < 2)
            {
                continue;
            }

            var timeText = NormalizeStructuredMetadata(cells[0].TextContent);
            if (string.IsNullOrWhiteSpace(timeText) || IsCancelledTimeText(timeText))
            {
                timeText = lastValidTimeText;
            }
            else
            {
                lastValidTimeText = timeText;
            }

            if (!TryParseStructuredDateTime(timeText, out var startsAt))
            {
                return false;
            }

            try
            {
                var tuple = new FixtureTuple(
                    startsAt,
                    NormalizeStructuredMetadata(cells[1].TextContent),
                    NormalizeStructuredMetadata(cells[2].TextContent));
                if (!expected.TryGetValue(tuple, out var match) || !TryExtractFormFixtureId(row, out var formFixtureId) || !seen.Add(tuple))
                {
                    return false;
                }

                fixtures.Add(new OpenFixtureReference(match[0], tuple, formFixtureId));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return fixtures.Count == matches.Count;
    }

    private static bool TryExtractFormFixtureId(IElement row, out string? fixtureId)
    {
        fixtureId = null;
        var ids = row.QuerySelectorAll("input[name]")
            .Select(item => item.GetAttribute("name"))
            .Where(name => name?.StartsWith("spieltippForms[", StringComparison.Ordinal) == true)
            .Select(name => Regex.Match(name!, @"^spieltippForms\[([^\]]+)\]"))
            .Where(match => match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length > 1)
        {
            return false;
        }

        fixtureId = ids.Length == 1 ? ids[0] : null;
        return true;
    }

    private bool TryExtractOutcomeFixtureReferences(
        IDocument document,
        string community,
        int matchday,
        out List<OutcomeFixtureReference> fixtures)
    {
        fixtures = [];
        var table = document.QuerySelector("#spielplanSpiele tbody");
        if (table is null)
        {
            return false;
        }

        foreach (var row in table.QuerySelectorAll("tr.clickable"))
        {
            var cells = row.QuerySelectorAll("td");
            var detailUrl = row.GetAttribute("data-url");
            var homeTeam = cells.Length >= 3 ? NormalizeStructuredMetadata(cells[1].TextContent) : string.Empty;
            var awayTeam = cells.Length >= 3 ? NormalizeStructuredMetadata(cells[2].TextContent) : string.Empty;
            var timeText = cells.Length >= 1 ? NormalizeStructuredMetadata(cells[0].TextContent) : string.Empty;
            var fixtureId = ExtractTippSpielId(detailUrl);
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam) || string.IsNullOrWhiteSpace(fixtureId) ||
                !TryCreateOutcomeDetailUri(detailUrl, community, matchday, fixtureId, out _))
            {
                return false;
            }

            if (!TryParseStructuredDateTime(timeText, out var startsAt))
            {
                return false;
            }

            try
            {
                if (!TryCreateOutcomeDetailUri(detailUrl, community, matchday, fixtureId, out var detailUri) ||
                    !TryReadSingleQueryValue(detailUri, "tippsaisonId", out var seasonId))
                {
                    return false;
                }
                fixtures.Add(new OutcomeFixtureReference(
                    fixtureId,
                    detailUrl!,
                    seasonId,
                    new FixtureTuple(startsAt, homeTeam, awayTeam)));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return fixtures.Count > 0;
    }

    private bool TryCreateOutcomeDetailUri(
        string? detailUrl,
        string community,
        int matchday,
        string expectedFixtureId,
        out Uri detailUri)
    {
        detailUri = null!;
        if (string.IsNullOrWhiteSpace(detailUrl) || _httpClient.BaseAddress is null ||
            !Uri.TryCreate(_httpClient.BaseAddress, detailUrl, out var parsed) ||
            !IsExpectedCommunityFinalUri(parsed, community, "/tippuebersicht/spiel") ||
            !HasExactQuerySet(parsed,
                ("tippspielId", expectedFixtureId),
                ("tippsaisonId", null),
                ("spieltagIndex", matchday.ToString(CultureInfo.InvariantCulture))))
        {
            return false;
        }

        detailUri = parsed;
        return true;
    }

    private static bool TryGetExactStructuredDetailValue(IDocument document, string label, out string value)
    {
        var values = document.QuerySelectorAll(".spieldaten-infos-label")
            .Where(item => string.Equals(NormalizeStructuredMetadata(item.TextContent), label, StringComparison.Ordinal))
            .Select(item => item.NextElementSibling)
            .Where(item => item is not null && item.ClassList.Contains("spieldaten-infos-value"))
            .Select(item => NormalizeStructuredMetadata(item!.TextContent))
            .ToArray();
        value = values.Length == 1 ? values[0] : string.Empty;
        return values.Length == 1 && !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryMapSchadensfresseStructuredCompetition(
        string competition,
        out BundesligaSeasonSubcompetition subcompetition,
        out ResultBasis resultBasis)
    {
        subcompetition = default;
        resultBasis = default;
        var mapping = SchadensfresseRulesCanonicalJson.Expected.ResultBases
            .Where(item => string.Equals(item.SourceLabel, competition, StringComparison.Ordinal))
            .ToArray();
        return mapping.Length == 1 &&
            BundesligaSeasonRoutingIdentityValues.TryParseBundesligaSeasonSubcompetition(mapping[0].Subcompetition, out subcompetition) &&
            BundesligaSeasonRoutingIdentityValues.TryParseResultBasis(mapping[0].ResultBasis, out resultBasis);
    }

    private bool IsExpectedCommunityFinalUri(Uri? uri, string community, string expectedPath) =>
        uri is not null && _finalAuthorityValidator(uri) &&
        string.Equals(uri.AbsolutePath, $"/{community}{expectedPath}", StringComparison.Ordinal) &&
        string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo);

    public static bool IsCanonicalKicktippAuthority(Uri uri) =>
        uri.IsAbsoluteUri &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) &&
        string.Equals(uri.Host, "www.kicktipp.de", StringComparison.OrdinalIgnoreCase) &&
        uri.Port == 443 &&
        string.IsNullOrEmpty(uri.UserInfo);

    private static bool HasExactQuerySet(Uri? uri, params (string Key, string? Value)[] expected)
    {
        if (uri is null || (string.IsNullOrEmpty(uri.Query) && expected.Length == 0)) return uri is not null;
        if (uri is null || string.IsNullOrEmpty(uri.Query) || ContainsMalformedPercentEncoding(uri.Query[1..])) return false;
        var actual = uri.Query[1..].Split('&', StringSplitOptions.None).Select(part => part.Split('=', 2)).ToArray();
        if (actual.Length != expected.Length || actual.Any(parts => parts.Length != 2)) return false;
        return expected.All(item => TryReadSingleQueryValue(uri, item.Key, out var value) &&
            (item.Value is null ? !string.IsNullOrWhiteSpace(value) : string.Equals(value, item.Value, StringComparison.Ordinal)));
    }

    private static bool TryReadSingleQueryValue(Uri? uri, string key, out string value)
    {
        value = string.Empty;
        if (uri is null || string.IsNullOrEmpty(uri.Query))
        {
            return false;
        }

        var query = uri.Query[1..];
        if (ContainsMalformedPercentEncoding(query))
        {
            return false;
        }

        var matches = query.Split('&', StringSplitOptions.None)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2 && string.Equals(Uri.UnescapeDataString(parts[0].Replace("+", " ", StringComparison.Ordinal)), key, StringComparison.Ordinal))
            .Select(parts => Uri.UnescapeDataString(parts[1].Replace("+", " ", StringComparison.Ordinal)))
            .ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        value = matches[0];
        return true;
    }

    private static bool IsLoginDocument(IDocument document) =>
        document.QuerySelector("form#loginFormular") is not null ||
        NormalizeStructuredMetadata(document.Title).Contains("Login", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeStructuredMetadata(string? value) =>
        NormalizeWhitespace(value).Normalize(NormalizationForm.FormC);

    private static bool HasOnlyRequiredStructuredDetailLabels(IDocument document)
    {
        var labelElements = document.QuerySelectorAll(".spieldaten-infos-label");
        var labels = labelElements.Select(item => NormalizeStructuredMetadata(item.TextContent)).ToArray();
        var required = new[] { "Wettbewerb", "Spieltag", "Termin", "Tipptermin" };
        var values = document.QuerySelectorAll(".spieldaten-infos-value");
        return labels.Length == required.Length &&
            values.Length == required.Length &&
            required.All(label => labels.Count(value => string.Equals(value, label, StringComparison.Ordinal)) == 1) &&
            labelElements.All(label =>
                label.NextElementSibling is { } value &&
                value.ClassList.Contains("spieldaten-infos-value") &&
                !string.IsNullOrWhiteSpace(NormalizeStructuredMetadata(value.TextContent)));
    }

    private static bool TryParseStructuredDateTime(string value, out Instant instant)
    {
        instant = default;
        if (!DateTime.TryParseExact(value, ["dd.MM.yy HH:mm", "dd.MM.yyyy HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        instant = BerlinTimeZone.AtLeniently(LocalDateTime.FromDateTime(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified))).ToInstant();
        return true;
    }

    private sealed record FixtureTuple(Instant StartsAt, string HomeTeam, string AwayTeam);
    private sealed record OpenFixtureReference(Match Match, FixtureTuple Tuple, string? FormFixtureId);
    private sealed record OutcomeFixtureReference(string KicktippFixtureId, string DetailUrl, string TippsaisonId, FixtureTuple Tuple);
    private sealed record StructuredFixtureDetail(string Competition, string RoundName, Instant StartsAt, Instant Deadline);

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
        var hasPenaltyShootoutMarker = HasPenaltyShootoutMarker(matchRow);
        var match = new Match(homeTeam, awayTeam, startsAt, matchday, isCancelled)
        {
            // These are intentionally only source facts. Fixture IDs and typed subcompetitions are
            // unavailable on this page and must not be inferred from team names or display prefixes.
            KicktippRoundName = kicktippRoundName,
            ResultBasis = hasPenaltyShootoutMarker
                ? ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout
                : null
        };
        if (!string.Equals(competition, CompetitionIds.FifaWorldCup2026, StringComparison.OrdinalIgnoreCase))
        {
            return match;
        }

        if (TryMapWorldCupKnockoutStage(kicktippRoundName, out var stage))
        {
            return match with
            {
                ResultBasis = ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout,
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
            ResultBasis = ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout,
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
    public Task<List<BonusQuestion>> GetOpenBonusQuestionsAsync(string community) =>
        GetOpenBonusQuestionsAsync(community, CancellationToken.None);

    public async Task<List<BonusQuestion>> GetOpenBonusQuestionsAsync(string community, CancellationToken cancellationToken)
    {
        var isSchadensfresse = string.Equals(community, "schadensfresse", StringComparison.Ordinal);
        try
        {
            var url = $"{community}/tippabgabe?bonus=true";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode || (isSchadensfresse && response.StatusCode != HttpStatusCode.OK))
            {
                if (isSchadensfresse)
                {
                    throw new KicktippBonusQuestionIdentityException(
                        "schadensfresse open bonus questions could not be retrieved as an exact identity set.");
                }

                _logger.LogError("Failed to fetch tippabgabe page for bonus questions. Status: {StatusCode}", response.StatusCode);
                return new List<BonusQuestion>();
            }

            if (isSchadensfresse && !IsExactSchadensfresseBonusFinalUri(response.RequestMessage?.RequestUri))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions did not reach the exact authenticated target.");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // Preserve the command token through AngleSharp parsing; target cancellation is never an empty source state.
            TargetBonusTableTokenValidator? targetStructure = null;
            IDocument document;
            if (isSchadensfresse)
            {
                targetStructure = new TargetBonusTableTokenValidator();
                var parser = new HtmlParser(new HtmlParserOptions { OnToken = targetStructure.ObserveToken }, _browsingContext);
                parser.Error += (_, error) => targetStructure.ObserveError(error as HtmlErrorEvent);
                document = await parser.ParseDocumentAsync(content, cancellationToken);
            }
            else
            {
                document = await _browsingContext.OpenAsync(req => req.Content(content), cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();

            if (isSchadensfresse && IsLoginDocument(document))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions resolved to a login surface.");
            }
            if (isSchadensfresse && !targetStructure!.HasExactTargetStructure())
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions contain non-direct target table markup.");
            }

            var bonusQuestions = new List<BonusQuestion>();
            
            // Parse bonus questions from the tippabgabeFragen table
            var targetTables = isSchadensfresse ? document.QuerySelectorAll("#tippabgabeFragen").ToArray() : [];
            var targetBodies = isSchadensfresse ? document.QuerySelectorAll("#tippabgabeFragen > tbody").ToArray() : [];
            if (isSchadensfresse && (targetTables.Length != 1 || targetBodies.Length != 1))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions have an ambiguous question table.");
            }

            var bonusTable = isSchadensfresse ? targetBodies[0] : document.QuerySelector("#tippabgabeFragen tbody");
            if (bonusTable == null)
            {
                if (isSchadensfresse)
                {
                    throw new KicktippBonusQuestionIdentityException(
                        "schadensfresse open bonus questions have no unambiguous question table.");
                }

                _logger.LogDebug("No bonus questions table found - this is normal if no bonus questions are available");
                return bonusQuestions;
            }
            
            var questionRows = isSchadensfresse ? bonusTable.Children.ToArray() : bonusTable.QuerySelectorAll("tr").ToArray();
            if (isSchadensfresse && questionRows.Any(row => !string.Equals(row.LocalName, "tr", StringComparison.Ordinal)))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions contain an unexpected table child.");
            }
            if (isSchadensfresse && questionRows.Length == 0)
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions have no exact question rows.");
            }
            _logger.LogDebug("Found {QuestionRowCount} potential bonus question rows", questionRows.Length);
            
            foreach (var row in questionRows)
            {
                var cells = isSchadensfresse ? row.Children.ToArray() : row.QuerySelectorAll("td").ToArray();
                if (cells.Length < 3 || (isSchadensfresse && (cells.Length != 3 || cells.Any(cell => !string.Equals(cell.LocalName, "td", StringComparison.Ordinal)))))
                {
                    if (isSchadensfresse)
                    {
                        throw new KicktippBonusQuestionIdentityException(
                            "schadensfresse open bonus questions contain a malformed question row.");
                    }

                    continue;
                }
                
                // Extract deadline and question text
                var deadlineText = cells[0]?.TextContent?.Trim();
                var questionText = cells[1]?.TextContent?.Trim();
                
                if (string.IsNullOrEmpty(questionText))
                {
                    if (isSchadensfresse)
                    {
                        throw new KicktippBonusQuestionIdentityException(
                            "schadensfresse open bonus questions contain a question without exact text.");
                    }

                    continue;
                }
                
                // The generic parser preserves its historical MinValue fallback. The target
                // identity contract instead requires a real, exact source deadline.
                var targetDeadline = default(ZonedDateTime);
                if (isSchadensfresse && !TryParseExactBonusQuestionDeadline(deadlineText, out targetDeadline))
                {
                    throw new KicktippBonusQuestionIdentityException(
                        "schadensfresse open bonus questions contain a missing or malformed deadline.");
                }
                var deadline = isSchadensfresse ? targetDeadline : ParseMatchDateTime(deadlineText ?? "");
                
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

                    if (isSchadensfresse)
                    {
                        ValidateExactSchadensfresseQuestionSelects(selectElements, options, out formFieldName);
                    }
                }
                
                if (options.Any())
                {
                    var kicktippQuestionId = ExtractKicktippQuestionId(formFieldName);
                    if (isSchadensfresse && string.IsNullOrWhiteSpace(kicktippQuestionId))
                    {
                        throw new KicktippBonusQuestionIdentityException(
                            "schadensfresse open bonus questions contain a missing or malformed stable question ID.");
                    }

                    bonusQuestions.Add(new BonusQuestion(
                        Text: questionText,
                        Deadline: deadline,
                        Options: options,
                        MaxSelections: maxSelections,
                        FormFieldName: formFieldName)
                    {
                        KicktippQuestionId = kicktippQuestionId
                    });
                }
                else if (isSchadensfresse)
                {
                    throw new KicktippBonusQuestionIdentityException(
                        "schadensfresse open bonus questions contain a question without a complete option set.");
                }
            }

            if (isSchadensfresse
                && bonusQuestions.GroupBy(question => question.KicktippQuestionId, StringComparer.Ordinal)
                    .Any(group => group.Count() != 1))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions contain duplicate stable question IDs.");
            }

            _logger.LogInformation("Successfully parsed {QuestionCount} bonus questions", bonusQuestions.Count);
            return bonusQuestions;
        }
        catch (OperationCanceledException) when (isSchadensfresse)
        {
            throw;
        }
        catch (KicktippBonusQuestionIdentityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (isSchadensfresse)
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions could not be parsed as an exact identity set.", ex);
            }

            _logger.LogError(ex, "Exception in GetOpenBonusQuestionsAsync");
            return new List<BonusQuestion>();
        }
    }

    private static string? ExtractKicktippQuestionId(string? formFieldName)
    {
        if (string.IsNullOrWhiteSpace(formFieldName))
        {
            return null;
        }

        var match = Regex.Match(formFieldName, @"^fragetippForms\[(?<id>\d+)\]\.antwortIds\[\d+\]$");
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static bool TryParseExactBonusQuestionDeadline(string? value, out ZonedDateTime deadline)
    {
        deadline = default;
        if (string.IsNullOrWhiteSpace(value)
            || !DateTime.TryParseExact(
                value.Trim(),
                ["dd.MM.yy HH:mm", "dd.MM.yyyy HH:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        deadline = BerlinTimeZone.AtLeniently(
            LocalDateTime.FromDateTime(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified)));
        return true;
    }

    private sealed class TargetBonusTableTokenValidator
    {
        private const string TargetId = "tippabgabeFragen";
        private const int MaximumTargetNesting = 64;
        private readonly List<string> _targetStack = [];
        private int _targetTableCount;
        private int _targetStart = -1;
        private int _targetEnd = -1;
        private int _tbodyCount;
        private int _rowCount;
        private int _cellCount;
        private bool _targetClosed;
        private bool _structureInvalid;
        private bool _onlyIgnorableAfterTargetClose = true;
        private ParseErrorSnapshot? _pendingStructuralError;
        private ParseErrorSnapshot? _targetStructuralError;

        public void ObserveToken(HtmlToken token, TextRange range)
        {
            if (token.Type == HtmlTokenType.StartTag && token.Name == "table" && HasExactTargetTableId(token))
            {
                _targetTableCount++;
                if (_targetTableCount == 1)
                {
                    _targetStart = range.Start.Index;
                    if (_pendingStructuralError is { } pending && pending.Position >= _targetStart)
                    {
                        _targetStructuralError = pending;
                    }
                }
                else
                {
                    _structureInvalid = true;
                }
            }

            if (_targetStart < 0 || _structureInvalid)
            {
                return;
            }

            if (_targetClosed)
            {
                ObserveAfterTargetClose(token);
                return;
            }

            ObserveTargetSubtreeToken(token, range);
        }

        public void ObserveError(HtmlErrorEvent? error)
        {
            if (error is null || !IsTargetStructuralError((HtmlParseError)error.Code))
            {
                return;
            }

            var snapshot = new ParseErrorSnapshot((HtmlParseError)error.Code, error.Position.Index);
            if (_targetStart < 0)
            {
                // Tokenization/parser errors can precede the start-tag callback. Retaining only the
                // latest candidate is sufficient: a later target start can only be relevant to an
                // error at or after its own source position.
                _pendingStructuralError = snapshot;
            }
            else if (_targetStructuralError is null)
            {
                _targetStructuralError = snapshot;
            }
        }

        public bool HasExactTargetStructure()
        {
            return _targetTableCount == 1 &&
                   !_structureInvalid &&
                   _targetClosed &&
                   _targetStack.Count == 0 &&
                   _tbodyCount == 1 &&
                   _rowCount > 0 &&
                   _cellCount > 0 &&
                   (_targetStructuralError is not { } error ||
                    error.Position < _targetStart || error.Position >= _targetEnd);
        }

        private void ObserveAfterTargetClose(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Comment ||
                (token.Type == HtmlTokenType.Character && IsAsciiHtmlWhitespace(token.Data)))
            {
                return;
            }

            if (_onlyIgnorableAfterTargetClose && token.Type == HtmlTokenType.EndTag && token.Name == "table")
            {
                _structureInvalid = true;
            }
            _onlyIgnorableAfterTargetClose = false;
        }

        private void ObserveTargetSubtreeToken(HtmlToken token, TextRange range)
        {
            switch (token.Type)
            {
                case HtmlTokenType.Comment:
                    return;
                case HtmlTokenType.Character:
                    if (TryPeekTarget(out var characterParent) &&
                        (characterParent == "tbody" || characterParent == "tr") &&
                        !IsAsciiHtmlWhitespace(token.Data))
                    {
                        _structureInvalid = true;
                    }
                    return;
                case HtmlTokenType.StartTag:
                    if (TryPeekTarget(out var parent) && !IsAllowedDirectTargetChild(parent, token.Name))
                    {
                        _structureInvalid = true;
                        return;
                    }

                    if (token.Name == "tbody" && TryPeekTarget(out var bodyParent) && bodyParent == "table") _tbodyCount++;
                    if (token.Name == "tr" && TryPeekTarget(out var rowParent) && rowParent == "tbody") _rowCount++;
                    if (token.Name == "td" && TryPeekTarget(out var cellParent) && cellParent == "tr") _cellCount++;
                    if (!IsSelfClosing(token) && !IsHtmlVoidElement(token.Name))
                    {
                        if (_targetStack.Count == MaximumTargetNesting)
                        {
                            _structureInvalid = true;
                            return;
                        }
                        _targetStack.Add(token.Name);
                    }
                    return;
                case HtmlTokenType.EndTag:
                    if (IsSelfClosing(token) || !TryPeekTarget(out var expected))
                    {
                        _structureInvalid = true;
                        return;
                    }

                    if (expected != token.Name)
                    {
                        _structureInvalid = true;
                        return;
                    }

                    _targetStack.RemoveAt(_targetStack.Count - 1);
                    if (token.Name == "table")
                    {
                        _targetClosed = true;
                        _targetEnd = range.End.Index;
                    }
                    return;
            }
        }

        private static bool HasExactTargetTableId(HtmlToken token) =>
            token is HtmlTagToken tag && tag.Attributes.Any(attribute => attribute.Name == "id" && attribute.Value == TargetId);

        private static bool IsSelfClosing(HtmlToken token) => token is HtmlTagToken tag && tag.IsSelfClosing;

        private bool TryPeekTarget(out string name)
        {
            if (_targetStack.Count > 0)
            {
                name = _targetStack[^1];
                return true;
            }

            name = string.Empty;
            return false;
        }

        private static bool IsAllowedDirectTargetChild(string parent, string child) =>
            parent is not ("table" or "tbody" or "tr") ||
            (parent == "table" && child == "tbody") ||
            (parent == "tbody" && child == "tr") ||
            (parent == "tr" && child == "td");

        private static bool IsAsciiHtmlWhitespace(string value) =>
            value.All(character => character is '\t' or '\n' or '\f' or '\r' or ' ');

        private static bool IsTargetStructuralError(HtmlParseError error) => error is
            HtmlParseError.AttributeDuplicateOmitted or
            HtmlParseError.EndTagCannotHaveAttributes or
            HtmlParseError.EndTagCannotBeSelfClosed or
            HtmlParseError.TagClosingMismatch or
            HtmlParseError.TagDoesNotMatchCurrentNode or
            HtmlParseError.TagClosedWrong or
            HtmlParseError.TagCannotEndHere or
            HtmlParseError.EOF;

        private static bool IsHtmlVoidElement(string name) => name is "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or "input" or "link" or "meta" or "param" or "source" or "track" or "wbr";
        private sealed record ParseErrorSnapshot(HtmlParseError Code, int Position);
    }

    private bool IsExactSchadensfresseBonusFinalUri(Uri? uri) =>
        uri is not null &&
        _finalAuthorityValidator(uri) &&
        string.Equals(uri.AbsolutePath, "/schadensfresse/tippabgabe", StringComparison.Ordinal) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        HasExactQuerySet(uri, ("bonus", "true"));

    private static void ValidateExactSchadensfresseQuestionSelects(
        IHtmlCollection<IElement> selectElements,
        List<BonusQuestionOption> firstOptions,
        out string? canonicalFormFieldName)
    {
        canonicalFormFieldName = null;
        if (selectElements.Length == 0)
        {
            throw new KicktippBonusQuestionIdentityException(
                "schadensfresse open bonus questions contain a question without selects.");
        }

        string? questionId = null;
        var selectionIndexes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in selectElements)
        {
            if (element is not IHtmlSelectElement select
                || !TryExtractExactKicktippQuestionSelectIdentity(select.Name, out var currentId, out var selectionIndex)
                || !selectionIndexes.Add(selectionIndex))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions contain a malformed or conflicting select identity.");
            }

            if (questionId is not null && !string.Equals(questionId, currentId, StringComparison.Ordinal))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions contain conflicting question IDs in one row.");
            }

            questionId = currentId;
            var currentOptions = ExtractExactQuestionOptions(select);
            if (currentOptions.Count == 0 || !currentOptions.SequenceEqual(firstOptions))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions contain conflicting select option sets.");
            }
        }

        canonicalFormFieldName = selectElements[0].GetAttribute("name");
    }

    private static List<BonusQuestionOption> ExtractExactQuestionOptions(IHtmlSelectElement select)
    {
        var options = new List<BonusQuestionOption>();
        foreach (var option in select.QuerySelectorAll("option"))
        {
            if (option is not IHtmlOptionElement typedOption)
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions contain a malformed select option.");
            }

            var text = typedOption.Text.Trim();
            if (typedOption.Value == "-1")
            {
                continue;
            }

            if (!typedOption.HasAttribute("value") || string.IsNullOrEmpty(typedOption.Value) || string.IsNullOrEmpty(text))
            {
                throw new KicktippBonusQuestionIdentityException(
                    "schadensfresse open bonus questions contain a missing option identity.");
            }

            options.Add(new BonusQuestionOption(typedOption.Value, text));
        }

        if (options.GroupBy(option => option.Id, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || options.GroupBy(option => option.Text, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new KicktippBonusQuestionIdentityException(
                "schadensfresse open bonus questions contain duplicate option identities.");
        }

        return options;
    }

    private static bool TryExtractExactKicktippQuestionSelectIdentity(string? formFieldName, out string questionId, out string selectionIndex)
    {
        questionId = string.Empty;
        selectionIndex = string.Empty;
        if (string.IsNullOrWhiteSpace(formFieldName)) return false;
        var match = Regex.Match(formFieldName, @"^fragetippForms\[(?<id>\d+)\]\.antwortIds\[(?<index>\d+)\]$");
        if (!match.Success) return false;
        questionId = match.Groups["id"].Value;
        selectionIndex = match.Groups["index"].Value;
        return true;
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
        _browsingContext?.Dispose();
    }
}
