# KicktippIntegration.Tests

Unit tests for the `KicktippIntegration` project, specifically the `KicktippClient` class which handles HTML parsing of Kicktipp pages.

## Test Approach

Tests use [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) to serve HTML fixtures at the correct paths, allowing us to test the HTML parsing logic without hitting the real Kicktipp website.

### Fixture Types

The test suite uses two types of fixtures organized by community under `Fixtures/Html/`:

#### Synthetic Fixtures (`Synthetic/{community}/`)

Handcrafted HTML files designed for testing specific edge cases with predictable data:

- **Purpose**: Test parsing logic for specific scenarios (date inheritance, empty values, etc.)
- **Format**: Unencrypted `.html` files
- **Assertions**: Use concrete value assertions (e.g., exact team names, dates, scores)
- **Location**: `Fixtures/Html/Synthetic/test-community/`

#### Real Fixtures (`Real/{community}/`)

Encrypted snapshots of actual Kicktipp pages:

- **Purpose**: Validate parsing against real-world HTML structure
- **Format**: AES-256-GCM encrypted `.html.enc` files
- **Assertions**: Use invariant-based assertions (counts, structure, required fields)
- **Location**: `Fixtures/Html/Real/ehonda-test-buli/`

### Testing Strategy

**Synthetic fixture tests** assert concrete values:
```csharp
// Synthetic fixtures have predictable data - assert exact values
StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
var predictions = await client.GetPlacedPredictionsAsync("test-community");
await Assert.That(predictions).HasCount().EqualTo(3);
```

**Real fixture tests** assert invariants:
```csharp
// Real fixtures may change - test structure and invariants, not specific values
StubWithRealFixture(community, "tippabgabe");
var predictions = await client.GetPlacedPredictionsAsync(community);
await Assert.That(predictions).HasCount().GreaterThan(0);
// For POST operations, verify the exact values we submitted appear in the form
```

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

3. Create encrypted fixtures using the orchestrator:
   ```powershell
   # Fetch and encrypt snapshots for a community
   dotnet run --project src/Orchestrator -- snapshots all --community ehonda-test-buli
   ```

### Working with Fixtures

To update real fixtures:

1. Run the orchestrator to fetch and encrypt new snapshots:
   ```powershell
   dotnet run --project src/Orchestrator -- snapshots all --community ehonda-test-buli
   ```
2. Commit the `.html.enc` files in `Fixtures/Html/Real/{community}/`
3. Delete any unencrypted HTML files (the orchestrator does this by default)

## Running Tests

```powershell
# Run all tests
dotnet run --project tests/KicktippIntegration.Tests

# Run specific test class
dotnet run --project tests/KicktippIntegration.Tests -- --treenode-filter "/*/*/KicktippClient_GetStandings_Tests/*"

# Run tests matching a pattern
dotnet run --project tests/KicktippIntegration.Tests -- --treenode-filter "/*/*/KicktippClient_PlaceBet*/*"
```
