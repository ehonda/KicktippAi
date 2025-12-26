# KicktippIntegration.Tests

Unit tests for the `KicktippIntegration` project, specifically the `KicktippClient` class which handles HTML parsing of Kicktipp pages.

## Test Approach

Tests use [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) to serve HTML fixtures at the correct paths, allowing us to test the HTML parsing logic without hitting the real Kicktipp website.

## Encrypted Fixtures

HTML fixtures are encrypted using AES-256-GCM to avoid committing actual Kicktipp page content. This approach:

- Allows fixtures to be version controlled
- Protects potentially sensitive page structure
- Works both locally and in CI (using secrets)

### Setup

1. Generate an encryption key:
   ```powershell
   .\Encrypt-Fixture.ps1 -GenerateKey
   ```

2. Store the key in one of these locations:
   - **Local development**: `<repo>/../KicktippAi.Secrets/tests/KicktippIntegration.Tests/.env`
     ```env
     KICKTIPP_FIXTURE_KEY=<your-base64-key>
     ```
   - **CI/CD**: Set as a repository secret named `KICKTIPP_FIXTURE_KEY`

3. Create encrypted fixtures using the helper script:
   ```powershell
   .\Encrypt-Fixture.ps1 -InputPath path/to/page.html -OutputPath tests/KicktippIntegration.Tests/Fixtures/Html/page-name.html.enc
   ```

### Working with Fixtures

Fixtures are stored in `Fixtures/Html/` as `.html.enc` files. To add a new fixture:

1. Save the HTML page locally (do NOT commit unencrypted files)
2. Run the encryption script
3. Commit the `.html.enc` file
4. Delete the original HTML file

### Tests Without Fixtures

Tests that require encrypted fixtures are marked with `[FixtureRequired]`. If the encryption key is not available:
- These tests will be **skipped** (not failed)
- A warning message explains why

This allows contributors without the key to still build and run other tests.

## Running Tests

```powershell
# Run all tests
dotnet run --project tests/KicktippIntegration.Tests

# Run specific test class
dotnet run --project tests/KicktippIntegration.Tests -- --treenode-filter "/*/*/KicktippClient_GetStandings_Tests/*"
```
