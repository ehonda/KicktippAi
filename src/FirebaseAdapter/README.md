# Firebase Adapter

This project provides a Firebase Firestore implementation of the `IPredictionRepository` interface.

## Overview

The Firebase adapter uses Google Cloud Firestore to persist match predictions and match information. It implements all methods from the `IPredictionRepository` interface and provides additional functionality for managing match schedules.

## Features

- **Persistent Storage**: Store predictions and matches in Google Cloud Firestore
- **Matchday Management**: Organize matches by matchday (1-34 for Bundesliga)
- **Competition Scoping**: All data is scoped to "bundesliga-2025-26" competition
- **Audit Trail**: Tracks creation and update timestamps
- **Deterministic IDs**: Uses consistent document IDs for reliable querying

## Data Model

### Collections

#### `predictions`
Stores match predictions with the following structure:
- `homeTeam`: Home team name
- `awayTeam`: Away team name  
- `startsAt`: Match start time (UTC timestamp)
- `matchday`: Match day number (1-34)
- `homeGoals`: Predicted home team goals
- `awayGoals`: Predicted away team goals
- `createdAt`: When prediction was first created
- `updatedAt`: When prediction was last updated
- `competition`: Competition identifier ("bundesliga-2025-26")

#### `matches`
Stores match information for matchday management:
- `homeTeam`: Home team name
- `awayTeam`: Away team name
- `startsAt`: Match start time (UTC timestamp)  
- `matchday`: Match day number (1-34)
- `competition`: Competition identifier

### Document IDs

Document IDs are generated deterministically using the format:
```
{homeTeam}_{awayTeam}_{startsAtTicks}_{matchday}
```

This ensures:
- Uniqueness across all matches
- Consistent IDs for the same match
- Easy querying and updates

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
services.AddFirebaseDatabase(configuration);
```

The main methods include:

- `SavePredictionAsync()`: Store or update a match prediction
- `GetPredictionAsync()`: Retrieve a prediction for a specific match
- `GetMatchDayAsync()`: Get all matches for a matchday
- `GetMatchDayWithPredictionsAsync()`: Get matches with their predictions
- `GetAllPredictionsAsync()`: Retrieve all stored predictions
- `HasPredictionAsync()`: Check if a prediction exists
- `StoreMatchAsync()`: Store match information (for schedule management)

## Error Handling

All operations include proper error handling and logging:
- Network failures are retried automatically by the Firestore SDK
- Validation errors are logged and re-thrown
- All operations support cancellation tokens

## Future Enhancements

- Batch operations for better performance
- Offline support with local caching
- Competition configuration (currently hardcoded to Bundesliga 2025/26)
- Archive old seasons
- Analytics queries (win rate, accuracy, etc.)
