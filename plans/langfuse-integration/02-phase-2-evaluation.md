# Phase 2 - Evaluation & Experiments

> **Status**: Complete for the initial Langfuse integration.

This file is now a completion summary. The detailed planning, task tracking, and handoff notes under [phase-2](phase-2) are retained as historical implementation records.

For current experiment usage, read [docs/langfuse/experiments](../../docs/langfuse/experiments).

## Objective

Use Langfuse evaluation features to systematically measure prediction quality across models, prompts, and evaluation-time strategies, replacing ad-hoc analysis with persistent experiment data and reproducible reports.

## Completed Initial Integration

The initial repository workflow now supports:

- preparing historical football prediction datasets for repeatable experiments
- synchronizing prepared datasets to hosted Langfuse datasets
- running comparable experiment variants against fixed prepared datasets
- emitting SDK-compatible experiment markers for Langfuse Experiments Beta
- scoring predictions with Kicktipp points at item and aggregate run level
- exporting comparable Langfuse-backed runs into a normalized analysis bundle
- generating JSON, Markdown, and HTML statistical reports with repo-local Python tooling
- publishing browser-friendly experiment analysis pages through the repository's Pages workflow

## Current Source Of Truth

- [docs/langfuse/experiments/README.md](../../docs/langfuse/experiments/README.md) explains how to discover the current command surface.
- [docs/langfuse/experiments/data-model.md](../../docs/langfuse/experiments/data-model.md) documents the active experiment data model.
- [docs/langfuse/experiments/running-experiments.md](../../docs/langfuse/experiments/running-experiments.md) documents current run workflows.
- [docs/langfuse/experiments/analyzing-experiments.md](../../docs/langfuse/experiments/analyzing-experiments.md) documents current analysis and reporting.

## Historical Notes

The Phase 2 task trackers still contain useful context about why the current workflow looks the way it does, including the hosted dataset decision, scoring model, experiment identity markers, analysis contract, and older Langfuse UI/API quirks.

Use those trackers for design archaeology or behavior changes. Do not treat incomplete task statuses in the old trackers as the default guide for running today's experiments.
