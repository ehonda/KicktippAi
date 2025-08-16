# Repredictions and Context History

## Objective

We want to enable conditional reprediction of matchdays / bonus questions based on detected changes of context that was used as input to the generated predictions.

## Implementation Status

- ✅ **Context Collection**: Fully implemented with versioned storage and automated workflows
- 🔄 **Database Context Integration**: Pending - adjust matchday/bonus commands to use stored context
- 🔄 **Reprediction Logic**: Pending - implement `--repredict` and `--max-repredict-count` parameters
- 🔄 **Verification Enhancements**: Pending - compare prediction timestamps with context changes
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

### Adjustments to `verify`

- Compare the _latest_ predictions (via highest Repredict-Index) not only with Kicktipp state, but also their `createdAt` with the `createdAt` of the latest version of the associated Context-Documents
  - Need to know which Context-Documents are being used
  - Best to store the names on the document

### Adjustments to `verify-bonus`

- Analogous approach. Here we compare `createdAt` with `updatedAt` of KPI-Documents

### Workflow Adjustments

- Always make predictions with `--repredict` and `--max-repredict-count 3` (or similar)
- This essentially replaces `--override-database`. That becomes deprecated
  - For backwards compatibility, we can initially just always overwrite the latest `RepredictIndex`

### Adjustments to `cost` Command

- Include reprediction costs in calculations
- Add repredictions (count and cost) as columns
