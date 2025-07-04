using AngleSharp;
using AngleSharp.Html.Dom;
using KicktippAi.Poc.Models;
using System.Net;

namespace KicktippAi.Poc.Services;

/// <summary>
/// Service for interacting with kicktipp.de website
/// Based on the Python implementation in ehonda/kicktipp-cli
/// </summary>
public class KicktippService : IDisposable
{
    private const string BaseUrl = "https://www.kicktipp.de";
    private const string LoginUrl = $"{BaseUrl}/info/profil/login";
    
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private readonly IBrowsingContext _browsingContext;

    public KicktippService()
    {
        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler() { CookieContainer = _cookieContainer };
        _httpClient = new HttpClient(handler);
        
        var config = Configuration.Default.WithDefaultLoader();
        _browsingContext = BrowsingContext.New(config);
    }

    /// <summary>
    /// Attempt to login to kicktipp.de with provided credentials
    /// </summary>
    /// <param name="credentials">Username and password</param>
    /// <returns>True if login was successful</returns>
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

    /// <summary>
    /// Get the login cookie value for token-based authentication
    /// </summary>
    /// <returns>Login cookie value or null if not found</returns>
    public async Task<string?> GetLoginTokenAsync()
    {
        try
        {
            // Check both HTTPS and HTTP variants
            var httpsUri = new Uri("https://www.kicktipp.de");
            var httpUri = new Uri("http://www.kicktipp.de");
            
            var httpsCookies = _cookieContainer.GetCookies(httpsUri);
            var httpCookies = _cookieContainer.GetCookies(httpUri);
            
            // Combine all cookies
            var allCookies = new List<Cookie>();
            foreach (Cookie cookie in httpsCookies) allCookies.Add(cookie);
            foreach (Cookie cookie in httpCookies) allCookies.Add(cookie);
            
            // Try to find the login cookie (case-insensitive)
            foreach (var cookie in allCookies)
            {
                if (cookie.Name.Equals("login", StringComparison.OrdinalIgnoreCase))
                {
                    await SaveTokenToFileAsync("LOGIN_TOKEN", cookie.Value);
                    return cookie.Value;
                }
            }
            
            // If not found, try other potential cookie names based on common patterns
            string[] potentialNames = { "sessionid", "session", "auth", "token", "sid", "PHPSESSID" };
            
            foreach (var cookie in allCookies)
            {
                foreach (var potentialName in potentialNames)
                {
                    if (cookie.Name.Equals(potentialName, StringComparison.OrdinalIgnoreCase))
                    {
                        await SaveTokenToFileAsync($"{cookie.Name.ToUpper()}_TOKEN", cookie.Value);
                        return cookie.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting login cookie: {ex.Message}");
        }
        
        return null;
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

    /// <summary>
    /// Save a token to the .env file for later use
    /// </summary>
    /// <param name="tokenName">Name of the token variable</param>
    /// <param name="tokenValue">Value of the token</param>
    private static async Task SaveTokenToFileAsync(string tokenName, string tokenValue)
    {
        try
        {
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            var lines = new List<string>();
            
            // Read existing .env file if it exists
            if (File.Exists(envPath))
            {
                lines.AddRange(await File.ReadAllLinesAsync(envPath));
            }
            
            // Remove existing token entry if present
            lines.RemoveAll(line => line.StartsWith($"{tokenName}=", StringComparison.OrdinalIgnoreCase));
            
            // Add new token entry
            lines.Add($"{tokenName}={tokenValue}");
            
            // Write back to file
            await File.WriteAllLinesAsync(envPath, lines);
            
            Console.WriteLine($"Token saved to .env file as {tokenName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not save token to file: {ex.Message}");
        }
    }

    /// <summary>
    /// Get open predictions for the specified community
    /// </summary>
    /// <param name="community">Community name (e.g., "ehonda-test")</param>
    /// <returns>List of matches available for prediction</returns>
    public async Task<List<Match>> GetOpenPredictionsAsync(string community)
    {
        if (string.IsNullOrEmpty(community))
        {
            throw new ArgumentException("Community name cannot be empty");
        }

        try
        {
            Console.WriteLine($"Fetching open predictions for community: {community}");
            
            // Build the URL for the community's tippabgabe page
            var tippabgabeUrl = $"{BaseUrl}/{community}/tippabgabe";
            Console.WriteLine($"Navigating to: {tippabgabeUrl}");
            
            // Fetch the page
            var response = await _httpClient.GetAsync(tippabgabeUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to access predictions page. Status: {response.StatusCode}");
                return new List<Match>();
            }
            
            var pageContent = await response.Content.ReadAsStringAsync();
            var document = await _browsingContext.OpenAsync(req => req.Content(pageContent));
            
            // Find the main content area (kicktipp-content)
            var contentArea = document.QuerySelector("#kicktipp-content");
            if (contentArea == null)
            {
                Console.WriteLine("Could not find content area on the predictions page");
                return new List<Match>();
            }
            
            // Find the table with predictions
            var tbody = contentArea.QuerySelector("tbody");
            if (tbody == null)
            {
                Console.WriteLine("No predictions table found");
                return new List<Match>();
            }
            
            var matches = new List<Match>();
            var rows = tbody.QuerySelectorAll("tr");
            
            Console.WriteLine($"Found {rows.Length} table rows to process");
            
            DateTimeOffset? lastMatchDate = null;
            
            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 3) continue; // Need at least date, home team, road team
                
                try
                {
                    // Parse match data from table cells
                    // Based on Python: row[0]=date, row[1]=hometeam, row[2]=roadteam
                    var dateText = cells[0].TextContent?.Trim() ?? "";
                    var homeTeam = cells[1].TextContent?.Trim() ?? "";
                    var roadTeam = cells[2].TextContent?.Trim() ?? "";
                    
                    if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(roadTeam))
                        continue;
                    
                    // Parse date - German format: "dd.MM.yy HH:mm"
                    DateTimeOffset? matchDate = null;
                    if (!string.IsNullOrEmpty(dateText))
                    {
                        if (DateTimeOffset.TryParseExact(dateText, "dd.MM.yy HH:mm", 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            System.Globalization.DateTimeStyles.None, out var parsedDate))
                        {
                            matchDate = parsedDate;
                            lastMatchDate = matchDate;
                        }
                    }
                    
                    // If no date in this row, use the last parsed date (matches can share dates)
                    if (!matchDate.HasValue && lastMatchDate.HasValue)
                    {
                        matchDate = lastMatchDate;
                    }
                    
                    var match = new Match
                    {
                        HomeTeam = homeTeam,
                        RoadTeam = roadTeam,
                        MatchDate = matchDate
                    };
                    
                    matches.Add(match);
                    Console.WriteLine($"Parsed match: {match}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing table row: {ex.Message}");
                    continue;
                }
            }
            
            Console.WriteLine($"Successfully parsed {matches.Count} matches");
            return matches;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while fetching predictions: {ex.Message}");
            return new List<Match>();
        }
    }

    /// <summary>
    /// Place random bets for open predictions in the specified community
    /// Based on the Python place_bets function from kicktipp-cli
    /// </summary>
    /// <param name="community">Community name (e.g., "ehonda-test")</param>
    /// <param name="dryRun">If true, don't actually submit bets, just show what would be bet</param>
    /// <param name="overrideBets">If true, override existing bets, otherwise skip them</param>
    /// <returns>True if betting process completed successfully</returns>
    public async Task<bool> PlaceRandomBetsAsync(string community, bool dryRun = false, bool overrideBets = false)
    {
        if (string.IsNullOrEmpty(community))
        {
            throw new ArgumentException("Community name cannot be empty");
        }

        try
        {
            Console.WriteLine($"Starting bet placement for community: {community}");
            Console.WriteLine($"Dry run: {dryRun}, Override existing bets: {overrideBets}");
            
            // Build the URL for the community's tippabgabe page
            var tippabgabeUrl = $"{BaseUrl}/{community}/tippabgabe";
            Console.WriteLine($"Navigating to: {tippabgabeUrl}");
            
            // Fetch the page
            var response = await _httpClient.GetAsync(tippabgabeUrl);
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
            Console.WriteLine($"Found {rows.Length} betting rows to process");
            
            var predictor = new SimplePredictor();
            var formData = new List<KeyValuePair<string, string>>();
            var betsPlaced = 0;
            var betsSkipped = 0;
            
            DateTimeOffset? lastMatchDate = null;
            
            // Add hidden fields from the form
            var hiddenInputs = betForm.QuerySelectorAll("input[type=hidden]").OfType<IHtmlInputElement>();
            foreach (var input in hiddenInputs)
            {
                if (!string.IsNullOrEmpty(input.Name) && input.Value != null)
                {
                    formData.Add(new KeyValuePair<string, string>(input.Name, input.Value));
                }
            }
            
            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 4) continue; // Need at least date, home team, road team, and bet inputs
                
                try
                {
                    // Parse match data - cells[3] should contain the bet input fields
                    var dateText = cells[0].TextContent?.Trim() ?? "";
                    var homeTeam = cells[1].TextContent?.Trim() ?? "";
                    var roadTeam = cells[2].TextContent?.Trim() ?? "";
                    
                    if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(roadTeam))
                        continue;
                    
                    // Parse date
                    DateTimeOffset? matchDate = null;
                    if (!string.IsNullOrEmpty(dateText))
                    {
                        if (DateTimeOffset.TryParseExact(dateText, "dd.MM.yy HH:mm", 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            System.Globalization.DateTimeStyles.None, out var parsedDate))
                        {
                            matchDate = parsedDate;
                            lastMatchDate = matchDate;
                        }
                    }
                    
                    if (!matchDate.HasValue && lastMatchDate.HasValue)
                    {
                        matchDate = lastMatchDate;
                    }
                    
                    var match = new Match
                    {
                        HomeTeam = homeTeam,
                        RoadTeam = roadTeam,
                        MatchDate = matchDate
                    };
                    
                    // Find bet input fields in the row (based on Python: _heimTipp and _gastTipp)
                    var homeInput = cells[3].QuerySelector("input[id$='_heimTipp']") as IHtmlInputElement;
                    var awayInput = cells[3].QuerySelector("input[id$='_gastTipp']") as IHtmlInputElement;
                    
                    if (homeInput == null || awayInput == null)
                    {
                        Console.WriteLine($"{match} - no betting inputs found, skipping");
                        continue;
                    }
                    
                    // Check if bets are already placed
                    var hasExistingHomeBet = !string.IsNullOrEmpty(homeInput.Value);
                    var hasExistingAwayBet = !string.IsNullOrEmpty(awayInput.Value);
                    
                    if ((hasExistingHomeBet || hasExistingAwayBet) && !overrideBets)
                    {
                        var existingBet = $"{homeInput.Value ?? ""}:{awayInput.Value ?? ""}";
                        Console.WriteLine($"{match} - skipped, already placed {existingBet}");
                        betsSkipped++;
                        continue;
                    }
                    
                    // Generate prediction
                    var prediction = predictor.Predict(match);
                    
                    Console.WriteLine($"{match} - betting {prediction}");
                    
                    // Add bet to form data
                    if (!string.IsNullOrEmpty(homeInput.Name) && !string.IsNullOrEmpty(awayInput.Name))
                    {
                        formData.Add(new KeyValuePair<string, string>(homeInput.Name, prediction.HomeGoals.ToString()));
                        formData.Add(new KeyValuePair<string, string>(awayInput.Name, prediction.AwayGoals.ToString()));
                    }
                    else
                    {
                        Console.WriteLine($"{match} - input field names are missing, skipping");
                        continue;
                    }
                    
                    betsPlaced++;
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
            
            if (dryRun)
            {
                Console.WriteLine("INFO: Dry run - no bets were actually submitted");
                return true;
            }
            
            // Submit the form with all bets
            Console.WriteLine("Submitting betting form...");
            
            // Find submit button (based on Python: submit='submitbutton')
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
            var formActionUrl = string.IsNullOrEmpty(betForm.Action) ? tippabgabeUrl : 
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

    public void Dispose()
    {
        _httpClient?.Dispose();
        _browsingContext?.Dispose();
    }
}
