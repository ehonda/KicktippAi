using System.Collections.Immutable;
using EHonda.KicktippAi.Core;

namespace KicktippIntegration;

/// <summary>
/// Exact, Bundesliga-2026/27-only Kicktipp authority surface. Current commands can
/// depend on this interface without gaining access to any legacy team/text lookup.
/// </summary>
public interface IBundesligaTypedKicktippClient
{
    Task<IReadOnlyList<TypedMatchSnapshot>> GetTypedOpenMatchSnapshotsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedMatchInventoryScope scope,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BundesligaTypedPlacedMatchPrediction>> GetTypedPlacedMatchPredictionsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedMatchReadScope scope,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BundesligaTypedPlacedMatchPrediction>> PlaceTypedMatchPredictionsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedMatchPlacementBatch predictions,
        bool overrideExisting,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TypedBonusSnapshot>> GetTypedOpenBonusSnapshotsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedBonusInventoryScope scope,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BundesligaTypedPlacedBonusPrediction>> GetTypedPlacedBonusPredictionsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedBonusReadScope scope,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BundesligaTypedPlacedBonusPrediction>> PlaceTypedBonusPredictionsAsync(
        BundesligaPredictionAuthority authority,
        BundesligaTypedBonusPlacementBatch predictions,
        bool overrideExisting,
        CancellationToken cancellationToken = default);
}

public sealed class KicktippTypedAuthorityException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public sealed record BundesligaTypedMatchSourceIdentity
{
    private BundesligaTypedMatchSourceIdentity(
        StableLocalItemKey key,
        string sourceCompetitionLabel,
        BundesligaSeasonSubcompetition subcompetition,
        string exactRound,
        ResultBasis resultBasis,
        string homeTeam,
        string awayTeam,
        int matchday) =>
        (Key, SourceCompetitionLabel, Subcompetition, ExactRound, ResultBasis, HomeTeam, AwayTeam, Matchday) =
        (key, sourceCompetitionLabel, subcompetition, exactRound, resultBasis, homeTeam, awayTeam, matchday);

    public StableLocalItemKey Key { get; }
    public string SourceCompetitionLabel { get; }
    public BundesligaSeasonSubcompetition Subcompetition { get; }
    public string ExactRound { get; }
    public ResultBasis ResultBasis { get; }
    public string HomeTeam { get; }
    public string AwayTeam { get; }
    public int Matchday { get; }

    public static BundesligaTypedMatchSourceIdentity Create(
        StableLocalItemKey key,
        string sourceCompetitionLabel,
        BundesligaSeasonSubcompetition subcompetition,
        string exactRound,
        ResultBasis resultBasis,
        string homeTeam,
        string awayTeam,
        int matchday)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.ItemKind != BundesligaPredictionItemKind.Match)
        {
            throw new InvalidDataException("Typed Kicktipp match identity requires a match key.");
        }

        ValidateEnum(subcompetition, nameof(subcompetition));
        ValidateEnum(resultBasis, nameof(resultBasis));
        ValidateText(sourceCompetitionLabel, nameof(sourceCompetitionLabel));
        ValidateText(exactRound, nameof(exactRound));
        ValidateText(homeTeam, nameof(homeTeam));
        ValidateText(awayTeam, nameof(awayTeam));
        if (string.Equals(homeTeam, awayTeam, StringComparison.Ordinal) || matchday < 1)
        {
            throw new InvalidDataException("Typed Kicktipp match teams or matchday are invalid.");
        }

        return new BundesligaTypedMatchSourceIdentity(
            key, sourceCompetitionLabel, subcompetition, exactRound, resultBasis,
            homeTeam, awayTeam, matchday);
    }

    internal void RequireSnapshot(TypedMatchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Key != Key || snapshot.Subcompetition != Subcompetition
            || !string.Equals(snapshot.ExactRound, ExactRound, StringComparison.Ordinal)
            || snapshot.ResultBasis != ResultBasis
            || !string.Equals(snapshot.HomeTeam, HomeTeam, StringComparison.Ordinal)
            || !string.Equals(snapshot.AwayTeam, AwayTeam, StringComparison.Ordinal)
            || snapshot.Matchday != Matchday)
        {
            throw new InvalidDataException("Typed Kicktipp match snapshot drifts from its exact source identity.");
        }
    }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(character => character is '\r' or '\n' or '\0'))
        {
            throw new ArgumentException("Value must be exact single-line source text.", parameterName);
        }
    }

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown typed Kicktipp enum value.");
        }
    }
}

public sealed record BundesligaTypedBonusSourceIdentity
{
    private BundesligaTypedBonusSourceIdentity(
        StableLocalItemKey key,
        BundesligaSeasonSubcompetition subcompetition) =>
        (Key, Subcompetition) = (key, subcompetition);

    public StableLocalItemKey Key { get; }
    public BundesligaSeasonSubcompetition Subcompetition { get; }

    public static BundesligaTypedBonusSourceIdentity Create(
        StableLocalItemKey key,
        BundesligaSeasonSubcompetition subcompetition)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.ItemKind != BundesligaPredictionItemKind.Bonus)
        {
            throw new InvalidDataException("Typed Kicktipp bonus identity requires a bonus key.");
        }
        if (!Enum.IsDefined(subcompetition))
        {
            throw new ArgumentOutOfRangeException(nameof(subcompetition), subcompetition, "Unknown bonus subcompetition.");
        }
        return new BundesligaTypedBonusSourceIdentity(key, subcompetition);
    }

    internal void RequireSnapshot(TypedBonusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Key != Key || snapshot.Subcompetition != Subcompetition)
        {
            throw new InvalidDataException("Typed Kicktipp bonus snapshot drifts from its exact source identity.");
        }
    }
}

public sealed class BundesligaTypedMatchInventoryScope
{
    private readonly ImmutableArray<BundesligaTypedMatchSourceIdentity> _items;
    private BundesligaTypedMatchInventoryScope(IEnumerable<BundesligaTypedMatchSourceIdentity> items) =>
        _items = items.ToImmutableArray();
    public IReadOnlyList<BundesligaTypedMatchSourceIdentity> Items => _items;
    public static BundesligaTypedMatchInventoryScope Create(IEnumerable<BundesligaTypedMatchSourceIdentity> items) =>
        new(ValidateInventory(items, item => item.Key, "match"));

    internal static IEnumerable<T> ValidateInventory<T>(
        IEnumerable<T> items,
        Func<T, StableLocalItemKey> key,
        string description)
    {
        ArgumentNullException.ThrowIfNull(items);
        var materialized = items.ToArray();
        if (materialized.Any(item => item is null))
        {
            throw new InvalidDataException($"Typed Kicktipp {description} scope contains a null item.");
        }
        var ordered = materialized.OrderBy(item => key(item).KicktippItemId, StringComparer.Ordinal).ToArray();
        if (ordered.Select(key).Distinct().Count() != ordered.Length
            || ordered.Select(item => key(item).KicktippItemId).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidDataException($"Typed Kicktipp {description} scope contains a duplicate identity.");
        }
        return ordered;
    }
}

public sealed class BundesligaTypedBonusInventoryScope
{
    private readonly ImmutableArray<BundesligaTypedBonusSourceIdentity> _items;
    private BundesligaTypedBonusInventoryScope(IEnumerable<BundesligaTypedBonusSourceIdentity> items) =>
        _items = items.ToImmutableArray();
    public IReadOnlyList<BundesligaTypedBonusSourceIdentity> Items => _items;
    public static BundesligaTypedBonusInventoryScope Create(IEnumerable<BundesligaTypedBonusSourceIdentity> items) =>
        new(BundesligaTypedMatchInventoryScope.ValidateInventory(items, item => item.Key, "bonus"));
}

public sealed record BundesligaTypedMatchSnapshotBinding
{
    private BundesligaTypedMatchSnapshotBinding(
        BundesligaTypedMatchSourceIdentity sourceIdentity,
        TypedMatchSnapshot snapshot) =>
        (SourceIdentity, Snapshot) = (sourceIdentity, snapshot);
    public BundesligaTypedMatchSourceIdentity SourceIdentity { get; }
    public TypedMatchSnapshot Snapshot { get; }
    public static BundesligaTypedMatchSnapshotBinding Create(
        BundesligaTypedMatchSourceIdentity sourceIdentity,
        TypedMatchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        sourceIdentity.RequireSnapshot(snapshot);
        return new BundesligaTypedMatchSnapshotBinding(sourceIdentity, snapshot);
    }
}

public sealed record BundesligaTypedBonusSnapshotBinding
{
    private BundesligaTypedBonusSnapshotBinding(
        BundesligaTypedBonusSourceIdentity sourceIdentity,
        TypedBonusSnapshot snapshot) =>
        (SourceIdentity, Snapshot) = (sourceIdentity, snapshot);
    public BundesligaTypedBonusSourceIdentity SourceIdentity { get; }
    public TypedBonusSnapshot Snapshot { get; }
    public static BundesligaTypedBonusSnapshotBinding Create(
        BundesligaTypedBonusSourceIdentity sourceIdentity,
        TypedBonusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        sourceIdentity.RequireSnapshot(snapshot);
        return new BundesligaTypedBonusSnapshotBinding(sourceIdentity, snapshot);
    }
}

public sealed class BundesligaTypedMatchReadScope
{
    private readonly ImmutableArray<BundesligaTypedMatchSnapshotBinding> _items;
    private BundesligaTypedMatchReadScope(IEnumerable<BundesligaTypedMatchSnapshotBinding> items) =>
        _items = items.ToImmutableArray();
    public IReadOnlyList<BundesligaTypedMatchSnapshotBinding> Items => _items;
    public static BundesligaTypedMatchReadScope Create(IEnumerable<BundesligaTypedMatchSnapshotBinding> items) =>
        new(BundesligaTypedMatchInventoryScope.ValidateInventory(items, item => item.Snapshot.Key, "match read"));
}

public sealed class BundesligaTypedBonusReadScope
{
    private readonly ImmutableArray<BundesligaTypedBonusSnapshotBinding> _items;
    private BundesligaTypedBonusReadScope(IEnumerable<BundesligaTypedBonusSnapshotBinding> items) =>
        _items = items.ToImmutableArray();
    public IReadOnlyList<BundesligaTypedBonusSnapshotBinding> Items => _items;
    public static BundesligaTypedBonusReadScope Create(IEnumerable<BundesligaTypedBonusSnapshotBinding> items) =>
        new(BundesligaTypedMatchInventoryScope.ValidateInventory(items, item => item.Snapshot.Key, "bonus read"));
}

public sealed record BundesligaTypedMatchSubmission
{
    private BundesligaTypedMatchSubmission(TypedMatchSnapshot snapshot, BetPrediction prediction) =>
        (Snapshot, Prediction) = (snapshot, prediction);
    public TypedMatchSnapshot Snapshot { get; }
    public BetPrediction Prediction { get; }
    public static BundesligaTypedMatchSubmission Create(TypedMatchSnapshot snapshot, BetPrediction prediction)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(prediction);
        if (prediction.HomeGoals < 0 || prediction.AwayGoals < 0)
        {
            throw new InvalidDataException("Typed match goals cannot be negative.");
        }
        return new BundesligaTypedMatchSubmission(snapshot, prediction);
    }
}

public sealed record BundesligaTypedBonusSubmission
{
    private readonly ImmutableArray<string> _selectedOptionIds;
    private BundesligaTypedBonusSubmission(TypedBonusSnapshot snapshot, IEnumerable<string> selectedOptionIds)
    {
        Snapshot = snapshot;
        _selectedOptionIds = selectedOptionIds.ToImmutableArray();
    }
    public TypedBonusSnapshot Snapshot { get; }
    public IReadOnlyList<string> SelectedOptionIds => _selectedOptionIds;
    public static BundesligaTypedBonusSubmission Create(
        TypedBonusSnapshot snapshot,
        IEnumerable<string> selectedOptionIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(selectedOptionIds);
        var selected = selectedOptionIds.ToArray();
        if (selected.Length == 0 || selected.Length > snapshot.MaxSelections
            || selected.Any(string.IsNullOrWhiteSpace)
            || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length
            || selected.Any(id => snapshot.Options.All(option => !string.Equals(option.Id, id, StringComparison.Ordinal))))
        {
            throw new InvalidDataException("Typed bonus selections must be exact, unique snapshot option IDs.");
        }
        return new BundesligaTypedBonusSubmission(snapshot, selected);
    }
}

public sealed class BundesligaTypedMatchPlacementBatch
{
    private readonly ImmutableArray<BundesligaTypedMatchSubmission> _predictions;
    private BundesligaTypedMatchPlacementBatch(
        BundesligaTypedMatchReadScope scope,
        IEnumerable<BundesligaTypedMatchSubmission> predictions)
    {
        Scope = scope;
        _predictions = predictions.ToImmutableArray();
    }
    public BundesligaTypedMatchReadScope Scope { get; }
    public IReadOnlyList<BundesligaTypedMatchSubmission> Predictions => _predictions;
    public static BundesligaTypedMatchPlacementBatch Create(
        BundesligaTypedMatchReadScope scope,
        IEnumerable<BundesligaTypedMatchSubmission> predictions)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var materialized = ValidateSubmissions(predictions, item => item.Snapshot.Key, "match");
        RequireContained(scope.Items.Select(item => item.Snapshot), materialized.Select(item => item.Snapshot), "match");
        return new BundesligaTypedMatchPlacementBatch(scope, materialized);
    }

    internal static T[] ValidateSubmissions<T>(IEnumerable<T> predictions, Func<T, StableLocalItemKey> key, string description)
    {
        ArgumentNullException.ThrowIfNull(predictions);
        var materialized = predictions.ToArray();
        if (materialized.Any(item => item is null)
            || materialized.Select(key).Distinct().Count() != materialized.Length)
        {
            throw new InvalidDataException($"Typed Kicktipp {description} submissions contain null or duplicate items.");
        }
        return materialized.OrderBy(item => key(item).KicktippItemId, StringComparer.Ordinal).ToArray();
    }

    internal static void RequireContained<TSnapshot>(
        IEnumerable<TSnapshot> scope,
        IEnumerable<TSnapshot> submitted,
        string description) where TSnapshot : class
    {
        var scopeItems = scope.ToArray();
        if (submitted.Any(item => !scopeItems.Contains(item)))
        {
            throw new InvalidDataException($"Typed Kicktipp {description} submission is outside the exact read scope.");
        }
    }
}

public sealed class BundesligaTypedBonusPlacementBatch
{
    private readonly ImmutableArray<BundesligaTypedBonusSubmission> _predictions;
    private BundesligaTypedBonusPlacementBatch(
        BundesligaTypedBonusReadScope scope,
        IEnumerable<BundesligaTypedBonusSubmission> predictions)
    {
        Scope = scope;
        _predictions = predictions.ToImmutableArray();
    }
    public BundesligaTypedBonusReadScope Scope { get; }
    public IReadOnlyList<BundesligaTypedBonusSubmission> Predictions => _predictions;
    public static BundesligaTypedBonusPlacementBatch Create(
        BundesligaTypedBonusReadScope scope,
        IEnumerable<BundesligaTypedBonusSubmission> predictions)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var materialized = BundesligaTypedMatchPlacementBatch.ValidateSubmissions(
            predictions, item => item.Snapshot.Key, "bonus");
        BundesligaTypedMatchPlacementBatch.RequireContained(
            scope.Items.Select(item => item.Snapshot), materialized.Select(item => item.Snapshot), "bonus");
        return new BundesligaTypedBonusPlacementBatch(scope, materialized);
    }
}

public sealed class BundesligaTypedPlacedMatchPrediction
{
    internal BundesligaTypedPlacedMatchPrediction(
        TypedMatchSnapshot snapshot,
        BetPrediction? prediction) => (Snapshot, Prediction) = (snapshot, prediction);
    public TypedMatchSnapshot Snapshot { get; }
    public BetPrediction? Prediction { get; }
}

public sealed class BundesligaTypedPlacedBonusPrediction
{
    private readonly ImmutableArray<string> _selectedOptionIds;
    internal BundesligaTypedPlacedBonusPrediction(
        TypedBonusSnapshot snapshot,
        IEnumerable<string> selectedOptionIds)
    {
        Snapshot = snapshot;
        _selectedOptionIds = selectedOptionIds.ToImmutableArray();
    }
    public TypedBonusSnapshot Snapshot { get; }
    public IReadOnlyList<string> SelectedOptionIds => _selectedOptionIds;
    public bool HasPrediction => !_selectedOptionIds.IsEmpty;
}
