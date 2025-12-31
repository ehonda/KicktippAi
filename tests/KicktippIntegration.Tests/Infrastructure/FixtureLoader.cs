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

        var fixturePath = GetRealFixturePath(community, fixtureName);
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
        var fixturePath = GetSyntheticFixturePath(community, fixtureName);
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException($"Synthetic fixture file not found: {fixturePath}");
        }

        return File.ReadAllText(fixturePath);
    }

    /// <summary>
    /// Checks if a real fixture file exists for a specific community.
    /// </summary>
    /// <param name="community">The Kicktipp community.</param>
    /// <param name="fixtureName">Name of the fixture (without .html.enc extension).</param>
    /// <returns>True if the fixture file exists.</returns>
    public static bool RealFixtureExists(string community, string fixtureName)
    {
        var fixturePath = GetRealFixturePath(community, fixtureName);
        return File.Exists(fixturePath);
    }

    /// <summary>
    /// Checks if a synthetic fixture file exists for a specific community.
    /// </summary>
    /// <param name="community">The community folder.</param>
    /// <param name="fixtureName">Name of the fixture (without .html extension).</param>
    /// <returns>True if the fixture file exists.</returns>
    public static bool SyntheticFixtureExists(string community, string fixtureName)
    {
        var fixturePath = GetSyntheticFixturePath(community, fixtureName);
        return File.Exists(fixturePath);
    }

    /// <summary>
    /// Gets the full path to a real fixture file for a specific community.
    /// </summary>
    /// <param name="community">The Kicktipp community.</param>
    /// <param name="fixtureName">Name of the fixture (without .html.enc extension).</param>
    /// <returns>Full path to the fixture file.</returns>
    public static string GetRealFixturePath(string community, string fixtureName)
    {
        var fixturesDir = GetFixturesDirectory();
        return Path.Combine(fixturesDir, "Real", community, $"{fixtureName}.html.enc");
    }

    /// <summary>
    /// Gets the full path to a synthetic fixture file for a specific community.
    /// </summary>
    /// <param name="community">The community folder.</param>
    /// <param name="fixtureName">Name of the fixture (without .html extension).</param>
    /// <returns>Full path to the fixture file.</returns>
    public static string GetSyntheticFixturePath(string community, string fixtureName)
    {
        var fixturesDir = GetFixturesDirectory();
        return Path.Combine(fixturesDir, "Synthetic", community, $"{fixtureName}.html");
    }

    /// <summary>
    /// Gets the directory where fixtures are stored.
    /// </summary>
    public static string GetFixturesDirectory()
    {
        // Look for Fixtures/Html relative to the test project
        var testProjectDir = FindTestProjectDirectory();
        return Path.Combine(testProjectDir, "Fixtures", "Html");
    }

    /// <summary>
    /// Lists all available real fixture names for a specific community.
    /// </summary>
    /// <param name="community">The Kicktipp community.</param>
    /// <returns>Enumerable of fixture names (without .html.enc extension).</returns>
    public static IEnumerable<string> ListRealFixtures(string community)
    {
        var fixturesDir = Path.Combine(GetFixturesDirectory(), "Real", community);
        if (!Directory.Exists(fixturesDir))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(fixturesDir, "*.html.enc")
            .Select(f => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f)));
    }

    /// <summary>
    /// Lists all available synthetic fixture names for a specific community.
    /// </summary>
    /// <param name="community">The community folder.</param>
    /// <returns>Enumerable of fixture names (without .html extension).</returns>
    public static IEnumerable<string> ListSyntheticFixtures(string community)
    {
        var fixturesDir = Path.Combine(GetFixturesDirectory(), "Synthetic", community);
        if (!Directory.Exists(fixturesDir))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(fixturesDir, "*.html")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)!;
    }

    private static string FindTestProjectDirectory()
    {
        // Start from the executing assembly location
        var assemblyLocation = typeof(FixtureLoader).Assembly.Location;
        var directory = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);

        // Walk up looking for KicktippIntegration.Tests.csproj
        while (directory != null)
        {
            var projectFile = Path.Combine(directory.FullName, "KicktippIntegration.Tests.csproj");
            if (File.Exists(projectFile))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        // Fallback: look relative to solution root
        return SolutionPathUtility.FindDirectoryUnderSolutionRoot(Path.Combine("tests", "KicktippIntegration.Tests"));
    }
}
