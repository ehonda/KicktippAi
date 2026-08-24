# Bundesliga 2026/27 Community Onboarding

Updated: 2026-08-21

This is the authoritative community, context, model-slot, credential-name, and Langfuse-environment matrix accepted by [ADR-0039](../../plans/bundesliga-2026-27/decisions/0039-record-bundesliga-community-and-credential-topology.md). Every row uses competition `bundesliga-2026-27`. A row marked gated or nondeployable cannot be turned into a workflow until its owner-controlled fields are recorded in the [model configuration ledger](model-config-onboarding.md).

## Community matrix

| Row ID | Posting target | Community context | Prediction behavior | Model and prompt slot | Local Kicktipp source | Actions Kicktipp names | Langfuse environment | Owner and state |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `dev-luna` | `ehonda-dev-buli-2627` | `ehonda-dev-buli-2627` | Self-contained; overwrite-capable plumbing validation | `validation-luna-none`: exact Luna/none identity below | Base `.env`; no sibling is required | No Actions participant pair is assigned by P0-17; the safe path is local CLI | `development` | Project owner; configured and authorized for validation |
| `arena-luna-self-contained` | `ehonda-ai-arena` | `ehonda-ai-arena` | Self-contained plumbing validation | `validation-luna-none`: exact Luna/none identity below | `.env.ehonda-ai-arena`; reserved for the Luna participant | `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` | `production` | Project owner; configured and authorized only for the P0-20 validation ladder |
| `pes-production-reference` | `pes-squad` | `pes-squad` | Independent reference production generation | `production-primary`: **OWNER GATE** | `.env.pes-squad` naming/presence confirmed; P0-21 controls use | `PES_SQUAD_KICKTIPP_USERNAME` / `PES_SQUAD_KICKTIPP_PASSWORD` | `production` | Community administrator owns membership/setup; project owner owns model approval; nondeployable until both gates pass |
| `schadensfresse-production-independent` | `schadensfresse` | `schadensfresse` | Independent production generation | `production-primary`: **OWNER GATE** | `.env.schadensfresse` naming/presence confirmed; P0-21 controls use | `SCHADENSFRESSE_KICKTIPP_USERNAME` / `SCHADENSFRESSE_KICKTIPP_PASSWORD` | `production` | Community administrator owns membership/setup; project owner owns model approval; nondeployable until both gates pass |
| `arena-production-copy` | `ehonda-ai-arena` | `pes-squad` | Copy-post the stored `pes-squad` production prediction; fixture compatibility required; bonus requires exact normalized question and options | The exact same `production-primary` identity as `pes-production-reference`: **OWNER GATE** | No accepted local production-participant profile; never reuse `.env.ehonda-ai-arena` merely because it targets the arena | Exact model-specific username/password names are **OWNER GATE** and do not exist as placeholders | `production` | Project owner; nondeployable until participant/configuration approval and compatibility evidence |
| `arena-challenger-slot` | `ehonda-ai-arena` | `ehonda-ai-arena` | Self-contained independent generation; template for zero or more admitted challengers | `arena-challenger-<n>`: every field is **OWNER GATE** | No accepted local challenger profile | Exact model-specific username/password names are **OWNER GATE** and do not exist as placeholders | `production` | Project owner; template is nondeployable and admits no challenger by itself |

An arena validation trace uses Langfuse environment `production` because the posting target is a production community. That telemetry classification does not promote `validation-luna-none` to the production model or to a challenger slot.

## Exact validation identity

Both validation rows use all of the following values explicitly:

| Field | Value |
| --- | --- |
| Competition | `bundesliga-2026-27` |
| Model | `gpt-5.6-luna` |
| Reasoning effort | `none` |
| Maximum output tokens | `10000` |
| Match prompt | `kicktippai/bundesliga-2026-27/predict-one-match`, version `2` |
| Match prompt normalized SHA-256 | `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1` |
| Bonus prompt | `kicktippai/bundesliga-2026-27/predict-bonus`, version `1` |
| Bonus prompt normalized SHA-256 | `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9` |

This identity is authorized for plumbing validation only. It is never copied into `production-primary` or `arena-challenger-<n>` without an explicit owner decision.

## Gated configuration slots

`production-primary` must eventually record one exact model, reasoning effort, maximum output cap, numbered match and bonus prompt versions, service-tier/fallback behavior, whole-season cost ceiling, and estimator evidence. `pes-production-reference`, `schadensfresse-production-independent`, and `arena-production-copy` use that same exact stored identity; otherwise copy-posting cannot safely find the reference prediction.

Each admitted `arena-challenger-<n>` must independently record the same fields plus its exact participant credential names. The template row is not an admitted configuration, and there are currently zero approved challenger rows.

## Credential resolution

Local ordinary prediction and verification commands choose credentials from the posting target:

```text
posting target: ehonda-ai-arena
community context: pes-squad
credential suffix: ehonda-ai-arena
```

They never select credentials from `community_context`. The base `.env` is loaded at startup. When `.env.<posting-community>` exists, it overrides only `KICKTIPP_USERNAME` and `KICKTIPP_PASSWORD`; otherwise the existing environment remains unchanged. No credential value belongs in output, logs, tracked evidence, or this matrix.

The names-only local audit on 2026-08-21 found `.env`, `.env.ehonda-ai-arena`, `.env.pes-squad`, `.env.schadensfresse`, and `firebase.json`. No file contents were inspected. The base `.env` remains the development credential source, while `.env.ehonda-ai-arena` identifies the confirmed Luna participant only.

Shared Actions configuration uses `FIREBASE_PROJECT_ID`, `FIREBASE_SERVICE_ACCOUNT_JSON`, `OPENAI_API_KEY`, and `LANGFUSE_SECRET_KEY`; `LANGFUSE_PUBLIC_KEY` is a repository variable rather than a secret. Existing tracked workflows establish the `pes-squad` and `schadensfresse` names. The owner confirmed the reserved arena Luna pair, but the available token could not enumerate GitHub secret names. P0-20 therefore verifies connectivity and behavior without displaying values.

## Workflow and activation boundary

P0-17 adds no workflow. Existing Bundesliga 2025/26 and WM26 community
entrypoints remain `workflow_call`-only historical files with no active
`workflow_dispatch` or schedule. P0-18 makes reusable workflows accept the
explicit Bundesliga identity. The first P0-19 copy creates exactly one
manual-only `arena-luna-self-contained` triad; the `dev-luna` row remains a
local CLI path because P0-17 assigns it no Actions participant. Production and
challenger entrypoints remain owner-gated. P0-20 alone exercises the local dev
cycle and arena Luna ladder, including its separately authorized temporary
arena schedule. P0-21 owns production manual evidence and final schedule
activation.
