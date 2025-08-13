using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Core;
using CsvHelper;
using KicktippIntegration;

namespace ContextProviders.Kicktipp;

public class KicktippContextProvider : IContextProvider<DocumentContext>
{
    private readonly IKicktippClient _kicktippClient;
    private readonly string _community;
    private readonly Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>> _teamHistoryLazy;
    private readonly Lazy<Task<IReadOnlyDictionary<string, (List<MatchResult> homeHistory, List<MatchResult> awayHistory)>>> _homeAwayHistoryLazy;
    private readonly Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>> _headToHeadHistoryLazy;
    private readonly Lazy<Task<IReadOnlyDictionary<string, List<HeadToHeadResult>>>> _detailedHeadToHeadHistoryLazy;

    public KicktippContextProvider(IKicktippClient kicktippClient, string community)
    {
        _kicktippClient = kicktippClient ?? throw new ArgumentNullException(nameof(kicktippClient));
        _community = community ?? throw new ArgumentNullException(nameof(community));
        _teamHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>>(LoadTeamHistoryAsync);
        _homeAwayHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, (List<MatchResult> homeHistory, List<MatchResult> awayHistory)>>>(LoadHomeAwayHistoryAsync);
        _headToHeadHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>>(LoadHeadToHeadHistoryAsync);
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
            
            try
            {
                var (homeTeamHistory, awayTeamHistory) = await _kicktippClient.GetHomeAwayHistoryAsync(_community, homeTeam, awayTeam);
                var cacheKey = $"{homeTeam}|{awayTeam}";
                homeAwayHistory[cacheKey] = (homeTeamHistory, awayTeamHistory);
            }
            catch (Exception)
            {
                // Continue with other matches if one fails
                var cacheKey = $"{homeTeam}|{awayTeam}";
                homeAwayHistory[cacheKey] = (new List<MatchResult>(), new List<MatchResult>());
            }
        }

        return homeAwayHistory;
    }
    
    private async Task<IReadOnlyDictionary<string, List<MatchResult>>> LoadHeadToHeadHistoryAsync()
    {
        var matchesWithHistory = await _kicktippClient.GetMatchesWithHistoryAsync(_community);
        var headToHeadHistory = new Dictionary<string, List<MatchResult>>();

        foreach (var matchWithHistory in matchesWithHistory)
        {
            var homeTeam = matchWithHistory.Match.HomeTeam;
            var awayTeam = matchWithHistory.Match.AwayTeam;
            
            try
            {
                var history = await _kicktippClient.GetHeadToHeadHistoryAsync(_community, homeTeam, awayTeam);
                var cacheKey = $"{homeTeam}|{awayTeam}";
                headToHeadHistory[cacheKey] = history;
            }
            catch (Exception)
            {
                // Continue with other matches if one fails
                var cacheKey = $"{homeTeam}|{awayTeam}";
                headToHeadHistory[cacheKey] = new List<MatchResult>();
            }
        }

        return headToHeadHistory;
    }

    private async Task<IReadOnlyDictionary<string, List<HeadToHeadResult>>> LoadDetailedHeadToHeadHistoryAsync()
    {
        var matchesWithHistory = await _kicktippClient.GetMatchesWithHistoryAsync(_community);
        var detailedHeadToHeadHistory = new Dictionary<string, List<HeadToHeadResult>>();

        foreach (var matchWithHistory in matchesWithHistory)
        {
            var homeTeam = matchWithHistory.Match.HomeTeam;
            var awayTeam = matchWithHistory.Match.AwayTeam;
            
            try
            {
                var history = await _kicktippClient.GetHeadToHeadDetailedHistoryAsync(_community, homeTeam, awayTeam);
                var cacheKey = $"{homeTeam}|{awayTeam}";
                detailedHeadToHeadHistory[cacheKey] = history;
            }
            catch (Exception)
            {
                // Continue with other matches if one fails
                var cacheKey = $"{homeTeam}|{awayTeam}";
                detailedHeadToHeadHistory[cacheKey] = new List<HeadToHeadResult>();
            }
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
        var csvContent = ConvertStandingsToCsv(standings);
        
        return new DocumentContext(
            Name: "bundesliga-standings.csv",
            Content: csvContent);
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
        
        var csvContent = ConvertMatchResultsToCsv(matchResults);
        
        // Use naming convention: recent-history-{team-abbreviation}.csv
        var teamAbbreviation = GetTeamAbbreviation(teamName);
        
        return new DocumentContext(
            Name: $"recent-history-{teamAbbreviation}.csv",
            Content: csvContent);
    }

    /// <summary>
    /// Gets home/away specific history for both teams in a match.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>A document context containing home team's home history and away team's away history.</returns>
    public async Task<DocumentContext> HomeHistory(string homeTeam, string awayTeam)
    {
        try
        {
            var homeAwayHistory = await _homeAwayHistoryLazy.Value;
            var cacheKey = $"{homeTeam}|{awayTeam}";
            
            var (homeTeamHistory, _) = homeAwayHistory.TryGetValue(cacheKey, out var history) 
                ? history 
                : (new List<MatchResult>(), new List<MatchResult>());
            
            var csvContent = new StringBuilder();
            csvContent.AppendLine("Competition,Home_Team,Away_Team,Score");
            
            foreach (var result in homeTeamHistory)
            {
                var score = $"{result.HomeGoals?.ToString() ?? ""}:{result.AwayGoals?.ToString() ?? ""}";
                csvContent.AppendLine($"\"{result.Competition}\",\"{result.HomeTeam}\",\"{result.AwayTeam}\",{score}");
            }
            
            var homeAbbreviation = GetTeamAbbreviation(homeTeam);
            return new DocumentContext(
                Name: $"home-history-{homeAbbreviation}.csv",
                Content: csvContent.ToString());
        }
        catch (Exception)
        {
            var homeAbbreviation = GetTeamAbbreviation(homeTeam);
            return new DocumentContext(
                Name: $"home-history-{homeAbbreviation}.csv",
                Content: "Competition,Home_Team,Away_Team,Score\n");
        }
    }
    
    public async Task<DocumentContext> AwayHistory(string homeTeam, string awayTeam)
    {
        try
        {
            var homeAwayHistory = await _homeAwayHistoryLazy.Value;
            var cacheKey = $"{homeTeam}|{awayTeam}";
            
            var (_, awayTeamHistory) = homeAwayHistory.TryGetValue(cacheKey, out var history) 
                ? history 
                : (new List<MatchResult>(), new List<MatchResult>());
            
            var csvContent = new StringBuilder();
            csvContent.AppendLine("Competition,Home_Team,Away_Team,Score");
            
            foreach (var result in awayTeamHistory)
            {
                var score = $"{result.HomeGoals?.ToString() ?? ""}:{result.AwayGoals?.ToString() ?? ""}";
                csvContent.AppendLine($"\"{result.Competition}\",\"{result.HomeTeam}\",\"{result.AwayTeam}\",{score}");
            }
            
            var awayAbbreviation = GetTeamAbbreviation(awayTeam);
            return new DocumentContext(
                Name: $"away-history-{awayAbbreviation}.csv",
                Content: csvContent.ToString());
        }
        catch (Exception)
        {
            var awayAbbreviation = GetTeamAbbreviation(awayTeam);
            return new DocumentContext(
                Name: $"away-history-{awayAbbreviation}.csv",
                Content: "Competition,Home_Team,Away_Team,Score\n");
        }
    }

    /// <summary>
    /// Gets head-to-head history between two teams.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <returns>A document context containing head-to-head match history.</returns>
    public async Task<DocumentContext> HeadToHeadHistory(string homeTeam, string awayTeam)
    {
        try
        {
            var detailedHeadToHeadHistory = await _detailedHeadToHeadHistoryLazy.Value;
            var cacheKey = $"{homeTeam}|{awayTeam}";
            
            var history = detailedHeadToHeadHistory.TryGetValue(cacheKey, out var cachedHistory) 
                ? cachedHistory 
                : new List<HeadToHeadResult>();
            
            var csvContent = ConvertHeadToHeadResultsToCsv(history);
            
            // Use naming convention: head-to-head-{team1-abbreviation}-vs-{team2-abbreviation}.csv
            var homeAbbreviation = GetTeamAbbreviation(homeTeam);
            var awayAbbreviation = GetTeamAbbreviation(awayTeam);
            
            return new DocumentContext(
                Name: $"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv",
                Content: csvContent);
        }
        catch (Exception)
        {
            // Return empty context on error
            return new DocumentContext(
                Name: $"head-to-head-{GetTeamAbbreviation(homeTeam)}-vs-{GetTeamAbbreviation(awayTeam)}.csv",
                Content: "League,Matchday,Played_At,Home_Team,Away_Team,Score\n");
        }
    }
    
    /// <summary>
    /// Gets the community scoring rules as context.
    /// </summary>
    /// <returns>A document context containing the scoring rules.</returns>
    public Task<DocumentContext> CommunityScoringRules()
    {
        // Hard-coded content from instructions.md lines 55-97
        var content = @"# Prediction Community Rules

## Scoring System

| Result Type | Tendency | Goal Difference | Exact Result |
|-------------|----------|-----------------|--------------|
| Win         | 2        | 3               | 4            |
| Draw        | 3        | -               | 4            |

* Tendency: Predicting the winner or a draw
* Goal Difference: Predicting the winner and the goal difference
* Exact Result: Predicting the exact score

## Examples

### Tendency

```text
Prediction: 2:1
Outcome:    3:1

Prediction: 1:1
Outcome:    2:2
```

### Goal Difference

```text
Prediction: 2:1
Outcome:    3:2
```

### Exact Result

```text
Prediction: 2:1
Outcome:    2:1

Prediction: 1:1
Outcome:    1:1
```";
        
        return Task.FromResult(new DocumentContext(
            Name: "community-rules-scoring-only.md",
            Content: content));
    }
    
    /// <summary>
    /// Converts team standings to CSV format.
    /// </summary>
    private string ConvertStandingsToCsv(List<TeamStanding> standings)
    {
        using var stringWriter = new StringWriter();
        using var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
        
        // Write header
        csvWriter.WriteField("Position");
        csvWriter.WriteField("Team");
        csvWriter.WriteField("Games");
        csvWriter.WriteField("Points");
        csvWriter.WriteField("Goal_Ratio");
        csvWriter.WriteField("Goals_For");
        csvWriter.WriteField("Goals_Against");
        csvWriter.WriteField("Wins");
        csvWriter.WriteField("Draws");
        csvWriter.WriteField("Losses");
        csvWriter.NextRecord();
        
        // Write data rows
        foreach (var standing in standings)
        {
            csvWriter.WriteField(standing.Position);
            csvWriter.WriteField(standing.TeamName);
            csvWriter.WriteField(standing.GamesPlayed);
            csvWriter.WriteField(standing.Points);
            csvWriter.WriteField($"{standing.GoalsFor}:{standing.GoalsAgainst}");
            csvWriter.WriteField(standing.GoalsFor);
            csvWriter.WriteField(standing.GoalsAgainst);
            csvWriter.WriteField(standing.Wins);
            csvWriter.WriteField(standing.Draws);
            csvWriter.WriteField(standing.Losses);
            csvWriter.NextRecord();
        }
        
        return stringWriter.ToString();
    }
    
    /// <summary>
    /// Converts match results to CSV format.
    /// </summary>
    private string ConvertMatchResultsToCsv(List<MatchResult> matchResults)
    {
        using var stringWriter = new StringWriter();
        using var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
        
        // Write header with enhanced format for head-to-head
        csvWriter.WriteField("League");
        csvWriter.WriteField("Matchday");
        csvWriter.WriteField("Played_At");
        csvWriter.WriteField("Home_Team");
        csvWriter.WriteField("Away_Team");
        csvWriter.WriteField("Score");
        csvWriter.NextRecord();
        
        // Write data rows
        foreach (var result in matchResults)
        {
            csvWriter.WriteField(result.Competition);
            // TODO: Extract matchday information from competition field if available
            csvWriter.WriteField(""); // Placeholder for matchday
            csvWriter.WriteField(""); // Placeholder for played_at date
            csvWriter.WriteField(result.HomeTeam);
            csvWriter.WriteField(result.AwayTeam);
            
            // Format score or leave empty for pending matches
            var scoreText = result.HomeGoals.HasValue && result.AwayGoals.HasValue 
                ? $"{result.HomeGoals}:{result.AwayGoals}" 
                : "";
            csvWriter.WriteField(scoreText);
            csvWriter.NextRecord();
        }
        
        return stringWriter.ToString();
    }

    private string ConvertHeadToHeadResultsToCsv(List<HeadToHeadResult> headToHeadResults)
    {
        using var stringWriter = new StringWriter();
        using var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
        
        // Write header with proper column separation
        csvWriter.WriteField("League");
        csvWriter.WriteField("Matchday");
        csvWriter.WriteField("Played_At");
        csvWriter.WriteField("Home_Team");
        csvWriter.WriteField("Away_Team");
        csvWriter.WriteField("Score");
        csvWriter.NextRecord();
        
        // Write data rows
        foreach (var result in headToHeadResults)
        {
            csvWriter.WriteField(result.League);
            csvWriter.WriteField(result.Matchday);
            csvWriter.WriteField(result.PlayedAt);
            csvWriter.WriteField(result.HomeTeam);
            csvWriter.WriteField(result.AwayTeam);
            csvWriter.WriteField(result.Score);
            csvWriter.NextRecord();
        }
        
        return stringWriter.ToString();
    }
    
    /// <summary>
    /// Gets a team abbreviation for file naming.
    /// </summary>
    private string GetTeamAbbreviation(string teamName)
    {
        // Common team abbreviations based on the instructions.md example
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "RB Leipzig", "rbl" },
            { "VfB Stuttgart", "vfb" },
            { "FC Bayern München", "fcb" },
            { "Bayer 04 Leverkusen", "b04" },
            { "Borussia Dortmund", "bvb" },
            { "1. FC Union Berlin", "fcu" },
            { "SC Freiburg", "scf" },
            { "Eintracht Frankfurt", "sge" },
            { "Werder Bremen", "svw" },
            { "VfL Wolfsburg", "wob" },
            { "FC Augsburg", "fca" },
            { "Bor. Mönchengladbach", "bmg" },
            { "FSV Mainz 05", "m05" },
            { "FC St. Pauli", "fcs" },
            { "1899 Hoffenheim", "tsg" },
            { "1. FC Heidenheim 1846", "fch" },
            { "Holstein Kiel", "ksx" },
            { "VfL Bochum", "vfl" }
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
