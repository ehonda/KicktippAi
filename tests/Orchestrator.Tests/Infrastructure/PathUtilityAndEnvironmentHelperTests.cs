using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Orchestrator.Tests.Infrastructure;

[NotInParallel("ProcessState")]
public class PathUtilityAndEnvironmentHelperTests : TempDirectoryTestBase
{
    private const string FirebaseProjectIdEnvVar = "FIREBASE_PROJECT_ID";
    private const string FirebaseServiceAccountJsonEnvVar = "FIREBASE_SERVICE_ACCOUNT_JSON";
    private const string KicktippPasswordEnvVar = "KICKTIPP_PASSWORD";
    private const string KicktippUsernameEnvVar = "KICKTIPP_USERNAME";
    private const string TestEnvVar = "KICKTIPP_AI_TEST_ENV";

    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new();
    private string _originalCurrentDirectory = null!;

    protected override string TestDirectoryName => "PathUtilityAndEnvironmentHelperTests";

    [Before(Test)]
    public void SaveProcessState()
    {
        _originalCurrentDirectory = Directory.GetCurrentDirectory();

        RememberEnvironmentVariable(FirebaseProjectIdEnvVar);
        RememberEnvironmentVariable(FirebaseServiceAccountJsonEnvVar);
        RememberEnvironmentVariable(KicktippPasswordEnvVar);
        RememberEnvironmentVariable(KicktippUsernameEnvVar);
        RememberEnvironmentVariable(TestEnvVar);
    }

    [After(Test)]
    public void RestoreProcessState()
    {
        Directory.SetCurrentDirectory(_originalCurrentDirectory);

        foreach (var (name, value) in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Test]
    public async Task Finding_solution_root_returns_nearest_ancestor_containing_solution_file()
    {
        var (solutionRoot, _) = CreateSolutionAndSecretsDirectories();
        var nestedDirectory = Path.Combine(solutionRoot, "src", "Orchestrator", "bin", "Debug");
        Directory.CreateDirectory(nestedDirectory);

        Directory.SetCurrentDirectory(nestedDirectory);

        var result = PathUtility.FindSolutionRoot();

        await Assert.That(result).IsEqualTo(solutionRoot);
    }

    [Test]
    public async Task Finding_solution_root_throws_when_solution_file_is_missing()
    {
        Directory.SetCurrentDirectory(TestDirectory);

        await Assert.That(() => PathUtility.FindSolutionRoot())
            .Throws<DirectoryNotFoundException>();
    }

    [Test]
    public async Task Path_helpers_use_solution_root_and_sibling_secrets_directory()
    {
        var (solutionRoot, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var nestedDirectory = Path.Combine(solutionRoot, "src", "Orchestrator");
        Directory.CreateDirectory(nestedDirectory);

        Directory.SetCurrentDirectory(nestedDirectory);

        var instructionsPath = PathUtility.GetInstructionsTemplatePath();
        var envPath = PathUtility.GetEnvFilePath("Orchestrator");
        var communityEnvPath = PathUtility.GetEnvFilePath("Orchestrator", "pes-squad");
        var firebasePath = PathUtility.GetFirebaseJsonPath();

        await Assert.That(instructionsPath).IsEqualTo(
            Path.Combine(solutionRoot, "prompts", "reasoning-models", "predict-one-match", "v0-handcrafted", "instructions_template.md"));
        await Assert.That(Path.GetFullPath(envPath)).IsEqualTo(Path.Combine(secretsRoot, "src", "Orchestrator", ".env"));
        await Assert.That(Path.GetFullPath(communityEnvPath)).IsEqualTo(Path.Combine(secretsRoot, "src", "Orchestrator", ".env.pes-squad"));
        await Assert.That(Path.GetFullPath(firebasePath)).IsEqualTo(Path.Combine(secretsRoot, "src", "Orchestrator", "firebase.json"));
    }

    [Test]
    public async Task Loading_environment_variables_loads_dotenv_and_firebase_credentials()
    {
        var (_, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var orchestratorSecretsDirectory = Path.Combine(secretsRoot, "src", "Orchestrator");
        Directory.CreateDirectory(orchestratorSecretsDirectory);

        File.WriteAllText(Path.Combine(orchestratorSecretsDirectory, ".env"), $"{TestEnvVar}=loaded-from-dotenv");
        File.WriteAllText(
            Path.Combine(orchestratorSecretsDirectory, "firebase.json"),
            """
            {
              "project_id": "firebase-project-123"
            }
            """);

        var logger = new FakeLogger<PathUtilityAndEnvironmentHelperTests>();

        EnvironmentHelper.LoadEnvironmentVariables(logger);

        await Assert.That(Environment.GetEnvironmentVariable(TestEnvVar)).IsEqualTo("loaded-from-dotenv");
        await Assert.That(Environment.GetEnvironmentVariable(FirebaseProjectIdEnvVar)).IsEqualTo("firebase-project-123");
        await Assert.That(Environment.GetEnvironmentVariable(FirebaseServiceAccountJsonEnvVar)).Contains("firebase-project-123");

        var logMessages = logger.Collector.GetSnapshot().Select(record => record.Message).ToList();
        await Assert.That(logMessages.Any(message => message.Contains("Loaded .env file from:"))).IsTrue();
        await Assert.That(logMessages.Any(message => message.Contains("Loaded Firebase credentials from:"))).IsTrue();
    }

    [Test]
    public async Task Loading_community_kicktipp_credentials_overrides_existing_kicktipp_environment_variables()
    {
        var (_, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var orchestratorSecretsDirectory = Path.Combine(secretsRoot, "src", "Orchestrator");
        Directory.CreateDirectory(orchestratorSecretsDirectory);
        File.WriteAllText(
            Path.Combine(orchestratorSecretsDirectory, ".env.pes-squad"),
            "KICKTIPP_USERNAME=pes-user\nKICKTIPP_PASSWORD=pes-pass");

        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, "base-user");
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, "base-pass");

        var logger = new FakeLogger<PathUtilityAndEnvironmentHelperTests>();

        EnvironmentHelper.LoadCommunityKicktippCredentials(logger, "pes-squad");

        await Assert.That(Environment.GetEnvironmentVariable(KicktippUsernameEnvVar)).IsEqualTo("pes-user");
        await Assert.That(Environment.GetEnvironmentVariable(KicktippPasswordEnvVar)).IsEqualTo("pes-pass");
        await Assert.That(logger.Collector.GetSnapshot().Any(record => record.Message.Contains("Loaded community-specific Kicktipp credentials from:"))).IsTrue();
    }

    [Test]
    public async Task Loading_environment_variables_without_files_logs_guidance_messages()
    {
        CreateSolutionAndSecretsDirectories();
        var logger = new FakeLogger<PathUtilityAndEnvironmentHelperTests>();

        EnvironmentHelper.LoadEnvironmentVariables(logger);

        var logMessages = logger.Collector.GetSnapshot().Select(record => record.Message).ToList();

        await Assert.That(logMessages.Any(message => message.Contains("No .env file found at:"))).IsTrue();
        await Assert.That(logMessages.Any(message => message.Contains("Please create a .env file"))).IsTrue();
        await Assert.That(logMessages.Any(message => message.Contains("No Firebase credentials file found at:"))).IsTrue();
        await Assert.That(logMessages.Any(message => message.Contains("Firebase integration will be disabled"))).IsTrue();
    }

    [Test]
    public async Task Existing_firebase_environment_variables_prevent_file_loading()
    {
        var (_, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var orchestratorSecretsDirectory = Path.Combine(secretsRoot, "src", "Orchestrator");
        Directory.CreateDirectory(orchestratorSecretsDirectory);
        File.WriteAllText(
            Path.Combine(orchestratorSecretsDirectory, "firebase.json"),
            """
            {
              "project_id": "project-from-file"
            }
            """);

        Environment.SetEnvironmentVariable(FirebaseProjectIdEnvVar, "project-from-env");
        Environment.SetEnvironmentVariable(FirebaseServiceAccountJsonEnvVar, "{\"project_id\":\"project-from-env\"}");

        var logger = new FakeLogger<PathUtilityAndEnvironmentHelperTests>();

        EnvironmentHelper.LoadEnvironmentVariables(logger);

        await Assert.That(Environment.GetEnvironmentVariable(FirebaseProjectIdEnvVar)).IsEqualTo("project-from-env");
        await Assert.That(logger.Collector.GetSnapshot().Any(record => record.Message.Contains("Firebase credentials already set via environment variables"))).IsTrue();
    }

    [Test]
    public async Task Invalid_firebase_json_logs_parse_error()
    {
        var (_, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var orchestratorSecretsDirectory = Path.Combine(secretsRoot, "src", "Orchestrator");
        Directory.CreateDirectory(orchestratorSecretsDirectory);
        File.WriteAllText(Path.Combine(orchestratorSecretsDirectory, "firebase.json"), "{ invalid json");

        var logger = new FakeLogger<PathUtilityAndEnvironmentHelperTests>();

        EnvironmentHelper.LoadEnvironmentVariables(logger);

        var errorLogs = logger.Collector.GetSnapshot()
            .Where(record => record.Level == LogLevel.Error)
            .ToList();

        await Assert.That(errorLogs.Count).IsGreaterThan(0);
        await Assert.That(errorLogs.Any(record => record.Message.Contains("Failed to parse Firebase JSON file"))).IsTrue();
    }

    private void RememberEnvironmentVariable(string name)
    {
        if (!_originalEnvironmentVariables.ContainsKey(name))
        {
            _originalEnvironmentVariables[name] = Environment.GetEnvironmentVariable(name);
        }
    }

    private (string SolutionRoot, string SecretsRoot) CreateSolutionAndSecretsDirectories()
    {
        var workspaceRoot = Path.Combine(TestDirectory, "workspace");
        var solutionRoot = Path.Combine(workspaceRoot, "KicktippAi");
        var secretsRoot = Path.Combine(workspaceRoot, "KicktippAi.Secrets");

        Directory.CreateDirectory(solutionRoot);
        Directory.CreateDirectory(secretsRoot);
        File.WriteAllText(Path.Combine(solutionRoot, "KicktippAi.slnx"), "test");

        Directory.SetCurrentDirectory(solutionRoot);
        return (solutionRoot, secretsRoot);
    }
}
