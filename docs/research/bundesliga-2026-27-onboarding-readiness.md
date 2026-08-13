# Bundesliga 2026/27 agent onboarding readiness

Status: research and implementation proposal  
Date: 2026-08-13  
Scope: repository changes and operating procedure needed to run KicktippAi agents for the 2026/27 Bundesliga season

## Executive summary

KicktippAi can be prepared for Bundesliga 2026/27 without redesigning the prediction pipeline, but it is not currently safe to point the old Bundesliga workflows at the new season. The repository still treats `bundesliga-2025-26` as the default Bundesliga competition, the local Bundesliga prompts name the 2025/26 season, the old community workflows are disabled, and one match-completion rule recognizes only the old competition ID.

The WM26 onboarding skill is a useful foundation. Its sequencing—resolve the competition, establish context, configure workflows, record the model, estimate cost, validate in a development community, inspect traces, then activate schedules—is equally applicable to Bundesliga. The WM26-specific parts should become a competition profile rather than be copied: FIFA rankings, national-team final squads, the World Cup played-date map, tournament stage handling, and the hosted WM26 prompts do not belong in a Bundesliga onboarding path.

For the two special WM26 context families, the recommended Bundesliga equivalents are:

| WM26 document | Bundesliga 2026/27 equivalent | Recommendation |
|---|---|---|
| FIFA ranking | Club Elo snapshot | Add `club-elo-{team}.csv` per team and an aggregate `club-elo-rankings` KPI document. Club Elo is the closest result-based, all-club analogue, including promoted clubs. |
| Final lineup | Current club roster | Generalize the existing seed/manifest plus DuckDB enrichment pipeline into `roster-{team}.csv` documents and an aggregate `team-rosters` KPI document. Do not call club squads “lineups.” |

The Transfermarkt DuckDB is valuable enrichment, but it cannot currently be the roster source of truth. The 2026-08-13 artifact has current valuations and transfers, while its Bundesliga club/player season membership still reflects 2025/26. In particular, it cannot discover complete squads for promoted Elversberg, Schalke, and Paderborn. Use an authoritative 18-club membership manifest/seed, then join DuckDB data for age, position, market value, and stable identifiers. This is the same sound separation already used by the WM26 lineup collector: authoritative membership first, enrichment second.

The minimum viable launch is therefore: add the new competition ID and prompt route, fix competition-wide matchday completion semantics, create an exact team manifest, add Club Elo and roster collectors, make context collection profile-driven, seed isolated Firestore context, create explicit 2026/27 community workflows, and validate one complete matchday and bonus run before enabling schedules.

## External season facts

The official Bundesliga schedule starts on Friday, 28 August 2026 with Bayern München–VfB Stuttgart and ends on 22 May 2027. The official fixtures contain 18 clubs and nine matches per matchday. Sources: [Bundesliga fixture list](https://products.bundesliga.com/fixtures) and [official 2026/27 schedule announcement](https://www.bundesliga.com/de/bundesliga/news/spielplan-saison-start-termine-daten-2026-27-22043).

The 18 participants are:

FC Augsburg, 1. FC Union Berlin, SV Werder Bremen, Borussia Dortmund, SV Elversberg, Eintracht Frankfurt, Sport-Club Freiburg, Hamburger SV, TSG 1899 Hoffenheim, 1. FC Köln, RB Leipzig, Bayer 04 Leverkusen, 1. FSV Mainz 05, Borussia Mönchengladbach, FC Bayern München, SC Paderborn 07, FC Schalke 04, and VfB Stuttgart.

This agrees with the official [2026/27 club overview](https://www.bundesliga.com/de/bundesliga/clubs?firsttab=kader). Elversberg, Schalke, and Paderborn are the promoted clubs; the league's [season changes overview](https://www.bundesliga.com/de/bundesliga/news/saison-2026-27-neuheiten-alles-anders-37592) confirms the promotion set.

These names should not be copied directly into code as the final identifiers. The roster/context manifest must first capture the exact names returned by the target Kicktipp community, then map each one to an internal slug, the official Bundesliga/DFB page, Club Elo name, and Transfermarkt club ID where available.

## What is reusable now

The ordinary Bundesliga match context already has the right conceptual shape in [`MatchContextDocumentCatalog`](../../src/Core/MatchContextDocumentCatalog.cs): standings, rules, recent team history, home/away history, and head-to-head history are required; transfer documents are optional. These are still useful and should remain. Unlike World Cup prediction, Bundesliga should retain the home/away and head-to-head context.

The storage layer is also largely ready for a new season. Repository document IDs are legacy/unscoped only for `bundesliga-2025-26`; an explicit `bundesliga-2026-27` argument will create competition-scoped documents. That is desirable because it prevents the new season from silently reusing 2025/26 matches, contexts, predictions, or KPIs.

The existing WM26 roster machinery in [`CollectContextLineupsCommand`](../../src/Orchestrator/Commands/Operations/CollectContext/CollectContextLineupsCommand.cs) and [`Wm26LineupSource`](../../src/Orchestrator/Commands/Operations/CollectContext/Wm26LineupSource.cs) already provides most of the difficult mechanics:

- authoritative membership seed plus team manifest;
- DuckDB enrichment by stable identifiers;
- per-team documents and aggregate KPI context;
- deterministic CSV generation and freshness tracking;
- preservation of last-known-good data and missing-data checks.

The context upload commands already accept a competition argument, so generalized collectors can store the new documents in the correct partition. The base GitHub workflow already has optional FIFA-ranking and lineup switches; it needs generalized collector/profile inputs, not a second orchestration system.

The existing whole-season analysis also provides an initial budget frame: a Bundesliga season has 306 matches, and the repository's previous evidence projected 493 prediction calls after re-predictions. That evidence is suitable for a pre-launch planning estimate, but the selected 2026/27 model, reasoning level, prompt, and context sizes still need a dedicated ledger entry and current price estimate. See [`whole-season-cost-estimates.md`](../experiments/whole-season-cost-estimates.md).

## Blocking and high-risk repository gaps

### Competition identity and storage

[`CompetitionIds`](../../src/Core/CompetitionIds.cs) contains `bundesliga-2025-26` and `fifa-world-cup-2026`, but no 2026/27 ID. [`CompetitionResolver`](../../src/Orchestrator/Infrastructure/CompetitionResolver.cs) consequently defaults every non-WM26 community to 2025/26. The new constant should be `bundesliga-2026-27`, and every new workflow should pass it explicitly even after the default is advanced.

The existing legacy mapping must remain unchanged: `bundesliga-2025-26` may still map to the historical unprefixed document scheme, whereas `bundesliga-2026-27` must remain explicit. This preserves reproducibility of old data while isolating the new season.

A wider search finds additional 2025/26 fallbacks in the Firebase repository constructors/factory, [`KicktippContextProvider`](../../src/ContextProviders.Kicktipp/KicktippContextProvider.cs), and [`KicktippSeasonMetadata`](../../src/Orchestrator/Infrastructure/KicktippSeasonMetadata.cs). Production composition should stop relying on these optional defaults and propagate the resolved competition explicitly. Do not mechanically replace every literal: the defaults in [`FirestoreModels`](../../src/FirebaseAdapter/Models/FirestoreModels.cs) may be intentional compatibility behavior for legacy documents that lack a competition field, and the observability examples/datasets are historical 2025/26 artifacts. Classify each occurrence as current default, legacy-read compatibility, or historical fixture and add tests around that distinction.

[`FirebaseMatchOutcomeRepository`](../../src/FirebaseAdapter/FirebaseMatchOutcomeRepository.cs) applies the “nine completed matches” rule only when the competition equals `Bundesliga2025_26`. A new Bundesliga competition would therefore consider a partially populated matchday complete as soon as all records currently present are complete. Replace the single-ID comparison with competition metadata or an `IsBundesliga`/expected-match-count rule that covers both seasons.

### Prompts and model configuration

The local Bundesliga prompts in [`prompts/o3`](../../prompts/o3) and [`prompts/gpt-5`](../../prompts/gpt-5) name the 2025/26 season. They must not be used unchanged. Prefer a versioned 2026/27 prompt or a competition-aware prompt route, rather than overwriting paths needed to reproduce old experiments. The production workflow must record an exact model and reasoning level; a floating or implicit default is insufficient for cost tracking and trace diagnosis.

WM26 gets special hosted-prompt behavior from the resolver. Bundesliga currently uses the local route. Either is workable, but the choice should be explicit and tested before onboarding communities.

### Workflows and supported communities

The old `pes-squad`, `schadensfresse`, and `ehonda-ai-arena-*` Bundesliga workflow files still exist, but their dispatches and schedules are disabled after 2025/26. They also omit a competition value, which currently resolves to 2025/26. Create or update a 2026/27 workflow triad for each participating community:

1. context collection;
2. matchday prediction;
3. bonus prediction.

Initially expose manual dispatch only. Add schedules after an end-to-end dry run succeeds. Make the competition argument explicit in all three workflows and ensure context collection completes before predictions can consume its documents.

[`CollectContextDevCommand`](../../src/Orchestrator/Commands/Operations/Dev/CollectContextDevCommand.cs) currently runs Kicktipp, FIFA rankings, and WM26 lineups unconditionally. It should select collectors from a competition profile, or a separate Bundesliga development command should be introduced. Simply reusing it would create irrelevant FIFA calls and misleading document names.

### Team aliases

The catalog abbreviation map reflects the 2025/26 field. Fallback slugging will produce plausible names for the promoted teams, but “plausible” is not sufficient for context lookup. Add all 18 exact Kicktipp names and source aliases to a checked-in manifest, with uniqueness tests. This manifest should be the join boundary among Kicktipp, official squad sources, Club Elo, and Transfermarkt.

### Bonus context routing

[`FirebaseKpiContextProvider`](../../src/FirebaseAdapter/FirebaseKpiContextProvider.cs) loads `team-data` and `fifa-rankings` broadly, and uses WM26 lineup wording for a top-scorer branch. Make KPI selection competition-aware. Bundesliga bonus prompts should be able to request `club-elo-rankings`, `team-squad-summary`, and, only when useful, `team-rosters`. Injecting all 18 full rosters into every bonus question would add substantial cost without consistently adding signal.

## Ranking replacement research

There is no literal FIFA ranking for clubs. The useful replacement must answer a slightly different question: “How strong is this club now, independent of the current table?”

| Candidate | Strengths | Limitations | Verdict |
|---|---|---|---|
| [Club Elo](https://clubelo.com/) | Result-based daily Elo, includes German second-tier history and currently covers all 18 Bundesliga clubs, including promoted clubs. Its [data description](https://clubelo.com/Data) explains the domestic and second-league coverage. | Unofficial; exact aliases, service availability, and reuse terms need validation. The convenient CSV interface is documented by third parties rather than a formal versioned API; see the [`soccerdata` Club Elo reference](https://soccerdata.readthedocs.io/en/stable/reference/clubelo.html). | Best primary analogue. Put it behind a provider, save source-dated snapshots, and retain last-known-good data. |
| [Opta Power Rankings](https://theanalyst.com/articles/power-rankings-your-club-ranked) | Broad global coverage and a richer Elo/xG-style model. | No clearly documented open, stable export suitable for this repository's scheduled collection and redistribution. | Research fallback, not the launch dependency. |
| [UEFA club coefficients](https://www.uefa.com/nationalassociations/uefarankings/club/about/) | Official and well-defined. | Measures five seasons of European results; non-participants receive an association floor. It is a poor discriminator for newly promoted and non-European Bundesliga clubs. | Do not use as the FIFA-ranking replacement. |
| Aggregate Transfermarkt squad value | Available from the same enrichment data and intuitively useful before matchday one. | Subjective, roster-dependent, and currently incomplete for promoted squads; it is not a performance rating. | Use as a complementary squad summary, not a ranking replacement. |

Recommended per-team CSV:

```csv
Global_Rank,Bundesliga_Rank,Team,ELO,Rated_At
```

Use one `club-elo-{slug}.csv` document per team and one `club-elo-rankings` aggregate KPI document. `Rated_At` must be the rating snapshot date, not merely collection time. Rows should be deterministic, CRLF-terminated, and preserved as last-known-good if the upstream source fails or returns fewer than 18 mapped clubs.

Before enabling unattended collection, verify Club Elo's terms and endpoint behavior. If it cannot be used operationally, the robust fallback is a locally computed cross-division Elo from an appropriately licensed Bundesliga and 2. Bundesliga results dataset—not UEFA coefficients or market value relabeled as a ranking.

## Roster context from DuckDB

### Audit result

The local WM26 cache was compared with the current `transfermarkt-datasets` DuckDB release on 2026-08-13. The upstream project publishes a weekly refreshed, CC0 database containing clubs, players, games, appearances, valuations, lineups, transfers, competitions, and related tables. Source: [`dcaribou/transfermarkt-datasets`](https://github.com/dcaribou/transfermarkt-datasets).

The audited artifact identified [upstream commit `154367d`](https://github.com/dcaribou/transfermarkt-datasets/commit/154367dfa6d6eb0b86332e332f9df0a080c7ddce). It contains valuation rows through 2026-06-01 and 2026/27 transfer records, but the `L1` club and player season membership still tops out at 2025. Its Bundesliga club set is consequently the 2025/26 field. Paderborn's club record ends at 2019/20, Schalke's at 2022/23, and Elversberg has no usable club row. A lookup by current club ID produced only fragmentary promoted-club membership.

Therefore these fields must not define the 2026/27 roster:

- `players.current_club_id` / `current_club_name` without an authoritative membership check;
- `clubs.last_season = 2025` as the participant list;
- transfers alone, because loans, exits, future dates, and missing club mappings do not reconstruct a complete active squad.

### Recommended roster design

Use a checked-in seed/manifest as the source of truth for membership. The official Bundesliga [all-player overview](https://www.bundesliga.com/de/bundesliga/spieler), official club squad pages such as [Bayern's squad](https://www.bundesliga.com/de/bundesliga/clubs/fc-bayern-muenchen/kader), and the [DFB Data Center 2026/27 squad pages](https://datencenter.dfb.de/competitions/bundesliga/seasons/2026-2027/teams/borussia-dortmund?datacenter_name=datencenter) are suitable verification sources. Because automated reuse terms may differ, keep source URL and collected-at provenance, validate permitted collection, and support a manually reviewed seed rather than making launch depend on scraping.

The seed should include, at minimum:

- exact Kicktipp team name and canonical slug;
- player name and authoritative club membership;
- role (`Player` or `Coach`);
- stable Transfermarkt player/club ID when known;
- source URL and membership-as-of date in seed metadata.

The generalized collector should then enrich each member from DuckDB with age/date of birth, position, and latest market value. Missing enrichment is not missing membership: output `N/A` for unavailable supplemental values, report coverage, and fail only when membership or a required team is absent. A strict quality gate should require all 18 teams, unique mappings, a plausible player count per club, and explicit handling of unmatched players.

Recommended per-team prompt document:

```csv
Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR
```

Name these documents `roster-{slug}.csv`, with an aggregate `team-rosters` KPI document. Keep the WM26 `lineup-*` contract intact for historical behavior. Add a compact derived `team-squad-summary` KPI with team, coach, squad size, valued-player count, total and median market value, average age, and collection date. The summary is more appropriate than all full rosters for broad bonus questions.

The existing transfer table can later generate the optional `{team}-transfers.csv` documents, provided direction, season, loan returns, future-effective dates, and unknown fees are represented correctly. It is useful P1 automation, not a roster substitute or launch blocker.

## Recommended Bundesliga context profile

For a match between two Bundesliga teams, require:

- current Bundesliga standings;
- community rules;
- recent overall history for each team;
- home history for the home team and away history for the away team;
- head-to-head history;
- one Club Elo snapshot per team;
- one current roster document per team.

Keep transfer documents optional initially. At the beginning of the season, when standings contain little or no signal, Elo and roster context are especially important.

For bonus questions, choose from aggregate documents by question type:

- champion, relegation, and placement questions: `club-elo-rankings`, standings when meaningful, `team-squad-summary`, and relevant team/manager data;
- top scorer: compact summary plus targeted rosters, not necessarily all rosters for every other question;
- coach questions: refreshed manager data with a collected-at date;
- transfer questions: aggregate transfer data only after its completeness checks pass.

The document catalog should express this policy instead of hiding it in prompt text or provider conditionals.

## Adapting the onboarding skill

Do not turn [`wm26-onboarding`](../../.agents/skills/wm26-onboarding/SKILL.md) into a mixed World Cup/Bundesliga checklist. Extract or create a generic `competition-onboarding` skill and let WM26 and Bundesliga supply profiles.

The generic workflow should retain these proven stages from the WM26 skill:

1. identify the target community, environment, competition ID, and exact team names;
2. collect and validate required match/KPI context;
3. seed competition-scoped Firestore data without fallback to another competition;
4. configure the context, matchday, and bonus workflow triad;
5. verify Kicktipp membership, credentials, and community rules;
6. record the exact model, reasoning level, prompt version, and season-cost coverage;
7. run development collection and prediction validation;
8. inspect Langfuse traces for document selection and token/cost anomalies;
9. activate production schedules only after manual success;
10. document the onboarding result and close out repository changes.

The profile should declare collectors, required context documents, KPI routing, expected matches per matchday, schedule/cutoff behavior, prompt route, and validation commands.

The Bundesliga 2026/27 profile should specify:

- `competition = bundesliga-2026-27`;
- 18 teams, 34 matchdays, nine matches per matchday, 306 matches total;
- Kicktipp, Club Elo, and roster collectors; transfer collection optional;
- no FIFA collector, no final-national-squad logic, no WM26 played-date map, and no knockout-stage behavior;
- Bundesliga home/away/head-to-head context enabled;
- the 2026/27 prompt/model ledger and whole-season cost estimate;
- a development community that is safe for overwrite/validation operations.

The WM26 skill can remain as a thin specialized entry point for final FIFA squads, FIFA rankings, tournament date mapping, and World Cup prompt validation. This avoids regressing the completed tournament workflow while making future domestic-season onboarding repeatable.

## Two-week implementation plan

### P0: required before the first production prediction

1. **Add competition metadata.** Introduce `Bundesliga2026_27`, update current/default resolution deliberately, pass the ID explicitly everywhere, classify remaining 2025/26 fallbacks as legacy or historical, and add expected match count and season dates as metadata rather than scattered comparisons.
2. **Fix completion semantics.** Make all Bundesliga seasons require nine completed matches before a matchday is complete.
3. **Create the team manifest.** Record all 18 exact Kicktipp names and mappings for slugs, official sources, Club Elo, and Transfermarkt. Test uniqueness and total coverage.
4. **Version the prompts and model configuration.** Add/select Bundesliga 2026/27 match and bonus prompts and record the exact production model plus reasoning level.
5. **Generalize roster collection.** Retain the seed-plus-enrichment architecture, create an authoritative 18-team roster seed, emit `roster-*`, `team-rosters`, and `team-squad-summary`, and enforce coverage reporting.
6. **Add Club Elo collection.** Map all 18 teams, capture a source-dated snapshot, emit per-team and aggregate documents, and preserve last-known-good data on partial upstream failure.
7. **Make context profiles explicit.** Update the catalog, KPI routing, development collection, and base context workflow so Bundesliga selects its own collectors and document policy.
8. **Create community workflows.** Add the explicit competition to the context/matchday/bonus triad for every chosen community. Keep schedules disabled for the first run.
9. **Seed and validate.** Collect rules and season data into the new Firestore partition; verify no 2025/26 or WM26 fallback; run one development matchday and bonus cycle; inspect traces and rendered CSV documents.
10. **Activate safely.** Run production workflows manually once, confirm Kicktipp writes and model/cost metadata, then enable schedules before the first prediction cutoff.

### P1: valuable after launch safety is established

- derive transfer context from DuckDB with transfer-window semantics and completeness checks;
- replace or refresh manual `team-data` and manager-data artifacts;
- add question-aware KPI selection to control roster token cost;
- make the generic onboarding skill and competition profiles first-class repository tooling;
- add scheduled roster refreshes with membership-diff review;
- generalize the observability dataset helpers that intentionally hard-code the 2025/26 competition/season before using them for 2026/27 experiment preparation;
- measure context-token and re-prediction rates, then update the whole-season estimate.

## Verification checklist

The implementation is ready only when all of the following are true:

- `bundesliga-2026-27` resolves explicitly and never reads/writes legacy 2025/26 document IDs;
- a partially ingested matchday cannot be marked complete before all nine matches are complete;
- the 18 Kicktipp teams map one-to-one to document slugs, Club Elo clubs, and roster sources;
- the roster quality report covers all 18 clubs and clearly reports every unmatched enrichment row;
- Club Elo collection rejects partial mappings and retains the previous complete snapshot;
- a match trace contains standings, rules, histories, both Elo rows, and both roster documents;
- bonus traces receive only the aggregate/targeted context appropriate to each question;
- every generated CSV starts with its header, has deterministic rows, CRLF line endings, and a final line terminator;
- the selected prompt says 2026/27 and the trace records the intended model and reasoning level;
- context collection finishes before scheduled prediction runs;
- the model ledger and season-cost document cover the deployed configuration;
- manual development and production runs succeed before schedules are enabled.

## Suggested code ownership map

| Area | Primary repository locations |
|---|---|
| Competition identity/defaults | [`CompetitionIds.cs`](../../src/Core/CompetitionIds.cs), [`CompetitionResolver.cs`](../../src/Orchestrator/Infrastructure/CompetitionResolver.cs), [`KicktippSeasonMetadata.cs`](../../src/Orchestrator/Infrastructure/KicktippSeasonMetadata.cs), Firebase factories/repository constructors, and [`KicktippContextProvider.cs`](../../src/ContextProviders.Kicktipp/KicktippContextProvider.cs) |
| Context policy and team aliases | [`MatchContextDocumentCatalog.cs`](../../src/Core/MatchContextDocumentCatalog.cs), new Bundesliga manifest under `data/` |
| Roster collector | Generalize [`CollectContextLineupsCommand.cs`](../../src/Orchestrator/Commands/Operations/CollectContext/CollectContextLineupsCommand.cs) and [`Wm26LineupSource.cs`](../../src/Orchestrator/Commands/Operations/CollectContext/Wm26LineupSource.cs), while preserving WM26 entry points |
| Club strength collector | New provider/command beside [`FifaRankingSource.cs`](../../src/Orchestrator/Commands/Operations/CollectContext/FifaRankingSource.cs) and [`CollectContextFifaCommand.cs`](../../src/Orchestrator/Commands/Operations/CollectContext/CollectContextFifaCommand.cs) |
| Development orchestration | [`CollectContextDevCommand.cs`](../../src/Orchestrator/Commands/Operations/Dev/CollectContextDevCommand.cs) |
| Firestore completion/KPI routing | [`FirebaseMatchOutcomeRepository.cs`](../../src/FirebaseAdapter/FirebaseMatchOutcomeRepository.cs), [`FirebaseKpiContextProvider.cs`](../../src/FirebaseAdapter/FirebaseKpiContextProvider.cs) |
| Prompts | [`prompts/o3`](../../prompts/o3), [`prompts/gpt-5`](../../prompts/gpt-5), or a new versioned Bundesliga 2026/27 prompt directory |
| Automation | [`base-context-collection.yml`](../../.github/workflows/base-context-collection.yml) and the community-specific workflow triads |
| Operating procedure | New generic competition onboarding skill plus the existing [`wm26-onboarding`](../../.agents/skills/wm26-onboarding/SKILL.md) profile |

## Decisions still needed

These choices do not block repository preparation, but they must be recorded before production activation:

1. Which existing communities will participate in 2026/27, and whether a new Bundesliga development community will be used.
2. Which model/reasoning configuration and prompt storage route will be production defaults.
3. Whether Club Elo's operational/reuse terms are acceptable; if not, which licensed match-results feed will support locally computed Elo.
4. Whether official squad membership is maintained as a reviewed seed or collected automatically under acceptable source terms.
5. How frequently rosters and ratings refresh during the transfer window, and whether membership changes require review before publication.

None of these should be resolved by silently inheriting the 2025/26 or WM26 defaults.
