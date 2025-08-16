# Repredictions and Context History

## Objective

We want to enable conditional reprediction of matchdays / bonus questions based on detected changes of context that was used as input to the generated predictions.

## Requirements

### `collect-context` Command

- `--kicktipp` flag support
- Firebase integration
- Workflow integration
- Store context documents with versioning
- When the latest version differs from the fetched state, store a newer version

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
