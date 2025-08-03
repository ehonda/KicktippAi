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

    public KicktippContextProvider(IKicktippClient kicktippClient, string community)
    {
        _kicktippClient = kicktippClient ?? throw new ArgumentNullException(nameof(kicktippClient));
        _community = community ?? throw new ArgumentNullException(nameof(community));
        _teamHistoryLazy = new Lazy<Task<IReadOnlyDictionary<string, List<MatchResult>>>>(LoadTeamHistoryAsync);
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
        
        // Provide recent history for both teams
        yield return await RecentHistory(homeTeam);
        yield return await RecentHistory(awayTeam);
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
        
        // Write header
        csvWriter.WriteField("League");
        csvWriter.WriteField("Home_Team");
        csvWriter.WriteField("Away_Team");
        csvWriter.WriteField("Score");
        csvWriter.NextRecord();
        
        // Write data rows
        foreach (var result in matchResults)
        {
            csvWriter.WriteField(result.Competition);
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
