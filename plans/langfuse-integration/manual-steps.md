# Manual Steps

This document lists all steps that require manual action (account setup, UI configuration, credential management) and when they need to happen relative to the implementation phases.

---

## Before Phase 1

### 1. Create a Langfuse Cloud Account

- Go to [https://cloud.langfuse.com/auth/sign-up](https://cloud.langfuse.com/auth/sign-up)
- Create an account (free tier is sufficient to start)

### 2. Create a Langfuse Project

- In the Langfuse dashboard, create a new project (e.g., `KicktippAi`)
- Choose the EU data region (`https://cloud.langfuse.com`) or US region (`https://us.cloud.langfuse.com`) — this determines the `LANGFUSE_BASE_URL`

### 3. Generate API Credentials

- Navigate to **Project Settings → API Keys**
- Create a new API key pair
- Note down:
  - **Public Key** (`pk-lf-...`) → `LANGFUSE_PUBLIC_KEY`
  - **Secret Key** (`sk-lf-...`) → `LANGFUSE_SECRET_KEY`

### 4. Add Credentials to `.env`

Add the following to your local `.env` file (this file is gitignored):

```
LANGFUSE_PUBLIC_KEY=pk-lf-...
LANGFUSE_SECRET_KEY=sk-lf-...
LANGFUSE_BASE_URL=https://cloud.langfuse.com
```

### 5. Verify `.gitignore` Covers `.env`

Confirm that `.env` is listed in `.gitignore` so credentials are never committed. (It likely already is, since `OPENAI_API_KEY` and Firebase credentials are loaded the same way.)

### 6. Add Credentials to GitHub for CI/CD

Langfuse tracing also runs in GitHub Actions workflows. The public key is **not a secret** (it's used as a Basic Auth username and cannot authenticate on its own), so it goes in a repository **variable**. The secret key is sensitive and goes in a repository **secret**.

1. Go to **GitHub → Repository Settings → Secrets and variables → Actions**
2. Under the **Variables** tab, create:
   - `LANGFUSE_PUBLIC_KEY` = `pk-lf-...`
3. Under the **Secrets** tab, create (if not already present):
   - `LANGFUSE_SECRET_KEY` = `sk-lf-...`

The base workflows read `LANGFUSE_PUBLIC_KEY` from `vars.*` (repository variables) and `LANGFUSE_SECRET_KEY` from `secrets.*` (repository secrets). Caller workflows forward only the secret key; the variable is automatically available in reusable workflows within the same repository.

---

## After Phase 1 Implementation

### 6. Verify Traces in Langfuse Dashboard

After running a test prediction:

```powershell
dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli
```

- Open the Langfuse dashboard
- Navigate to **Traces**
- Confirm:
  - A trace appears with the expected session ID and tags
  - Child generation observations show model name, prompt content, response JSON, token usage, and cost
  - Cost values match the CLI output from `TokenUsageTracker`

### 7. Configure Custom Model Definitions (Optional)

If Langfuse's built-in model pricing doesn't cover all models used (or prices are outdated):

- Go to **Project Settings → Models**
- Add custom model definitions with correct per-token pricing
- This ensures Langfuse-inferred costs are accurate even if we don't send `cost_details` attributes

---

## Before Phase 2

### 8. Review Langfuse Pricing / Limits

Before uploading datasets and running experiments:

- Check Langfuse Cloud [pricing](https://langfuse.com/pricing) for dataset storage and trace volume
- Ensure the free tier or current plan covers expected usage
- Upgrade if necessary

### 9. Prepare Historical Data Export

- Identify which Firebase collections contain historical predictions and actual outcomes
- Decide on the dataset scope (e.g., last N matchdays, specific communities)
- This is a manual decision; the code to export and upload will be part of Phase 2 implementation

Phase 2 now has its own detailed implementation-time manual checklist in [phase-2/tasks/manual-steps.md](phase-2/tasks/manual-steps.md). Use that document during execution; keep this file as the cross-phase summary.

---

## Before Phase 3

### 10. Upload Initial Prompts to Langfuse (If Pursuing Phase 3)

- Go to **Prompt Management** in the Langfuse dashboard
- Create prompts matching the current file structure:
  - `match` (from `prompts/{model}/match.md`)
  - `match-justification` (from `prompts/{model}/match.justification.md`)
  - `bonus` (from `prompts/{model}/bonus.md`)
- Label the initial versions as `production`
- This can also be done programmatically via the REST API during Phase 3 implementation

---

## Summary Timeline

| When | Step | Type |
|------|------|------|
| Before Phase 1 | Create Langfuse account | Manual (one-time) |
| Before Phase 1 | Create project + API keys | Manual (one-time) |
| Before Phase 1 | Add credentials to `.env` | Manual (one-time) |
| Before Phase 1 | Verify `.gitignore` | Manual (one-time) |
| Before Phase 1 | Add credentials to GitHub (variable + secret) | Manual (one-time) |
| After Phase 1 | Verify traces in dashboard | Manual (validation) |
| After Phase 1 | Configure custom models (optional) | Manual (optional) |
| Before Phase 2 | Review pricing / limits | Manual (decision) |
| Before Phase 2 | Prepare historical data scope | Manual (decision) |
| Before Phase 3 | Upload prompts to Langfuse | Manual (if pursuing) |
