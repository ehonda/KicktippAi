using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.Infrastructure;

/// <summary>
/// Loads test fixtures at runtime.
/// 
/// The fixture directory structure is:
///   Fixtures/Html/
///     Synthetic/         - Unencrypted fixtures for testing with predictable data
///       {community}/     - Community-specific synthetic fixtures (e.g., test-community/)
///     Real/              - Encrypted fixtures from actual Kicktipp pages
///       {community}/     - Community-specific real fixtures (e.g., ehonda-test-buli/)
/// </summary>
public static class FixtureLoader
{
    private static readonly string FixturesDirectory = SolutionPathUtility.FindDirectoryUnderSolutionRoot(
        Path.Combine("tests", "KicktippIntegration.Tests", "Fixtures", "Html"));

    /// <summary>
    /// Loads and decrypts a real fixture file for a specific community.
    /// Real fixtures contain actual data from Kicktipp pages and are encrypted for privacy.
    /// </summary>
    /// <param name="community">The Kicktipp community (e.g., "ehonda-test-buli").</param>
    /// <param name="fixtureName">Name of the fixture (without .html.enc extension).</param>
    /// <returns>Decrypted HTML content.</returns>
    /// <exception cref="InvalidOperationException">Thrown if encryption key is not configured.</exception>
    /// <exception cref="FileNotFoundException">Thrown if fixture file does not exist.</exception>
    public static string LoadRealFixture(string community, string fixtureName)
    {
        var key = TestEnvironmentHelper.GetFixtureKey()
            ?? throw new InvalidOperationException(
                "KICKTIPP_FIXTURE_KEY environment variable is not set. " +
                "Please configure the encryption key in your .env file or environment variables.");

        var fixturePath = Path.Combine(FixturesDirectory, "Real", community, $"{fixtureName}.html.enc");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException($"Real fixture file not found: {fixturePath}");
        }

        var encryptedContent = File.ReadAllText(fixturePath);
        return FixtureEncryptor.Decrypt(encryptedContent, key);
    }

    /// <summary>
    /// Loads a synthetic (unencrypted) fixture file for a specific community.
    /// Synthetic fixtures contain predictable test data designed for specific edge cases.
    /// </summary>
    /// <param name="community">The community folder (e.g., "test-community").</param>
    /// <param name="fixtureName">Name of the fixture (without .html extension).</param>
    /// <returns>The HTML content.</returns>
    /// <exception cref="FileNotFoundException">Thrown if fixture file does not exist.</exception>
    public static string LoadSyntheticFixture(string community, string fixtureName)
    {
        var fixturePath = Path.Combine(FixturesDirectory, "Synthetic", community, $"{fixtureName}.html");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException($"Synthetic fixture file not found: {fixturePath}");
        }

        return File.ReadAllText(fixturePath);
    }
}
