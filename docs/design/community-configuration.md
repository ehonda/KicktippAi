# Community-Specific Configuration

This project intentionally keeps most community knobs close to the code paths that consume them. When adding or tuning a community, check the sections below rather than hunting through command implementations.

## Competition Resolution

Location: `src/Orchestrator/Infrastructure/CompetitionResolver.cs`

This maps Kicktipp communities to competition IDs and manual-run defaults. Currently:

- Existing Bundesliga communities default to `bundesliga-2025-26`.
- `ehonda-dev-wm26` resolves to `fifa-world-cup-2026`.
- WM26 defaults to `gpt-5-nano`, `reasoning-effort minimal`, Langfuse prompt source, and label `latest`.

Tune this when a new community needs a different competition, model default, prompt source, prompt name, or prompt label.

## Development Shortcuts

Locations:

- `src/Orchestrator/Infrastructure/CompetitionResolver.cs`
- `src/Orchestrator/Commands/Operations/Dev/`

`matchday-dev` and `bonus-dev` are guarded shortcuts for development communities. They set `--override-database` and `--override-kicktipp` for end-to-end manual verification while leaving the normal `matchday` and `bonus` commands conservative by default.

Only communities listed in `CompetitionResolver.SupportedDevCommunities` may use these shortcuts. Add a community there only when overwriting database and Kicktipp predictions is expected for that community.

## Match Context Documents

Location: `src/Core/MatchContextDocumentCatalog.cs`

This is the source of truth for required and optional context document names used by manual prediction commands, experiment reconstruction, and fallback context generation.

Bundesliga keeps the legacy policy:

- Required: standings, community rules, recent history for both teams, home/away history, head-to-head.
- Optional: per-team transfer documents.

WM26 starts with a smaller national-team policy:

- Required: `fifa-world-cup-2026-standings.csv`, community rules, recent history for both teams.
- Optional: none.
- Home/away history is omitted because national-team fixtures are not home/away in the same way.
- Head-to-head is omitted because national-team pairings are usually too sparse to be useful.

Tune this when a community should add or remove required/optional context documents. The console warning that reports `found X/Y required context documents` is driven by this catalog.

## Context Document Generation

Location: `src/ContextProviders.Kicktipp/KicktippContextProvider.cs`

The provider uses `MatchContextDocumentCatalog` to decide which documents to generate on demand. Keep this aligned by adding new document generation methods here only after adding the document names to the catalog.

## Team Naming

Location: `src/Core/MatchContextDocumentCatalog.cs`

Bundesliga teams use fixed abbreviations such as `fcb` and `bvb`. Unknown teams, including national teams, use stable slug-style identifiers such as `mexiko`, `suedafrika`, and `cote-d-ivoire`.

Tune this if a community needs official short names instead of slug fallback names.

## Community Rules

Location: `community-rules/*.md`

Each community context should have a matching rules file. `ehonda-dev-wm26.md` currently mirrors `pes-squad.md` because the dev WM community uses the same scoring rules.

## Prompt Selection

Locations:

- `src/Orchestrator/Infrastructure/CompetitionResolver.cs`
- `prompts/wm26/*.md`
- Langfuse prompt names documented in `docs/onboarding-wm26/README.md`

WM26 match and bonus predictions use Langfuse-hosted text prompts by default, with checked-in fallback files for availability problems. Fallback should almost never fire; it exists to avoid failed manual runs during an inopportune Langfuse outage or first-fetch problem.

Tune hosted prompt names/labels in `CompetitionResolver`; tune fallback text in `prompts/wm26`.

## Storage Scoping

Locations:

- `src/FirebaseAdapter/FirebasePredictionRepository.cs`
- `src/FirebaseAdapter/FirebaseContextRepository.cs`
- `src/FirebaseAdapter/FirebaseKpiRepository.cs`
- `src/FirebaseAdapter/FirebaseMatchOutcomeRepository.cs`

Bundesliga keeps legacy document IDs for compatibility. Non-Bundesliga competitions use competition-scoped IDs so WM26 data does not collide with Bundesliga data.

Tune these only when changing Firestore compatibility or adding a new storage collection shape.

## Real Fixtures

Location: `tests/KicktippIntegration.Tests/Fixtures/Html/Real/<community>/*.html.enc`

Encrypted fixtures validate real Kicktipp page structure without committing raw HTML. Regenerate a community snapshot with:

```powershell
dotnet run --project src/Orchestrator -- snapshots all --community <community>
```

Commit only `*.html.enc` files under the real fixture directory. Do not commit raw `kicktipp-snapshots` HTML.
