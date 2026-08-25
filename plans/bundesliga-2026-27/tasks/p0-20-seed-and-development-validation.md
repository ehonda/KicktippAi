# P0-20 — Seed and validate in development and the arena

- Status: Complete
- Priority: P0
- Depends on: P0-02 through P0-18, [P0-22](p0-22-history-played-dates.md), the authorized local Luna/none development path, and the Luna/none arena entrypoints copied from P0-19; final production P0-19 copies may remain gated on P0-06
- Decisions: [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0012](../decisions/0012-competition-aware-matchday-completion.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0038](../decisions/0038-bound-bonus-context-by-question-policy.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0044](../decisions/0044-select-canonical-preseason-history-sources.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md), [ADR-0047](../decisions/0047-observe-one-temporary-arena-luna-scheduled-cycle.md)

## Outcome

The new Firestore partition and prompt paths complete representative matchday
and bonus cycles through the local-only safe development path, then pass the
authorized arena local CLI, `workflow_dispatch`, and temporary-schedule ladder
with trace evidence. No development Actions triad exists.

## Work items

- [x] Run the full profile in dry-run and resolve every manifest, source, and quality warning.
- [x] Collect Kicktipp rules, standings, histories, outcomes, Club Elo, rosters, and squad summaries into `bundesliga-2026-27` with the explicit profile-owned `--full-season` mode, then run the strict history played-date reconstruction/audit.
- [x] Prove every selected recent/home/away row has an exact source-attributed played date, including one current Bundesliga fixture and one intervening non-league fixture; record zero unresolved/ambiguous rows and prove head-to-head content was not rewritten.
- [x] Query/inspect stored identities and prove no old unscoped Bundesliga or WM26 document satisfied the run.
- [x] Verify all CSVs render header-first, deterministic, CRLF-terminated content with a final terminator.
- [x] Run a local CLI development prediction cycle in `ehonda-dev-buli-2627` covering a complete nine-match matchday and representative champion/relegation/top-scorer/coach bonus questions; do not treat the arena Actions triad as a development-community workflow.
- [x] Use `gpt-5.6-luna` with `none` reasoning for autonomous dev and arena plumbing validation; do not judge or promote its prediction quality.
- [x] Using the owner-confirmed arena setup, validate the same cheap configuration through local CLI, `workflow_dispatch`, and an arena-only schedule.
- [x] Verify the owner-confirmed Firebase, OpenAI, Langfuse, Kicktipp, and workflow credentials by connectivity and behavior without displaying secret values.
- [x] Inspect Langfuse traces for selected document names, prompt version, model identity, reasoning, token usage, and cost anomalies.
- [x] Test missing roster, partial Elo, and eight-of-nine outcomes to confirm launch gates fail safely.
- [x] Record commands, trace IDs, document versions, timestamps, and results in the task's validation evidence section.

## Validation evidence

### Fail-closed development collection checkpoint — 2026-08-25

The initial authorized command completed successfully:

```powershell
dotnet run --no-build --project src/Orchestrator -- collect-context-dev --community ehonda-dev-buli-2627 --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --verbose
```

It published the current nine-fixture scope: 36 selected history documents,
standings, rules, and nine H2H documents. The subsequent strict read-only
inventory failed closed with `expected=401`, `present=86`, `missing=315`,
`unexpected=0`, `identityConflicts=0`, `expectedCsv=400`, `validCsv=85`, and
`invalidCsv=0`. The missing set decomposed exactly into 297 H2H documents and
18 selected home/away history documents. The strict history audit also failed
because those 18 selected documents were absent. No model, prediction, arena,
workflow, or schedule operation followed. The nine present H2H content hashes
were identical before and after the read-only audits.

[ADR-0042](../decisions/0042-publish-complete-preseason-context-atomically.md) recorded the first code-only repair. After independent review, integration,
push, and exact-head green CI, the authoritative retry is:

```powershell
dotnet run --no-build --project src/Orchestrator -- collect-context-dev --community ehonda-dev-buli-2627 --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --full-season --verbose
```

The retry and every subsequent live/model phase remain pending; this evidence
does not claim the strict 401-document gate has passed.

### Full-season canonical-history checkpoint — 2026-08-25

After the first implementation of [ADR-0042](../decisions/0042-publish-complete-preseason-context-atomically.md) was independently reviewed, integrated,
and green at exact head, the authorized full-season command above was run once.
It fetched and validated all 34 matchday pages, with exactly nine fixtures per
page and 306 distinct ordered fixtures in total. Context collection then failed
closed at the first matchday-2 fixture because the matchday-1 away source and
matchday-2 home source returned different bytes for the same global
`recent-history-vfb.csv` identity. The provider behavior is legitimate and
fixture-role sensitive; the collision exposed an incomplete source-selection
contract rather than corrupt history content.

No outcome refresh, history transformation, repository save, model,
prediction, arena, workflow, or schedule operation followed. Candidate history
content was neither printed nor retained. [ADR-0044](../decisions/0044-select-canonical-preseason-history-sources.md) records the superseding
two-phase contract: canonical global recent histories from matchday 1,
canonical home/away histories from each team's earliest fixture in that role
(all accepted selectors must lie in matchdays 1-2), and every ordered H2H from
its exact matchday page. The live retry remains paused until this amendment and
implementation are independently reviewed, integrated, pushed, and exact-head
CI is green.

### Full-season publication and failed hosted-prompt rung — 2026-08-25

After ADR-0044 was independently reviewed, integrated, pushed, and green at
exact head, the authorized `--full-season` command succeeded. It validated all
34 matchdays with nine fixtures each, selected the canonical 54 history
documents, collected 306 exact-matchday H2H documents, validated the exact 362
Kicktipp context subset, resolved 430 completed history rows, excluded exactly
the two accepted incomplete fixtures, and atomically saved 315 changed
documents while retaining 47 byte-identical documents.

The strict post-publication inventory passed with `expected=401`,
`present=401`, `expectedCsv=400`, `validCsv=400`, and zero missing, invalid,
unexpected, identity-conflicting, WM26, or unscoped documents. The stored
history audit passed with 54 documents and 430 rows. The sorted 306-document H2H
name/content-hash aggregate was
`3f1e361f5a052dd9a2e165af5aa1eacb65430cecbf65f672ac6554eecb9e4f2b`
both before and after the read-only audits.

The subsequent `matchday-dev` command started at
`2026-08-25T03:15:49.4305089Z` with the authorized
`gpt-5.6-luna`/`none`/cap-`10000` identity and requested hosted match prompt
v2/`production`. Langfuse returned HTTP 400 because the runtime supplied both
selectors; the runtime used the checked-in mirror. Before the stop took effect,
all nine ordered matchday-one identities were stored in Firestore and posted to
the development Kicktipp community at reprediction index 0: Bayern–VfB,
Köln–Hoffenheim, Elversberg–Leverkusen, Mainz–Paderborn, Union–Frankfurt,
Leipzig–Mönchengladbach, Dortmund–Hamburg, Freiburg–Bremen, and
Augsburg–Schalke. Their configured stored identity names match prompt v2, but
the observed template source was the local fallback, so they must not be reused
as hosted evidence. No prediction payload, prompt text, or context bytes are
retained here. Aggregate usage was 36,766 uncached input tokens, zero cached
input tokens, zero reasoning tokens, 153 output tokens, and approximately USD
0.0038. No bonus, arena, workflow, or schedule rung followed.
These fallback-based writes are failed-rung evidence and must be replaced and
verified on the hosted retry; they do not satisfy P0-20.

[ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md)
records the repair: retrieve immutable prompts by version only, prove the exact
returned name/version/`production` binding, and require that hosted preflight
before prediction-service construction in the Bundesliga dev shortcuts. Live
retry remained paused at that checkpoint until the repair was independently
reviewed, integrated, pushed, and exact-head CI was green.

### Corrected hosted local-development rung — 2026-08-25

After ADR-0045 was independently reviewed, integrated, pushed, and green at
exact head `59212665c5f079a9cc2b15409d502ac7cd0db0a5` (CI run
`32808395594`, all 12 jobs successful), the authorized local-development rung
was rerun in strict order:

```powershell
dotnet run --no-build --project src/Orchestrator -- matchday-dev --community ehonda-dev-buli-2627 --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --verbose
dotnet run --no-build --project src/Orchestrator -- bonus-dev --community ehonda-dev-buli-2627 --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --verbose
```

`matchday-dev` ran from `2026-08-25T04:24:27.4970498Z` through
`2026-08-25T04:25:05.6604361Z` and exited zero. Its hosted preflight returned
HTTP 200 and resolved exact match prompt
`kicktippai/bundesliga-2026-27/predict-one-match` version 2 with required
`production` membership before model construction. The command updated the
prior failed-rung records at reprediction index 0 and posted all nine Kicktipp
bets in this exact order: FC Bayern München–VfB Stuttgart,
1. FC Köln–1899 Hoffenheim, SV Elversberg–Bayer 04 Leverkusen,
FSV Mainz 05–SC Paderborn 07, 1. FC Union Berlin–Eintracht Frankfurt,
RB Leipzig–Bor. Mönchengladbach, Borussia Dortmund–Hamburger SV,
SC Freiburg–Werder Bremen, and FC Augsburg–FC Schalke 04. No prediction
values are retained here.

`bonus-dev` ran from `2026-08-25T04:25:22.2425643Z` through
`2026-08-25T04:25:43.2620088Z` and exited zero. Its hosted preflight returned
HTTP 200 and resolved exact bonus prompt
`kicktippai/bundesliga-2026-27/predict-bonus` version 1 with required
`production` membership before model construction. Kicktipp exposed exactly
five questions, all of which were stored and posted in this order:

1. `Welche Mannschaften belegen die Plätze 16-18?` (`Relegation`);
2. `Welche Mannschaft stellt den Spieler mit den meisten Toren?`
   (`TopScorer`);
3. `Wer wird Deutscher Meister?` (`Champion`);
4. `Wer wird Herbstmeister?` (`Unknown`, the accepted ADR-0038 two-document
   baseline);
5. `Wo findet der erste Trainerwechsel statt?` (`Coach`).

The category context selections were respectively 2, 20, 2, 2, and 20
documents. Their estimated token counts were 567, 9,398, 567, 567, and 9,398,
all within the immutable 20-document/32,000-token budgets.

Read-only verification used the complete stored identities rather than model
name alone:

```powershell
dotnet run --no-build --project src/Orchestrator -- verify gpt-5.6-luna --community ehonda-dev-buli-2627 --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --reasoning-effort none --max-output-tokens 10000 --prompt-source langfuse --langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-one-match --langfuse-prompt-label production --langfuse-prompt-version 2 --verbose --agent
dotnet run --no-build --project src/Orchestrator -- verify-bonus gpt-5.6-luna --community ehonda-dev-buli-2627 --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --reasoning-effort none --max-output-tokens 10000 --prompt-source langfuse --langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-bonus --langfuse-prompt-label production --langfuse-prompt-version 1 --verbose --agent
```

Both exited zero. Match verification found 9 Kicktipp predictions, 9 Firestore
predictions, and 9 exact matches in the order above. Bonus verification found
the same 5 exposed questions in Kicktipp and Firestore, all valid. A read-only
cost inventory independently found only the two accepted
`gpt-5.6-luna`/`none`/cap-`10000` prompt configurations in this scope, with 9
match and 5 bonus documents at reprediction index 0. This successful hosted run
therefore replaces the earlier fallback-based match records; those failed-rung
records are not reused as evidence.

The installed Langfuse CLI was queried with `core`, `metrics`, `metadata`,
`model`, `usage`, `prompt`, and trace-context fields only; input, output,
prompt text, and context payloads were not requested or retained. Immutable
prompt readback independently confirmed match v2 and bonus v1 are active text
prompts carrying `production`. The redacted traces were:

- matchday trace `15117e2e3082bb0987d3684df54d8ac6`, timestamp
  `2026-08-25T04:24:29.640Z`, 9 ordered `predict-match` observations;
- bonus trace `58adee48590f299900e7745a3d61c5d7`, timestamp
  `2026-08-25T04:25:24.303Z`, 5 ordered `predict-bonus` observations.

Every observation recorded environment `development`, community/context
`ehonda-dev-buli-2627`, competition `bundesliga-2026-27`, model
`gpt-5.6-luna`, reasoning `none`, output cap `10000`, requested and actual
source `langfuse`, requested label `production`, and `fallback=false`. Match
observations resolved prompt v2 with content SHA-256
`94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`;
bonus observations resolved prompt v1 with SHA-256
`332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`.
All calls requested and received the `flex` tier with no tier fallback. No
output cap was hit.

Match aggregate usage was 36,739 uncached input, zero cached input, zero
reasoning, and 153 output tokens, costing USD `0.0037657`. Bonus aggregate
usage was 37,327 uncached input, zero cached input, zero reasoning, and 98
output tokens, costing USD `0.0037915`. No WM26, arena, workflow, or schedule
operation followed. The earlier strict 401/401 context, 54-history/430-row,
306-H2H, and stable H2H hash evidence remains unchanged.

### Arena local and manual-Action rungs — 2026-08-25

The owner-authorized local arena commands used the same explicit matchday and
bonus arguments shown in the reusable workflows: community and context
`ehonda-ai-arena`, competition `bundesliga-2026-27`, model `gpt-5.6-luna`,
reasoning `none`, cap `10000`, Langfuse `production`, match v2, bonus v1, and
the 20-document/32,000-token bonus budgets. They ran matchday before bonus,
overwrote only index 0, and completed with direct Kicktipp and Firestore
verification. Payload-safe Langfuse inspection found:

- match trace `447ce81bf2389ed724d9b9f24c07effc`, 9 ordered generations,
  36,712 uncached / 0 cached / 0 reasoning / 153 output tokens, USD
  `0.003763`;
- bonus trace `46675bd758cdb1dfbe65fb1b295402f3`, 5 ordered generations,
  37,237 uncached / 0 cached / 0 reasoning / 98 output tokens, USD
  `0.0037825`.

The manual Actions rung then ran sequentially. Context dispatch
`32817062469`, job `97707392914`, used exact SHA
`1d93935cb84ccc0dc31d040a618b822acb4587c6` and succeeded from
`06:27:44Z` through `06:29:31Z`. Forced match dispatch `32820234994`, job
`97716606282`, and forced bonus dispatch `32821107656`, job `97719218938`,
used exact green SHA `a25c0ca0c131dbb500d9abba1998d73c4540a790`; their verification,
generation, and final-verification steps succeeded in order. Their redacted
traces were match `b33e7f9babba40def409c016ec0c14ae` and bonus
`8792574fb39eaa0bdf642fe133bee0af`. Match usage was 36,712 / 0 / 0 / 153,
USD `0.003763`; bonus usage was 37,327 / 0 / 0 / 98, USD `0.0037915`.

All local/manual observations used the exact hosted prompt names, immutable
versions, labels, content hashes recorded above, and the unchanged Club Elo
snapshot `1f63ba33cb4f46bf37d21000743ca1e86b035a7ffe5792e64dddfea2336a653e`
and roster snapshot
`0773e9baa4f73ced6e0f86e6eb4b513ef82669e0a80ed5180749a08ebc52a7fa`.
They produced no prompt fallback, output-cap hit, unexpected reprediction, or
out-of-order operation. Final verification found all 9 match and 5 bonus
records in both Firestore and Kicktipp.

### One authorized scheduled arena cycle and teardown — 2026-08-25

[ADR-0047](../decisions/0047-observe-one-temporary-arena-luna-scheduled-cycle.md)
authorized exactly one temporary scheduled occurrence. Activation SHA
`50cfb05d7bee245fb2ab4d77c6f86185b9f9d348` was green in CI run
`32825264396` before observation. The workflow appeared exactly once as
schedule-event run `32829701617` on `main` at that exact SHA. It was created
at `09:01:32Z` and completed successfully at `09:16:30Z` with this strict,
non-overlapping order:

1. context job `97745392027`, `09:01:37Z`–`09:03:56Z`;
2. match job `97746058288`, `09:04:00Z`–`09:11:20Z`;
3. bonus job `97748133517`, `09:11:23Z`–`09:16:29Z`.

Both forced prediction jobs passed their initial verification, generation,
and final verification. Match verification found 9 Kicktipp predictions, 9
Firestore predictions, and 9 exact matches; bonus verification found 5
questions, 5 Firestore predictions, and 5 valid Kicktipp matches. A separate
read-only Firestore cost/index inventory found only the exact match-v2 and
bonus-v1 Luna/none/cap-10000 identities: 9 match and 5 bonus documents, all at
index 0, with no index 1 or 2+ records.

The installed Langfuse CLI requested only payload-safe field groups. Exactly
two `production` traces occurred in the scheduled window:

- match trace `a9c1a280c6dd30a6a811b6facc6b53a3`, timestamp
  `09:06:43.183Z`, 9 `predict-match` observations, 36,712 / 0 / 0 / 153
  tokens and USD `0.004187`;
- bonus trace `2755cb86bee29b3c8428359cab2b029e`, timestamp
  `09:13:21.855Z`, 5 `predict-bonus` observations, 37,327 / 0 / 0 / 98
  tokens and USD `0.0057812`.

Every observation recorded the exact arena/Bundesliga/model/reasoning/cap
identity, requested and actual prompt source `langfuse`, label `production`,
the immutable prompt version/hash, index 0, and the two snapshot hashes above.
Match order was the canonical nine-match order already recorded in this task.
Bonus category order was `Relegation`, `TopScorer`, `Champion`, `Unknown`,
`Coach`, selecting 2, 20, 2, 2, and 20 documents with estimated token counts
567, 9,398, 567, 567, and 9,398. No prompt fallback occurred.

OpenAI returned exact retryable HTTP 429 `rate_limit_exceeded` responses for
four Flex requests. The configured `flex-first-standard-fallback` strategy
made its single designed retry with Standard processing: match observation
`42a962c2c6775c81` (Mainz–Paderborn, USD `0.000848`) and bonus observations
`a709bb760ded0e78`, `7a1d6b1073079e9d`, and `aa65c3d2f96fc9d6`
(Relegation/TopScorer/Champion, combined USD `0.0039794`). The other 10 calls
used Flex. This is inspected service-tier fallback and explains the higher
scheduled cost; it is not Langfuse prompt fallback, model reprediction, or an
unhandled workflow retry. The P0-20 plumbing gate permits this accepted
runtime path and records it for later production cost calibration; it does not
select a production model.

The post-run strict inventory remained `expected=401`, `present=401`,
`expectedCsv=400`, `validCsv=400`, with zero missing, unexpected,
identity-conflicting, or invalid-CSV documents. The strict history audit
remained 54 documents / 430 rows. The sorted 306-H2H
`name=contentSha256` LF-final aggregate was
`8d4f0933abed10276c7d3386bdb2b0d963dd40698a08f088b8169f8f5423a85d`.
Club Elo and roster publications were unchanged at the snapshot hashes above.
The schedule is removed by this teardown change, the manual arena triad is
unchanged, and ADR-0047 never authorizes a second cycle.

### Fail-closed coverage

Exact activation-head CI run `32825264396` passed all 12 jobs. Its test suites
include the dedicated missing-roster publication contract, the 17-of-18 Club
Elo seed rejection, and the full-season eight-of-nine matchday rejection.
Each rejects the incomplete input before publication or any downstream
provider/outcome/context write. The live ladder also preserved the complete
401-document context set through its earlier failed full-season and hosted
prompt checkpoints before the reviewed repairs were integrated.

## Complete when

- Every implementation, context, fail-closed, local-development, and Luna arena
  validation gate owned by P0-20 has direct evidence.
- Fail-closed scenarios preserve the last complete context and prevent prediction.
- No final production-model schedule has been enabled; only the explicitly authorized Luna/none arena validation schedule may have run.
- P0-21 retains production secret presence, authentication/readiness, POST
  permission, exact deadlines, external `schadensfresse` setup, the Club Elo
  operating decision, named operator/monitor/rollback ownership, manual
  production writes, final schedule activation, and first scheduled observation;
  none is claimed by P0-20 completion.
