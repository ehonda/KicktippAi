using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp;

/// <summary>
/// Interface for providing context documents from Kicktipp data sources.
/// </summary>
public interface IKicktippContextProvider : IContextProvider<DocumentContext>
{
    /// <summary>
    /// Gets context for the two teams in a match.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An enumerable of context documents for both teams.</returns>
    IAsyncEnumerable<DocumentContext> GetMatchContextAsync(
        string homeTeam,
        string awayTeam,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets context for bonus questions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An enumerable of context documents relevant for bonus questions.</returns>
    IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current Bundesliga standings as context.
    /// </summary>
    /// <returns>A document context containing the current standings.</returns>
    Task<DocumentContext> CurrentBundesligaStandings();

    /// <summary>
    /// Gets the community scoring rules as context.
    /// </summary>
    /// <returns>A document context containing the scoring rules.</returns>
    Task<DocumentContext> CommunityScoringRules();

    /// <summary>
    /// Gets recent match history for a specific team.
    /// </summary>
    /// <param name="teamName">The name of the team to get history for.</param>
    /// <returns>A document context containing the team's recent match history.</returns>
    Task<DocumentContext> RecentHistory(string teamName);

    /// <summary>
    /// Gets home history for the home team in a match.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>A document context containing home team's home history.</returns>
    Task<DocumentContext> HomeHistory(string homeTeam, string awayTeam);

    /// <summary>
    /// Gets away history for the away team in a match.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>A document context containing away team's away history.</returns>
    Task<DocumentContext> AwayHistory(string homeTeam, string awayTeam);

    /// <summary>
    /// Gets head-to-head history between two teams.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>A document context containing head-to-head match history.</returns>
    Task<DocumentContext> HeadToHeadHistory(string homeTeam, string awayTeam);
}
