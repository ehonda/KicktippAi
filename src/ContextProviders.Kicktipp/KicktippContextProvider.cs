using System.Runtime.CompilerServices;
using System.Text;
using ContextProviders.Kicktipp.Csv;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp;

public class KicktippContextProvider : IKicktippContextProvider
{
    private readonly IKicktippClient _kicktippClient;
    private readonly IFileProvider _communityRulesFileProvider;
    private readonly string _community;
    private readonly string _communityContext;
    private readonly Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>> _teamHistoryLazy;
    private readonly Lazy<Task<IReadOnlyDictionary<string, (List<MatchResult> homeHistory, List<MatchResult> awayHistory)>>> _homeAwayHistoryLazy;
    private readonly Lazy<Task<IReadOnlyDictionary<string, List<HeadToHeadResult>>>> _detailedHeadToHeadHistoryLazy;

    public KicktippContextProvider(
        IKicktippClient kicktippClient,
        IFileProvider communityRulesFileProvider,
        string community,
        string? communityContext = null)
    {
        _kicktippClient = kicktippClient ?? throw new ArgumentNullException(nameof(kicktippClient));
        _communityRulesFileProvider = communityRulesFileProvider ?? throw new ArgumentNullException(nameof(communityRulesFileProvider));
        _community = community ?? throw new ArgumentNullException(nameof(community));
        _communityContext = communityContext ?? community;
        _teamHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>>(LoadTeamHistoryAsync);
        _homeAwayHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, (List<MatchResult> homeHistory, List<MatchResult> awayHistory)>>>(LoadHomeAwayHistoryAsync);
        _detailedHeadToHeadHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, List<HeadToHeadResult>>>>(LoadDetailedHeadToHeadHistoryAsync);
    }

    private async Task<IReadOnlyDictionary<string, List<MatchResult>>> LoadTeamHistoryAsync()
    {
        var matchesWithHistory = await _kicktippClient.GetMatchesWithHistoryAsync(_community);
        var teamHistory = new Dictionary<string, List<MatchResult>>();

        foreach (var matchWithHistory in matchesWithHistory)
        {
            teamHistory[matchWithHistory.Match.HomeTeam] = matchWithHistory.HomeTeamHistory;
            teamHistory[matchWithHistory.Match.AwayTeam] = matchWithHistory.AwayTeamHistory;
        }

        return teamHistory;
    }
    
    private async Task<IReadOnlyDictionary<string, (List<MatchResult> homeHistory, List<MatchResult> awayHistory)>> LoadHomeAwayHistoryAsync()
    {
        var matchesWithHistory = await _kicktippClient.GetMatchesWithHistoryAsync(_community);
        var homeAwayHistory = new Dictionary<string, (List<MatchResult> homeHistory, List<MatchResult> awayHistory)>();

        foreach (var matchWithHistory in matchesWithHistory)
        {
            var homeTeam = matchWithHistory.Match.HomeTeam;
            var awayTeam = matchWithHistory.Match.AwayTeam;
            
            var (homeTeamHistory, awayTeamHistory) = await _kicktippClient.GetHomeAwayHistoryAsync(_community, homeTeam, awayTeam);
            var cacheKey = $"{homeTeam}|{awayTeam}";
            homeAwayHistory[cacheKey] = (homeTeamHistory, awayTeamHistory);
        }

        return homeAwayHistory;
    }

    private async Task<IReadOnlyDictionary<string, List<HeadToHeadResult>>> LoadDetailedHeadToHeadHistoryAsync()
    {
        var matchesWithHistory = await _kicktippClient.GetMatchesWithHistoryAsync(_community);
        var detailedHeadToHeadHistory = new Dictionary<string, List<HeadToHeadResult>>();

        foreach (var matchWithHistory in matchesWithHistory)
        {
            var homeTeam = matchWithHistory.Match.HomeTeam;
            var awayTeam = matchWithHistory.Match.AwayTeam;
            
            var history = await _kicktippClient.GetHeadToHeadDetailedHistoryAsync(_community, homeTeam, awayTeam);
            var cacheKey = $"{homeTeam}|{awayTeam}";
            detailedHeadToHeadHistory[cacheKey] = history;
        }

        return detailedHeadToHeadHistory;
    }
    
    public async IAsyncEnumerable<DocumentContext> GetContextAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Provide current Bundesliga standings
        yield return await CurrentBundesligaStandings();
        
        // Provide community scoring rules
        yield return await CommunityScoringRules();
    }
    
    /// <summary>
    /// Gets context for the two teams in a match.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>An enumerable of context documents for both teams.</returns>
    public async IAsyncEnumerable<DocumentContext> GetMatchContextAsync(string homeTeam, string awayTeam, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Provide current Bundesliga standings
        yield return await CurrentBundesligaStandings();
        
        // Provide community scoring rules
        yield return await CommunityScoringRules();
        
        // Provide recent history for both teams (Position 1 - already implemented)
        yield return await RecentHistory(homeTeam);
        yield return await RecentHistory(awayTeam);

        // Provide home/away specific history for both teams (Position 2)
        yield return await HomeHistory(homeTeam, awayTeam);
        yield return await AwayHistory(homeTeam, awayTeam);

        // Provide head-to-head history between the teams (Position 3)
        yield return await HeadToHeadHistory(homeTeam, awayTeam);
    }
    
    /// <summary>
    /// Gets context for bonus questions.
    /// </summary>
    /// <returns>An enumerable of context documents relevant for bonus questions.</returns>
    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Provide current Bundesliga standings
        yield return await CurrentBundesligaStandings();
        
        // Provide community scoring rules
        yield return await CommunityScoringRules();
        
        // For bonus questions, we could add historical season data, transfer information, etc.
        // For now, we'll use the standings as the primary context
    }
    
    /// <summary>
    /// Gets the current Bundesliga standings as context.
    /// </summary>
    /// <returns>A document context containing the current standings.</returns>
    public async Task<DocumentContext> CurrentBundesligaStandings()
    {
        var standings = await _kicktippClient.GetStandingsAsync(_community);
        
        return standings.ToCsvDocumentContext<TeamStanding, TeamStandingCsvMap>("bundesliga-standings");
    }
    
    /// <summary>
    /// Gets recent match history for a specific team.
    /// </summary>
    /// <param name="teamName">The name of the team to get history for.</param>
    /// <returns>A document context containing the team's recent match history.</returns>
    public async Task<DocumentContext> RecentHistory(string teamName)
    {
        var teamHistory = await _teamHistoryLazy.Value;
        var matchResults = teamHistory.TryGetValue(teamName, out var results) ? results : new List<MatchResult>();
        
        var teamAbbreviation = GetTeamAbbreviation(teamName);
        
        return matchResults.ToCsvDocumentContext<MatchResult, MatchResultCsvMap>($"recent-history-{teamAbbreviation}");
    }

    /// <summary>
    /// Gets home/away specific history for both teams in a match.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>A document context containing home team's home history and away team's away history.</returns>
    public async Task<DocumentContext> HomeHistory(string homeTeam, string awayTeam)
    {
        var homeAwayHistory = await _homeAwayHistoryLazy.Value;
        var cacheKey = $"{homeTeam}|{awayTeam}";
        
        var (homeTeamHistory, _) = homeAwayHistory.TryGetValue(cacheKey, out var history) 
            ? history 
            : (new List<MatchResult>(), new List<MatchResult>());
        
        var homeAbbreviation = GetTeamAbbreviation(homeTeam);
        
        return homeTeamHistory.ToCsvDocumentContext<MatchResult, MatchResultCsvMap>($"home-history-{homeAbbreviation}");
    }
    
    public async Task<DocumentContext> AwayHistory(string homeTeam, string awayTeam)
    {
        var homeAwayHistory = await _homeAwayHistoryLazy.Value;
        var cacheKey = $"{homeTeam}|{awayTeam}";
        
        var (_, awayTeamHistory) = homeAwayHistory.TryGetValue(cacheKey, out var history) 
            ? history 
            : (new List<MatchResult>(), new List<MatchResult>());
        
        var awayAbbreviation = GetTeamAbbreviation(awayTeam);
        
        return awayTeamHistory.ToCsvDocumentContext<MatchResult, MatchResultCsvMap>($"away-history-{awayAbbreviation}");
    }

    /// <summary>
    /// Gets head-to-head history between two teams.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>A document context containing head-to-head match history.</returns>
    public async Task<DocumentContext> HeadToHeadHistory(string homeTeam, string awayTeam)
    {
        var detailedHeadToHeadHistory = await _detailedHeadToHeadHistoryLazy.Value;
        var cacheKey = $"{homeTeam}|{awayTeam}";
        
        var history = detailedHeadToHeadHistory.TryGetValue(cacheKey, out var cachedHistory) 
            ? cachedHistory 
            : new List<HeadToHeadResult>();
        
        var homeAbbreviation = GetTeamAbbreviation(homeTeam);
        var awayAbbreviation = GetTeamAbbreviation(awayTeam);
        
        return history.ToCsvDocumentContext<HeadToHeadResult, HeadToHeadResultCsvMap>(
            $"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}");
    }
    
    /// <summary>
    /// Gets the community scoring rules as context.
    /// </summary>
    /// <returns>A document context containing the scoring rules.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the community-specific scoring rules file is not found.</exception>
    public async Task<DocumentContext> CommunityScoringRules()
    {
        var filePath = $"{_communityContext}.md";
        var fileInfo = _communityRulesFileProvider.GetFileInfo(filePath);
        
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                $"Community scoring rules file not found: {filePath}. Expected file for community context '{_communityContext}'.");
        }
        
        var content = await ReadFileContentAsync(fileInfo);
        return new DocumentContext(
            Name: $"community-rules-{_communityContext}.md",
            Content: content);
    }
    
    /// <summary>
    /// Reads the content from a file info asynchronously.
    /// </summary>
    /// <param name="fileInfo">The file info to read from.</param>
    /// <returns>The file content as a string.</returns>
    private static async Task<string> ReadFileContentAsync(IFileInfo fileInfo)
    {
        await using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
    
    /// <summary>
    /// Gets a team abbreviation for file naming.
    /// </summary>
    private string GetTeamAbbreviation(string teamName)
    {
        // Current season team abbreviations (2025-26 Bundesliga participants)
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "1. FC Heidenheim 1846", "fch" },
            { "1. FC Köln", "fck" },
            { "1. FC Union Berlin", "fcu" },
            { "1899 Hoffenheim", "tsg" },
            { "Bayer 04 Leverkusen", "b04" },
            { "Bor. Mönchengladbach", "bmg" },
            { "Borussia Dortmund", "bvb" },
            { "Eintracht Frankfurt", "sge" },
            { "FC Augsburg", "fca" },
            { "FC Bayern München", "fcb" },
            { "FC St. Pauli", "fcs" },
            { "FSV Mainz 05", "m05" },
            { "Hamburger SV", "hsv" },
            { "RB Leipzig", "rbl" },
            { "SC Freiburg", "scf" },
            { "VfB Stuttgart", "vfb" },
            { "VfL Wolfsburg", "wob" },
            { "Werder Bremen", "svw" }
        };
        
        if (abbreviations.TryGetValue(teamName, out var abbreviation))
        {
            return abbreviation;
        }
        
        // Fallback: create abbreviation from team name
        var words = teamName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var abbr = new StringBuilder();
        
        foreach (var word in words.Take(3)) // Take up to 3 words
        {
            if (word.Length > 0 && char.IsLetter(word[0]))
            {
                abbr.Append(char.ToLowerInvariant(word[0]));
            }
        }
        
        return abbr.Length > 0 ? abbr.ToString() : "unknown";
    }
}
