using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Observability.Cost;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.CostCommandTests;

/// <summary>
/// Tests for configuration file loading and merging in CostCommand.
/// </summary>
public class CostCommand_ConfigFile_Tests : CostCommandTests_Base
{
    [Test]
    public async Task Loading_config_from_valid_json_file_applies_settings()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3, 4, 5 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var config = new CostConfiguration
        {
            Matchdays = "1,2",
            Models = "gpt-4o",
            CommunityContexts = "test-community",
            Verbose = true
        };
        var configPath = CreateConfigFile(config);

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", configPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Matchdays: 1, 2");
        await Assert.That(output).Contains("Models: gpt-4o");
        await Assert.That(output).Contains("Community Contexts: test-community");
    }

    [Test]
    public async Task Loading_config_from_nonexistent_file_returns_error()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var nonexistentPath = Path.Combine(TestDirectory, "does-not-exist.json");

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", nonexistentPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Configuration file not found");
    }

    [Test]
    public async Task Loading_config_from_invalid_json_returns_error()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var configPath = CreateRawConfigFile("{ invalid json content }");

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", configPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Invalid JSON in configuration file");
    }

    [Test]
    public async Task Merging_cli_options_overrides_file_config()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3, 4, 5 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var (app, console, _, repo, _) = CreateCostCommandApp(mockRepo);

        var config = new CostConfiguration
        {
            Matchdays = "1,2",
            Models = "gpt-4o",
            CommunityContexts = "test-community",
            Verbose = true
        };
        var configPath = CreateConfigFile(config);

        // Act - CLI overrides matchdays
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--matchdays", "3,4,5"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Matchdays: 3, 4, 5");
        // Other file settings should still apply
        await Assert.That(output).Contains("Models: gpt-4o");
        await Assert.That(output).Contains("Community Contexts: test-community");
    }

    [Test]
    public async Task Merging_preserves_file_values_when_cli_not_specified()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3, 4, 5 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var config = new CostConfiguration
        {
            Matchdays = "1,2,3",
            Models = "o1-mini",
            CommunityContexts = "prod-community",
            Verbose = true,
            Bonus = true
        };
        var configPath = CreateConfigFile(config);

        // Act - Only specify --file, all settings from file should apply
        var exitCode = await app.RunAsync(["cost", "--file", configPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Matchdays: 1, 2, 3");
        await Assert.That(output).Contains("Models: o1-mini");
        await Assert.That(output).Contains("Community Contexts: prod-community");
        await Assert.That(output).Contains("Include Bonus: True");
    }

    [Test]
    public async Task Merging_cli_all_flag_overrides_file_filters()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3, 4, 5 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var config = new CostConfiguration
        {
            Matchdays = "1,2",
            Models = "gpt-4o",
            CommunityContexts = "test-community",
            Verbose = true
        };
        var configPath = CreateConfigFile(config);

        // Act - CLI --all overrides all filters
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--all"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All mode enabled");
        // All matchdays, models, and contexts should be used
        await Assert.That(output).Contains("all (5 found)");  // matchdays
        await Assert.That(output).Contains("all (2 found)");  // models
    }

    [Test]
    public async Task Config_file_with_all_flag_set_uses_all_data()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3, 4, 5 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Config file with All = true (not from CLI)
        var config = new CostConfiguration
        {
            All = true,
            Verbose = true
        };
        var configPath = CreateConfigFile(config);

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", configPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All mode enabled");
        // All matchdays, models, and contexts should be used
        await Assert.That(output).Contains("all (5 found)");  // matchdays
        await Assert.That(output).Contains("all (2 found)");  // models
    }

    [Test]
    public async Task Config_file_with_detailed_breakdown_shows_detailed_table()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (0.50, 5) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var config = new CostConfiguration
        {
            DetailedBreakdown = true
        };
        var configPath = CreateConfigFile(config);

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", configPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Detailed breakdown table should have these columns (may be truncated by Spectre.Console)
        await Assert.That(output).Contains("Commun"); // Community Context may be truncated
        await Assert.That(output).Contains("Model");
        await Assert.That(output).Contains("Catego"); // Category may be truncated
        await Assert.That(output).Contains("Index 0");
    }

    [Test]
    public async Task Config_file_with_null_values_uses_defaults()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Config with only matchdays set, everything else null
        var config = new CostConfiguration
        {
            Matchdays = "1"
        };
        var configPath = CreateConfigFile(config);

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Should use default false for bonus, verbose off in file but CLI overrides
        await Assert.That(output).Contains("Include Bonus: False");
    }

    [Test]
    public async Task Config_file_with_comments_parses_correctly()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var jsonWithComments = """
            {
                // This is a comment
                "matchdays": "1,2",
                "models": "gpt-4o",
                /* Multi-line
                   comment */
                "verbose": true
            }
            """;
        var configPath = CreateRawConfigFile(jsonWithComments);

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", configPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Matchdays: 1, 2");
    }

    [Test]
    public async Task Config_file_with_trailing_commas_parses_correctly()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var jsonWithTrailingCommas = """
            {
                "matchdays": "1,2",
                "models": "gpt-4o",
                "verbose": true,
            }
            """;
        var configPath = CreateRawConfigFile(jsonWithTrailingCommas);

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", configPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Matchdays: 1, 2");
    }

    [Test]
    public async Task Config_file_with_case_insensitive_properties_parses_correctly()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        var jsonWithDifferentCasing = """
            {
                "MATCHDAYS": "1,2",
                "Models": "gpt-4o",
                "VERBOSE": true
            }
            """;
        var configPath = CreateRawConfigFile(jsonWithDifferentCasing);

        // Act
        var exitCode = await app.RunAsync(["cost", "--file", configPath]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Matchdays: 1, 2");
    }

    [Test]
    public async Task Merging_cli_matchdays_with_verbose_logs_override()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3, 4, 5 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var logger = new FakeLogger<CostCommand>();
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo, logger: logger);

        var config = new CostConfiguration
        {
            Matchdays = "1,2",
            Verbose = true  // Enable verbose mode from file
        };
        var configPath = CreateConfigFile(config);

        // Act - CLI overrides matchdays
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--matchdays", "3,4,5"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        var hasOverrideLog = logger.Collector.GetSnapshot().Any(log => 
            log.Message.Contains("CLI override: Matchdays"));
        await Assert.That(hasOverrideLog).IsTrue();
    }

    [Test]
    public async Task Merging_cli_bonus_with_verbose_logs_override()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var logger = new FakeLogger<CostCommand>();
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo, logger: logger);

        var config = new CostConfiguration
        {
            Verbose = true  // Enable verbose mode from file
        };
        var configPath = CreateConfigFile(config);

        // Act - CLI sets bonus flag
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--bonus"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        var hasOverrideLog = logger.Collector.GetSnapshot().Any(log => 
            log.Message.Contains("CLI override: Bonus"));
        await Assert.That(hasOverrideLog).IsTrue();
    }

    [Test]
    public async Task Merging_cli_models_with_verbose_logs_override()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community" });
        var logger = new FakeLogger<CostCommand>();
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo, logger: logger);

        var config = new CostConfiguration
        {
            Models = "gpt-4o",
            Verbose = true  // Enable verbose mode from file
        };
        var configPath = CreateConfigFile(config);

        // Act - CLI overrides models
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--models", "o1-mini"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        var hasOverrideLog = logger.Collector.GetSnapshot().Any(log => 
            log.Message.Contains("CLI override: Models"));
        await Assert.That(hasOverrideLog).IsTrue();
    }

    [Test]
    public async Task Merging_cli_community_contexts_with_verbose_logs_override()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var logger = new FakeLogger<CostCommand>();
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo, logger: logger);

        var config = new CostConfiguration
        {
            CommunityContexts = "test-community",
            Verbose = true  // Enable verbose mode from file
        };
        var configPath = CreateConfigFile(config);

        // Act - CLI overrides community contexts
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--community-contexts", "prod-community"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        var hasOverrideLog = logger.Collector.GetSnapshot().Any(log => 
            log.Message.Contains("CLI override: CommunityContexts"));
        await Assert.That(hasOverrideLog).IsTrue();
    }

    [Test]
    public async Task Merging_cli_all_flag_with_verbose_logs_override()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var logger = new FakeLogger<CostCommand>();
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo, logger: logger);

        var config = new CostConfiguration
        {
            Matchdays = "1,2",
            Verbose = true  // Enable verbose mode from file
        };
        var configPath = CreateConfigFile(config);

        // Act - CLI sets all flag
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--all"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        var hasOverrideLog = logger.Collector.GetSnapshot().Any(log => 
            log.Message.Contains("CLI override: All"));
        await Assert.That(hasOverrideLog).IsTrue();
    }

    [Test]
    public async Task Merging_cli_detailed_breakdown_with_verbose_logs_override()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var logger = new FakeLogger<CostCommand>();
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo, logger: logger);

        var config = new CostConfiguration
        {
            Verbose = true  // Enable verbose mode from file
        };
        var configPath = CreateConfigFile(config);

        // Act - CLI sets detailed-breakdown flag
        var exitCode = await app.RunAsync(["cost", "--file", configPath, "--detailed-breakdown"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        var hasOverrideLog = logger.Collector.GetSnapshot().Any(log => 
            log.Message.Contains("CLI override: DetailedBreakdown"));
        await Assert.That(hasOverrideLog).IsTrue();
    }
}
