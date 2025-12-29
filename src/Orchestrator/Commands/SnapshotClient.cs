using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Commands;

/// <summary>
/// A simple HTTP client for fetching HTML snapshots from Kicktipp.
/// This client is specifically for snapshot generation and does not parse the HTML.
/// </summary>
public class SnapshotClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly IBrowsingContext _browsingContext;

    public SnapshotClient(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var config = Configuration.Default.WithDefaultLoader();
        _browsingContext = BrowsingContext.New(config);
    }

    /// <summary>
    /// Fetches the standings page (tabellen).
    /// </summary>
    public async Task<string?> FetchStandingsPageAsync(string community)
    {
        var url = $"{community}/tabellen";
        return await FetchPageAsync(url, "tabellen");
    }

    /// <summary>
    /// Fetches the main betting page (tippabgabe).
    /// </summary>
    public async Task<string?> FetchTippabgabePageAsync(string community)
    {
        var url = $"{community}/tippabgabe";
        return await FetchPageAsync(url, "tippabgabe");
    }

    /// <summary>
    /// Fetches the bonus questions page (tippabgabe?bonus=true).
    /// </summary>
    public async Task<string?> FetchBonusPageAsync(string community)
    {
        var url = $"{community}/tippabgabe?bonus=true";
        return await FetchPageAsync(url, "tippabgabe-bonus");
    }

    /// <summary>
    /// Fetches all spielinfo pages by traversing through them.
    /// Returns a list of (fileName, content) tuples.
    /// </summary>
    public async Task<List<(string fileName, string content)>> FetchAllSpielinfoAsync(string community)
    {
        var results = new List<(string fileName, string content)>();

        // First, get the tippabgabe page to find the link to spielinfos
        var tippabgabeUrl = $"{community}/tippabgabe";
        var response = await _httpClient.GetAsync(tippabgabeUrl);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch tippabgabe page. Status: {StatusCode}", response.StatusCode);
            return results;
        }

        var content = await response.Content.ReadAsStringAsync();
        var document = await _browsingContext.OpenAsync(req => req.Content(content));

        // Find the "Tippabgabe mit Spielinfos" link
        var spielinfoLink = document.QuerySelector("a[href*='spielinfo']");
        if (spielinfoLink == null)
        {
            _logger.LogWarning("Could not find Spielinfo link on tippabgabe page");
            return results;
        }

        var spielinfoUrl = spielinfoLink.GetAttribute("href");
        if (string.IsNullOrEmpty(spielinfoUrl))
        {
            _logger.LogWarning("Spielinfo link has no href attribute");
            return results;
        }

        // Make URL absolute if it's relative
        if (spielinfoUrl.StartsWith("/"))
        {
            spielinfoUrl = spielinfoUrl.Substring(1);
        }

        _logger.LogInformation("Starting to fetch spielinfo pages...");

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
                    _logger.LogWarning("Failed to fetch spielinfo page: {Url}. Status: {StatusCode}", 
                        currentUrl, spielinfoResponse.StatusCode);
                    break;
                }

                var spielinfoContent = await spielinfoResponse.Content.ReadAsStringAsync();
                matchCount++;

                // Generate filename from URL or index
                var fileName = $"spielinfo-{matchCount:D2}";
                results.Add((fileName, spielinfoContent));
                _logger.LogDebug("Fetched spielinfo page {Count}: {Url}", matchCount, currentUrl);

                // Parse to find next link
                var spielinfoDocument = await _browsingContext.OpenAsync(req => req.Content(spielinfoContent));
                var nextLink = FindNextMatchLink(spielinfoDocument);

                if (nextLink != null)
                {
                    currentUrl = nextLink;
                    if (currentUrl.StartsWith("/"))
                    {
                        currentUrl = currentUrl.Substring(1);
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
                _logger.LogError(ex, "Error fetching spielinfo page: {Url}", currentUrl);
                break;
            }
        }

        _logger.LogInformation("Fetched {Count} spielinfo pages", results.Count);
        return results;
    }

    private async Task<string?> FetchPageAsync(string url, string pageName)
    {
        try
        {
            _logger.LogDebug("Fetching {PageName} from {Url}", pageName, url);
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch {PageName}. Status: {StatusCode}", pageName, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Successfully fetched {PageName} ({Length} bytes)", pageName, content.Length);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching {PageName}", pageName);
            return null;
        }
    }

    /// <summary>
    /// Finds the next match link (right arrow navigation) on a spielinfo page.
    /// This mirrors the logic in KicktippClient.FindNextMatchLink.
    /// </summary>
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

            return href;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error finding next match link");
            return null;
        }
    }
}
