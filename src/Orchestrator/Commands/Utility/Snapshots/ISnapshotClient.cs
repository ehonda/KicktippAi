namespace Orchestrator.Commands.Utility.Snapshots;

/// <summary>
/// Interface for fetching HTML snapshots from Kicktipp.
/// </summary>
public interface ISnapshotClient
{
    /// <summary>
    /// Fetches the login page.
    /// Note: This is fetched without authentication to capture the login form structure.
    /// </summary>
    /// <returns>The HTML content of the login page, or null if the fetch failed.</returns>
    Task<string?> FetchLoginPageAsync();

    /// <summary>
    /// Fetches the standings page (tabellen).
    /// </summary>
    /// <param name="community">The community name.</param>
    /// <returns>The HTML content of the standings page, or null if the fetch failed.</returns>
    Task<string?> FetchStandingsPageAsync(string community);

    /// <summary>
    /// Fetches the main betting page (tippabgabe).
    /// </summary>
    /// <param name="community">The community name.</param>
    /// <returns>The HTML content of the betting page, or null if the fetch failed.</returns>
    Task<string?> FetchTippabgabePageAsync(string community);

    /// <summary>
    /// Fetches the bonus questions page (tippabgabe?bonus=true).
    /// </summary>
    /// <param name="community">The community name.</param>
    /// <returns>The HTML content of the bonus page, or null if the fetch failed.</returns>
    Task<string?> FetchBonusPageAsync(string community);

    /// <summary>
    /// Fetches all spielinfo pages (default view) by traversing through them.
    /// </summary>
    /// <param name="community">The community name.</param>
    /// <returns>A list of (fileName, content) tuples.</returns>
    Task<List<(string fileName, string content)>> FetchAllSpielinfoAsync(string community);

    /// <summary>
    /// Fetches all spielinfo pages with home/away history (ansicht=2) by traversing through them.
    /// </summary>
    /// <param name="community">The community name.</param>
    /// <returns>A list of (fileName, content) tuples with "-homeaway" suffix.</returns>
    Task<List<(string fileName, string content)>> FetchAllSpielinfoHomeAwayAsync(string community);

    /// <summary>
    /// Fetches all spielinfo pages with head-to-head history (ansicht=3) by traversing through them.
    /// </summary>
    /// <param name="community">The community name.</param>
    /// <returns>A list of (fileName, content) tuples with "-h2h" suffix.</returns>
    Task<List<(string fileName, string content)>> FetchAllSpielinfoHeadToHeadAsync(string community);
}
