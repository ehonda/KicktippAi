# Bundesliga 2026/27 Community Onboarding

Updated: 2026-08-27

This is the authoritative community, context, configuration, credential-name, and Langfuse-environment matrix selected by [ADR-0052](../../plans/bundesliga-2026-27/decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md). Every row uses competition `bundesliga-2026-27`. Repository entrypoints are manual-only; their presence does not satisfy P0-21's live gates.

## Community matrix

| Row ID | Posting target | Community context | Prediction behavior | Model and prompt slot | Local Kicktipp source | Actions Kicktipp names | Langfuse environment | Owner and state |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `dev-luna` | `ehonda-dev-buli-2627` | `ehonda-dev-buli-2627` | Self-contained; overwrite-capable plumbing validation | `validation-luna-none`: exact Luna/none identity below | Base `.env`; no sibling is required | No Actions participant pair is assigned by P0-17; the safe path is local CLI | `development` | Project owner; configured and authorized for validation |
| `arena-luna-self-contained` | `ehonda-ai-arena` | `ehonda-ai-arena` | Self-contained plumbing validation and admitted cheap challenger | Luna/`none`, cap `10000`, match v3, bonus v1 | `.env.ehonda-ai-arena`; reserved for this participant | `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` | `production` | Owner-selected; manual-only |
| `pes-production-reference` | `pes-squad` | `pes-squad` | Independent reference production generation | Sol/`xhigh`, cap `10000`, match v3, bonus v1 | `.env.pes-squad` | `PES_SQUAD_KICKTIPP_USERNAME` / `PES_SQUAD_KICKTIPP_PASSWORD` | `production` | Owner-selected; read readiness passed, POST permission/manual evidence pending |
| `schadensfresse-production-independent` | `schadensfresse` | `schadensfresse` | Independent production generation | Sol/`xhigh`, cap `10000`, match v3, bonus v1 | `.env.schadensfresse` | `SCHADENSFRESSE_KICKTIPP_USERNAME` / `SCHADENSFRESSE_KICKTIPP_PASSWORD` | `production` | Owner-selected; external new-season setup and live gates pending |
| `relaxdays-production-copy` | `relaxdays-tippt` | `pes-squad` | Guarded copy of stored `pes-squad` production prediction; target context exists for ADR-0048 bonus fallback | Exact `production-primary` Sol/`xhigh` identity | `.env.relaxdays-tippt` | `RELAXDAYS_TIPPT_KICKTIPP_USERNAME` / `RELAXDAYS_TIPPT_KICKTIPP_PASSWORD` | `production` | Owner-selected; runtime readiness/POST/manual evidence pending |
| `arena-production-copy` | `ehonda-ai-arena` | `pes-squad` | Guarded production copy; arena context exists for ADR-0048 bonus fallback | Exact `production-primary` Sol/`xhigh` identity | `.env.ehonda-ai-arena.gpt-5-6-sol-xhigh` | `EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH_KICKTIPP_PASSWORD` | `production` | Owner-selected; runtime readiness/POST/manual evidence pending |
| `arena-challenger-sol-high` | `ehonda-ai-arena` | `ehonda-ai-arena` | Self-contained independent generation | Sol/`high`, cap `10000`, match v3, bonus v1 | `.env.ehonda-ai-arena.gpt-5-6-sol-high` | `EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH_KICKTIPP_PASSWORD` | `production` | Owner-selected; runtime readiness/POST/manual evidence pending |
| `arena-challenger-luna-medium` | `ehonda-ai-arena` | `ehonda-ai-arena` | Self-contained independent generation | Luna/`medium`, cap `10000`, match v3, bonus v1 | `.env.ehonda-ai-arena.gpt-5-6-luna-medium` | `EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM_KICKTIPP_PASSWORD` | `production` | Owner-selected; runtime readiness/POST/manual evidence pending |
| `arena-challenger-terra-xhigh` | `ehonda-ai-arena` | `ehonda-ai-arena` | Self-contained independent generation | Terra/`xhigh`, cap `10000`, match v3, bonus v1 | `.env.ehonda-ai-arena.gpt-5-6-terra-xhigh` | `EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH_KICKTIPP_PASSWORD` | `production` | Owner-selected; runtime readiness/POST/manual evidence pending |

An arena validation trace uses Langfuse environment `production` because the posting target is a production community. That telemetry classification does not promote `validation-luna-none` to the production model or to a challenger slot.

## Exact validation identity

Both validation rows use all of the following values explicitly:

| Field | Value |
| --- | --- |
| Competition | `bundesliga-2026-27` |
| Model | `gpt-5.6-luna` |
| Reasoning effort | `none` |
| Maximum output tokens | `10000` |
| Match prompt | `kicktippai/bundesliga-2026-27/predict-one-match`, version `3` |
| Match prompt normalized SHA-256 | `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3` |
| Bonus prompt | `kicktippai/bundesliga-2026-27/predict-bonus`, version `1` |
| Bonus prompt normalized SHA-256 | `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9` |

This identity remains the plumbing identity and is now also an explicitly admitted arena challenger. It is not the production model.

## Fixed configuration topology

`production-primary` is Sol/`xhigh` with cap `10000`, match v3, bonus v1, and the existing Flex-first/Standard-fallback policy. `pes-production-reference`, `schadensfresse-production-independent`, `relaxdays-production-copy`, and `arena-production-copy` use that exact stored identity. The four admitted arena challengers are the exact self-contained rows above; no unresolved challenger template is deployed.

## Credential resolution

Local ordinary prediction and verification commands choose credentials from the posting target:

```text
posting target: ehonda-ai-arena
community context: pes-squad
credential profile: gpt-5-6-sol-xhigh
credential file: .env.ehonda-ai-arena.gpt-5-6-sol-xhigh
```

They never select credentials from `community_context`. The base `.env` is loaded at startup. A command without an explicit participant profile retains `.env.<posting-community>` behavior. `--kicktipp-credential-profile <profile>` selects `.env.<posting-community>.<profile>` after both slugs pass strict lowercase/path-safe validation. The selected file overrides only `KICKTIPP_USERNAME` and `KICKTIPP_PASSWORD`; otherwise the existing environment remains unchanged. No credential value belongs in output, logs, tracked evidence, or this matrix.

The prior names-only local audit found the base and original community profiles; this decision does not claim that the newly named local files exist. The base `.env` remains the development credential source, while `.env.ehonda-ai-arena` identifies the Luna/`none` participant only. Operators create the additional canonical profiles in the sibling secrets checkout before local use.

Shared Actions configuration uses `FIREBASE_PROJECT_ID`, `FIREBASE_SERVICE_ACCOUNT_JSON`, `OPENAI_API_KEY`, and `LANGFUSE_SECRET_KEY`; `LANGFUSE_PUBLIC_KEY` is a repository variable rather than a secret. The Owner confirmed every exact Kicktipp pair in this matrix provisioned on 2026-08-27. The connected token did not enumerate them, so this is Owner provisioning evidence rather than API inventory, authentication, readiness, or POST-permission evidence. P0-21 verifies runtime behavior without displaying values.

## Workflow and activation boundary

Existing Bundesliga 2025/26 and WM26 community entrypoints remain
`workflow_call`-only historical files. P0-19 now prepares every selected row as
an exact manual-only entrypoint/triad; `dev-luna` remains a local CLI path. No
current Bundesliga caller contains a schedule or `workflow_call`. P0-21 owns
production manual evidence, the activation ADR, deliberate schedules, and the
first scheduled observation.

The reusable context workflow's launch-roster input is false by default.
`pes-squad`, `relaxdays-tippt`, and prepared `schadensfresse` callers opt in to
the exact audited SHA/revision/date overlay before normal profile collection;
the shared arena context callers omit it and preserve verified enriched head
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
This is a prepared P0-21 publication path, not evidence that any of those
callers has run.
