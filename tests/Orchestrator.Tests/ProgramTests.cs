using System.Reflection;
using Orchestrator.Infrastructure;

namespace Orchestrator.Tests;

[NotInParallel("ProcessState")]
public class ProgramTests
{
    private const string LangfusePublicKeyEnvVar = "LANGFUSE_PUBLIC_KEY";
    private const string LangfuseSecretKeyEnvVar = "LANGFUSE_SECRET_KEY";
    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new();

    [Before(Test)]
    public void SaveState()
    {
        RememberEnvironmentVariable(LangfusePublicKeyEnvVar);
        RememberEnvironmentVariable(LangfuseSecretKeyEnvVar);
        ResetLangfuseRegistration();
        Environment.SetEnvironmentVariable(LangfusePublicKeyEnvVar, null);
        Environment.SetEnvironmentVariable(LangfuseSecretKeyEnvVar, null);
    }

    [After(Test)]
    public void RestoreState()
    {
        foreach (var (name, value) in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        ResetLangfuseRegistration();
    }

    [Test]
    public async Task Main_returns_success_for_help()
    {
        var exitCode = await Program.Main(["--help"]);

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Main_returns_success_for_version()
    {
        var exitCode = await Program.Main(["--version"]);

        await Assert.That(exitCode).IsEqualTo(0);
    }

    private void RememberEnvironmentVariable(string name)
    {
        if (!_originalEnvironmentVariables.ContainsKey(name))
        {
            _originalEnvironmentVariables[name] = Environment.GetEnvironmentVariable(name);
        }
    }

    private static void ResetLangfuseRegistration()
    {
        typeof(ServiceRegistrationExtensions)
            .GetField("_langfuseTracingRegistered", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, false);
    }
}
