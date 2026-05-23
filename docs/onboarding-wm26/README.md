# FIFA World Cup 2026 Manual Onboarding

This first pass supports manual participation for the development community `ehonda-dev-wm26` and competition `fifa-world-cup-2026`.

No scheduled GitHub Actions workflow is enabled yet.

## Defaults

`ehonda-dev-wm26` resolves to `fifa-world-cup-2026`. Existing communities default to `bundesliga-2025-26` and keep their legacy Firestore document IDs.

For WM 2026 manual prediction commands:

- prompt source: `langfuse`
- prompt label: `latest`
- model: `gpt-5-nano` when the model argument is omitted
- reasoning effort: `minimal` unless explicitly overridden

## Prompts

Hosted Langfuse text prompts:

- `kicktippai/wm26/predict-one-match`
- `kicktippai/wm26/predict-bonus`

Both prompts should use `{{context_documents}}` for the context insertion point and carry the `latest` label.

Checked-in fallback copies:

- `prompts/wm26/match.md`
- `prompts/wm26/bonus.md`

The fallback path should almost never run. Langfuse prompt fetching already has service-side and SDK-side caching semantics; the local fallback exists to avoid a failed manual run during an inopportune Langfuse outage or first-fetch problem. When fallback is used, the command prints a console warning and trace metadata includes `langfusePromptFallback=true`.

Hosted WM match prompts with justification are intentionally out of scope for v1. A WM hosted run with `--with-justification` fails clearly until a hosted justification prompt exists.

## Manual Commands

Matchday:

```powershell
dotnet run --project src/Orchestrator -- matchday -c ehonda-dev-wm26 --dry-run --verbose
```

Bonus:

```powershell
dotnet run --project src/Orchestrator -- bonus -c ehonda-dev-wm26 --dry-run --verbose
```

Verification:

```powershell
dotnet run --project src/Orchestrator -- verify matchday -c ehonda-dev-wm26
dotnet run --project src/Orchestrator -- verify bonus -c ehonda-dev-wm26
```

Context and outcomes:

```powershell
dotnet run --project src/Orchestrator -- collect-context kicktipp --community-context ehonda-dev-wm26
```

Every command also accepts `--competition fifa-world-cup-2026` for explicit runs or local experiments.

## Snapshot Collection

Encrypted fixture collection is manual-first for now:

```powershell
dotnet run --project src/Orchestrator -- snapshots all --community ehonda-dev-wm26
```

Commit only encrypted files under:

```text
tests/KicktippIntegration.Tests/Fixtures/Html/Real/ehonda-dev-wm26/*.html.enc
```

Do not commit raw `kicktipp-snapshots` HTML. If credentials or `KICKTIPP_FIXTURE_KEY` are missing, fix that locally before collecting snapshots.

## Follow-Ups

- Enable scheduled workflows after the manual dev path has been exercised.
- Decide production community naming and rollout timing.
- Add hosted WM justification prompts if we want justification mode.
- Add monitoring dashboards/alerts specific to WM prompt fallback and WM competition metadata.
