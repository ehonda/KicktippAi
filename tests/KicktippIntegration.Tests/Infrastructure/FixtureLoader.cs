using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.Infrastructure;

/// <summary>
/// Loads encrypted test fixtures and decrypts them at runtime.
/// </summary>
public static class FixtureLoader
{
    /// <summary>
    /// Loads and decrypts a fixture file.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture (without .html.enc extension).</param>
    /// <returns>Decrypted HTML content.</returns>
    /// <exception cref="InvalidOperationException">Thrown if encryption key is not configured.</exception>
    /// <exception cref="FileNotFoundException">Thrown if fixture file does not exist.</exception>
    public static string LoadFixture(string fixtureName)
    {
        var key = TestEnvironmentHelper.GetFixtureKey()
            ?? throw new InvalidOperationException(
                "KICKTIPP_FIXTURE_KEY environment variable is not set. " +
                "Please configure the encryption key in your .env file or environment variables.");

        var fixturePath = GetFixturePath(fixtureName);
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException($"Fixture file not found: {fixturePath}");
        }

        var encryptedContent = File.ReadAllText(fixturePath);
        return FixtureEncryptor.Decrypt(encryptedContent, key);
    }

    /// <summary>
    /// Checks if a fixture file exists.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture (without .html.enc extension).</param>
    /// <returns>True if the fixture file exists.</returns>
    public static bool FixtureExists(string fixtureName)
    {
        var fixturePath = GetFixturePath(fixtureName);
        return File.Exists(fixturePath);
    }

    /// <summary>
    /// Gets the full path to a fixture file.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture (without .html.enc extension).</param>
    /// <returns>Full path to the fixture file.</returns>
    public static string GetFixturePath(string fixtureName)
    {
        var fixturesDir = GetFixturesDirectory();
        return Path.Combine(fixturesDir, $"{fixtureName}.html.enc");
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
    /// Lists all available fixture names.
    /// </summary>
    /// <returns>Enumerable of fixture names (without .html.enc extension).</returns>
    public static IEnumerable<string> ListFixtures()
    {
        var fixturesDir = GetFixturesDirectory();
        if (!Directory.Exists(fixturesDir))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(fixturesDir, "*.html.enc")
            .Select(f => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f)));
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
