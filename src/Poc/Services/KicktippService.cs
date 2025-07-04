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
    private const string BaseUrl = "http://www.kicktipp.de";
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
            
            Console.WriteLine("Found login form, submitting credentials...");
            
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
            
            // Submit login form
            var formContent = new FormUrlEncodedContent(formData);
            var loginResponse = await _httpClient.PostAsync(LoginUrl, formContent);
            
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
    public string? GetLoginToken()
    {
        try
        {
            var uri = new Uri(BaseUrl);
            var cookies = _cookieContainer.GetCookies(uri);
            
            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name.Equals("login", StringComparison.OrdinalIgnoreCase))
                {
                    return cookie.Value;
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
        
        var loginSuccessful = !isStillOnLoginPage || hasProfileLinks || wasRedirected;
        
        if (loginSuccessful)
        {
            Console.WriteLine("Login appears successful - no longer on login page");
        }
        else
        {
            Console.WriteLine("Login may have failed - still appears to be on login page");
            
            // Look for error messages
            var errorElements = responseDocument.QuerySelectorAll(".error, .alert, .warning");
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

    public void Dispose()
    {
        _httpClient?.Dispose();
        _browsingContext?.Dispose();
    }
}
