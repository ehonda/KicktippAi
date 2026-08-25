using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Describes why a stored bonus prediction can or cannot be copied to a target question.
/// Values are stable payload-safe telemetry codes.
/// </summary>
public enum BonusPredictionCopyCompatibility
{
    Compatible = 0,
    QuestionMismatch = 1,
    MaxSelectionsMismatch = 2,
    OptionSetMismatch = 3,
    InvalidSourceSelection = 4
}

/// <summary>An immutable source option used to translate a copied prediction to target option IDs.</summary>
public sealed record BonusQuestionOptionProvenance(
    string SourceOptionId,
    string NormalizedText);

/// <summary>
/// Versioned, canonical provenance for the complete question and option set that produced a bonus prediction.
/// </summary>
public sealed record BonusQuestionCompatibilityManifest(
    int SchemaVersion,
    string NormalizedQuestionText,
    int MaxSelections,
    BonusQuestionOptionProvenance[] Options,
    string CompatibilitySha256)
{
    public const int CurrentSchemaVersion = 1;

    private static readonly Regex CollapsibleWhitespace = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Creates canonical provenance. Option order and source IDs do not affect the compatibility hash,
    /// while duplicate IDs or normalized texts are rejected because they cannot be mapped safely.
    /// </summary>
    public static BonusQuestionCompatibilityManifest Create(BonusQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        var normalizedQuestionText = NormalizeText(question.Text);
        if (normalizedQuestionText.Length == 0)
        {
            throw new InvalidDataException("Bonus question text is empty after normalization.");
        }

        if (question.Options is null || question.Options.Count == 0)
        {
            throw new InvalidDataException("Bonus question must contain at least one option.");
        }

        if (question.MaxSelections < 1 || question.MaxSelections > question.Options.Count)
        {
            throw new InvalidDataException(
                "Bonus question maximum selections must be between one and the number of options.");
        }

        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var normalizedTexts = new HashSet<string>(StringComparer.Ordinal);
        var options = new List<BonusQuestionOptionProvenance>(question.Options.Count);

        foreach (var option in question.Options)
        {
            if (option is null || string.IsNullOrWhiteSpace(option.Id))
            {
                throw new InvalidDataException("Bonus question option IDs must be non-empty.");
            }

            if (!sourceIds.Add(option.Id))
            {
                throw new InvalidDataException("Bonus question option IDs must be unique.");
            }

            var normalizedText = NormalizeText(option.Text);
            if (normalizedText.Length == 0)
            {
                throw new InvalidDataException("Bonus question option text is empty after normalization.");
            }

            if (!normalizedTexts.Add(normalizedText))
            {
                throw new InvalidDataException(
                    "Bonus question option texts must be unique after normalization.");
            }

            options.Add(new BonusQuestionOptionProvenance(option.Id, normalizedText));
        }

        var canonicalOptions = options
            .OrderBy(option => option.NormalizedText, StringComparer.Ordinal)
            .ToArray();
        var compatibilitySha256 = ComputeCompatibilitySha256(
            normalizedQuestionText,
            question.MaxSelections,
            canonicalOptions.Select(option => option.NormalizedText));

        return new BonusQuestionCompatibilityManifest(
            CurrentSchemaVersion,
            normalizedQuestionText,
            question.MaxSelections,
            canonicalOptions,
            compatibilitySha256);
    }

    /// <summary>Normalizes user-visible Kicktipp question and option text without discarding case or accents.</summary>
    public static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : CollapsibleWhitespace.Replace(
                value.Trim().Normalize(NormalizationForm.FormKC),
                " ");
    }

    /// <summary>Validates that a deserialized manifest is canonical and internally coherent.</summary>
    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported bonus-question compatibility schema version '{SchemaVersion}'.");
        }

        if (string.IsNullOrEmpty(NormalizedQuestionText)
            || !string.Equals(NormalizedQuestionText, NormalizeText(NormalizedQuestionText), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Stored normalized bonus question text is not canonical.");
        }

        if (Options is null || Options.Length == 0)
        {
            throw new InvalidDataException("Stored bonus-question compatibility options are missing.");
        }

        if (MaxSelections < 1 || MaxSelections > Options.Length)
        {
            throw new InvalidDataException("Stored bonus-question maximum selections are invalid.");
        }

        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var normalizedTexts = new HashSet<string>(StringComparer.Ordinal);
        string? previousNormalizedText = null;

        foreach (var option in Options)
        {
            if (option is null || string.IsNullOrWhiteSpace(option.SourceOptionId))
            {
                throw new InvalidDataException("Stored bonus option provenance contains an empty source ID.");
            }

            if (!sourceIds.Add(option.SourceOptionId))
            {
                throw new InvalidDataException("Stored bonus option provenance contains duplicate source IDs.");
            }

            if (string.IsNullOrEmpty(option.NormalizedText)
                || !string.Equals(option.NormalizedText, NormalizeText(option.NormalizedText), StringComparison.Ordinal))
            {
                throw new InvalidDataException("Stored bonus option text is not canonical.");
            }

            if (!normalizedTexts.Add(option.NormalizedText))
            {
                throw new InvalidDataException("Stored bonus option provenance contains duplicate normalized texts.");
            }

            if (previousNormalizedText is not null
                && StringComparer.Ordinal.Compare(previousNormalizedText, option.NormalizedText) >= 0)
            {
                throw new InvalidDataException("Stored bonus option provenance is not canonically ordered.");
            }

            previousNormalizedText = option.NormalizedText;
        }

        var expectedSha256 = ComputeCompatibilitySha256(
            NormalizedQuestionText,
            MaxSelections,
            Options.Select(option => option.NormalizedText));
        if (!string.Equals(expectedSha256, CompatibilitySha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Stored bonus-question compatibility hash is invalid.");
        }
    }

    /// <summary>
    /// Validates a target question and translates selected source option IDs to its compatible target IDs.
    /// Invalid target definitions throw; ordinary incompatibilities return a stable reason.
    /// </summary>
    public BonusPredictionCopyCompatibility TryMapPrediction(
        BonusQuestion targetQuestion,
        BonusPrediction sourcePrediction,
        out BonusPrediction? targetPrediction,
        out BonusQuestionCompatibilityManifest targetManifest)
    {
        ArgumentNullException.ThrowIfNull(targetQuestion);
        ArgumentNullException.ThrowIfNull(sourcePrediction);

        Validate();
        targetManifest = Create(targetQuestion);
        targetPrediction = null;

        if (!string.Equals(
                NormalizedQuestionText,
                targetManifest.NormalizedQuestionText,
                StringComparison.Ordinal))
        {
            return BonusPredictionCopyCompatibility.QuestionMismatch;
        }

        if (MaxSelections != targetManifest.MaxSelections)
        {
            return BonusPredictionCopyCompatibility.MaxSelectionsMismatch;
        }

        if (!Options.Select(option => option.NormalizedText).SequenceEqual(
                targetManifest.Options.Select(option => option.NormalizedText),
                StringComparer.Ordinal))
        {
            return BonusPredictionCopyCompatibility.OptionSetMismatch;
        }

        if (sourcePrediction.SelectedOptionIds is null
            || sourcePrediction.SelectedOptionIds.Count < 1
            || sourcePrediction.SelectedOptionIds.Count > MaxSelections
            || sourcePrediction.SelectedOptionIds.Distinct(StringComparer.Ordinal).Count()
                != sourcePrediction.SelectedOptionIds.Count)
        {
            return BonusPredictionCopyCompatibility.InvalidSourceSelection;
        }

        var sourceTextById = Options.ToDictionary(
            option => option.SourceOptionId,
            option => option.NormalizedText,
            StringComparer.Ordinal);
        var targetIdByText = targetManifest.Options.ToDictionary(
            option => option.NormalizedText,
            option => option.SourceOptionId,
            StringComparer.Ordinal);
        var mappedOptionIds = new List<string>(sourcePrediction.SelectedOptionIds.Count);

        foreach (var sourceOptionId in sourcePrediction.SelectedOptionIds)
        {
            if (!sourceTextById.TryGetValue(sourceOptionId, out var normalizedText)
                || !targetIdByText.TryGetValue(normalizedText, out var targetOptionId))
            {
                return BonusPredictionCopyCompatibility.InvalidSourceSelection;
            }

            mappedOptionIds.Add(targetOptionId);
        }

        targetPrediction = new BonusPrediction(mappedOptionIds);
        return BonusPredictionCopyCompatibility.Compatible;
    }

    private static string ComputeCompatibilitySha256(
        string normalizedQuestionText,
        int maxSelections,
        IEnumerable<string> normalizedOptionTexts)
    {
        var builder = new StringBuilder();
        AppendLengthPrefixed(builder, "bonus-question-compatibility-v1");
        AppendLengthPrefixed(builder, normalizedQuestionText);
        builder.Append(maxSelections).Append('\n');

        var options = normalizedOptionTexts.ToArray();
        builder.Append(options.Length).Append('\n');
        foreach (var option in options)
        {
            AppendLengthPrefixed(builder, option);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendLengthPrefixed(StringBuilder builder, string value)
    {
        builder.Append(Encoding.UTF8.GetByteCount(value)).Append(':').Append(value).Append('\n');
    }
}

/// <summary>Optional persistence capability required by Bundesliga cross-community bonus copying.</summary>
public interface IBonusPredictionCopyRepository
{
    Task<BonusPredictionMetadata?> GetBonusPredictionCopyCandidateAsync(
        BonusQuestion targetQuestion,
        PredictionModelConfig modelConfig,
        string sourceCommunityContext,
        CancellationToken cancellationToken = default);
}
