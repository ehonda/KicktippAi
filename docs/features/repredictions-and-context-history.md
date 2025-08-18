# Repredictions and Context History

## Objective

We want to enable conditional reprediction of matchdays / bonus questions based on detected changes of context that was used as input to the generated predictions.

## Implementation Status

- ✅ **Context Collection**: Fully implemented with versioned storage and automated workflows
- 🔄 **Database Context Integration**: Pending - adjust matchday/bonus commands to use stored context
- 🔄 **Reprediction Logic**: Pending - implement `--repredict` and `--max-repredict-count` parameters
- ✅ **Verification Enhancements**: Completed - compare prediction timestamps with context changes
- 🔄 **Workflow Updates**: Pending - integrate reprediction logic into automated workflows
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

### Adjustments to `matchday` / `bonus` Commands for Database Context

- **`bonus` command**
  - Already comes completely from the database
  - We can detect in `verify` that predictions (`createdAt`) are outdated compared to changes in kpi-documents (`updatedAt`)
  - _Here it's actually updatedAt, unlike matchday_
- **`matchday` command**
  - If current matchday context is available in DB, use it
  - Otherwise load on-demand (but don't save it to keep it clean)
- Both commands must support `--repredict` and `--max-repredict-count`
  - Add a `RepredictIndex` to Firebase documents (starts at 0)

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

### Workflow Adjustments

- Always make predictions with `--repredict` and `--max-repredict-count 3` (or similar)
- This essentially replaces `--override-database`. That becomes deprecated
  - For backwards compatibility, we can initially just always overwrite the latest `RepredictIndex`

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
