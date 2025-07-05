using System.Net;
using AngleSharp;
using AngleSharp.Html.Dom;
using Core;
using NodaTime;
using NodaTime.Extensions;

namespace KicktippIntegration;

/// <summary>
/// Implementation of IKicktippClient for interacting with kicktipp.de website
/// Based on the POC KicktippService implementation
/// </summary>
public class KicktippClient : IKicktippClient, IDisposable
{
    private const string BaseUrl = "https://www.kicktipp.de";
    private const string LoginUrl = $"{BaseUrl}/info/profil/login";
    
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private readonly IBrowsingContext _browsingContext;

    public KicktippClient(HttpClient httpClient)
    {
        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler() { CookieContainer = _cookieContainer };
        _httpClient = httpClient ?? new HttpClient(handler);
        
        var config = Configuration.Default.WithDefaultLoader();
        _browsingContext = BrowsingContext.New(config);
    }

    /// <inheritdoc />
    public async Task<bool> LoginAsync(KicktippCredentials credentials)
    {
        if (!credentials.IsValid)
        {
            throw new ArgumentException("Invalid credentials provided");
        }

        try
        {
            Console.WriteLine("Navigating to login page...");
            
            // Get the login page
            var loginPageResponse = await _httpClient.GetAsync(LoginUrl);
            if (!loginPageResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to access login page. Status: {loginPageResponse.StatusCode}");
                return false;
            }
            
            var loginPageContent = await loginPageResponse.Content.ReadAsStringAsync();
            var loginDocument = await _browsingContext.OpenAsync(req => req.Content(loginPageContent));
            
            // Find the login form
            var loginForm = loginDocument.QuerySelector("form") as IHtmlFormElement;
            if (loginForm == null)
            {
                Console.WriteLine("Could not find login form on the page");
                return false;
            }
            
            Console.WriteLine("Found login form, parsing form action...");
            
            // Parse the form action URL - this is crucial for the correct POST target
            var formAction = loginForm.Action;
            var formActionUrl = string.IsNullOrEmpty(formAction) ? LoginUrl : 
                (formAction.StartsWith("http") ? formAction : 
                 formAction.StartsWith("/") ? $"{BaseUrl}{formAction}" : 
                 $"{BaseUrl}/info/profil/{formAction}");
            
            // Prepare form data (field names from Python implementation)
            var formData = new List<KeyValuePair<string, string>>
            {
                new("kennung", credentials.Username),
                new("passwort", credentials.Password)
            };
            
            // Add hidden fields
            var hiddenInputs = loginForm.QuerySelectorAll("input[type=hidden]").OfType<IHtmlInputElement>();
            foreach (var input in hiddenInputs)
            {
                if (!string.IsNullOrEmpty(input.Name) && !string.IsNullOrEmpty(input.Value))
                {
                    formData.Add(new KeyValuePair<string, string>(input.Name, input.Value));
                }
            }
            
            // Submit login form to the parsed action URL
            var formContent = new FormUrlEncodedContent(formData);
            var loginResponse = await _httpClient.PostAsync(formActionUrl, formContent);
            
            if (!loginResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Login request failed. Status: {loginResponse.StatusCode}");
                return false;
            }
            
            // Check login success
            return await CheckLoginSuccessAsync(loginResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during login: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<KicktippLoginToken?> GetLoginTokenAsync()
    {
        try
        {
            // For now, we'll extract token from cookies or page content
            // This is a simplified implementation - in reality, we'd need to parse the actual token
            var response = await _httpClient.GetAsync(BaseUrl);
            if (response.IsSuccessStatusCode)
            {
                // Create a token with 24-hour expiration as a placeholder
                // In real implementation, we'd extract the actual token and expiration
                return new KicktippLoginToken("placeholder_token", DateTimeOffset.UtcNow.AddHours(24));
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<Match>> GetOpenPredictionsAsync(string community)
    {
        try
        {
            var url = $"{BaseUrl}/{community}/tippabgabe";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch tippabgabe page. Status: {response.StatusCode}");
                return new List<Match>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(content));

            var matches = new List<Match>();
            
            // Parse matches from the tippabgabe table
            var matchTable = document.QuerySelector("#tippabgabeSpiele tbody");
            if (matchTable == null)
            {
                Console.WriteLine("Could not find tippabgabe table");
                return matches;
            }
            
            var matchRows = matchTable.QuerySelectorAll("tr");
            Console.WriteLine($"Found {matchRows.Length} potential match rows");
            
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
                            Console.WriteLine($"Found open match: {homeTeam} vs {awayTeam} at {timeText}");
                            
                            // Parse the date/time - for now use a simple approach
                            // Format appears to be "08.07.25 21:00"
                            var startsAt = ParseMatchDateTime(timeText);
                            
                            matches.Add(new Match(homeTeam, awayTeam, startsAt));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing match row: {ex.Message}");
                    continue;
                }
            }

            Console.WriteLine($"Successfully parsed {matches.Count} open matches");
            return matches;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in GetOpenPredictionsAsync: {ex.Message}");
            return new List<Match>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> PlaceBetAsync(string community, Match match, BetPrediction prediction, bool overrideBet = false)
    {
        try
        {
            var url = $"{BaseUrl}/{community}/tippabgabe";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to access betting page. Status: {response.StatusCode}");
                return false;
            }
            
            var pageContent = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(pageContent));
            
            // Find the bet form
            var betForm = document.QuerySelector("form") as IHtmlFormElement;
            if (betForm == null)
            {
                Console.WriteLine("Could not find betting form on the page");
                return false;
            }
            
            // Find the main content area
            var contentArea = document.QuerySelector("#kicktipp-content");
            if (contentArea == null)
            {
                Console.WriteLine("Could not find content area on the betting page");
                return false;
            }
            
            // Find the table with predictions
            var tbody = contentArea.QuerySelector("tbody");
            if (tbody == null)
            {
                Console.WriteLine("No betting table found");
                return false;
            }
            
            var rows = tbody.QuerySelectorAll("tr");
            var formData = new List<KeyValuePair<string, string>>();
            var matchFound = false;
            
            // Add hidden fields from the form
            var hiddenInputs = betForm.QuerySelectorAll("input[type=hidden]").OfType<IHtmlInputElement>();
            foreach (var input in hiddenInputs)
            {
                if (!string.IsNullOrEmpty(input.Name) && input.Value != null)
                {
                    formData.Add(new KeyValuePair<string, string>(input.Name, input.Value));
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
                            Console.WriteLine($"No betting inputs found for {match}, skipping");
                            continue;
                        }
                        
                        // Check if bets are already placed
                        var hasExistingHomeBet = !string.IsNullOrEmpty(homeInput.Value);
                        var hasExistingAwayBet = !string.IsNullOrEmpty(awayInput.Value);
                        
                        if ((hasExistingHomeBet || hasExistingAwayBet) && !overrideBet)
                        {
                            var existingBet = $"{homeInput.Value ?? ""}:{awayInput.Value ?? ""}";
                            Console.WriteLine($"{match} - skipped, already placed {existingBet}");
                            return true; // Consider this successful - bet already exists
                        }
                        
                        // Add bet to form data
                        if (!string.IsNullOrEmpty(homeInput.Name) && !string.IsNullOrEmpty(awayInput.Name))
                        {
                            formData.Add(new KeyValuePair<string, string>(homeInput.Name, prediction.HomeGoals.ToString()));
                            formData.Add(new KeyValuePair<string, string>(awayInput.Name, prediction.AwayGoals.ToString()));
                            matchFound = true;
                            Console.WriteLine($"{match} - betting {prediction}");
                        }
                        else
                        {
                            Console.WriteLine($"{match} - input field names are missing, skipping");
                            continue;
                        }
                        
                        break; // Found our match, no need to continue
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing betting row: {ex.Message}");
                    continue;
                }
            }
            
            if (!matchFound)
            {
                Console.WriteLine($"Match {match} not found in betting form");
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
                 betForm.Action.StartsWith("/") ? $"{BaseUrl}{betForm.Action}" : 
                 $"{BaseUrl}/{community}/{betForm.Action}");
            
            var formContent = new FormUrlEncodedContent(formData);
            var submitResponse = await _httpClient.PostAsync(formActionUrl, formContent);
            
            if (submitResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully submitted bet for {match}!");
                return true;
            }
            else
            {
                Console.WriteLine($"✗ Failed to submit bet. Status: {submitResponse.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during bet placement: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> PlaceBetsAsync(string community, Dictionary<Match, BetPrediction> bets, bool overrideBets = false)
    {
        try
        {
            var url = $"{BaseUrl}/{community}/tippabgabe";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to access betting page. Status: {response.StatusCode}");
                return false;
            }
            
            var pageContent = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(pageContent));
            
            // Find the bet form
            var betForm = document.QuerySelector("form") as IHtmlFormElement;
            if (betForm == null)
            {
                Console.WriteLine("Could not find betting form on the page");
                return false;
            }
            
            // Find the main content area
            var contentArea = document.QuerySelector("#kicktipp-content");
            if (contentArea == null)
            {
                Console.WriteLine("Could not find content area on the betting page");
                return false;
            }
            
            // Find the table with predictions
            var tbody = contentArea.QuerySelector("tbody");
            if (tbody == null)
            {
                Console.WriteLine("No betting table found");
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
                        Console.WriteLine($"No betting inputs found for {matchKey}, skipping");
                        continue;
                    }
                    
                    // Check if bets are already placed
                    var hasExistingHomeBet = !string.IsNullOrEmpty(homeInput.Value);
                    var hasExistingAwayBet = !string.IsNullOrEmpty(awayInput.Value);
                    
                    if ((hasExistingHomeBet || hasExistingAwayBet) && !overrideBets)
                    {
                        var existingBet = $"{homeInput.Value ?? ""}:{awayInput.Value ?? ""}";
                        Console.WriteLine($"{matchKey} - skipped, already placed {existingBet}");
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
                        Console.WriteLine($"{matchKey} - betting {prediction}");
                    }
                    else
                    {
                        Console.WriteLine($"{matchKey} - input field names are missing, skipping");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing betting row: {ex.Message}");
                    continue;
                }
            }
            
            Console.WriteLine($"Summary: {betsPlaced} bets to place, {betsSkipped} skipped");
            
            if (betsPlaced == 0)
            {
                Console.WriteLine("No bets to place");
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
                 betForm.Action.StartsWith("/") ? $"{BaseUrl}{betForm.Action}" : 
                 $"{BaseUrl}/{community}/{betForm.Action}");
            
            var formContent = new FormUrlEncodedContent(formData);
            var submitResponse = await _httpClient.PostAsync(formActionUrl, formContent);
            
            if (submitResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully submitted {betsPlaced} bets!");
                return true;
            }
            else
            {
                Console.WriteLine($"✗ Failed to submit bets. Status: {submitResponse.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during bet placement: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckLoginSuccessAsync(HttpResponseMessage loginResponse)
    {
        var responseContent = await loginResponse.Content.ReadAsStringAsync();
        var responseDocument = await _browsingContext.OpenAsync(req => req.Content(responseContent));
        
        // Check if still on login page (Python implementation logic)
        var loginDiv = responseDocument.QuerySelector("div[content='Login']");
        var isStillOnLoginPage = loginDiv != null;
        
        // Check for profile links (indicates successful login)
        var profileElements = responseDocument.QuerySelectorAll("a[href*='/info/profil/']");
        var hasProfileLinks = profileElements.Any();
        
        // Check for redirect
        var currentUrl = loginResponse.RequestMessage?.RequestUri?.ToString() ?? "";
        var wasRedirected = !currentUrl.Contains("/login");
        
        // Look for user-specific content or logout links
        var logoutElements = responseDocument.QuerySelectorAll("a[href*='logout'], a[href*='abmelden']");
        var hasLogoutLinks = logoutElements.Any();
        
        var loginSuccessful = !isStillOnLoginPage || hasProfileLinks || wasRedirected || hasLogoutLinks;
        
        if (loginSuccessful)
        {
            Console.WriteLine("Login appears successful - no longer on login page");
        }
        else
        {
            Console.WriteLine("Login may have failed - still appears to be on login page");
            
            // Look for error messages
            var errorElements = responseDocument.QuerySelectorAll(".error, .alert, .warning, .fehler");
            foreach (var error in errorElements)
            {
                var errorText = error.TextContent?.Trim();
                if (!string.IsNullOrEmpty(errorText))
                {
                    Console.WriteLine($"Error message: {errorText}");
                }
            }
        }
        
        return loginSuccessful;
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
            Console.WriteLine($"Could not parse match time: {timeText}, using current time");
            return DateTimeOffset.Now.ToZonedDateTime();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing match time '{timeText}': {ex.Message}");
            return DateTimeOffset.Now.ToZonedDateTime();
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _browsingContext?.Dispose();
    }
}
