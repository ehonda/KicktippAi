using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;
using NodaTime;
using NodaTime.Text;

namespace Orchestrator.Commands.Observability.Experiments;

internal sealed record PreparedExperimentRunOptions(
    string Model,
    string PromptKey,
    bool IncludeJustification,
    string? EvaluationTime,
    string? EvaluationPolicyKind,
    string? EvaluationPolicyOffset,
    string? DatasetName,
    string PromptSource,
    string? LangfusePromptName,
    string? LangfusePromptLabel,
    int? LangfusePromptVersion,
    string BatchStrategy,
    int? BatchSize = null,
    int? BatchCount = null,
    string? ReasoningEffort = null,
    int? MaxOutputTokenCount = null,
    int? Parallelism = null);

internal static class PreparedExperimentCommandSupport
{
    private static readonly DateTimeZone HistoricalSamplingZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
    private static readonly ZonedDateTimePattern HistoricalSamplingCutoffPattern =
        ZonedDateTimePattern.GeneralFormatOnlyIso.WithZoneProvider(DateTimeZoneProviders.Tzdb);

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<T> LoadJsonFileAsync<T>(string path, CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(path);
        var raw = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        var value = JsonSerializer.Deserialize<T>(raw, JsonOptions);
        return value ?? throw new InvalidOperationException($"JSON file '{absolutePath}' could not be deserialized.");
    }

    public static PreparedExperimentRunMetadata NormalizeRunMetadata(
        PreparedExperimentRunMetadata runMetadata,
        PreparedExperimentManifest manifest,
        PreparedExperimentRunOptions options)
    {
        if (!string.IsNullOrWhiteSpace(runMetadata.Model)
            && !string.Equals(runMetadata.Model, options.Model, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Run metadata model '{runMetadata.Model}' does not match requested model '{options.Model}'.");
        }

        var normalizedReasoningEffort = string.IsNullOrWhiteSpace(runMetadata.ReasoningEffort)
            ? options.ReasoningEffort
            : runMetadata.ReasoningEffort.Trim().ToLowerInvariant();
        var runSubjectId = string.IsNullOrWhiteSpace(runMetadata.RunSubjectId)
            ? string.IsNullOrWhiteSpace(normalizedReasoningEffort)
                ? options.Model
                : $"{options.Model}:reasoning-effort:{normalizedReasoningEffort}"
            : runMetadata.RunSubjectId;
        var runSubjectDisplayName = string.IsNullOrWhiteSpace(runMetadata.RunSubjectDisplayName)
            ? PreparedExperimentSupport.BuildRunSubjectDisplayName(options.Model, normalizedReasoningEffort)
            : runMetadata.RunSubjectDisplayName;

        var canonicalCompetition = CompetitionIds.Canonicalize(manifest.Competition);
        if (!string.IsNullOrWhiteSpace(runMetadata.Competition)
            && !string.Equals(CompetitionIds.Canonicalize(runMetadata.Competition), canonicalCompetition, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Run metadata competition does not match the prepared manifest competition.");
        }
        if (!string.IsNullOrWhiteSpace(runMetadata.CommunityContext)
            && !string.Equals(runMetadata.CommunityContext, manifest.CommunityContext, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Run metadata community context does not match the prepared manifest community context.");
        }

        var manifestSelectedItemIdsCount = manifest.SelectedItemIds.Count > 0
            ? manifest.SelectedItemIds.Count
            : manifest.Items.Count;
        var manifestSelectedItemIdsHash = string.IsNullOrWhiteSpace(manifest.SelectedItemIdsHash)
            ? ExperimentArtifactSupport.ComputeSelectedItemIdsHash(
                manifest.SelectedItemIds.Count > 0
                    ? manifest.SelectedItemIds
                    : manifest.Items.Select(item => item.SliceDatasetItemId))
            : manifest.SelectedItemIdsHash;
        var forceManifestSelectedItemIdentity = manifest.HistoricalCompatibility is not null;
        var normalizedSelectedItemIdsCount = forceManifestSelectedItemIdentity
            ? manifestSelectedItemIdsCount
            : runMetadata.SelectedItemIdsCount > 0
                ? runMetadata.SelectedItemIdsCount
                : manifestSelectedItemIdsCount;
        var normalizedSelectedItemIdsHash = forceManifestSelectedItemIdentity
            ? manifestSelectedItemIdsHash
            : string.IsNullOrWhiteSpace(runMetadata.SelectedItemIdsHash)
                ? manifestSelectedItemIdsHash
                : runMetadata.SelectedItemIdsHash;

        return runMetadata with
        {
            Runner = string.IsNullOrWhiteSpace(runMetadata.Runner) ? "match-experiment-runner" : runMetadata.Runner,
            TaskType = string.IsNullOrWhiteSpace(runMetadata.TaskType)
                ? PreparedExperimentSupport.ResolveTaskType(manifest)
                : runMetadata.TaskType,
            CommunityContext = string.IsNullOrWhiteSpace(runMetadata.CommunityContext)
                ? manifest.CommunityContext
                : runMetadata.CommunityContext,
            Model = options.Model,
            Competition = canonicalCompetition,
            SourceDatasetName = string.IsNullOrWhiteSpace(runMetadata.SourceDatasetName)
                ? manifest.SourceDatasetName
                : runMetadata.SourceDatasetName,
            DatasetName = string.IsNullOrWhiteSpace(runMetadata.DatasetName)
                ? options.DatasetName ?? manifest.SliceDatasetName
                : runMetadata.DatasetName,
            PromptKey = string.IsNullOrWhiteSpace(runMetadata.PromptKey) ? options.PromptKey : runMetadata.PromptKey,
            PromptSource = string.IsNullOrWhiteSpace(runMetadata.PromptSource) ? options.PromptSource : runMetadata.PromptSource,
            LangfusePromptName = string.IsNullOrWhiteSpace(runMetadata.LangfusePromptName) ? options.LangfusePromptName : runMetadata.LangfusePromptName,
            LangfusePromptLabel = string.IsNullOrWhiteSpace(runMetadata.LangfusePromptLabel) ? options.LangfusePromptLabel : runMetadata.LangfusePromptLabel,
            LangfusePromptVersion = runMetadata.LangfusePromptVersion ?? options.LangfusePromptVersion,
            ReasoningEffort = normalizedReasoningEffort,
            MaxOutputTokenCount = options.MaxOutputTokenCount ?? runMetadata.MaxOutputTokenCount,
            SliceKind = string.IsNullOrWhiteSpace(runMetadata.SliceKind) ? manifest.SliceKind : runMetadata.SliceKind,
            SliceKey = string.IsNullOrWhiteSpace(runMetadata.SliceKey) ? manifest.SliceKey : runMetadata.SliceKey,
            SourcePoolKey = string.IsNullOrWhiteSpace(runMetadata.SourcePoolKey) ? manifest.SourcePoolKey : runMetadata.SourcePoolKey,
            SelectedItemIdsCount = normalizedSelectedItemIdsCount,
            SelectedItemIdsHash = normalizedSelectedItemIdsHash,
            SampleSize = runMetadata.SampleSize > 0 ? runMetadata.SampleSize : manifest.SampleSize > 0 ? manifest.SampleSize : manifest.Items.Count,
            MatchCount = runMetadata.MatchCount ?? manifest.MatchCount,
            Repetitions = runMetadata.Repetitions ?? manifest.Repetitions,
            SampleSeed = runMetadata.SampleSeed ?? manifest.SampleSeed,
            SampleMethod = string.IsNullOrWhiteSpace(runMetadata.SampleMethod) ? manifest.SampleMethod : runMetadata.SampleMethod,
            HistoricalCompatibilityMode = manifest.HistoricalCompatibility?.Mode,
            OfficialKnowledgeCutoff = manifest.HistoricalCompatibility?.OfficialKnowledgeCutoff,
            SamplingCutoff = manifest.HistoricalCompatibility?.SamplingCutoff,
            HistoricalContextDocumentCount = manifest.HistoricalCompatibility?.ContextDocumentCount,
            HistoricalEligibilityPolicy = manifest.HistoricalCompatibility?.EligibilityPolicy,
            HistoricalEligibleFixtureCount = manifest.HistoricalCompatibility?.EligibleFixtureCount,
            HistoricalEligibleFixtureIdsHash = manifest.HistoricalCompatibility?.EligibleFixtureIdsHash,
            PromptVersion = string.IsNullOrWhiteSpace(runMetadata.PromptVersion)
                ? string.IsNullOrWhiteSpace(runMetadata.PromptKey) ? options.PromptKey : runMetadata.PromptKey
                : runMetadata.PromptVersion,
            SourceDatasetKind = string.IsNullOrWhiteSpace(runMetadata.SourceDatasetKind)
                ? PreparedExperimentSupport.ResolveTaskType(manifest)
                : runMetadata.SourceDatasetKind,
            DatasetItemIdMap = runMetadata.DatasetItemIdMap.Count > 0
                ? runMetadata.DatasetItemIdMap
                : PreparedExperimentSupport.CreateDatasetItemIdMap(manifest),
            BatchStrategy = string.IsNullOrWhiteSpace(runMetadata.BatchStrategy) ? options.BatchStrategy : runMetadata.BatchStrategy,
            BatchSize = options.BatchSize ?? runMetadata.BatchSize,
            BatchCount = options.BatchCount ?? runMetadata.BatchCount,
            Parallelism = options.Parallelism ?? runMetadata.Parallelism,
            RunSubjectId = runSubjectId,
            RunSubjectDisplayName = runSubjectDisplayName
        };
    }

    public static DateTimeOffset? ParseExplicitEvaluationTime(PreparedExperimentRunMetadata runMetadata)
    {
        if (string.IsNullOrWhiteSpace(runMetadata.EvaluationTime))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                runMetadata.EvaluationTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedRoundtrip))
        {
            return parsedRoundtrip;
        }

        return Commands.Observability.EvaluationTimeParser.Parse(runMetadata.EvaluationTime);
    }

    public static EvaluationTimestampPolicy ParseEvaluationTimestampPolicy(PreparedExperimentRunMetadata runMetadata)
    {
        if (runMetadata.EvaluationTimestampPolicy is null)
        {
            throw new InvalidOperationException("Run metadata must contain evaluationTimestampPolicy.");
        }

        if (!string.Equals(
                runMetadata.EvaluationTimestampPolicy.Reference,
                EvaluationTimestampPolicy.StartsAtReference,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Evaluation policy reference must be '{EvaluationTimestampPolicy.StartsAtReference}'.");
        }

        return EvaluationTimestampPolicyParser.Parse(
            runMetadata.EvaluationTimestampPolicy.Kind,
            runMetadata.EvaluationTimestampPolicy.Offset);
    }

    public static PreparedExperimentManifest ValidateManifest(PreparedExperimentManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Competition))
        {
            throw new InvalidOperationException("Slice manifest must contain a competition.");
        }

        var canonicalCompetition = CompetitionIds.Canonicalize(manifest.Competition);
        manifest = manifest with { Competition = canonicalCompetition };
        if (manifest.Items.Count == 0)
        {
            throw new InvalidOperationException("Slice manifest must contain at least one item.");
        }

        var seenHostedIds = new HashSet<string>(StringComparer.Ordinal);
        var hasHistoricalCompatibilityMarker = manifest.HistoricalCompatibility is not null
                                               || manifest.HistoricalArtifactSha256 is not null
                                               || manifest.Items.Any(item => item.HistoricalContextManifest is not null
                                                                             || item.ExpectedHomeGoals is not null
                                                                             || item.ExpectedAwayGoals is not null);
        if (string.Equals(canonicalCompetition, CompetitionIds.Bundesliga2025_26, StringComparison.Ordinal)
            && hasHistoricalCompatibilityMarker)
        {
            ValidateHistoricalCompatibility(manifest);
        }
        else if (!string.Equals(canonicalCompetition, CompetitionIds.Bundesliga2025_26, StringComparison.Ordinal)
                 && hasHistoricalCompatibilityMarker)
        {
            throw new InvalidOperationException(
                "Historical experiment compatibility provenance is only valid for bundesliga-2025-26.");
        }

        foreach (var item in manifest.Items)
        {
            if (string.IsNullOrWhiteSpace(item.SourceDatasetItemId))
            {
                throw new InvalidOperationException("Each slice manifest item must contain sourceDatasetItemId.");
            }

            if (string.IsNullOrWhiteSpace(item.SliceDatasetItemId))
            {
                throw new InvalidOperationException("Each slice manifest item must contain sliceDatasetItemId.");
            }

            if (!seenHostedIds.Add(item.SliceDatasetItemId))
            {
                throw new InvalidOperationException($"Duplicate slice dataset item id '{item.SliceDatasetItemId}' found in manifest.");
            }

            if (string.IsNullOrWhiteSpace(item.HomeTeam) || string.IsNullOrWhiteSpace(item.AwayTeam))
            {
                throw new InvalidOperationException("Each slice manifest item must contain non-empty homeTeam and awayTeam values.");
            }

            if (item.Matchday < 1)
            {
                throw new InvalidOperationException($"Slice manifest item '{item.SliceDatasetItemId}' has an invalid matchday.");
            }

            if (string.Equals(canonicalCompetition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
            {
                if (item.ResolvedContextManifest is null)
                {
                    throw new InvalidOperationException(
                        $"Prepared Bundesliga 2026/27 item '{item.SliceDatasetItemId}' is missing required immutable resolvedContextManifest.");
                }

                if (item.PredictionCreatedAt is null)
                {
                    throw new InvalidOperationException(
                        $"Prepared Bundesliga 2026/27 item '{item.SliceDatasetItemId}' is missing required predictionCreatedAt provenance.");
                }

                if (!string.Equals(item.ResolvedContextManifest.CommunityContext, manifest.CommunityContext, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Prepared Bundesliga 2026/27 item '{item.SliceDatasetItemId}' has a resolvedContextManifest with a different community scope.");
                }

                if (!string.Equals(item.ResolvedContextManifest.Competition, canonicalCompetition, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Prepared Bundesliga 2026/27 item '{item.SliceDatasetItemId}' has a resolvedContextManifest with a different competition scope.");
                }
            }
            else if (string.Equals(canonicalCompetition, CompetitionIds.Bundesliga2025_26, StringComparison.Ordinal)
                     && hasHistoricalCompatibilityMarker)
            {
                ValidateHistoricalItem(manifest, item);
            }
        }

        return manifest;
    }

    private static void ValidateHistoricalCompatibility(PreparedExperimentManifest manifest)
    {
        var compatibility = manifest.HistoricalCompatibility
            ?? throw new InvalidOperationException(
                "Bundesliga 2025/26 prepared experiments require an explicit historicalCompatibility contract.");
        if (!string.Equals(compatibility.Mode, ResolvedHistoricalExperimentContextManifest.LegacyIdHashV1, StringComparison.Ordinal)
            || !string.Equals(compatibility.BoundPromptSource, PreparedHistoricalExperimentCompatibility.PromptSource, StringComparison.Ordinal)
            || !string.Equals(compatibility.BoundPromptName, PreparedHistoricalExperimentCompatibility.PromptName, StringComparison.Ordinal)
            || !string.Equals(compatibility.BoundPromptLabel, PreparedHistoricalExperimentCompatibility.PromptLabel, StringComparison.Ordinal)
            || compatibility.BoundPromptVersion != PreparedHistoricalExperimentCompatibility.PromptVersion
            || !string.Equals(compatibility.BoundEvaluationPolicyKind, PreparedHistoricalExperimentCompatibility.EvaluationPolicyKind, StringComparison.Ordinal)
            || !string.Equals(compatibility.BoundEvaluationPolicyReference, PreparedHistoricalExperimentCompatibility.EvaluationPolicyReference, StringComparison.Ordinal)
            || !string.Equals(compatibility.BoundEvaluationPolicyOffset, PreparedHistoricalExperimentCompatibility.EvaluationPolicyOffset, StringComparison.Ordinal)
            || compatibility.ContextDocumentCount != 7
            || !string.Equals(compatibility.EligibilityPolicy, PreparedHistoricalExperimentCompatibility.RequiredEligibilityPolicy, StringComparison.Ordinal)
            || compatibility.EligibleFixtureCount < 1
            || !DocumentPublicationContract.IsLowercaseSha256(compatibility.EligibleFixtureIdsHash))
        {
            throw new InvalidOperationException(
                "Bundesliga 2025/26 historical compatibility route must bind the canonical legacy-ID hash mode, seven-document context, complete context-eligible pool, hosted Bundesliga match prompt v2/production, and startsAt -12h evaluation policy.");
        }

        if (!DateOnly.TryParseExact(
                compatibility.OfficialKnowledgeCutoff,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var officialCutoff))
        {
            throw new InvalidOperationException("Historical compatibility officialKnowledgeCutoff must use yyyy-MM-dd.");
        }

        if (string.IsNullOrWhiteSpace(manifest.StartsAfter)
            || !string.Equals(manifest.StartsAfter, compatibility.SamplingCutoff, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Historical compatibility samplingCutoff must exactly match the manifest startsAfter cutoff.");
        }

        var requiredSamplingCutoff = BuildRequiredHistoricalSamplingCutoff(officialCutoff);
        if (!string.Equals(compatibility.SamplingCutoff, requiredSamplingCutoff, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Historical compatibility samplingCutoff must be exactly the Europe/Berlin local midnight two days after officialKnowledgeCutoff: '{requiredSamplingCutoff}'.");
        }

        ValidateHistoricalTopology(manifest);

        if (manifest.MatchCount is not int matchCount
            || compatibility.EligibleFixtureCount < matchCount)
        {
            throw new InvalidOperationException(
                "Historical compatibility selected fixture count exceeds the bound complete context-eligible pool.");
        }

        if (!DocumentPublicationContract.IsLowercaseSha256(manifest.HistoricalArtifactSha256)
            || !string.Equals(
                manifest.HistoricalArtifactSha256,
                ComputeHistoricalArtifactSha256(manifest),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Historical experiment artifact hash does not match its cutoff, route, fixture, score, and context bindings.");
        }
    }

    public static string ComputeHistoricalArtifactSha256(PreparedExperimentManifest manifest)
    {
        var compatibility = manifest.HistoricalCompatibility
            ?? throw new InvalidOperationException("Cannot hash a historical artifact without historicalCompatibility.");
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            WriteHashField(writer, manifest.TaskType);
            WriteHashField(writer, manifest.Competition);
            WriteHashField(writer, manifest.CommunityContext);
            WriteHashField(writer, manifest.Season);
            WriteHashField(writer, manifest.SliceKey);
            WriteHashField(writer, manifest.SliceKind);
            WriteHashField(writer, manifest.SampleMethod);
            WriteHashField(writer, manifest.SourcePoolKey);
            WriteHashField(writer, manifest.SourceDatasetName);
            WriteHashField(writer, manifest.SliceDatasetName);
            WriteHashField(writer, manifest.SampleSeed?.ToString(CultureInfo.InvariantCulture));
            WriteHashField(writer, manifest.SampleSize.ToString(CultureInfo.InvariantCulture));
            WriteHashField(writer, manifest.MatchCount?.ToString(CultureInfo.InvariantCulture));
            WriteHashField(writer, manifest.Repetitions?.ToString(CultureInfo.InvariantCulture));
            foreach (var selectedItemId in manifest.SelectedItemIds)
            {
                WriteHashField(writer, selectedItemId);
            }
            WriteHashField(writer, manifest.SelectedItemIdsHash);
            WriteHashField(writer, manifest.StartsAfter);
            WriteHashField(writer, compatibility.Mode);
            WriteHashField(writer, compatibility.OfficialKnowledgeCutoff);
            WriteHashField(writer, compatibility.SamplingCutoff);
            WriteHashField(writer, compatibility.BoundPromptSource);
            WriteHashField(writer, compatibility.BoundPromptName);
            WriteHashField(writer, compatibility.BoundPromptLabel);
            WriteHashField(writer, compatibility.BoundPromptVersion.ToString(CultureInfo.InvariantCulture));
            WriteHashField(writer, compatibility.BoundEvaluationPolicyKind);
            WriteHashField(writer, compatibility.BoundEvaluationPolicyReference);
            WriteHashField(writer, compatibility.BoundEvaluationPolicyOffset);
            WriteHashField(writer, compatibility.ContextDocumentCount.ToString(CultureInfo.InvariantCulture));
            WriteHashField(writer, compatibility.EligibilityPolicy);
            WriteHashField(writer, compatibility.EligibleFixtureCount.ToString(CultureInfo.InvariantCulture));
            WriteHashField(writer, compatibility.EligibleFixtureIdsHash);
            foreach (var item in manifest.Items)
            {
                WriteHashField(writer, item.SourceDatasetItemId);
                WriteHashField(writer, item.SliceDatasetItemId);
                WriteHashField(writer, item.HomeTeam);
                WriteHashField(writer, item.AwayTeam);
                WriteHashField(writer, item.Matchday.ToString(CultureInfo.InvariantCulture));
                WriteHashField(writer, item.StartsAt);
                WriteHashField(writer, item.TippSpielId);
                WriteHashField(writer, item.FixtureIndex?.ToString(CultureInfo.InvariantCulture));
                WriteHashField(writer, item.RepetitionIndex?.ToString(CultureInfo.InvariantCulture));
                WriteHashField(writer, item.ExpectedHomeGoals?.ToString(CultureInfo.InvariantCulture));
                WriteHashField(writer, item.ExpectedAwayGoals?.ToString(CultureInfo.InvariantCulture));
                WriteHashField(writer, item.HistoricalContextManifest?.ManifestSha256);
            }
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload.ToArray())).ToLowerInvariant();
    }

    internal static string BuildRequiredHistoricalSamplingCutoff(DateOnly officialKnowledgeCutoff)
    {
        var localCutoffDate = new LocalDate(
            officialKnowledgeCutoff.Year,
            officialKnowledgeCutoff.Month,
            officialKnowledgeCutoff.Day).PlusDays(2);
        return HistoricalSamplingCutoffPattern.Format(HistoricalSamplingZone.AtStartOfDay(localCutoffDate));
    }

    private static void ValidateHistoricalTopology(PreparedExperimentManifest manifest)
    {
        if (!string.Equals(manifest.TaskType, "repeated-match-slice", StringComparison.Ordinal)
            || !string.Equals(manifest.SliceKind, "repeated-match-slice", StringComparison.Ordinal)
            || !string.Equals(manifest.SampleMethod, "repeated-match-slice", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(manifest.SliceKey)
            || string.IsNullOrWhiteSpace(manifest.SourcePoolKey)
            || string.IsNullOrWhiteSpace(manifest.SliceDatasetName)
            || !string.Equals(
                manifest.SourceDatasetName,
                ExperimentArtifactSupport.BuildSourceDatasetName(manifest.CommunityContext),
                StringComparison.Ordinal)
            || !string.Equals(manifest.Season, ExperimentArtifactSupport.Season, StringComparison.Ordinal)
            || manifest.SampleSeed is null)
        {
            throw new InvalidOperationException(
                "Historical compatibility requires complete repeated-match-slice task, dataset, season, slice, source-pool, and sample-seed provenance.");
        }

        if (manifest.MatchCount is not int matchCount || matchCount < 1
            || manifest.Repetitions is not int repetitions || repetitions < 1)
        {
            throw new InvalidOperationException(
                "Historical compatibility requires positive matchCount and repetitions values.");
        }

        var expectedSampleSize = checked(matchCount * repetitions);
        if (manifest.SampleSize != expectedSampleSize || manifest.Items.Count != expectedSampleSize)
        {
            throw new InvalidOperationException(
                "Historical compatibility requires sampleSize and item count to equal matchCount multiplied by repetitions.");
        }

        if (manifest.SelectedItemIds is null
            || manifest.SelectedItemIds.Count != matchCount
            || manifest.SelectedItemIds.Any(string.IsNullOrWhiteSpace)
            || manifest.SelectedItemIds.Distinct(StringComparer.Ordinal).Count() != matchCount)
        {
            throw new InvalidOperationException(
                "Historical compatibility selectedItemIds must contain exactly one unique source identity per fixture.");
        }

        var sourceIdsByFixture = new List<string>(matchCount);
        var tippSpielIds = new HashSet<string>(StringComparer.Ordinal);
        for (var fixtureIndex = 1; fixtureIndex <= matchCount; fixtureIndex += 1)
        {
            var fixtureItems = manifest.Items
                .Where(item => item.FixtureIndex == fixtureIndex)
                .OrderBy(item => item.RepetitionIndex)
                .ToList();
            if (fixtureItems.Count != repetitions
                || !fixtureItems.Select(item => item.RepetitionIndex).SequenceEqual(
                    Enumerable.Range(1, repetitions).Select(value => (int?)value)))
            {
                throw new InvalidOperationException(
                    $"Historical fixture index {fixtureIndex} must contain every repetition exactly once.");
            }

            var fixture = fixtureItems[0];
            if (string.IsNullOrWhiteSpace(fixture.TippSpielId)
                || !tippSpielIds.Add(fixture.TippSpielId))
            {
                throw new InvalidOperationException(
                    $"Historical fixture index {fixtureIndex} must bind a unique non-empty TippSpiel identity.");
            }

            var expectedSourceId = ExperimentArtifactSupport.BuildHostedDatasetItemId(
                manifest.Competition,
                manifest.CommunityContext,
                fixture.TippSpielId);
            if (!string.Equals(fixture.SourceDatasetItemId, expectedSourceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Historical fixture index {fixtureIndex} sourceDatasetItemId does not match its canonical competition/community/TippSpiel identity.");
            }

            foreach (var item in fixtureItems)
            {
                if (item.FixtureIndex is null
                    || item.RepetitionIndex is null
                    || string.IsNullOrWhiteSpace(item.TippSpielId)
                    || !string.Equals(item.SourceDatasetItemId, fixture.SourceDatasetItemId, StringComparison.Ordinal)
                    || !string.Equals(item.TippSpielId, fixture.TippSpielId, StringComparison.Ordinal)
                    || !string.Equals(item.HomeTeam, fixture.HomeTeam, StringComparison.Ordinal)
                    || !string.Equals(item.AwayTeam, fixture.AwayTeam, StringComparison.Ordinal)
                    || item.Matchday != fixture.Matchday
                    || !string.Equals(item.StartsAt, fixture.StartsAt, StringComparison.Ordinal)
                    || item.ExpectedHomeGoals != fixture.ExpectedHomeGoals
                    || item.ExpectedAwayGoals != fixture.ExpectedAwayGoals
                    || !string.Equals(
                        item.HistoricalContextManifest?.ManifestSha256,
                        fixture.HistoricalContextManifest?.ManifestSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Historical fixture index {fixtureIndex} contains partial or inconsistent repeated-fixture provenance.");
                }

                var expectedSliceId = ExperimentArtifactSupport.BuildRepeatedMatchSliceDatasetItemId(
                    expectedSourceId,
                    manifest.SliceKey,
                    fixtureIndex,
                    matchCount,
                    item.RepetitionIndex.Value,
                    repetitions);
                if (!string.Equals(item.SliceDatasetItemId, expectedSliceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Historical item '{item.SliceDatasetItemId}' does not match its generated repeated-match-slice identity.");
                }
            }

            sourceIdsByFixture.Add(expectedSourceId);
        }

        var expectedOrder = Enumerable.Range(1, matchCount)
            .SelectMany(fixtureIndex => Enumerable.Range(1, repetitions)
                .Select(repetitionIndex => (FixtureIndex: fixtureIndex, RepetitionIndex: repetitionIndex)));
        if (!manifest.Items
                .Select(item => (item.FixtureIndex ?? 0, item.RepetitionIndex ?? 0))
                .SequenceEqual(expectedOrder)
            || !manifest.SelectedItemIds.SequenceEqual(sourceIdsByFixture, StringComparer.Ordinal)
            || !DocumentPublicationContract.IsLowercaseSha256(manifest.SelectedItemIdsHash)
            || !string.Equals(
                manifest.SelectedItemIdsHash,
                ExperimentArtifactSupport.ComputeSelectedItemIdsHash(sourceIdsByFixture),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Historical compatibility fixture order, selectedItemIds, or selectedItemIdsHash does not match the generated topology.");
        }
    }

    private static void WriteHashField(BinaryWriter writer, string? value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void ValidateHistoricalItem(
        PreparedExperimentManifest manifest,
        PreparedExperimentManifestItem item)
    {
        if (item.ResolvedContextManifest is not null || item.PredictionCreatedAt is not null)
        {
            throw new InvalidOperationException(
                $"Historical item '{item.SliceDatasetItemId}' must not use the live Bundesliga 2026/27 context manifest fields.");
        }

        var historicalManifest = item.HistoricalContextManifest
            ?? throw new InvalidOperationException(
                $"Historical item '{item.SliceDatasetItemId}' is missing its hash-bound historicalContextManifest.");
        if (item.ExpectedHomeGoals is null or < 0 || item.ExpectedAwayGoals is null or < 0)
        {
            throw new InvalidOperationException(
                $"Historical item '{item.SliceDatasetItemId}' is missing a valid embedded completed score.");
        }

        var startsAt = Commands.Observability.EvaluationTimeParser.Parse(item.StartsAt);
        var samplingCutoff = Commands.Observability.EvaluationTimeParser.Parse(manifest.HistoricalCompatibility!.SamplingCutoff);
        if (startsAt <= samplingCutoff)
        {
            throw new InvalidOperationException(
                $"Historical item '{item.SliceDatasetItemId}' does not start strictly after the bound sampling cutoff.");
        }

        if (historicalManifest.EvaluationTimestamp != startsAt.AddHours(-12))
        {
            throw new InvalidOperationException(
                $"Historical item '{item.SliceDatasetItemId}' context timestamp is not exactly startsAt -12h.");
        }

        ResolvedHistoricalExperimentContextManifest.ValidateForMatch(
            historicalManifest,
            new Match(item.HomeTeam, item.AwayTeam, default, item.Matchday),
            manifest.CommunityContext);
    }

    public static void EnsureTaskType(PreparedExperimentManifest manifest, string expectedTaskType)
    {
        var actualTaskType = PreparedExperimentSupport.ResolveTaskType(manifest);
        if (!string.Equals(actualTaskType, expectedTaskType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The manifest describes a '{actualTaskType}' dataset, but this command expects '{expectedTaskType}'.");
        }
    }
}
