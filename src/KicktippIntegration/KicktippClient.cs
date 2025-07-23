using System.Net;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Extensions;

namespace KicktippIntegration;

/// <summary>
/// Implementation of IKicktippClient for interacting with kicktipp.de website
/// Authentication is handled automatically via KicktippAuthenticationHandler
/// </summary>
public class KicktippClient : IKicktippClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KicktippClient> _logger;
    private readonly IBrowsingContext _browsingContext;
    private readonly IMemoryCache _cache;

    public KicktippClient(HttpClient httpClient, ILogger<KicktippClient> logger, IMemoryCache cache)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        
        var config = Configuration.Default.WithDefaultLoader();
        _browsingContext = BrowsingContext.New(config);
    }

    /// <inheritdoc />
    public async Task<List<Match>> GetOpenPredictionsAsync(string community)
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
            
            // Parse matches from the tippabgabe table
            var matchTable = document.QuerySelector("#tippabgabeSpiele tbody");
            if (matchTable == null)
            {
                _logger.LogWarning("Could not find tippabgabe table");
                return matches;
            }
            
            var matchRows = matchTable.QuerySelectorAll("tr");
            _logger.LogDebug("Found {MatchRowCount} potential match rows", matchRows.Length);
            
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
                        
                        // Check if this row has betting inputs (indicates open match)
                        var bettingInputs = cells[3].QuerySelectorAll("input[type='text']");
                        if (bettingInputs.Length >= 2)
                        {
                            _logger.LogDebug("Found open match: {HomeTeam} vs {AwayTeam} at {Time}", homeTeam, awayTeam, timeText);
                            
                            // Parse the date/time - for now use a simple approach
                            // Format appears to be "08.07.25 21:00"
                            var startsAt = ParseMatchDateTime(timeText);
                            
                            matches.Add(new Match(homeTeam, awayTeam, startsAt, 1));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing match row");
                    continue;
                }
            }

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
            
            if (submitResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("✓ Successfully submitted {BetsPlaced} bets!", betsPlaced);
                return true;
            }
            else
            {
                _logger.LogError("✗ Failed to submit bets. Status: {StatusCode}", submitResponse.StatusCode);
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
            
            // Find the standings table
            var standingsTable = document.QuerySelector("table.sporttabelle tbody");
            if (standingsTable == null)
            {
                _logger.LogWarning("Could not find standings table");
                return standings;
            }
            
            var rows = standingsTable.QuerySelectorAll("tr");
            _logger.LogDebug("Found {RowCount} team rows in standings table", rows.Length);
            
            foreach (var row in rows)
            {
                try
                {
                    var cells = row.QuerySelectorAll("td");
                    if (cells.Length >= 9) // Need at least 9 columns for all data
                    {
                        // Extract data from table cells
                        var positionText = cells[0].TextContent?.Trim().TrimEnd('.') ?? "";
                        var teamNameElement = cells[1].QuerySelector("div");
                        var teamName = teamNameElement?.TextContent?.Trim() ?? "";
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
                                losses);
                            
                            standings.Add(teamStanding);
                            _logger.LogDebug("Parsed team standing: {Position}. {TeamName} - {Points} points", 
                                position, teamName, points);
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
    public async Task<List<MatchWithHistory>> GetMatchesWithHistoryAsync(string community)
    {
        // Create cache key based on community
        var cacheKey = $"matches_history_{community}";
        
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
            var tippabgabeUrl = $"{community}/tippabgabe";
            var response = await _httpClient.GetAsync(tippabgabeUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
                return matches;
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

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
                    var matchWithHistory = await ExtractMatchWithHistoryFromSpielinfoPage(spielinfoDocument);
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

    private async Task<MatchWithHistory?> ExtractMatchWithHistoryFromSpielinfoPage(IDocument document)
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

            var startsAt = ParseMatchDateTime(timeText);
            var match = new Match(homeTeam, awayTeam, startsAt, 1);

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
                    if (cells.Length < 4)
                        continue;

                    var competition = cells[0].TextContent?.Trim() ?? "";
                    var homeTeam = cells[1].TextContent?.Trim() ?? "";
                    var awayTeam = cells[2].TextContent?.Trim() ?? "";
                    
                    // Extract result and outcome
                    var resultCell = cells[3];
                    var homeGoals = (int?)null;
                    var awayGoals = (int?)null;
                    var outcome = MatchOutcome.Pending;

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
                                
                                // Determine outcome from team's perspective
                                var isHomeTeam = cells[1].ClassList.Contains("sieg") || cells[1].ClassList.Contains("niederlage") || cells[1].ClassList.Contains("remis");
                                var isAwayTeam = cells[2].ClassList.Contains("sieg") || cells[2].ClassList.Contains("niederlage") || cells[2].ClassList.Contains("remis");
                                
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
                                    // Fallback: determine from score
                                    outcome = homeScore == awayScore ? MatchOutcome.Draw : 
                                             homeScore > awayScore ? MatchOutcome.Win : MatchOutcome.Loss;
                                }
                            }
                        }
                    }

                    var matchResult = new MatchResult(competition, homeTeam, awayTeam, homeGoals, awayGoals, outcome);
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
            if (string.IsNullOrWhiteSpace(timeText))
            {
                _logger.LogWarning("Match time text is empty, using current time");
                return DateTimeOffset.Now.ToZonedDateTime();
            }

            // Expected format: "22.08.25 20:30"
            _logger.LogDebug("Attempting to parse time: '{TimeText}'", timeText);
            if (DateTime.TryParseExact(timeText, "dd.MM.yy HH:mm", null, System.Globalization.DateTimeStyles.None, out var dateTime))
            {
                _logger.LogDebug("Successfully parsed time: {DateTime}", dateTime);
                // Convert to DateTimeOffset and then to ZonedDateTime
                // Assume Central European Time (Germany)
                var dateTimeOffset = new DateTimeOffset(dateTime, TimeSpan.FromHours(1)); // CET offset
                return dateTimeOffset.ToZonedDateTime();
            }
            
            // Fallback to current time if parsing fails
            _logger.LogWarning("Could not parse match time: '{TimeText}', using current time", timeText);
            return DateTimeOffset.Now.ToZonedDateTime();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing match time '{TimeText}'", timeText);
            return DateTimeOffset.Now.ToZonedDateTime();
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<Match, BetPrediction?>> GetPlacedPredictionsAsync(string community)
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

            var placedPredictions = new Dictionary<Match, BetPrediction?>();
            
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
                        
                        // Handle date inheritance: if timeText is empty, use the last valid time
                        if (string.IsNullOrWhiteSpace(timeText))
                        {
                            if (!string.IsNullOrWhiteSpace(lastValidTimeText))
                            {
                                timeText = lastValidTimeText;
                                _logger.LogDebug("Using inherited time for {HomeTeam} vs {AwayTeam}: '{InheritedTime}'", homeTeam, awayTeam, timeText);
                            }
                            else
                            {
                                _logger.LogWarning("No previous valid time to inherit for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
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
                            var match = new Match(homeTeam, awayTeam, startsAt, 1);
                            
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

    public void Dispose()
    {
        _httpClient?.Dispose();
        _browsingContext?.Dispose();
    }
}
