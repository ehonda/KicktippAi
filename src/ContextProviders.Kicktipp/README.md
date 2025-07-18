# ContextProviders.Kicktipp

This library provides context providers that extract and format information from Kicktipp.de for use in AI prediction systems.

## Overview

The `KicktippContextProvider` implements the `IContextProvider<DocumentContext>` interface and provides structured context data about:

- **Current Bundesliga Standings**: Latest league table information
- **Recent Team History**: Match results and performance data for specific teams  
- **Community Scoring Rules**: Kicktipp community configuration and scoring system

## Usage

```csharp
var contextProvider = new KicktippContextProvider();

// Get all available context
await foreach (var context in contextProvider.GetContextAsync())
{
    Console.WriteLine($"{context.Name}: {context.Content}");
}

// Get specific context types
var standings = await contextProvider.CurrentBundesligaStandings();
var teamHistory = await contextProvider.RecentHistory("FC Bayern München");
var scoringRules = await contextProvider.CommunityScoringRules();
```

## Dependencies

- **Core**: For `DocumentContext` and `IContextProvider<T>` interfaces
- **KicktippIntegration**: For accessing Kicktipp.de data via `IKicktippClient`

## Implementation Status

🚧 **Work in Progress**: The methods currently return empty implementations as placeholders. The actual data fetching logic will be implemented in the next development phase.
