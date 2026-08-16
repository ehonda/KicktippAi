# Firebase Adapter

This project provides Firebase Firestore implementations of the prediction, context, KPI, and match-outcome repository interfaces.

## Overview

The Firebase adapter uses Google Cloud Firestore to persist match predictions and match information. It implements all methods from the `IPredictionRepository` interface and provides additional functionality for managing match schedules.

## Features

- **Persistent Storage**: Store predictions and matches in Google Cloud Firestore
- **Matchday Management**: Organize matches by matchday (1-34 for Bundesliga)
- **Competition Scoping**: Every repository requires an explicit, nonblank competition identifier
- **Audit Trail**: Tracks creation and update timestamps
- **Deterministic IDs**: Uses consistent document IDs for reliable querying

## Data Model

### Collections

#### `match-predictions`
Stores match predictions with the following structure:
- `homeTeam`: Home team name
- `awayTeam`: Away team name  
- `startsAt`: Match start time (UTC timestamp)
- `matchday`: Match day number (1-34)
- `homeGoals`: Predicted home team goals
- `awayGoals`: Predicted away team goals
- `createdAt`: When prediction was first created
- `updatedAt`: When prediction was last updated
- `competition`: Required competition identifier (for example, `bundesliga-2026-27`)
- `communityContext`: Required community partition

#### `matches`
Stores match information for matchday management:
- `homeTeam`: Home team name
- `awayTeam`: Away team name
- `startsAt`: Match start time (UTC timestamp)  
- `matchday`: Match day number (1-34)
- `competition`: Competition identifier

### Competition isolation

Context, KPI, and match-outcome document IDs include both the competition and community identity. Prediction documents retain GUID IDs and are isolated by required `competition` and `communityContext` fields on every query and write.

Missing competition identity is rejected when a repository is constructed. Legacy unscoped documents therefore cannot satisfy a current-season query. Historical data is not migrated or deleted by this adapter.

## Dependencies

- **Google.Cloud.Firestore**: Firebase Admin SDK for .NET
- **NodaTime**: Date/time handling with proper timezone support
- **Microsoft.Extensions.Logging**: Structured logging
- **Core**: Reference to the core domain models

## Firebase Setup Requirements

To use this adapter, you'll need:

1. **Google Cloud Project** with Firestore enabled
2. **Service Account Key** with Firestore permissions
3. **Environment Variable** `GOOGLE_APPLICATION_CREDENTIALS` pointing to the service account key file

## Usage

The repository will be registered with dependency injection. See [DI-SETUP.md](DI-SETUP.md) for detailed configuration instructions.

**Quick Setup:**
```csharp
// In Program.cs or Startup.cs
services.AddFirebaseDatabase(configuration, CompetitionIds.Bundesliga2026_27);
```

The main methods include:

- `SavePredictionAsync(..., bool overrideCreatedAt = false)`: Store or update a match prediction. When `overrideCreatedAt` is true (used by `--override-database`), the original `createdAt` timestamp is reset to now so that outdated checks compare against the new context documents used for the forced regeneration.
- `GetPredictionAsync()`: Retrieve a prediction for a specific match
- `GetMatchDayAsync()`: Get all matches for a matchday
- `GetMatchDayWithPredictionsAsync()`: Get matches with their predictions
- `GetAllPredictionsAsync()`: Retrieve all stored predictions
- `HasPredictionAsync()`: Check if a prediction exists
- `SaveBonusPredictionAsync(..., bool overrideCreatedAt = false)`: Same override semantics as match predictions.
- `StoreMatchAsync()`: Store match information (for schedule management)

## Error Handling

All operations include proper error handling and logging:
- Network failures are retried automatically by the Firestore SDK
- Validation errors are logged and re-thrown
- All operations support cancellation tokens

## Future Enhancements

- Batch operations for better performance
- Offline support with local caching
- Archive old seasons
- Analytics queries (win rate, accuracy, etc.)
