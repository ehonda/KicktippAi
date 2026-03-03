# Phase 3 — Prompt Management

> **Status**: Future — to be evaluated after Phase 2.

## Objective

Optionally migrate prompt templates from the file-based system (`prompts/` directory) to Langfuse's prompt management, enabling non-code prompt iteration, version control, and A/B testing.

## Current State

- Prompts live in `prompts/{model-family}/` as Markdown files (`match.md`, `match.justification.md`, `bonus.md`)
- `InstructionsTemplateProvider` loads them at startup, maps models to prompt families (e.g., `o4-mini` → `prompts/o3/`)
- Prompt changes require a code commit and redeployment

## What Langfuse Offers

- **Version control**: Each prompt version is tracked, labeled (e.g., `production`, `staging`), and auditable
- **A/B testing**: Serve different prompt versions to different runs and compare results
- **Playground**: Edit and test prompts directly in the Langfuse UI
- **Link to traces**: See which prompt version produced which traces/predictions
- **No latency impact**: Prompts are cached client-side by the SDK

## Assessment

This is the **lowest-priority phase** because:
- The current file-based system works well and is version-controlled via Git
- There is no team of non-technical prompt editors — changes are made by the developer
- Migration adds complexity (REST API calls to fetch prompts, fallback logic, caching)
- The primary value (A/B testing, linking prompts to traces) requires Phase 1 and Phase 2 to be useful

## If Pursued

1. Use Langfuse's REST API (`GET /api/public/v2/prompts/{name}`) since there is no .NET SDK
2. Create a `LangfusePromptProvider` implementing the same interface as `InstructionsTemplateProvider`
3. Add a configuration flag to choose between file-based and Langfuse-based prompts
4. Keep file-based prompts as a fallback for offline / CI scenarios
5. Upload existing prompt templates to Langfuse as the initial versions

## Open Questions

- Is there a real use case for non-developer prompt editing in this project?
- Does the added complexity justify the benefits over Git-based versioning?
- Should this be deferred indefinitely in favor of keeping the simpler approach?
