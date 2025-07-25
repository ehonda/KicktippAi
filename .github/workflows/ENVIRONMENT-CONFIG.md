# Environment Configuration Guide

The automated predictions workflow supports multiple environments that can be easily configured through GitHub repository variables.

## Environment Overview

### Staging Environment
- **Purpose**: Testing and development predictions
- **Default Model**: `o4-mini` (faster, cheaper)
- **Default Status**: Enabled
- **Use Case**: Daily testing, development work

### Production Environment  
- **Purpose**: Final production predictions
- **Default Model**: `o1` (more accurate, slower)
- **Default Status**: Disabled
- **Use Case**: Final season predictions, important matches

## Configuration

### Method 1: Repository Variables (Recommended)

Go to your repository → Settings → Secrets and variables → Actions → Variables tab

Set these repository variables to override the defaults:

| Variable Name | Purpose | Default Value | Valid Values |
|---------------|---------|---------------|--------------|
| `STAGING_ENABLED` | Enable/disable staging environment | `true` | `true`, `false` |
| `STAGING_MODEL` | Model for staging predictions | `o4-mini` | `o4-mini`, `o1` |
| `PRODUCTION_ENABLED` | Enable/disable production environment | `false` | `true`, `false` |
| `PRODUCTION_MODEL` | Model for production predictions | `o1` | `o4-mini`, `o1` |

### Method 2: Workflow File Defaults

The defaults are set in the workflow file itself in the `env` section. You can modify these directly in the code.

## Behavior

### Configuration Validation
The workflow will **fail with an error** if both `STAGING_ENABLED` and `PRODUCTION_ENABLED` are set to `true`. This prevents accidental misconfigurations where you might expect production runs but get staging runs instead.

**Error message**: "Configuration Error: Both STAGING_ENABLED and PRODUCTION_ENABLED are set to true"

### Scheduled Runs
When the workflow runs on schedule (twice daily):

1. **If only staging is enabled**: Uses staging environment with staging model
2. **If only production is enabled**: Uses production environment with production model  
3. **If both are enabled**: **FAILS with configuration error**
4. **If neither is enabled**: Workflow skips execution with message "no environment enabled"

### Manual Runs
When triggered manually via workflow_dispatch:

1. **Explicit environment**: If you select a specific environment, it uses that with your chosen model
2. **Auto-detection**: If you choose "auto", it follows the same logic as scheduled runs
3. **Force prediction**: Available regardless of verification result

## Common Configuration Scenarios

### Scenario 1: Development Phase
```
STAGING_ENABLED=true
STAGING_MODEL=o4-mini  
PRODUCTION_ENABLED=false
```
→ Runs twice daily with o4-mini for testing

### Scenario 2: Production Ready
```
STAGING_ENABLED=false
PRODUCTION_ENABLED=true
PRODUCTION_MODEL=o1
```
→ Runs twice daily with o1 for final predictions

### Scenario 3: Configuration Error (Will Fail)
```
STAGING_ENABLED=true
STAGING_MODEL=o4-mini
PRODUCTION_ENABLED=true  # ERROR: Both cannot be true
```
→ Workflow fails with configuration error message

### Scenario 4: Paused
```
STAGING_ENABLED=false
PRODUCTION_ENABLED=false
```
→ Scheduled runs will skip, manual runs still available

## Environment Switching

### Quick Toggle Examples

**Enable Production, Disable Staging:**
```
STAGING_ENABLED=false
PRODUCTION_ENABLED=true
```

**Pause All Scheduled Runs:**
```
STAGING_ENABLED=false
PRODUCTION_ENABLED=false
```

**Emergency Switch to Different Model:**
```
STAGING_ENABLED=true
STAGING_MODEL=o1  # Upgrade staging to production model
PRODUCTION_ENABLED=false
```

## Manual Overrides

Even with scheduled environments configured, you can always:

1. **Trigger manually** with any model/environment combination
2. **Force predictions** even when verification passes
3. **Override environment detection** by selecting specific environment

This gives you full control for testing, emergency runs, or one-off predictions.

## Monitoring

The workflow summary shows:
- Which environment was used
- Which model was selected  
- Whether it was scheduled or manual
- What action was taken

Check the Actions tab → workflow run → Summary section for details.
