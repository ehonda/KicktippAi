using Core;

namespace ContextProviders.Kicktipp;

public class KicktippContextProvider : IContextProvider<DocumentContext>
{
    public async IAsyncEnumerable<DocumentContext> GetContextAsync(CancellationToken cancellationToken = default)
    {
        // Provide current Bundesliga standings
        yield return await CurrentBundesligaStandings();
        
        // Provide community scoring rules
        yield return await CommunityScoringRules();
    }
    
    /// <summary>
    /// Gets the current Bundesliga standings as context.
    /// </summary>
    /// <returns>A document context containing the current standings.</returns>
    public async Task<DocumentContext> CurrentBundesligaStandings()
    {
        // TODO: Implement fetching actual standings from KicktippClient
        await Task.CompletedTask;
        
        // For testing purposes, provide sample standings data
        var sampleContent = @"Current Bundesliga Table (Season 2024/25):
1. FC Bayern München - 42 points (14-0-0)
2. RB Leipzig - 33 points (11-0-3)
3. VfB Stuttgart - 30 points (9-3-2)
4. Bayer 04 Leverkusen - 28 points (8-4-2)
5. Borussia Dortmund - 27 points (8-3-3)
6. 1. FC Union Berlin - 25 points (7-4-3)
7. SC Freiburg - 24 points (7-3-4)
8. Eintracht Frankfurt - 23 points (6-5-3)
...";
        
        return new DocumentContext(
            Name: "Current Bundesliga Standings",
            Content: sampleContent);
    }
    
    /// <summary>
    /// Gets recent match history for a specific team.
    /// </summary>
    /// <param name="teamName">The name of the team to get history for.</param>
    /// <returns>A document context containing the team's recent match history.</returns>
    public async Task<DocumentContext> RecentHistory(string teamName)
    {
        // TODO: Implement fetching actual match history from KicktippClient
        await Task.CompletedTask;
        
        return new DocumentContext(
            Name: $"Recent History - {teamName}",
            Content: "");
    }
    
    /// <summary>
    /// Gets the community scoring rules as context.
    /// </summary>
    /// <returns>A document context containing the scoring rules.</returns>
    public async Task<DocumentContext> CommunityScoringRules()
    {
        // TODO: Implement fetching actual scoring rules from KicktippClient
        await Task.CompletedTask;
        
        // For testing purposes, provide sample scoring rules
        var sampleContent = @"Kicktipp Community Scoring Rules:
- Correct result (exact score): 4 points
- Correct winner + goal difference: 3 points  
- Correct winner only: 2 points
- Wrong prediction: 0 points

Bonus points:
- Correct first goalscorer: +1 point
- Correct time of first goal: +1 point

Season bonuses:
- Correct champion prediction: +10 points
- Correct relegation prediction: +5 points per team";
        
        return new DocumentContext(
            Name: "Community Scoring Rules",
            Content: sampleContent);
    }
}
