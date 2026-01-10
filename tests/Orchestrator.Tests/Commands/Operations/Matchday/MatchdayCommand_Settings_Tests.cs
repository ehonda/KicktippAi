using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> settings validation.
/// </summary>
public class MatchdayCommand_Settings_Tests : MatchdayCommandTests_Base
{
    #region Settings Display Tests

    [Test]
    public async Task Running_command_displays_model_name()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("gpt-4o");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_verbose_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Running_command_without_verbose_does_not_show_verbose_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Verbose mode enabled");
    }

    [Test]
    public async Task Running_command_with_override_kicktipp_shows_override_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--override-kicktipp");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Override mode enabled");
    }

    [Test]
    public async Task Running_command_with_override_database_shows_override_database_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--override-database");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Override database mode enabled");
    }

    [Test]
    public async Task Running_command_with_agent_mode_shows_agent_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Agent mode enabled");
    }

    [Test]
    public async Task Running_command_with_dry_run_shows_dry_run_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--dry-run");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run mode enabled");
    }

    [Test]
    public async Task Running_command_with_estimated_costs_shows_estimated_costs_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--estimated-costs", "o3");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Estimated costs will be calculated");
        await Assert.That(output).Contains("o3");
    }

    [Test]
    public async Task Running_command_with_justification_shows_justification_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--with-justification");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Justification output enabled");
    }

    [Test]
    public async Task Running_command_with_repredict_shows_reprediction_mode_message()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Reprediction mode enabled");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_shows_max_repredictions_value()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "3");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Reprediction mode enabled");
        await Assert.That(output).Contains("max repredictions: 3");
    }

    #endregion

    #region Settings Validation Error Tests

    [Test]
    public async Task Running_command_with_justification_and_agent_returns_error()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--with-justification", "--agent");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--with-justification cannot be used with --agent");
    }

    [Test]
    public async Task Running_command_with_override_database_and_repredict_returns_error()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--override-database", "--repredict");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--override-database cannot be used with reprediction flags");
    }

    [Test]
    public async Task Running_command_with_override_database_and_max_repredictions_returns_error()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--override-database", "--max-repredictions", "2");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--override-database cannot be used with reprediction flags");
    }

    [Test]
    public async Task Running_command_with_negative_max_repredictions_returns_error()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "-1");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--max-repredictions must be 0 or greater");
    }

    #endregion

    #region Community Context Tests

    [Test]
    public async Task Running_command_displays_community_name()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "my-test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using community:");
        await Assert.That(output).Contains("my-test-community");
    }

    [Test]
    public async Task Running_command_with_community_context_displays_both_community_and_context()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "my-community", "--community-context", "shared-context");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using community:");
        await Assert.That(output).Contains("my-community");
        await Assert.That(output).Contains("Using community context:");
        await Assert.That(output).Contains("shared-context");
    }

    #endregion
}
