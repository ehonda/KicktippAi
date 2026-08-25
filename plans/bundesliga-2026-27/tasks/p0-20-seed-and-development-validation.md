# P0-20 — Seed and validate in development and the arena

- Status: Not started
- Priority: P0
- Depends on: P0-02 through P0-18, [P0-22](p0-22-history-played-dates.md), the authorized local Luna/none development path, and the Luna/none arena entrypoints copied from P0-19; final production P0-19 copies may remain gated on P0-06
- Decisions: [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0012](../decisions/0012-competition-aware-matchday-completion.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0038](../decisions/0038-bound-bonus-context-by-question-policy.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0044](../decisions/0044-select-canonical-preseason-history-sources.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md)

## Outcome

The new Firestore partition and prompt paths complete representative matchday
and bonus cycles through the local-only safe development path, then pass the
authorized arena local CLI, `workflow_dispatch`, and temporary-schedule ladder
with trace evidence. No development Actions triad exists.

## Work items

- [ ] Run the full profile in dry-run and resolve every manifest, source, and quality warning.
- [ ] Collect Kicktipp rules, standings, histories, outcomes, Club Elo, rosters, and squad summaries into `bundesliga-2026-27` with the explicit profile-owned `--full-season` mode, then run the strict history played-date reconstruction/audit.
- [ ] Prove every selected recent/home/away row has an exact source-attributed played date, including one current Bundesliga fixture and one intervening non-league fixture; record zero unresolved/ambiguous rows and prove head-to-head content was not rewritten.
- [ ] Query/inspect stored identities and prove no old unscoped Bundesliga or WM26 document satisfied the run.
- [ ] Verify all CSVs render header-first, deterministic, CRLF-terminated content with a final terminator.
- [ ] Run a local CLI development prediction cycle in `ehonda-dev-buli-2627` covering a complete nine-match matchday and representative champion/relegation/top-scorer/coach bonus questions; do not treat the arena Actions triad as a development-community workflow.
- [ ] Use `gpt-5.6-luna` with `none` reasoning for autonomous dev and arena plumbing validation; do not judge or promote its prediction quality.
- [ ] Using the owner-confirmed arena setup, validate the same cheap configuration through local CLI, `workflow_dispatch`, and an arena-only schedule.
- [ ] Verify the owner-confirmed Firebase, OpenAI, Langfuse, Kicktipp, and workflow credentials by connectivity and behavior without displaying secret values.
- [ ] Inspect Langfuse traces for selected document names, prompt version, model identity, reasoning, token usage, and cost anomalies.
- [ ] Test missing roster, partial Elo, and eight-of-nine outcomes to confirm launch gates fail safely.
- [ ] Record commands, trace IDs, document versions, timestamps, and results in the task's validation evidence section.

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
retry remains paused until the repair is independently reviewed, integrated,
pushed, and exact-head CI is green.

## Complete when

- Every P0 launch gate except final production-model execution/scheduling has direct evidence.
- Fail-closed scenarios preserve the last complete context and prevent prediction.
- No final production-model schedule has been enabled; only the explicitly authorized Luna/none arena validation schedule may have run.
