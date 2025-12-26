using DotNetEnv;
using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.Infrastructure;

/// <summary>
/// Helper for loading test environment variables from the secrets directory.
/// Follows the same pattern as Orchestrator's EnvironmentHelper.
/// </summary>
public static class TestEnvironmentHelper
{
    private static bool _loaded;
    private static readonly object Lock = new();

    /// <summary>
    /// Loads environment variables from the secrets directory.
    /// Safe to call multiple times - will only load once.
    /// </summary>
    public static void EnsureLoaded()
    {
        lock (Lock)
        {
            if (_loaded) return;

            var envPath = GetEnvFilePath();

            if (File.Exists(envPath))
            {
                Env.Load(envPath);
            }

            _loaded = true;
        }
    }

    /// <summary>
    /// Gets the fixture encryption key from environment variables.
    /// Returns null if not configured.
    /// </summary>
    public static string? GetFixtureKey()
    {
        EnsureLoaded();
        return Environment.GetEnvironmentVariable("KICKTIPP_FIXTURE_KEY");
    }

    /// <summary>
    /// Checks if fixture encryption key is available.
    /// </summary>
    public static bool HasFixtureKey() => !string.IsNullOrEmpty(GetFixtureKey());

    /// <summary>
    /// Gets the path to the .env file for tests.
    /// </summary>
    private static string GetEnvFilePath()
    {
        var solutionRoot = SolutionPathUtility.FindSolutionRoot();
        return Path.Combine(solutionRoot, "..", "KicktippAi.Secrets", "tests", "KicktippIntegration.Tests", ".env");
    }
}
