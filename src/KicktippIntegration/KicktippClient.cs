using System.Net;
using AngleSharp;
using AngleSharp.Html.Dom;
using Core;
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

    public KicktippClient(HttpClient httpClient, ILogger<KicktippClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
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
                            
                            matches.Add(new Match(homeTeam, awayTeam, startsAt));
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

    private ZonedDateTime ParseMatchDateTime(string timeText)
    {
        try
        {
            // Expected format: "08.07.25 21:00"
            if (DateTime.TryParseExact(timeText, "dd.MM.yy HH:mm", null, System.Globalization.DateTimeStyles.None, out var dateTime))
            {
                // Convert to DateTimeOffset and then to ZonedDateTime
                // Assume Central European Time (Germany)
                var dateTimeOffset = new DateTimeOffset(dateTime, TimeSpan.FromHours(1)); // CET offset
                return dateTimeOffset.ToZonedDateTime();
            }
            
            // Fallback to current time if parsing fails
            _logger.LogWarning("Could not parse match time: {TimeText}, using current time", timeText);
            return DateTimeOffset.Now.ToZonedDateTime();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing match time '{TimeText}'", timeText);
            return DateTimeOffset.Now.ToZonedDateTime();
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _browsingContext?.Dispose();
    }
}
