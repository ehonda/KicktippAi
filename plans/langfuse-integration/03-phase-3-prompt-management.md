# Phase 3 - Prompt Management

> **Status**: POC complete; broader migration deferred.

## Objective

Evaluate Langfuse hosted prompts for KicktippAi without disrupting the stable file-based prompt workflow.

## Current State

- Production and default local experiment runs continue to use prompts from the `prompts/` directory.
- Experiment runs can opt into Langfuse hosted prompts.
- Hosted prompt runs record prompt source, prompt name, label, and version in run metadata and trace metadata.
- Prediction observations include Langfuse prompt-link tags when prompt metadata is available.
- The current POC prompt is documented in [running-experiments.md](../../docs/langfuse/experiments/running-experiments.md).

## POC Scope

The hosted prompt path is intentionally narrow:

- match prediction experiment runs only
- opt-in usage only
- no hosted prompt support for justification prompts yet
- no production prompt migration yet

This keeps hosted prompt experimentation available while preserving the simpler, Git-versioned file prompt workflow for ordinary prediction runs.

## Deferred Work

- Decide whether hosted prompts should become part of production prediction workflows.
- Add a promotion/versioning convention for Langfuse prompt labels.
- Add hosted prompt support for bonus predictions if needed.
- Add hosted prompt support for justification prompts if the experiment workflow needs it.
- Document prompt creation and promotion once the POC becomes a supported workflow.
