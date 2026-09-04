# Community-Specific Configuration

This project intentionally keeps most community knobs close to the code paths that consume them. When adding or tuning a community, check the sections below rather than hunting through command implementations.

## Competition Resolution

Location: `src/Orchestrator/Infrastructure/CompetitionResolver.cs`

This maps Kicktipp communities to competition IDs and manual-run defaults. Currently:

- Existing Bundesliga communities default to `bundesliga-2025-26`.
- `ehonda-dev-wm26` resolves to `fifa-world-cup-2026`.
- `rabetrabauken2026` resolves to `fifa-world-cup-2026` as the WM26 reference production community context.
- `ehonda-ai-arena` resolves to `fifa-world-cup-2026` as a WM26 community. Its scheduled `gpt-5-nano` / `minimal` workflows and manual-only comparison workflows are self-contained and use `community_context: ehonda-ai-arena`.
- The selected WM26 secondary copy-posting pattern is restricted to the `o3 high` production workflows. Only the matching `ehonda-ai-arena` `o3 high` workflows should point `community_context` at `rabetrabauken2026`.
- The guarded WM26 `matchday-dev` and `bonus-dev` commands use `gpt-5-nano` with `reasoning-effort minimal`, Langfuse prompt source, and label `latest`.

Those dev command defaults exist for low-cost development and manual testing.
They are not the WM26 production configuration. Production or scheduled
workflows must pass an explicit model and reasoning effort through the reusable
prediction workflow inputs. The selected WM26 production path does this with
`model: "o3"`, `reasoning_effort: "high"`, and
`max_output_tokens: 40000`; the manual-only `o3 medium` comparison workflows
keep `community_context: "ehonda-ai-arena"` aligned with their self-contained
context path.

Tune this when a new community needs a different competition, dev/test default,
prompt source, prompt name, or prompt label.

## Bundesliga 2026/27 Prediction Authority

[ADR-0065](../../plans/bundesliga-2026-27/decisions/0065-require-global-typed-prediction-authority-and-isolated-cutover.md) and the [P1-13 design](../../plans/bundesliga-2026-27/designs/p1-13-global-typed-prediction-authority-and-cutover.md) define the season-wide authority boundary. Use these terms consistently:

- **Posting Community:** the Kicktipp community whose form is read or written.
- **Prediction-source Community:** The community under which the candidate prediction was generated and stored. It equals the Posting Community for self-contained generation; for an accepted copy it may differ and is identified by the Copy Binding.
- **Community Context:** the community whose rules and prediction context govern generation.

Every current Matchday, RandomMatch, VerifyMatchday, Bonus, and VerifyBonus item must resolve through the Posting Community's immutable identity-seed generation to a Stable Local Item Key and matching Snapshot Hash. Arena participants share one Posting Community namespace; participant credentials or model identity do not create new item identity. A copy requires an immutable, versioned, one-to-one Copy Binding with exact posting/source identities and exact bonus option identities. Correspondence does not prove scoring or prediction compatibility.

A match scheduled instant comes only from exact ID-bearing fixture evidence and the same-ID structured detail `Termin`. Cancelled/empty evidence, inherited prior-row state, `Instant.MinValue` or another sentinel, missing/duplicate/unparsable detail, and fixture/detail conflict reject the whole selected operation before any current read or downstream call. A same-ID reschedule preserves the Stable Local Item Key but creates a new additive seed generation and Snapshot Hash; the old snapshot is not current.

The boundary covers typed current reads, saves, reprediction, copy, exact-ID POST, and exact readback. It rejects team-only, time-only, team-and-time-only, question-text, form-order, prefix, `latest`, partition, and default-based current authority. An unsupported, unknown, mixed-authority, or unbound selected item fails the entire batch before a current database read, prompt or service call, model call, mutation, or POST. Legacy Rows remain historical, audit, and cost evidence only after cutover.

Audit/cost access uses separate configured reads for each physical authority and returns explicitly authority-labelled non-current DTOs. A later shared combiner may combine, sort, or total only after independent retrieval and must retain row labels and per-authority subtotals. No repository method, query, enumeration, current lookup, fallback, copy, or reprediction may span authorities.

P1-10 R4b may add DFB/CL route IDs/contracts, fail-closed dispatch, and synthetic tests only. It may not add a prompt body or mirror, assert an unverified hash, or imply fallback. A checked-in mirror/test follows only after evidence records the exact hosted name, numbered immutable version, normalized readback hash, and required `production` membership; the test then proves normalized mirror/readback equality.

P1-13 owns this global foundation and its atomic deployed-runtime/storage cutover. P1-10 retains Schadensfresse-specific primary composition and activation. Credentials continue to follow the Posting Community, never the Prediction-source Community or Community Context.

## Development Shortcuts

Locations:

- `src/Orchestrator/Infrastructure/CompetitionResolver.cs`
- `src/Orchestrator/Commands/Operations/Dev/`

`matchday-dev` and `bonus-dev` are guarded shortcuts for development communities. They set `--override-database` and `--override-kicktipp` for end-to-end manual verification while leaving the normal `matchday` and `bonus` commands conservative by default. `collect-context-dev` is the matching guarded context seed path; it collects Kicktipp context and then uploads the WM26 FIFA ranking plus lineup context/KPI documents.

Only communities listed in `CompetitionResolver.SupportedDevCommunities` may use these shortcuts. Add a community there only when overwriting database and Kicktipp predictions is expected for that community.

## Match Context Documents

Location: `src/Core/MatchContextDocumentCatalog.cs`

This is the source of truth for required and optional context document names used by manual prediction commands, experiment reconstruction, and fallback context generation.

Bundesliga keeps the legacy policy:

- Required: standings, community rules, recent history for both teams, home/away history, head-to-head.
  - Bundesliga 2026/27 additionally requires two canonical roster documents and two Club Elo documents resolved through their immutable publication snapshots; transfer documents are not part of the live contract.

WM26 starts with a smaller national-team policy:

- Required: `fifa-world-cup-2026-standings.csv`, community rules, recent history for both teams, `fifa-ranking-{team-slug}.csv` for both teams, and `lineup-{team-slug}.csv` for both teams.
- Optional: none.
- Home/away history is omitted because national-team fixtures are not home/away in the same way.
- Head-to-head is omitted because national-team pairings are usually too sparse to be useful.
- WM26 ranking files are generated live by `collect-context fifa` and stored in Firestore; no static ranking CSVs are checked in.
- WM26 lineup files are generated by `collect-context lineups` from `data/wm26/lineups/lineups-seed.csv` plus the current Transfermarkt DuckDB snapshot.

Tune this when a community should add or remove required/optional context documents. The console warning that reports `found X/Y required context documents` is driven by this catalog.

## Bonus KPI Documents

Location: `src/FirebaseAdapter/FirebaseKpiContextProvider.cs`

Bonus predictions use KPI documents instead of `MatchContextDocumentCatalog`.

- Existing generic bonus behavior still auto-includes `team-data` when present.
- WM26 bonus predictions also auto-include the KPI document `fifa-rankings` when present.
- WM26 bonus predictions include `lineups` only for the exact top-scorer-team question.
- `fifa-rankings` is generated live by `collect-context fifa` with CSV header `Rank,Team,ELO,Published_At`.
- `lineups` is generated by `collect-context lineups` with CSV header `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`.
- Upload or update rankings with `collect-context fifa` and lineups with `collect-context lineups`, or use the combined `collect-context-dev` shortcut, before WM26 bonus predictions.

## Context Document Generation

Location: `src/ContextProviders.Kicktipp/KicktippContextProvider.cs`

The provider uses `MatchContextDocumentCatalog` to decide which documents to generate on demand. Keep this aligned by adding new document generation methods here only after adding the document names to the catalog.

For WM26, recent history and standings still come from the existing data sources, while `fifa-ranking-{team-slug}.csv` documents are Firestore context generated by `collect-context fifa` and `lineup-{team-slug}.csv` documents are generated by `collect-context lineups`. If prediction-time fallback cannot assemble the required WM26 ranking or lineup documents from Firestore, the command fails clearly and the fix is to run the corresponding collection step.

## Team Naming

Location: `src/Core/MatchContextDocumentCatalog.cs`

Bundesliga teams use fixed abbreviations such as `fcb` and `bvb`. Unknown teams, including national teams, use stable slug-style identifiers such as `mexiko`, `suedafrika`, and `cote-d-ivoire`.

Tune this if a community needs official short names instead of slug fallback names.

## Community Rules

Location: `community-rules/*.md`

Each community context should have a matching rules file. `ehonda-dev-wm26.md` and `rabetrabauken2026.md` currently mirror `pes-squad.md` because the WM26 communities use the same scoring rules.

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

The current recovery deployment keeps Bundesliga legacy document IDs for compatibility and reads only the legacy collections. P1-13 stages the initial `bundesliga-2026-27-typed-v1` authority epoch in physically and query-isolated collections:

- `match-predictions-bundesliga-2026-27-typed-v1`;
- `bonus-predictions-bundesliga-2026-27-typed-v1`; and
- `matches-bundesliga-2026-27-typed-v1`.

The typed draft reads only those staging collections. It may not mutate, delete, backfill, or treat a Legacy Row as current. Runtime/workflow routing and storage authority cut over atomically for every participating Bundesliga community after the existing Owner gates; merging the Git implementation is not activation. If a post-cutover POST has occurred, rollback disables the affected lane and reconciles exact Kicktipp and typed-storage state before reuse. Schadensfresse returns to quarantine if required while the seven unaffected recovery pairs retain their accepted fallback.

Non-Bundesliga competitions use competition-scoped IDs so WM26 data does not collide with Bundesliga data. P1-13 changes no WM26 storage contract.

Tune these only when changing Firestore compatibility or adding a new storage collection shape.

## Real Fixtures

Location: `tests/KicktippIntegration.Tests/Fixtures/Html/Real/<community>/*.html.enc`

Encrypted fixtures validate real Kicktipp page structure without committing raw HTML. Regenerate a community snapshot with:

```powershell
dotnet run --project src/Orchestrator -- snapshots all --community <community>
```

Commit only `*.html.enc` files under the real fixture directory. Do not commit raw `kicktipp-snapshots` HTML.
