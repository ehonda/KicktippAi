using DotNetEnv;
using DotNetEnv.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Orchestrator.Tests.Infrastructure;

// These tests mutate the process-wide current directory and environment variables.
[NotInParallel]
public class PathUtilityAndEnvironmentHelperTests : TempDirectoryTestBase
{
    private const string FirebaseProjectIdEnvVar = "FIREBASE_PROJECT_ID";
    private const string FirebaseServiceAccountJsonEnvVar = "FIREBASE_SERVICE_ACCOUNT_JSON";
    private const string KicktippPasswordEnvVar = "KICKTIPP_PASSWORD";
    private const string KicktippUsernameEnvVar = "KICKTIPP_USERNAME";
    private const string SiblingOnlyEnvVar = "KICKTIPP_AI_SIBLING_ONLY";
    private const string DotNetEnvProbeVar = "KICKTIPP_AI_DOTENV_PROBE";
    private const string TestEnvVar = "KICKTIPP_AI_TEST_ENV";

    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new();
    private readonly Dictionary<string, string> _originalDotNetEnvFallback = new(StringComparer.Ordinal);
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
        RememberEnvironmentVariable(SiblingOnlyEnvVar);
        RememberEnvironmentVariable(DotNetEnvProbeVar);
        RememberEnvironmentVariable(TestEnvVar);

        _originalDotNetEnvFallback.Clear();
        foreach (var (name, value) in Env.FakeEnvVars)
        {
            _originalDotNetEnvFallback[name] = value;
        }

        Env.FakeEnvVars.Clear();
    }

    [After(Test)]
    public void RestoreProcessState()
    {
        Directory.SetCurrentDirectory(_originalCurrentDirectory);

        foreach (var (name, value) in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        Env.FakeEnvVars.Clear();
        foreach (var (name, value) in _originalDotNetEnvFallback)
        {
            Env.FakeEnvVars[name] = value;
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
    public async Task Secret_path_helpers_use_original_repository_locator()
    {
        var (worktreeRoot, _) = CreateSolutionAndSecretsDirectories();
        var originalWorkspaceRoot = Path.Combine(TestDirectory, "original-workspace");
        var originalRoot = Path.Combine(originalWorkspaceRoot, "KicktippAi");
        var originalSecretsRoot = Path.Combine(originalWorkspaceRoot, "KicktippAi.Secrets");
        Directory.CreateDirectory(originalRoot);
        Directory.CreateDirectory(originalSecretsRoot);
        File.WriteAllText(Path.Combine(originalRoot, "KicktippAi.slnx"), "test");
        WriteOriginalRepositoryLocator(worktreeRoot, originalRoot);

        var instructionsPath = PathUtility.GetInstructionsTemplatePath();
        var envPath = PathUtility.GetEnvFilePath("Orchestrator");
        var firebasePath = PathUtility.GetFirebaseJsonPath();

        await Assert.That(instructionsPath).IsEqualTo(
            Path.Combine(worktreeRoot, "prompts", "reasoning-models", "predict-one-match", "v0-handcrafted", "instructions_template.md"));
        await Assert.That(Path.GetFullPath(envPath)).IsEqualTo(Path.Combine(originalSecretsRoot, "src", "Orchestrator", ".env"));
        await Assert.That(Path.GetFullPath(firebasePath)).IsEqualTo(Path.Combine(originalSecretsRoot, "src", "Orchestrator", "firebase.json"));
    }

    [Test]
    public async Task Loading_environment_variables_loads_dotenv_and_firebase_credentials()
    {
        ClearFirebaseEnvironmentVariables();
        Environment.SetEnvironmentVariable(TestEnvVar, null);

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
        await Assert.That(logger.Collector.GetSnapshot().Any(record =>
            record.Message.Contains("pes-user") || record.Message.Contains("pes-pass"))).IsFalse();
    }

    [Test]
    public async Task Missing_community_file_preserves_base_credentials()
    {
        CreateSolutionAndSecretsDirectories();
        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, "base-user");
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, "base-pass");

        var logger = new FakeLogger<PathUtilityAndEnvironmentHelperTests>();

        EnvironmentHelper.LoadCommunityKicktippCredentials(logger, "missing-community");

        await Assert.That(Environment.GetEnvironmentVariable(KicktippUsernameEnvVar)).IsEqualTo("base-user");
        await Assert.That(Environment.GetEnvironmentVariable(KicktippPasswordEnvVar)).IsEqualTo("base-pass");
    }

    [Test]
    public async Task Community_file_overrides_only_kicktipp_credential_pair()
    {
        var (_, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var orchestratorSecretsDirectory = Path.Combine(secretsRoot, "src", "Orchestrator");
        Directory.CreateDirectory(orchestratorSecretsDirectory);
        File.WriteAllText(
            Path.Combine(orchestratorSecretsDirectory, ".env.test-community"),
            "KICKTIPP_USERNAME=community-user\n" +
            "KICKTIPP_PASSWORD=community-pass\n" +
            "KICKTIPP_AI_TEST_ENV=must-not-load\n" +
            $"{SiblingOnlyEnvVar}=must-not-leak");
        Environment.SetEnvironmentVariable(TestEnvVar, "base-value");
        Environment.SetEnvironmentVariable(SiblingOnlyEnvVar, null);

        EnvironmentHelper.LoadCommunityKicktippCredentials(
            new FakeLogger<PathUtilityAndEnvironmentHelperTests>(),
            "test-community");

        await Assert.That(Environment.GetEnvironmentVariable(TestEnvVar)).IsEqualTo("base-value");
        await Assert.That(Environment.GetEnvironmentVariable(SiblingOnlyEnvVar)).IsNull();
        await Assert.That(Env.FakeEnvVars.ContainsKey(SiblingOnlyEnvVar)).IsFalse();

        var laterParse = Env
            .LoadContents(
                $"{DotNetEnvProbeVar}=${{{SiblingOnlyEnvVar}:-fallback}}",
                Env.NoEnvVars())
            .ToDotEnvDictionary();
        await Assert.That(laterParse[DotNetEnvProbeVar]).IsEqualTo("fallback");
    }

    [Test]
    [Arguments("KICKTIPP_USERNAME=community-user")]
    [Arguments("KICKTIPP_PASSWORD=community-pass")]
    public async Task Incomplete_community_credential_pair_preserves_both_base_values(string siblingContents)
    {
        var (_, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var orchestratorSecretsDirectory = Path.Combine(secretsRoot, "src", "Orchestrator");
        Directory.CreateDirectory(orchestratorSecretsDirectory);
        File.WriteAllText(
            Path.Combine(orchestratorSecretsDirectory, ".env.test-community"),
            $"{siblingContents}\n{SiblingOnlyEnvVar}=must-not-leak");
        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, "base-user");
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, "base-pass");

        await Assert.That(() => EnvironmentHelper.LoadCommunityKicktippCredentials(
                new FakeLogger<PathUtilityAndEnvironmentHelperTests>(),
                "test-community"))
            .Throws<InvalidOperationException>();

        await Assert.That(Environment.GetEnvironmentVariable(KicktippUsernameEnvVar)).IsEqualTo("base-user");
        await Assert.That(Environment.GetEnvironmentVariable(KicktippPasswordEnvVar)).IsEqualTo("base-pass");
        await Assert.That(Environment.GetEnvironmentVariable(SiblingOnlyEnvVar)).IsNull();
        await Assert.That(Env.FakeEnvVars.ContainsKey(SiblingOnlyEnvVar)).IsFalse();
        await Assert.That(Env.FakeEnvVars.ContainsKey(KicktippUsernameEnvVar)).IsFalse();
        await Assert.That(Env.FakeEnvVars.ContainsKey(KicktippPasswordEnvVar)).IsFalse();
    }

    [Test]
    public async Task Repeated_load_rejects_interpolation_instead_of_inheriting_prior_sibling_value()
    {
        var (_, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var orchestratorSecretsDirectory = Path.Combine(secretsRoot, "src", "Orchestrator");
        Directory.CreateDirectory(orchestratorSecretsDirectory);
        File.WriteAllText(
            Path.Combine(orchestratorSecretsDirectory, ".env.first-community"),
            "KICKTIPP_USERNAME=first-user\nKICKTIPP_PASSWORD=first-pass");
        File.WriteAllText(
            Path.Combine(orchestratorSecretsDirectory, ".env.second-community"),
            "KICKTIPP_USERNAME=second-user\nKICKTIPP_PASSWORD=${KICKTIPP_PASSWORD}");
        var logger = new FakeLogger<PathUtilityAndEnvironmentHelperTests>();

        EnvironmentHelper.LoadCommunityKicktippCredentials(logger, "first-community");
        await Assert.That(() => EnvironmentHelper.LoadCommunityKicktippCredentials(logger, "second-community"))
            .Throws<InvalidOperationException>();

        await Assert.That(Environment.GetEnvironmentVariable(KicktippUsernameEnvVar)).IsEqualTo("first-user");
        await Assert.That(Environment.GetEnvironmentVariable(KicktippPasswordEnvVar)).IsEqualTo("first-pass");
        await Assert.That(Env.FakeEnvVars.ContainsKey(KicktippUsernameEnvVar)).IsFalse();
        await Assert.That(Env.FakeEnvVars.ContainsKey(KicktippPasswordEnvVar)).IsFalse();
    }

    [Test]
    public async Task Quoted_credentials_support_literal_dollar_signs_without_global_interpolation()
    {
        var (_, secretsRoot) = CreateSolutionAndSecretsDirectories();
        var orchestratorSecretsDirectory = Path.Combine(secretsRoot, "src", "Orchestrator");
        Directory.CreateDirectory(orchestratorSecretsDirectory);
        File.WriteAllText(
            Path.Combine(orchestratorSecretsDirectory, ".env.quoted-community"),
            "KICKTIPP_USERNAME='user$name'\nKICKTIPP_PASSWORD=\"pass\\$word\"");
        Env.FakeEnvVars["name"] = "must-not-expand";
        Env.FakeEnvVars["word"] = "must-not-expand";

        EnvironmentHelper.LoadCommunityKicktippCredentials(
            new FakeLogger<PathUtilityAndEnvironmentHelperTests>(),
            "quoted-community");

        await Assert.That(Environment.GetEnvironmentVariable(KicktippUsernameEnvVar)).IsEqualTo("user$name");
        await Assert.That(Environment.GetEnvironmentVariable(KicktippPasswordEnvVar)).IsEqualTo("pass$word");
        await Assert.That(Env.FakeEnvVars["name"]).IsEqualTo("must-not-expand");
        await Assert.That(Env.FakeEnvVars["word"]).IsEqualTo("must-not-expand");
    }

    [Test]
    [Arguments("../escape")]
    [Arguments("nested/community")]
    [Arguments(@"nested\community")]
    [Arguments(@"C:\escape")]
    [Arguments(".")]
    [Arguments("..")]
    [Arguments(" pes-squad")]
    [Arguments("pes-squad ")]
    [Arguments("pes--squad")]
    [Arguments("PES-squad")]
    public async Task Invalid_community_slug_is_rejected_before_path_resolution_or_state_change(string community)
    {
        Directory.SetCurrentDirectory(TestDirectory);
        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, "base-user");
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, "base-pass");
        Env.FakeEnvVars[SiblingOnlyEnvVar] = "unchanged";

        await Assert.That(() => EnvironmentHelper.LoadCommunityKicktippCredentials(
                new FakeLogger<PathUtilityAndEnvironmentHelperTests>(),
                community))
            .Throws<ArgumentException>();

        await Assert.That(Environment.GetEnvironmentVariable(KicktippUsernameEnvVar)).IsEqualTo("base-user");
        await Assert.That(Environment.GetEnvironmentVariable(KicktippPasswordEnvVar)).IsEqualTo("base-pass");
        await Assert.That(Env.FakeEnvVars.Count).IsEqualTo(1);
        await Assert.That(Env.FakeEnvVars[SiblingOnlyEnvVar]).IsEqualTo("unchanged");
    }

    [Test]
    public async Task Loading_environment_variables_without_files_logs_guidance_messages()
    {
        ClearFirebaseEnvironmentVariables();

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
        ClearFirebaseEnvironmentVariables();

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

    private static void ClearFirebaseEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable(FirebaseProjectIdEnvVar, null);
        Environment.SetEnvironmentVariable(FirebaseServiceAccountJsonEnvVar, null);
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

    private static void WriteOriginalRepositoryLocator(string solutionRoot, string originalRoot)
    {
        var locatorDirectory = Path.Combine(solutionRoot, ".codex-local");
        Directory.CreateDirectory(locatorDirectory);
        File.WriteAllText(Path.Combine(locatorDirectory, "original-repository-path"), originalRoot);
    }
}
