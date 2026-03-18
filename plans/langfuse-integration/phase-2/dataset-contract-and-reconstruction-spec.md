# Phase 2 — Dataset Contract And Reconstruction Spec

This document is the handoff-ready contract for Task 4 and Task 5 implementation.

## Canonical Hosted Dataset

- Dataset meaning: one completed Bundesliga 2025/2026 match in `pes-squad`, independent of any specific historical prediction run
- Dataset name: `match-predictions/bundesliga-2025-26/pes-squad`
- Dataset item ID format: `bundesliga-2025-26__pes-squad__ts{TippSpielId}`

## Canonical Hosted Dataset Item Shape

- `input`
  - `homeTeam`
  - `awayTeam`
  - `startsAt`
- `expectedOutput`
  - `homeGoals`
  - `awayGoals`
- `metadata`
  - `competition`
  - `season`
  - `communityContext`
  - `matchday`
  - `matchdayLabel`
  - `homeTeam`
  - `awayTeam`
  - `tippSpielId`

## Exclusions

The hosted dataset must not persist replay metadata or local runner conveniences.

Excluded fields include:

- `model`
- `predictionCreatedAt`
- `includeJustification`
- `promptTemplatePath`
- `contextDocumentNames`
- `resolvedContextDocuments`
- baseline or historical prediction fields
- local runner payload fields such as system prompt text

## Deterministic Reconstruction Rule For Task 5

Task 5 reconstructs prompt context from an evaluation timestamp policy rather than from stored replay metadata.

1. Choose an evaluation timestamp policy such as `startsAt - 7 days`
2. Derive the required context-document names from `homeTeam`, `awayTeam`, and `communityContext`
3. Resolve the latest version of each required document whose `createdAt` is less than or equal to the chosen evaluation timestamp
4. Include transfer documents only if a version exists by that timestamp

## Required Match Context Documents

- `bundesliga-standings.csv`
- `community-rules-{communityContext}.md`
- `recent-history-{homeAbbreviation}.csv`
- `recent-history-{awayAbbreviation}.csv`
- `home-history-{homeAbbreviation}.csv`
- `away-history-{awayAbbreviation}.csv`
- `head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv`

## Optional Match Context Documents

- `{homeAbbreviation}-transfers.csv`
- `{awayAbbreviation}-transfers.csv`

## Verified Claim From Current Orchestration

Current match prediction uses a deterministic fixed required context-document set plus optional transfers when available. It does not dynamically choose arbitrary subsets from all stored context documents.

Relevant implementation references:

- `src/Orchestrator/Commands/Operations/Matchday/MatchdayCommand.cs`
- `src/Orchestrator/Commands/Operations/RandomMatch/RandomMatchCommand.cs`
- `src/ContextProviders.Kicktipp/KicktippContextProvider.cs`
- `src/FirebaseAdapter/FirebaseMatchOutcomeRepository.cs`
