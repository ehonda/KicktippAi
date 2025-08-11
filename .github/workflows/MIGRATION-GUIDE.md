# Multi-Community Migration Guide

This document guides you through migrating from the old staging/production environment system to the new multi-community workflow architecture.

## What Changed

### Old System (Removed)
- Single workflow with staging/production environments
- Environment variables: `STAGING_ENABLED`, `PRODUCTION_ENABLED`, etc.
- Shared Kicktipp credentials
- Global scheduling for all environments

### New System (Current)

- Separate workflows per community
- Direct input parameters per community
- Community-specific Kicktipp credentials
- Individual schedules per community
- Fixed models per community (no runtime overrides)

## Migration Steps

### 1. Remove Old Workflows

The following files are no longer needed and should be deleted:
- `automated-predictions.yml` (old version)
- `automated-bonus-predictions.yml` (old version)

### 2. Remove Old Environment Variables

Delete these repository variables from GitHub Settings → Actions → Variables:
- `STAGING_ENABLED`
- `STAGING_MODEL` 
- `STAGING_COMMUNITY`
- `PRODUCTION_ENABLED`
- `PRODUCTION_MODEL`
- `PRODUCTION_COMMUNITY`

### 3. Update Secrets

#### Rename Existing Secrets
If you had generic Kicktipp credentials, rename them to be community-specific:

**Before:**
- `KICKTIPP_USERNAME`
- `KICKTIPP_PASSWORD`

**After (example for test-community community):**
- `TEST_COMMUNITY_KICKTIPP_USERNAME`
- `TEST_COMMUNITY_KICKTIPP_PASSWORD`

#### Add New Community Secrets
For each additional community, add:
- `{COMMUNITY}_KICKTIPP_USERNAME`
- `{COMMUNITY}_KICKTIPP_PASSWORD`

Where `{COMMUNITY}` is the uppercase community name with dashes replaced by underscores.

#### Keep Global Secrets
These secrets remain the same:
- `FIREBASE_PROJECT_ID`
- `FIREBASE_SERVICE_ACCOUNT_JSON`
- `OPENAI_API_KEY`

### 4. Create Community Workflows

For each community you want to support, create two workflow files using the templates in the README.md.

## Configuration Reference

### Community Workflow Configuration

Each community workflow is configured with direct parameters:

- **`community`**: Kicktipp community name (string)
- **`model`**: OpenAI model to use for predictions (o4-mini, o1)  
- **`community_context`**: Community context when generating predictions (or using stored ones from the database) (string)

### Example Parameters
```yaml
with:
  community: "my-test-community"
  model: "o4-mini"
  community_context: "my-context"
```

### Secret Naming Convention

**Pattern:** `{COMMUNITY_UPPER}_KICKTIPP_{CREDENTIAL}`

**Examples:**
- Community `my-test-community` → `MY_TEST_COMMUNITY_KICKTIPP_USERNAME`
- Community `prod-football-league` → `PROD_FOOTBALL_LEAGUE_KICKTIPP_USERNAME`

### Schedule Configuration

Use standard cron expressions in the `schedule` section:

```yaml
schedule:
  - cron: '0 23 * * *'    # Daily at 23:00 UTC (~midnight Europe/Berlin)
  - cron: '30 6 * * MON'  # Mondays at 06:30 UTC (~7:30 Europe/Berlin)
```

**Common Patterns:**
- `'0 23 * * *'` - Daily at ~midnight Europe/Berlin
- `'0 11 * * *'` - Daily at ~noon Europe/Berlin  
- `'0 20 * * SUN'` - Weekly on Sunday evening
- `'30 6,18 * * *'` - Twice daily at custom times

## Example Migration

### Before (old system)
You had one workflow running for "my-test-community" community in staging mode.

### After (new system)

1. **Create matchday workflow** (`ehonda-test-buli-matchday.yml`):
   ```yaml
   name: Test Community - Matchday Predictions
   on:
     schedule:
       - cron: '0 23 * * *'
       - cron: '0 11 * * *'
     workflow_dispatch:
       inputs:
         force_prediction:
           description: 'Force prediction even if verify passes'
           required: false
           default: false
           type: boolean
   jobs:
     call-base-workflow:
       uses: ./.github/workflows/base-matchday-predictions.yml
       with:
         community: "my-test-community"
         model: "o4-mini"
         community_context: "my-context"
         trigger_type: ${{ github.event_name == 'schedule' && 'scheduled' || 'manual' }}
         force_prediction: ${{ github.event.inputs.force_prediction == 'true' }}
       secrets:
         kicktipp_username: ${{ secrets.MY_TEST_COMMUNITY_KICKTIPP_USERNAME }}
         kicktipp_password: ${{ secrets.MY_TEST_COMMUNITY_KICKTIPP_PASSWORD }}
         # ... other secrets
   ```

2. **Create bonus workflow** (`my-test-community-bonus.yml`):
   Similar structure but calls `base-bonus-predictions.yml`

3. **Configure secrets:**
   - `MY_TEST_COMMUNITY_KICKTIPP_USERNAME`
   - `MY_TEST_COMMUNITY_KICKTIPP_PASSWORD`

## Verification

After migration:

1. **Check workflows:** Go to Actions tab, verify new workflows appear
2. **Test manual triggers:** Try running each community workflow manually
3. **Monitor scheduled runs:** Wait for next scheduled execution
4. **Verify predictions:** Check that predictions are posted correctly

## Troubleshooting

### Workflow Not Appearing
- Check YAML syntax (use YAML validator)
- Ensure file is in `.github/workflows/` directory
- Verify file has `.yml` or `.yaml` extension

### Secret Access Errors  
- Verify secret names match exactly (case-sensitive)
- Check secret values are set in repository settings
- Ensure secrets don't contain extra whitespace

### Schedule Not Working
- Remember cron runs in UTC, not local time
- Test with manual trigger first
- Check GitHub Actions quotas/limits

### Community Name Mismatch

- Ensure `community` parameter matches exact Kicktipp community name
- Verify `community_context` matches your KPI data setup
- Check case sensitivity in community names
