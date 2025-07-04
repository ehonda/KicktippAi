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
            
            Console.WriteLine($"✅ Form action URL: {formActionUrl}");
            
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
                    Console.WriteLine($"📝 Added hidden field: {input.Name} = '{input.Value}'");
                }
            }
            
            // Submit login form to the parsed action URL
            var formContent = new FormUrlEncodedContent(formData);
            var loginResponse = await _httpClient.PostAsync(formActionUrl, formContent);
            
            // Debug: Check cookies after login attempt
            Console.WriteLine("DEBUG: Checking cookies after login attempt...");
            var cookiesAfterLoginHttps = _cookieContainer.GetCookies(new Uri("https://www.kicktipp.de"));
            var cookiesAfterLoginHttp = _cookieContainer.GetCookies(new Uri("http://www.kicktipp.de"));
            Console.WriteLine($"DEBUG: Found {cookiesAfterLoginHttps.Count} HTTPS cookies after login");
            Console.WriteLine($"DEBUG: Found {cookiesAfterLoginHttp.Count} HTTP cookies after login");
            
            foreach (Cookie cookie in cookiesAfterLoginHttps)
            {
                var anonymizedValue = AnonymizeCookieValue(cookie.Value);
                Console.WriteLine($"DEBUG: Post-login HTTPS cookie - Name: '{cookie.Name}', Value: '{anonymizedValue}', Domain: '{cookie.Domain}'");
            }
            foreach (Cookie cookie in cookiesAfterLoginHttp)
            {
                var anonymizedValue = AnonymizeCookieValue(cookie.Value);
                Console.WriteLine($"DEBUG: Post-login HTTP cookie - Name: '{cookie.Name}', Value: '{anonymizedValue}', Domain: '{cookie.Domain}'");
            }
            
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
            
            Console.WriteLine($"DEBUG: Found {httpsCookies.Count} cookies for HTTPS");
            Console.WriteLine($"DEBUG: Found {httpCookies.Count} cookies for HTTP");
            
            // Combine all cookies
            var allCookies = new List<Cookie>();
            foreach (Cookie cookie in httpsCookies) allCookies.Add(cookie);
            foreach (Cookie cookie in httpCookies) allCookies.Add(cookie);
            
            // Debug: List all cookies
            foreach (var cookie in allCookies)
            {
                var anonymizedValue = AnonymizeCookieValue(cookie.Value);
                Console.WriteLine($"DEBUG: Cookie - Name: '{cookie.Name}', Value: '{anonymizedValue}', Domain: '{cookie.Domain}', Path: '{cookie.Path}'");
            }
            
            // Try to find the login cookie (case-insensitive)
            foreach (var cookie in allCookies)
            {
                if (cookie.Name.Equals("login", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"DEBUG: Found login cookie");
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
                        Console.WriteLine($"DEBUG: Found potential session cookie '{cookie.Name}'");
                        await SaveTokenToFileAsync($"{cookie.Name.ToUpper()}_TOKEN", cookie.Value);
                        return cookie.Value;
                    }
                }
            }
            
            Console.WriteLine("DEBUG: No login or session cookie found");
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
        
        // Debug: Show response URL and status
        var responseUrl = loginResponse.RequestMessage?.RequestUri?.ToString() ?? "";
        Console.WriteLine($"DEBUG: Login response URL: {responseUrl}");
        Console.WriteLine($"DEBUG: Login response status: {loginResponse.StatusCode}");
        
        // Check if still on login page (Python implementation logic)
        var loginDiv = responseDocument.QuerySelector("div[content='Login']");
        var isStillOnLoginPage = loginDiv != null;
        Console.WriteLine($"DEBUG: Still on login page: {isStillOnLoginPage}");
        
        // Check for profile links (indicates successful login)
        var profileElements = responseDocument.QuerySelectorAll("a[href*='/info/profil/']");
        var hasProfileLinks = profileElements.Any();
        Console.WriteLine($"DEBUG: Has profile links: {hasProfileLinks}");
        
        // Check for redirect
        var currentUrl = loginResponse.RequestMessage?.RequestUri?.ToString() ?? "";
        var wasRedirected = !currentUrl.Contains("/login");
        Console.WriteLine($"DEBUG: Was redirected: {wasRedirected}");
        
        // Additional checks for common success indicators
        var titleElement = responseDocument.QuerySelector("title");
        var pageTitle = titleElement?.TextContent?.Trim() ?? "";
        Console.WriteLine($"DEBUG: Page title: '{pageTitle}'");
        
        // Look for user-specific content or logout links
        var logoutElements = responseDocument.QuerySelectorAll("a[href*='logout'], a[href*='abmelden']");
        var hasLogoutLinks = logoutElements.Any();
        Console.WriteLine($"DEBUG: Has logout links: {hasLogoutLinks}");
        
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
    /// Anonymize a cookie value for safe debug output
    /// </summary>
    /// <param name="value">The cookie value to anonymize</param>
    /// <returns>Anonymized value showing only first and last few characters</returns>
    private static string AnonymizeCookieValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "[empty]";
        
        if (value.Length <= 8)
            return "[***]";
        
        // Ensure we hide at least 12 characters in the middle for better privacy
        if (value.Length <= 20)
            return $"{value[..3]}...{value[^3..]}";
        
        return $"{value[..4]}...{value[^4..]}";
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

    public void Dispose()
    {
        _httpClient?.Dispose();
        _browsingContext?.Dispose();
    }
}
