using System.Runtime.CompilerServices;
using ContextProviders.Kicktipp.Csv;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp;

public class KicktippContextProvider : IKicktippContextProvider
{
    private readonly IKicktippClient _kicktippClient;
    private readonly IFileProvider _communityRulesFileProvider;
    private readonly IFileProvider _worldCupContextDocumentsFileProvider;
    private readonly string _community;
    private readonly string _communityContext;
    private readonly string _competition;
    private readonly int? _matchday;
    private readonly Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>> _teamHistoryLazy;
    private readonly Lazy<Task<IReadOnlyDictionary<string, (List<MatchResult> homeHistory, List<MatchResult> awayHistory)>>> _homeAwayHistoryLazy;
    private readonly Lazy<Task<IReadOnlyDictionary<string, List<HeadToHeadResult>>>> _detailedHeadToHeadHistoryLazy;

    public KicktippContextProvider(
        IKicktippClient kicktippClient,
        IFileProvider communityRulesFileProvider,
        string community,
        string? communityContext = null,
        string? competition = null,
        int? matchday = null,
        IFileProvider? worldCupContextDocumentsFileProvider = null)
    {
        _kicktippClient = kicktippClient ?? throw new ArgumentNullException(nameof(kicktippClient));
        _communityRulesFileProvider = communityRulesFileProvider ?? throw new ArgumentNullException(nameof(communityRulesFileProvider));
        _worldCupContextDocumentsFileProvider = worldCupContextDocumentsFileProvider ?? WorldCup2026ContextDocumentsFileProvider.Create();
        _community = community ?? throw new ArgumentNullException(nameof(community));
        _communityContext = communityContext ?? community;
        _competition = competition ?? CompetitionIds.Bundesliga2025_26;
        _matchday = matchday;
        _teamHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>>(LoadTeamHistoryAsync);
        _homeAwayHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, (List<MatchResult> homeHistory, List<MatchResult> awayHistory)>>>(LoadHomeAwayHistoryAsync);
        _detailedHeadToHeadHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, List<HeadToHeadResult>>>>(LoadDetailedHeadToHeadHistoryAsync);
    }

    private async Task<IReadOnlyDictionary<string, List<MatchResult>>> LoadTeamHistoryAsync()
    {
        var matchesWithHistory = await GetMatchesWithHistoryAsync();
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
        var matchesWithHistory = await GetMatchesWithHistoryAsync();
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
        var matchesWithHistory = await GetMatchesWithHistoryAsync();
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

    private Task<List<MatchWithHistory>> GetMatchesWithHistoryAsync()
    {
        return _matchday.HasValue
            ? _kicktippClient.GetMatchesWithHistoryAsync(_community, _matchday.Value)
            : _kicktippClient.GetMatchesWithHistoryAsync(_community);
    }
    
    public async IAsyncEnumerable<DocumentContext> GetContextAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selection = MatchContextDocumentCatalog.ForCommunity(_communityContext, _competition);
        foreach (var documentName in selection.RequiredDocumentNames)
        {
            if (documentName == MatchContextDocumentCatalog.GetStandingsDocumentName(_competition))
            {
                yield return await CurrentStandings();
            }
            else if (documentName == $"community-rules-{_communityContext}.md")
            {
                yield return await CommunityScoringRules();
            }
        }
    }
    
    /// <summary>
    /// Gets context for the two teams in a match.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>An enumerable of context documents for both teams.</returns>
    public async IAsyncEnumerable<DocumentContext> GetMatchContextAsync(string homeTeam, string awayTeam, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selection = MatchContextDocumentCatalog.ForMatch(homeTeam, awayTeam, _communityContext, _competition);
        var homeAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(homeTeam);
        var awayAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(awayTeam);

        foreach (var documentName in selection.RequiredDocumentNames)
        {
            if (documentName == MatchContextDocumentCatalog.GetStandingsDocumentName(_competition))
            {
                yield return await CurrentStandings();
            }
            else if (documentName == $"community-rules-{_communityContext}.md")
            {
                yield return await CommunityScoringRules();
            }
            else if (documentName == $"recent-history-{homeAbbreviation}.csv")
            {
                yield return await RecentHistory(homeTeam);
            }
            else if (documentName == $"recent-history-{awayAbbreviation}.csv")
            {
                yield return await RecentHistory(awayTeam);
            }
            else if (documentName == MatchContextDocumentCatalog.GetFifaRankingDocumentName(homeTeam))
            {
                yield return await FifaRanking(homeTeam);
            }
            else if (documentName == MatchContextDocumentCatalog.GetFifaRankingDocumentName(awayTeam))
            {
                yield return await FifaRanking(awayTeam);
            }
            else if (documentName == $"home-history-{homeAbbreviation}.csv")
            {
                yield return await HomeHistory(homeTeam, awayTeam);
            }
            else if (documentName == $"away-history-{awayAbbreviation}.csv")
            {
                yield return await AwayHistory(homeTeam, awayTeam);
            }
            else if (documentName == $"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv")
            {
                yield return await HeadToHeadHistory(homeTeam, awayTeam);
            }
        }
    }
    
    /// <summary>
    /// Gets context for bonus questions.
    /// </summary>
    /// <returns>An enumerable of context documents relevant for bonus questions.</returns>
    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selection = MatchContextDocumentCatalog.ForCommunity(_communityContext, _competition);
        foreach (var documentName in selection.RequiredDocumentNames)
        {
            if (documentName == MatchContextDocumentCatalog.GetStandingsDocumentName(_competition))
            {
                yield return await CurrentStandings();
            }
            else if (documentName == $"community-rules-{_communityContext}.md")
            {
                yield return await CommunityScoringRules();
            }
        }
    }
    
    /// <summary>
    /// Gets the current competition standings as context.
    /// </summary>
    /// <returns>A document context containing the current standings.</returns>
    public async Task<DocumentContext> CurrentStandings()
    {
        var standings = await _kicktippClient.GetStandingsAsync(_community);

        return standings.ToCsvDocumentContext<TeamStanding, TeamStandingCsvMap>(
            MatchContextDocumentCatalog.GetStandingsDocumentBaseName(_competition));
    }

    /// <summary>
    /// Gets the current Bundesliga standings as context.
    /// </summary>
    /// <returns>A document context containing the current standings.</returns>
    public Task<DocumentContext> CurrentBundesligaStandings()
    {
        return CurrentStandings();
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
        
        var teamAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(teamName);
        
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
        
        var homeAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(homeTeam);
        
        return homeTeamHistory.ToCsvDocumentContext<MatchResult, MatchResultCsvMap>($"home-history-{homeAbbreviation}");
    }
    
    public async Task<DocumentContext> AwayHistory(string homeTeam, string awayTeam)
    {
        var homeAwayHistory = await _homeAwayHistoryLazy.Value;
        var cacheKey = $"{homeTeam}|{awayTeam}";
        
        var (_, awayTeamHistory) = homeAwayHistory.TryGetValue(cacheKey, out var history) 
            ? history 
            : (new List<MatchResult>(), new List<MatchResult>());
        
        var awayAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(awayTeam);
        
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
        
        var homeAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(homeTeam);
        var awayAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(awayTeam);
        
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

    private async Task<DocumentContext> FifaRanking(string teamName)
    {
        var documentName = MatchContextDocumentCatalog.GetFifaRankingDocumentName(teamName);
        var fileInfo = _worldCupContextDocumentsFileProvider.GetFileInfo(documentName);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                $"WM26 FIFA ranking file not found: {documentName}. Expected checked-in file for team '{teamName}'.");
        }

        var content = await ReadFileContentAsync(fileInfo);
        return new DocumentContext(documentName, content);
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
}
