using Microsoft.Extensions.Options;
using AngleSharp;
using AngleSharp.Html.Dom;

namespace KicktippIntegration.Authentication;

/// <summary>
/// HTTP message handler that manages Kicktipp authentication
/// Automatically handles login and maintains session cookies
/// </summary>
public class KicktippAuthenticationHandler : DelegatingHandler
{
    private const string BaseUrl = "https://www.kicktipp.de";
    private const string LoginUrl = $"{BaseUrl}/info/profil/login";
    
    private readonly IOptions<KicktippOptions> _options;
    private readonly IBrowsingContext _browsingContext;
    private readonly SemaphoreSlim _loginSemaphore = new(1, 1);
    private bool _isLoggedIn = false;

    public KicktippAuthenticationHandler(IOptions<KicktippOptions> options)
    {
        _options = options;
        var config = Configuration.Default.WithDefaultLoader();
        _browsingContext = BrowsingContext.New(config);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Ensure we're logged in before making any requests
        await EnsureLoggedInAsync(cancellationToken);
        
        // Send the original request
        var response = await base.SendAsync(request, cancellationToken);
        
        // If we get a 401/403 or are redirected to login, try to re-authenticate
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.RequestMessage?.RequestUri?.ToString().Contains("login") == true)
        {
            Console.WriteLine("Authentication may have expired, attempting re-login...");
            _isLoggedIn = false;
            await EnsureLoggedInAsync(cancellationToken);
            
            // Retry the original request
            var retryRequest = await CloneRequestAsync(request);
            response = await base.SendAsync(retryRequest, cancellationToken);
        }
        
        return response;
    }

    private async Task EnsureLoggedInAsync(CancellationToken cancellationToken)
    {
        if (_isLoggedIn) return;
        
        // Use a semaphore for async-safe synchronization
        await _loginSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_isLoggedIn) return;
            
            // Perform login 
            await PerformLoginAsync(cancellationToken);
        }
        finally
        {
            _loginSemaphore.Release();
        }
    }

    private async Task PerformLoginAsync(CancellationToken cancellationToken)
    {
        try
        {
            var credentials = _options.Value.ToCredentials();
            if (!credentials.IsValid)
            {
                throw new InvalidOperationException("Invalid Kicktipp credentials configured");
            }

            Console.WriteLine("Performing Kicktipp authentication...");
            
            // Get the login page first
            var loginPageRequest = new HttpRequestMessage(HttpMethod.Get, LoginUrl);
            var loginPageResponse = await base.SendAsync(loginPageRequest, cancellationToken);
            
            if (!loginPageResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to access login page: {loginPageResponse.StatusCode}");
            }

            var loginPageContent = await loginPageResponse.Content.ReadAsStringAsync(cancellationToken);
            var loginDocument = await _browsingContext.OpenAsync(req => req.Content(loginPageContent));
            
            // Find the login form
            var loginForm = loginDocument.QuerySelector("form") as IHtmlFormElement;
            if (loginForm == null)
            {
                throw new InvalidOperationException("Could not find login form on the page");
            }
            
            // Parse the form action URL - use the action from the form
            var formAction = loginForm.Action;
            var formActionUrl = string.IsNullOrEmpty(formAction) ? LoginUrl : 
                (formAction.StartsWith("http") ? formAction : $"{BaseUrl}{formAction}");
            
            // Prepare form data with the exact field names from the HTML
            var formData = new List<KeyValuePair<string, string>>
            {
                new("kennung", credentials.Username),
                new("passwort", credentials.Password)
            };
            
            // Add hidden fields (like _charset_)
            var hiddenInputs = loginForm.QuerySelectorAll("input[type=hidden]").OfType<IHtmlInputElement>();
            foreach (var input in hiddenInputs)
            {
                if (!string.IsNullOrEmpty(input.Name))
                {
                    formData.Add(new KeyValuePair<string, string>(input.Name, input.Value ?? ""));
                }
            }

            // Submit the login form
            var loginRequest = new HttpRequestMessage(HttpMethod.Post, formActionUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };
            
            var loginResponse = await base.SendAsync(loginRequest, cancellationToken);
            
            if (!loginResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Login request failed: {loginResponse.StatusCode}");
            }
            
            // Check if login was successful
            // The most reliable indicator is that we're no longer on a login page
            var responseContent = await loginResponse.Content.ReadAsStringAsync(cancellationToken);
            var currentUrl = loginResponse.RequestMessage?.RequestUri?.ToString() ?? "";
            
            // Simple and reliable: if we're not on a login-related URL, login was successful
            var loginSuccessful = !currentUrl.Contains("/login") && !currentUrl.Contains("/profil/login");
            
            // Additional check: look for login form on the response page
            // If we still see a login form, login probably failed
            if (loginSuccessful)
            {
                var responseDocument = await _browsingContext.OpenAsync(req => req.Content(responseContent));
                var stillHasLoginForm = responseDocument.QuerySelector("form#loginFormular") != null;
                
                if (stillHasLoginForm)
                {
                    loginSuccessful = false;
                }
            }
            
            if (loginSuccessful)
            {
                Console.WriteLine("✓ Kicktipp authentication successful");
                _isLoggedIn = true;
            }
            else
            {
                Console.WriteLine("Login failed - still on login page or login form present");
                throw new UnauthorizedAccessException("Kicktipp login failed - check credentials");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Kicktipp authentication failed: {ex.Message}");
            throw;
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        
        // Copy headers
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        // Copy content if present
        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);
            
            // Copy content headers
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        
        return clone;
    }
}
