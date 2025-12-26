using TUnit.Core;

namespace KicktippIntegration.Tests.Infrastructure;

/// <summary>
/// Marks a test that requires encrypted fixtures to be available.
/// Tests with this attribute will be skipped with a warning if the fixture key is not configured.
/// </summary>
public class FixtureRequiredAttribute : SkipAttribute
{
    private const string SkipReason = 
        "⚠️ KICKTIPP_FIXTURE_KEY not configured. " +
        "Encrypted fixtures cannot be decrypted without the key. " +
        "Set the key in KicktippAi.Secrets/tests/KicktippIntegration.Tests/.env or as an environment variable.";

    public FixtureRequiredAttribute() : base(SkipReason)
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        // Ensure environment is loaded
        TestEnvironmentHelper.EnsureLoaded();
        
        // Skip if fixture key is not available
        return Task.FromResult(!TestEnvironmentHelper.HasFixtureKey());
    }
}
