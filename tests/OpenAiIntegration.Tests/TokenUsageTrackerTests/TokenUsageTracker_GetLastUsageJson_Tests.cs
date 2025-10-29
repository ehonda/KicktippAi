using System.Text.Json;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker GetLastUsageJson method
/// </summary>
public class TokenUsageTracker_GetLastUsageJson_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task GetLastUsageJson_with_no_usage_returns_null()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);

        // Act
        var json = tracker.GetLastUsageJson();

        // Assert
        await Assert.That(json).IsNull();
    }

    [Test]
    public async Task GetLastUsageJson_with_basic_usage_returns_valid_json()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var json = tracker.GetLastUsageJson();

        // Assert
        await Assert.That(json).IsNotNull();
        
        var data = JsonSerializer.Deserialize<JsonElement>(json!);
        await Assert.That(data.GetProperty("InputTokenCount").GetInt32()).IsEqualTo(1000);
        await Assert.That(data.GetProperty("OutputTokenCount").GetInt32()).IsEqualTo(500);
    }

    [Test]
    public async Task GetLastUsageJson_with_cached_tokens_includes_input_details()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500,
            cachedInputTokens: 600);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var json = tracker.GetLastUsageJson();

        // Assert
        await Assert.That(json).IsNotNull();
        
        var data = JsonSerializer.Deserialize<JsonElement>(json!);
        await Assert.That(data.GetProperty("InputTokenCount").GetInt32()).IsEqualTo(1000);
        await Assert.That(data.GetProperty("InputTokenDetails").GetProperty("CachedTokenCount").GetInt32()).IsEqualTo(600);
    }

    [Test]
    public async Task GetLastUsageJson_with_reasoning_tokens_includes_output_details()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 1500,
            reasoningTokens: 1000);

        // Act
        tracker.AddUsage("o3", usage);
        var json = tracker.GetLastUsageJson();

        // Assert
        await Assert.That(json).IsNotNull();
        
        var data = JsonSerializer.Deserialize<JsonElement>(json!);
        await Assert.That(data.GetProperty("OutputTokenCount").GetInt32()).IsEqualTo(1500);
        await Assert.That(data.GetProperty("OutputTokenDetails").GetProperty("ReasoningTokenCount").GetInt32()).IsEqualTo(1000);
    }

    [Test]
    public async Task GetLastUsageJson_with_all_token_types_includes_all_details()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage = CreateChatTokenUsage(
            inputTokens: 5000,
            outputTokens: 3000,
            cachedInputTokens: 2000,
            reasoningTokens: 1500);

        // Act
        tracker.AddUsage("o3", usage);
        var json = tracker.GetLastUsageJson();

        // Assert
        await Assert.That(json).IsNotNull();
        
        var data = JsonSerializer.Deserialize<JsonElement>(json!);
        await Assert.That(data.GetProperty("InputTokenCount").GetInt32()).IsEqualTo(5000);
        await Assert.That(data.GetProperty("OutputTokenCount").GetInt32()).IsEqualTo(3000);
        await Assert.That(data.GetProperty("InputTokenDetails").GetProperty("CachedTokenCount").GetInt32()).IsEqualTo(2000);
        await Assert.That(data.GetProperty("OutputTokenDetails").GetProperty("ReasoningTokenCount").GetInt32()).IsEqualTo(1500);
    }

    [Test]
    public async Task GetLastUsageJson_returns_only_last_usage()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage1 = CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = CreateChatTokenUsage(inputTokens: 2000, outputTokens: 1000);
        var usage3 = CreateChatTokenUsage(inputTokens: 3000, outputTokens: 1500);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("gpt-4o", usage2);
        tracker.AddUsage("gpt-4o", usage3);
        var json = tracker.GetLastUsageJson();

        // Assert - Should show only usage3
        await Assert.That(json).IsNotNull();
        
        var data = JsonSerializer.Deserialize<JsonElement>(json!);
        await Assert.That(data.GetProperty("InputTokenCount").GetInt32()).IsEqualTo(3000);
        await Assert.That(data.GetProperty("OutputTokenCount").GetInt32()).IsEqualTo(1500);
    }

    [Test]
    public async Task GetLastUsageJson_without_cached_tokens_has_null_input_details()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var json = tracker.GetLastUsageJson();

        // Assert
        await Assert.That(json).IsNotNull();
        
        var data = JsonSerializer.Deserialize<JsonElement>(json!);
        var hasInputDetails = data.TryGetProperty("InputTokenDetails", out var inputDetails);
        if (hasInputDetails)
        {
            await Assert.That(inputDetails.ValueKind).IsEqualTo(JsonValueKind.Null);
        }
    }

    [Test]
    public async Task GetLastUsageJson_without_reasoning_tokens_has_null_output_details()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var json = tracker.GetLastUsageJson();

        // Assert
        await Assert.That(json).IsNotNull();
        
        var data = JsonSerializer.Deserialize<JsonElement>(json!);
        var hasOutputDetails = data.TryGetProperty("OutputTokenDetails", out var outputDetails);
        if (hasOutputDetails)
        {
            await Assert.That(outputDetails.ValueKind).IsEqualTo(JsonValueKind.Null);
        }
    }

    [Test]
    public async Task GetLastUsageJson_returns_compact_json_without_indentation()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var json = tracker.GetLastUsageJson();

        // Assert - Should not contain newlines (compact format)
        await Assert.That(json).IsNotNull();
        await Assert.That(json!.Contains('\n')).IsFalse();
    }
}
