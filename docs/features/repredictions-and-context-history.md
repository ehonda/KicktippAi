# Repredictions and Context History

## Objective

We want to enable conditional reprediction of matchdays / bonus questions based on detected changes of context that was used as input to the generated predictions.

## Implementation Status

- ✅ **Context Collection**: Fully implemented with versioned storage and automated workflows
- ✅ **Database Context Integration**: Completed - matchday commands now use stored context documents
- ✅ **Reprediction Logic**: Completed - implemented `--repredict` and `--max-repredictions` parameters
- ✅ **Verification Enhancements**: Completed - compare prediction timestamps with context changes
- ✅ **Workflow Updates**: Completed - integrated reprediction logic into automated workflows
- 🔄 **Cost Command Updates**: Pending - include reprediction costs in calculations

## Requirements

### `collect-context` Command ✅ **COMPLETED**

**Implementation Details:**

- ✅ **Command Structure**: Implemented as `collect-context kicktipp` subcommand using Spectre.Console composing commands pattern
- ✅ **Firebase Integration**: Full integration with versioned context document storage using `IContextRepository` and `FirebaseContextRepository`
- ✅ **Workflow Integration**: Individual GitHub workflows for `pes-squad` and `schadensfresse` communities, running every 12 hours
- ✅ **Versioned Storage**: Context documents stored with automatic versioning and duplicate detection
- ✅ **Change Detection**: Only stores new versions when content differs from latest version, preventing redundant storage
- ✅ **Community Context Support**: Uses `--community-context` parameter to organize context by scoring rules/community type
- ✅ **DateTimeOffset Usage**: Timezone-safe timestamps using `DateTimeOffset` instead of `DateTime`

**Key Implementation Notes:**

- Uses community-context for organization rather than specific Kicktipp communities (more flexible approach)
- Collects context from all current matchday matches for comprehensive coverage
- Implements proper CLI inheritance with global and subcommand-specific options
- Firebase composite index required: `collection: context-documents, fields: community-context (Ascending), name (Ascending), createdAt (Descending)`

### Adjustments to `matchday` / `bonus` Commands for Database Context ✅ **COMPLETED**

**Implementation Details:**

- ✅ **MatchdayCommand Database Integration**: Complete integration with Firebase context repository
  - Made `IContextRepository` a required service - Firebase configuration now mandatory
  - Replaced bulk context retrieval with targeted document fetching (7 specific documents per match)
  - Implemented hybrid approach: database-first with on-demand fallback and warnings
  - Added team abbreviation support for proper document name generation
  - Optimized performance: reduced from 47 to 7 context documents per match prediction

- ✅ **Context Document Targeting**: Retrieves exactly the same 7 documents as on-demand provider
  - `bundesliga-standings.csv` - current league standings
  - `community-rules-{communityContext}.md` - scoring rules for the community
  - `recent-history-{homeTeam}.csv` - recent match history for home team
  - `recent-history-{awayTeam}.csv` - recent match history for away team  
  - `home-history-{homeTeam}.csv` - home-specific match history for home team
  - `away-history-{awayTeam}.csv` - away-specific match history for away team
  - `head-to-head-{homeTeam}-vs-{awayTeam}.csv` - direct matchup history

- ✅ **Team Abbreviation Updates**: Updated hardcoded team abbreviations for 2025-26 Bundesliga season
  - Removed non-participating teams: `Holstein Kiel`, `VfL Bochum`
  - Added newly participating teams: `1. FC Köln` (fck), `Hamburger SV` (hsv)
  - Maintains fallback logic for unknown teams

**Key Implementation Notes:**

- Uses latest version of each context document type from the database
- Provides clear warnings when falling back to on-demand context generation
- Maintains backwards compatibility through graceful fallback mechanisms
- Enhanced verbose logging shows exactly which documents are retrieved and their versions

- **`bonus` command**
  - Already comes completely from the database
  - We can detect in `verify` that predictions (`createdAt`) are outdated compared to changes in kpi-documents (`updatedAt`)
  - _Here it's actually updatedAt, unlike matchday_

- ✅ **Reprediction Support**: Both commands now support `--repredict` and `--max-repredictions`
  - Added `RepredictionIndex` property to Firebase documents (starts at 0)
  - Repredictions create new documents preserving prediction history
  - Latest prediction retrieval orders by reprediction index descending

### Verification Enhancements ✅ **COMPLETED**

**Implementation Details:**

- ✅ **VerifyBonusCommand Enhancements**: Complete outdated prediction detection for bonus questions
  - Uses only `IKpiRepository` (required service) for KPI document validation
  - Removed backwards compatibility with old naming schemes - follows current clean naming only
  - Compares bonus prediction `createdAt` timestamps with KPI document `createdAt` timestamps
  - Added `--check-outdated` parameter to enable timestamp-based validation
  - Enhanced `KpiDocument` model with `DateTimeOffset CreatedAt` property for timezone-safe comparisons
  - Proper Firebase Firestore timestamp conversion using `ToDateTimeOffset()`

- ✅ **VerifyMatchdayCommand Enhancements**: Outdated prediction detection for matchday predictions  
  - Uses `IContextRepository` for context document validation
  - Compares matchday prediction `createdAt` timestamps with context document `createdAt` timestamps
  - Maintains backwards compatibility with display suffix stripping (e.g., " (kpi-context)")
  - Enhanced error handling for missing context documents

**Key Implementation Differences:**

- **Bonus Predictions**: Use KPI documents only (team/manager data) - no context repository dependency
- **Matchday Predictions**: Use context documents (tables, matchday data) - context repository required
- **Timezone Safety**: All timestamps use `DateTimeOffset` instead of `DateTime` for proper UTC handling
- **No Normalization for Bonus**: Removed backwards compatibility logic - follows current naming scheme only
- **Maintained Normalization for Matchday**: Keeps display suffix stripping for context document lookups

### Adjustments to `verify` ✅ **COMPLETED**

- ✅ **Outdated Detection**: Compares matchday prediction `createdAt` timestamps with context document `createdAt` timestamps
- ✅ **Context Repository Integration**: Uses `IContextRepository` for context document validation
- ✅ **Display Suffix Handling**: Maintains backwards compatibility by stripping display suffixes like " (kpi-context)"
- ✅ **Enhanced Error Handling**: Graceful handling of missing context documents with warning messages
- ✅ **Metadata Integration**: Leverages prediction metadata to identify which context documents were used
- ✅ **Verbose Logging**: Shows detailed context document checking when `--verbose` flag is used

**Implementation Notes:**

- Requires both prediction repository and context repository for outdated checking
- Checks all context documents that were used as input for the specific prediction
- Maintains existing prediction validation and Kicktipp synchronization logic

### Adjustments to `verify-bonus` ✅ **COMPLETED**

**Implementation Details:**

- ✅ **Outdated Detection**: Compares bonus prediction `createdAt` timestamps with KPI document `createdAt` timestamps
- ✅ **KPI Repository Integration**: Uses `IKpiRepository` as required service (no optional context repository)
- ✅ **Clean Naming Scheme**: Follows current document naming without backwards compatibility
- ✅ **Enhanced Error Handling**: Proper error messages for missing KPI documents
- ✅ **Timezone Safety**: Uses `DateTimeOffset` for all timestamp comparisons
- ✅ **Verbose Logging**: Shows detailed timestamp comparison information when `--verbose` flag is used

**Key Features:**

- Validates that bonus predictions are not outdated compared to KPI documents used as context
- Returns exit code 1 when predictions are found to be outdated
- Maintains existing validation for prediction correctness and Kicktipp synchronization
- Only checks documents that were actually used as context input for the specific prediction

### Reprediction Logic Implementation ✅ **COMPLETED**

**Implementation Details:**

- ✅ **Database Models Enhanced**: Added `RepredictionIndex` property to both `FirestoreMatchPrediction` and `FirestoreBonusPrediction` models
  - Default value of 0 for first predictions
  - Incremented for each subsequent reprediction (0 → 1 → 2, etc.)
  - Firestore property `repredictionIndex` with proper serialization

- ✅ **Repository Interface Extended**: Added new methods for reprediction management
  - `GetMatchRepredictionIndexAsync()` and `GetBonusRepredictionIndexAsync()` - get current reprediction index
  - `SaveRepredictionAsync()` and `SaveBonusRepredictionAsync()` - save new repredictions with specific index
  - Updated existing get methods to retrieve latest reprediction (highest index)

- ✅ **Command Line Interface**: New parameters for reprediction control
  - `--repredict` flag enables reprediction mode
  - `--max-repredictions N` limits maximum repredictions (0-based index)
  - Proper validation prevents conflicting flags (`--override-database` vs reprediction flags)
  - `IsRepredictMode` helper property for cleaner logic

- ✅ **Prediction Commands Updated**: Both `matchday` and `bonus` commands support full reprediction workflow
  - Check current reprediction index before generating new predictions
  - Respect max reprediction limits with clear status messages
  - Skip prediction when limit reached, display latest prediction
  - Save repredictions as new documents (preserves prediction history)
  - Enhanced logging shows reprediction indices and limits

**Key Implementation Features:**

- **History Preservation**: Each reprediction creates a new document, maintaining complete prediction history
- **Latest Retrieval**: Database queries order by `repredictionIndex DESC` to get most recent predictions
- **Limit Enforcement**: Commands check current index against `--max-repredictions` before generating new predictions
- **Clear Feedback**: Detailed console output shows reprediction status, indices, and limits
- **Validation**: Prevents conflicting usage of `--override-database` with reprediction flags
- **Backwards Compatibility**: Normal prediction workflow unchanged when reprediction flags not used

**Usage Examples:**

```bash
# Create first prediction (reprediction index 0)
dotnet run -- matchday o4-mini --community ehonda-test-buli

# Create reprediction (unlimited)
dotnet run -- matchday o4-mini --community ehonda-test-buli --repredict

# Create reprediction with limit (allows indices 0, 1, 2)
dotnet run -- matchday o4-mini --community ehonda-test-buli --max-repredictions 2

# Same functionality for bonus predictions
dotnet run -- bonus o4-mini --community ehonda-test-buli --max-repredictions 2
```

**Database Requirements:**

- Firestore composite index required for reprediction queries
- Index fields: `awayTeam`, `communityContext`, `competition`, `homeTeam`, `model`, `startsAt`, `repredictionIndex` (descending)
- Similar index needed for bonus predictions with `questionText` instead of team/match fields

### Workflow Adjustments ✅ **COMPLETED**

**Implementation Details:**

- ✅ **Enhanced Verify Commands**: Updated both `verify` and `verify-bonus` commands to use `--check-outdated` flag
  - Detects when predictions are outdated compared to context documents or KPI documents
  - Returns exit code 1 when predictions need to be regenerated due to context changes
  - Provides detailed timestamp comparison in verbose mode

- ✅ **Reprediction Integration**: Replaced `--override-database` with reprediction logic in workflows
  - Uses `--max-repredictions 2` by default (allows indices 0, 1, 2)
  - Preserves prediction history while creating new repredictions when context changes
  - Maintains backwards compatibility with `--force-prediction` for override scenarios

- ✅ **Configurable Max Repredictions**: Added `max_repredictions` input parameter to workflows
  - Default value of 2 (0-based index, allows 3 total predictions)
  - Can be overridden during manual workflow dispatch
  - Enables flexible reprediction limits based on operational needs

- ✅ **Enhanced Workflow Logic**: Improved prediction generation decision making
  - Uses reprediction flags when verify fails (normal automated operation)
  - Falls back to `--override-database` only when `force_prediction` is true
  - Clear logging shows which prediction strategy is being used

**Key Implementation Features:**

- **Automated Context-Driven Repredictions**: Workflows automatically detect context changes and create repredictions
- **History Preservation**: Each reprediction maintains complete prediction history in database
- **Flexible Limits**: Manual workflow dispatch can override default reprediction limits when needed
- **Backwards Compatibility**: Force prediction option preserves legacy override behavior
- **Enhanced Monitoring**: Workflow summaries include reprediction count and strategy used

**Workflow Command Examples:**

```bash
# Normal scheduled execution (uses reprediction logic)
dotnet run -- verify o3 --community pes-squad --community-context pes --check-outdated
dotnet run -- matchday o3 --community pes-squad --community-context pes --max-repredictions 2

# Manual override (forces regeneration)
dotnet run -- matchday o3 --community pes-squad --community-context pes --override-database
```

**Updated Workflow Features:**

- Both `base-matchday-predictions.yml` and `base-bonus-predictions.yml` support reprediction logic
- Enhanced workflow summaries show max repredictions setting and strategy used
- Clear distinction between automated repredictions and manual overrides

### Adjustments to `cost` Command

- Include reprediction costs in calculations
- Add repredictions (count and cost) as columns

## Testing Status

### Completed Verification Testing ✅

- **VerifyBonusCommand**: Successfully tested with `--check-outdated --verbose` flags
  - Correctly detects outdated predictions when KPI documents are updated after prediction creation
  - Proper UTC timestamp comparison showing prediction vs KPI document creation times
  - Returns appropriate exit codes (1 for outdated predictions, 0 for valid predictions)
  - Example: Detected prediction created `2025-08-17 22:26:54 UTC` vs KPI document created `2025-08-18 21:47:23 UTC`

- **VerifyMatchdayCommand**: Context document outdated checking implemented
  - Uses context repository for timestamp comparisons
  - Maintains backwards compatibility with display suffix handling
  - Graceful error handling for missing context documents

### Completed Database Context Integration Testing ✅

- **MatchdayCommand Database Context**: Successfully tested with updated context retrieval
  - Verified retrieval of exactly 7 context documents from database (down from 47)
  - Confirmed proper document targeting: standings, community rules, team histories, head-to-head
  - Validated team abbreviation updates for 2025-26 Bundesliga season participants
  - Tested fallback mechanism when database documents are incomplete
  - Example output: `Using all 7 context documents from database` with version information for each document

### Completed Reprediction Implementation Testing ✅

- **Command Line Interface**: Successfully tested new reprediction parameters
  - ✅ `--repredict` flag properly enables reprediction mode
  - ✅ `--max-repredictions N` correctly limits reprediction count
  - ✅ Validation prevents conflicting flags (`--override-database` vs reprediction flags)
  - ✅ Help output shows new parameters with proper descriptions

- **Validation Logic**: Thoroughly tested parameter validation
  - ✅ Error when using `--override-database` with `--repredict`
  - ✅ Error when using `--override-database` with `--max-repredictions`
  - ✅ Error when `--max-repredictions` is negative
  - ✅ Clear error messages guide proper usage

- **Reprediction Workflow**: Tested core reprediction logic
  - ✅ Commands detect existing reprediction indices
  - ✅ Proper increment of reprediction index (0 → 1 → 2)
  - ✅ Respect for max reprediction limits
  - ✅ Skip prediction when limit reached with clear messaging
  - ✅ Display of current vs maximum reprediction counts

- **Database Integration**: Verified new repository methods
  - ✅ `GetMatchRepredictionIndexAsync()` and `GetBonusRepredictionIndexAsync()` return correct indices
  - ✅ `SaveRepredictionAsync()` and `SaveBonusRepredictionAsync()` create new documents
  - ✅ Latest prediction retrieval orders by reprediction index correctly
  - ⚠️ **Firestore Index Required**: Composite index needed for reprediction queries

**Known Issues:**

- Firestore composite index required for production database (error provides creation URL)
- Index must include: `repredictionIndex` field in descending order for proper latest retrieval
