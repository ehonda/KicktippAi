# Phase 2 — Evaluation & Experiments

> **Status**: Future — to be started after Phase 1 is complete and validated.

Detailed planning, task tracking, shared context, and Phase 2-specific manual steps now live under [phase-2](phase-2).

## Objective

Use Langfuse's evaluation features to systematically measure prediction quality across models and prompts, replacing ad-hoc analysis with persistent, visual experiment tracking.

## Motivation

- The project already has `analyze-match` CLI commands that compare prediction distributions, but results are ephemeral (console output only).
- Firebase stores historical predictions and actual match outcomes — this data can seed Langfuse datasets for repeatable experiments.
- Langfuse supports LLM-as-a-Judge evaluators, which could automatically score predictions.

## Planned Approach

### 1. Create Datasets from Historical Data

- Export past matches (with actual outcomes) and their predictions from Firebase
- Upload as Langfuse datasets via the [REST API](https://langfuse.com/docs/api-and-data-platform/features/public-api) (`POST /api/public/datasets`)
- Each dataset item: input = match-to-predict payload, expected output = actual match result, metadata = prompt reconstruction and filtering context

### 2. Run Experiments

- For each model/prompt combination, run predictions against the dataset
- Log each prediction as a Langfuse trace linked to the dataset item
- Langfuse's experiment UI shows side-by-side comparison of runs

### 3. Scoring

- **Exact match score**: Did the prediction match the actual result exactly?
- **Outcome accuracy**: Did the prediction get the match outcome correct (home win / draw / away win)?
- **Goal difference accuracy**: How close was the predicted goal difference?
- **LLM-as-a-Judge** (optional): Use a model to evaluate prediction justifications for reasoning quality

### 4. Integration with Traces

- Link Phase 1 production traces to evaluation scores so the Langfuse dashboard shows quality trends alongside cost and usage data

## Prerequisites

- Phase 1 complete (traces flowing to Langfuse)
- Langfuse REST API access (uses same credentials)
- Historical prediction data accessible from Firebase

## Open Questions

- How far back should historical data go for the initial dataset?
- What scoring dimensions matter most for prompt iteration decisions?
- Should evaluation run as a CLI command or a separate tool?

These questions are now broken down into implementation tasks in [phase-2/01-phase-2-tracker.md](phase-2/01-phase-2-tracker.md).
