---
applyTo: '**/tests/**'
---
# Test Coverage Instructions

These are guidelines to follow when working on test coverage for the project.

## Coverage Targets

**CRITICAL:** When implementing new features or fixing bugs, ALWAYS ENSURE that the relevant code areas have adequate test coverage.

## Tools

- The `Get-CoverageDetails.ps1` script is available to help analyze test coverage. Use it to identify untested code areas.
- MAKE SURE to have up to date local coverage data by running `Generate-CoverageReport.ps1` before analyzing coverage details.
  - ALWAYS focus the generation on the current area of focus to speed up the cycle, by using the `-Projects` parameter.
  - For example: `.\Generate-CoverageReport.ps1 -Projects OpenAiIntegration.Tests,Core.Tests`
- When checking coverage details via `Get-CoverageDetails.ps1`, there are different command line options available
  - `-Filter` filters for a class name and supports wildcards
  - Use the command help for further details: `Get-Help .\Get-CoverageDetails.ps1`

## Common Scenarios

### Checking uncovered lines for a Class

```console
# Exact Match
.\Get-CoverageDetails.ps1 -Filter "ClassName" -ShowUncovered

# Wildcard Match
.\Get-CoverageDetails.ps1 -Filter "*PartialClassName*" -ShowUncovered
```

### Show Line / Branch Coverage

```console
.\Get-CoverageDetails.ps1 -Detailed
