using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.SyncDataset;

public sealed class SyncDatasetCommand : AsyncCommand<SyncDatasetSettings>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonElement DefaultInputSchema = ParseJsonElement(
        """
        {
          "type": "object",
          "properties": {
            "homeTeam": {
              "type": "string",
              "minLength": 1,
              "description": "Exact home team name from the persisted match outcome"
            },
            "awayTeam": {
              "type": "string",
              "minLength": 1,
              "description": "Exact away team name from the persisted match outcome"
            },
            "startsAt": {
              "type": "string",
              "minLength": 1,
              "description": "Localized match start timestamp string emitted by the .NET exporter"
            }
          },
          "required": ["homeTeam", "awayTeam", "startsAt"],
          "additionalProperties": false
        }
        """);

    private static readonly JsonElement DefaultExpectedOutputSchema = ParseJsonElement(
        """
        {
          "type": "object",
          "properties": {
            "homeGoals": {
              "type": "integer",
              "minimum": 0,
              "description": "Actual home goals scored in the completed match"
            },
            "awayGoals": {
              "type": "integer",
              "minimum": 0,
              "description": "Actual away goals scored in the completed match"
            }
          },
          "required": ["homeGoals", "awayGoals"],
          "additionalProperties": false
        }
        """);

    private static readonly JsonElement MetadataSchema = ParseJsonElement(
        """
        {
          "type": "object",
          "properties": {
            "competition": {
              "type": "string",
              "minLength": 1,
              "description": "Competition identifier"
            },
            "season": {
              "type": "string",
              "minLength": 1,
              "description": "Season label"
            },
            "communityContext": {
              "type": "string",
              "minLength": 1,
              "description": "Community context slug"
            },
            "matchday": {
              "type": "integer",
              "minimum": 1,
              "description": "Bundesliga matchday number"
            },
            "matchdayLabel": {
              "type": "string",
              "minLength": 1,
              "description": "Human-readable matchday label"
            },
            "homeTeam": {
              "type": "string",
              "minLength": 1,
              "description": "Exact home team name"
            },
            "awayTeam": {
              "type": "string",
              "minLength": 1,
              "description": "Exact away team name"
            },
            "tippSpielId": {
              "type": "string",
              "minLength": 1,
              "description": "Kicktipp match identifier"
            }
          },
          "required": [
            "competition",
            "season",
            "communityContext",
            "matchday",
            "matchdayLabel",
            "homeTeam",
            "awayTeam",
            "tippSpielId"
          ],
          "additionalProperties": false
        }
        """);

    private readonly IAnsiConsole _console;
    private readonly ILangfusePublicApiClient _langfuseClient;
    private readonly ILogger<SyncDatasetCommand> _logger;

    public SyncDatasetCommand(
        IAnsiConsole console,
        ILangfusePublicApiClient langfuseClient,
        ILogger<SyncDatasetCommand> logger)
    {
        _console = console;
        _langfuseClient = langfuseClient;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SyncDatasetSettings settings)
    {
        try
        {
            var artifact = await LoadArtifactAsync(settings.InputPath, CancellationToken.None);
            var datasetName = settings.DatasetName ?? artifact.DatasetName;
            if (string.IsNullOrWhiteSpace(datasetName))
            {
                throw new InvalidOperationException("Dataset name missing from artifact and no --dataset-name override was provided.");
            }

            ValidateArtifact(artifact, datasetName);

            var datasetDefinition = BuildDatasetDefinition(artifact, datasetName);
            var dataset = settings.DryRun
                ? CreateDryRunDataset(datasetDefinition)
                : await CreateOrUpdateDatasetAsync(datasetDefinition, CancellationToken.None);

            var created = 0;
            var updated = 0;
            var unchanged = 0;
            var failures = new List<object>();

            foreach (var item in artifact.Items)
            {
                if (settings.DryRun)
                {
                    created += 1;
                    continue;
                }

                try
                {
                    var disposition = await SyncDatasetItemAsync(datasetName, item, CancellationToken.None);
                    switch (disposition)
                    {
                        case SyncDisposition.Created:
                            created += 1;
                            break;
                        case SyncDisposition.Updated:
                            updated += 1;
                            break;
                        default:
                            unchanged += 1;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(new
                    {
                        itemId = item.Id,
                        message = ex.Message
                    });
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException($"Dataset sync failed for {failures.Count} item(s): {JsonSerializer.Serialize(failures, SerializerOptions)}");
            }

            var summary = new
            {
                datasetName,
                datasetId = dataset.Id,
                datasetInputSchemaKeys = GetSchemaKeys(dataset.InputSchema),
                datasetExpectedOutputSchemaKeys = GetSchemaKeys(dataset.ExpectedOutputSchema),
                dryRun = settings.DryRun,
                itemCount = artifact.Items.Count,
                created,
                updated,
                unchanged,
                firstItemId = artifact.Items.FirstOrDefault()?.Id,
                lastItemId = artifact.Items.LastOrDefault()?.Id
            };

            _console.WriteLine(JsonSerializer.Serialize(summary, SerializerOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing sync-dataset command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private async Task<SyncDisposition> SyncDatasetItemAsync(
        string datasetName,
        SyncDatasetArtifactItem item,
        CancellationToken cancellationToken)
    {
        var existingItem = await _langfuseClient.GetDatasetItemAsync(item.Id, cancellationToken);

        if (existingItem is not null && HasSameCanonicalContent(existingItem, item, datasetName))
        {
            return SyncDisposition.Unchanged;
        }

        await _langfuseClient.CreateDatasetItemAsync(
            new LangfuseCreateDatasetItemRequest(
                item.Id,
                datasetName,
                item.Input,
                item.ExpectedOutput,
                item.Metadata),
            cancellationToken);

        return existingItem is null ? SyncDisposition.Created : SyncDisposition.Updated;
    }

    private static bool HasSameCanonicalContent(LangfuseDatasetItem existingItem, SyncDatasetArtifactItem item, string datasetName)
    {
        if (!string.IsNullOrWhiteSpace(existingItem.DatasetName)
            && !string.Equals(existingItem.DatasetName, datasetName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Dataset item '{item.Id}' already exists in dataset '{existingItem.DatasetName}', not '{datasetName}'.");
        }

        return LangfuseJsonUtilities.StableEquals(existingItem.Input, item.Input)
            && LangfuseJsonUtilities.StableEquals(existingItem.ExpectedOutput, item.ExpectedOutput)
            && LangfuseJsonUtilities.StableEquals(existingItem.Metadata, item.Metadata);
    }

    private async Task<LangfuseDataset> CreateOrUpdateDatasetAsync(
        LangfuseCreateDatasetRequest datasetDefinition,
        CancellationToken cancellationToken)
    {
        var createdDataset = await _langfuseClient.CreateDatasetAsync(datasetDefinition, cancellationToken);
        return createdDataset with
        {
            InputSchema = LangfuseJsonUtilities.IsDefined(createdDataset.InputSchema)
                ? createdDataset.InputSchema
                : datasetDefinition.InputSchema ?? default,
            ExpectedOutputSchema = LangfuseJsonUtilities.IsDefined(createdDataset.ExpectedOutputSchema)
                ? createdDataset.ExpectedOutputSchema
                : datasetDefinition.ExpectedOutputSchema ?? default
        };
    }

    private static LangfuseDataset CreateDryRunDataset(LangfuseCreateDatasetRequest datasetDefinition)
    {
        return new LangfuseDataset(
            string.Empty,
            datasetDefinition.Name,
            datasetDefinition.Description,
            datasetDefinition.Metadata is JsonElement metadata ? metadata : default,
            datasetDefinition.InputSchema ?? default,
            datasetDefinition.ExpectedOutputSchema ?? default);
    }

    private static LangfuseCreateDatasetRequest BuildDatasetDefinition(SyncDatasetArtifact artifact, string datasetName)
    {
        var firstItemMetadata = artifact.Items.FirstOrDefault()?.Metadata ?? default;
        var competition = GetStringProperty(firstItemMetadata, "competition") ?? "bundesliga-2025-26";
        var season = GetStringProperty(firstItemMetadata, "season") ?? "2025/2026";
        var communityContext = GetStringProperty(firstItemMetadata, "communityContext")
            ?? datasetName.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

        var datasetMetadata = LangfuseJsonUtilities.IsDefined(artifact.DatasetMetadata)
            ? artifact.DatasetMetadata
            : JsonSerializer.SerializeToElement(new
            {
                competition,
                communityContext,
                scope = "match-centric",
                season
            }, SerializerOptions);

        return new LangfuseCreateDatasetRequest(
            datasetName,
            artifact.DatasetDescription
            ?? $"Hosted dataset for {season} {communityContext} {competition} match experiments",
            datasetMetadata,
            LangfuseJsonUtilities.IsDefined(artifact.InputSchema) ? artifact.InputSchema : DefaultInputSchema,
            LangfuseJsonUtilities.IsDefined(artifact.ExpectedOutputSchema)
                ? artifact.ExpectedOutputSchema
                : DefaultExpectedOutputSchema);
    }

    private static void ValidateArtifact(SyncDatasetArtifact artifact, string datasetName)
    {
        Assert(!string.IsNullOrWhiteSpace(datasetName), "Dataset name must be a non-empty string.");

        var inputSchema = LangfuseJsonUtilities.IsDefined(artifact.InputSchema)
            ? artifact.InputSchema
            : DefaultInputSchema;
        var expectedOutputSchema = LangfuseJsonUtilities.IsDefined(artifact.ExpectedOutputSchema)
            ? artifact.ExpectedOutputSchema
            : DefaultExpectedOutputSchema;

        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in artifact.Items)
        {
            Assert(!string.IsNullOrWhiteSpace(item.Id), "Each dataset item must have a non-empty string id.");
            Assert(seenItemIds.Add(item.Id), $"Duplicate dataset item id '{item.Id}' found in artifact.");

            ValidateValueAgainstSchema(item.Input, inputSchema, $"Dataset item '{item.Id}' input");
            ValidateValueAgainstSchema(item.ExpectedOutput, expectedOutputSchema, $"Dataset item '{item.Id}' expectedOutput");
            ValidateValueAgainstSchema(item.Metadata, MetadataSchema, $"Dataset item '{item.Id}' metadata");
        }
    }

    private static void ValidateValueAgainstSchema(JsonElement value, JsonElement schema, string label)
    {
        Assert(schema.ValueKind == JsonValueKind.Object, $"{label} schema must be an object.");

        var schemaType = schema.GetProperty("type").GetString();
        switch (schemaType)
        {
            case "object":
                Assert(value.ValueKind == JsonValueKind.Object, $"{label} must be an object.");

                var properties = schema.TryGetProperty("properties", out var propertyNode)
                    ? propertyNode
                    : default;
                var required = schema.TryGetProperty("required", out var requiredNode)
                    ? requiredNode.EnumerateArray().Select(entry => entry.GetString()!).ToHashSet(StringComparer.Ordinal)
                    : [];
                var actualProperties = value.EnumerateObject().ToList();
                var allowedKeys = properties.ValueKind == JsonValueKind.Object
                    ? properties.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
                    : [];

                if (schema.TryGetProperty("additionalProperties", out var additionalProperties)
                    && additionalProperties.ValueKind == JsonValueKind.False)
                {
                    var unexpectedKeys = actualProperties
                        .Select(property => property.Name)
                        .Where(name => !allowedKeys.Contains(name))
                        .ToArray();
                    Assert(
                        unexpectedKeys.Length == 0,
                        $"{label} must not contain unexpected keys: {string.Join(", ", unexpectedKeys)}.");
                }

                foreach (var requiredKey in required)
                {
                    Assert(value.TryGetProperty(requiredKey, out _), $"{label}.{requiredKey} is required.");
                }

                if (properties.ValueKind == JsonValueKind.Object)
                {
                    foreach (var actualProperty in actualProperties)
                    {
                        if (properties.TryGetProperty(actualProperty.Name, out var propertySchema))
                        {
                            ValidateValueAgainstSchema(actualProperty.Value, propertySchema, $"{label}.{actualProperty.Name}");
                        }
                    }
                }

                return;

            case "string":
                Assert(value.ValueKind == JsonValueKind.String, $"{label} must be a non-empty string.");
                var stringValue = value.GetString() ?? string.Empty;
                Assert(!string.IsNullOrWhiteSpace(stringValue), $"{label} must be a non-empty string.");

                if (schema.TryGetProperty("minLength", out var minLengthNode))
                {
                    Assert(stringValue.Length >= minLengthNode.GetInt32(), $"{label} must be at least {minLengthNode.GetInt32()} characters long.");
                }

                if (schema.TryGetProperty("maxLength", out var maxLengthNode))
                {
                    Assert(stringValue.Length <= maxLengthNode.GetInt32(), $"{label} must be at most {maxLengthNode.GetInt32()} characters long.");
                }

                return;

            case "integer":
                Assert(value.ValueKind == JsonValueKind.Number, $"{label} must be an integer.");
                if (!value.TryGetInt64(out var integerValue))
                {
                    throw new InvalidOperationException($"{label} must be an integer.");
                }

                if (schema.TryGetProperty("minimum", out var minimumIntegerNode))
                {
                    Assert(integerValue >= minimumIntegerNode.GetInt64(), $"{label} must be at least {minimumIntegerNode.GetInt64()}.");
                }

                if (schema.TryGetProperty("maximum", out var maximumIntegerNode))
                {
                    Assert(integerValue <= maximumIntegerNode.GetInt64(), $"{label} must be at most {maximumIntegerNode.GetInt64()}.");
                }

                return;

            case "number":
                Assert(value.ValueKind == JsonValueKind.Number, $"{label} must be a number.");
                if (!value.TryGetDouble(out var numberValue))
                {
                    throw new InvalidOperationException($"{label} must be a number.");
                }

                if (schema.TryGetProperty("minimum", out var minimumNumberNode))
                {
                    Assert(numberValue >= minimumNumberNode.GetDouble(), $"{label} must be at least {minimumNumberNode.GetDouble()}.");
                }

                if (schema.TryGetProperty("maximum", out var maximumNumberNode))
                {
                    Assert(numberValue <= maximumNumberNode.GetDouble(), $"{label} must be at most {maximumNumberNode.GetDouble()}.");
                }

                return;

            case "boolean":
                Assert(value.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{label} must be a boolean.");
                return;

            default:
                throw new InvalidOperationException($"Unsupported schema type '{schemaType}' for {label}.");
        }
    }

    private static IReadOnlyList<string> GetSchemaKeys(JsonElement schema)
    {
        if (!LangfuseJsonUtilities.IsDefined(schema)
            || schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return properties.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!LangfuseJsonUtilities.IsDefined(element)
            || element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static async Task<SyncDatasetArtifact> LoadArtifactAsync(string inputPath, CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(inputPath);
        var raw = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        var artifact = JsonSerializer.Deserialize<SyncDatasetArtifact>(raw, SerializerOptions);
        return artifact ?? throw new InvalidOperationException($"Dataset artifact '{absolutePath}' could not be deserialized.");
    }

    private static JsonElement ParseJsonElement(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private enum SyncDisposition
    {
        Created,
        Updated,
        Unchanged
    }

    private sealed record SyncDatasetArtifact(
        [property: JsonPropertyName("datasetName")] string? DatasetName,
        [property: JsonPropertyName("datasetDescription")] string? DatasetDescription,
        [property: JsonPropertyName("datasetMetadata")] JsonElement DatasetMetadata,
        [property: JsonPropertyName("inputSchema")] JsonElement InputSchema,
        [property: JsonPropertyName("expectedOutputSchema")] JsonElement ExpectedOutputSchema,
        [property: JsonPropertyName("items")] IReadOnlyList<SyncDatasetArtifactItem> Items);

    private sealed record SyncDatasetArtifactItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("input")] JsonElement Input,
        [property: JsonPropertyName("expectedOutput")] JsonElement ExpectedOutput,
        [property: JsonPropertyName("metadata")] JsonElement Metadata);
}