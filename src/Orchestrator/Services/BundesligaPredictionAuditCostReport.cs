using System.Collections.Immutable;
using FirebaseAdapter;

namespace Orchestrator.Services;

public interface IBundesligaPredictionAuditCostReportReader
{
    Task<BundesligaPredictionAuditCostReport> ReadAsync(
        CancellationToken cancellationToken = default);
}

public sealed class BundesligaPredictionAuditCostSubtotal
{
    internal BundesligaPredictionAuditCostSubtotal(
        string authorityLabel,
        long rowCount,
        long? inputTokens,
        long? outputTokens,
        long unknownInputTokenCount,
        long unknownOutputTokenCount,
        decimal costUsd) =>
        (AuthorityLabel, RowCount, InputTokens, OutputTokens,
            UnknownInputTokenCount, UnknownOutputTokenCount, CostUsd) =
        (authorityLabel, rowCount, inputTokens, outputTokens,
            unknownInputTokenCount, unknownOutputTokenCount, costUsd);

    public string AuthorityLabel { get; }
    public long RowCount { get; }
    public long? InputTokens { get; }
    public long? OutputTokens { get; }
    public long UnknownInputTokenCount { get; }
    public long UnknownOutputTokenCount { get; }
    public decimal CostUsd { get; }
}

public sealed class BundesligaPredictionAuditCostReport
{
    private readonly ImmutableArray<FirebasePredictionAuditCostRow> _rows;
    private readonly ImmutableArray<BundesligaPredictionAuditCostSubtotal> _subtotals;

    internal BundesligaPredictionAuditCostReport(
        IEnumerable<FirebasePredictionAuditCostRow> rows,
        IEnumerable<BundesligaPredictionAuditCostSubtotal> subtotals,
        long rowCount,
        long? inputTokens,
        long? outputTokens,
        long unknownInputTokenCount,
        long unknownOutputTokenCount,
        decimal costUsd)
    {
        _rows = rows.ToImmutableArray();
        _subtotals = subtotals.ToImmutableArray();
        RowCount = rowCount;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        UnknownInputTokenCount = unknownInputTokenCount;
        UnknownOutputTokenCount = unknownOutputTokenCount;
        CostUsd = costUsd;
    }

    public IReadOnlyList<FirebasePredictionAuditCostRow> Rows => _rows;
    public IReadOnlyList<BundesligaPredictionAuditCostSubtotal> AuthoritySubtotals => _subtotals;
    public long RowCount { get; }
    public long? InputTokens { get; }
    public long? OutputTokens { get; }
    public long UnknownInputTokenCount { get; }
    public long UnknownOutputTokenCount { get; }
    public decimal CostUsd { get; }
}

public sealed class BundesligaPredictionAuditCostReportReader
    : IBundesligaPredictionAuditCostReportReader
{
    private readonly ImmutableArray<IFirebasePredictionAuditCostReader> _readers;

    public BundesligaPredictionAuditCostReportReader(
        ILegacyFirebaseMatchPredictionAuditCostReader legacyMatch,
        ILegacyFirebaseBonusPredictionAuditCostReader legacyBonus,
        ITypedFirebaseMatchPredictionAuditCostReader typedMatch,
        ITypedFirebaseBonusPredictionAuditCostReader typedBonus)
    {
        ArgumentNullException.ThrowIfNull(legacyMatch);
        ArgumentNullException.ThrowIfNull(legacyBonus);
        ArgumentNullException.ThrowIfNull(typedMatch);
        ArgumentNullException.ThrowIfNull(typedBonus);
        _readers = [legacyMatch, legacyBonus, typedMatch, typedBonus];
        RequireDistinctReaderScopes(_readers);
    }

    public async Task<BundesligaPredictionAuditCostReport> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reads = _readers.Select(reader => StartRead(reader, cancellationToken)).ToArray();
        await Task.WhenAll(reads).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var materialized = _readers.Zip(reads, static (reader, read) =>
            new ReaderResult(
                reader,
                (read.Result ?? throw new InvalidDataException(
                    "An isolated audit reader returned no materialized result."))
                .ToImmutableArray())).ToImmutableArray();
        return Combine(materialized);
    }

    private static Task<IReadOnlyList<FirebasePredictionAuditCostRow>> StartRead(
        IFirebasePredictionAuditCostReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            return reader.ReadAsync(cancellationToken)
                ?? Task.FromException<IReadOnlyList<FirebasePredictionAuditCostRow>>(
                    new InvalidDataException("An isolated audit reader returned no task."));
        }
        catch (Exception exception)
        {
            return Task.FromException<IReadOnlyList<FirebasePredictionAuditCostRow>>(exception);
        }
    }

    internal static BundesligaPredictionAuditCostReport Combine(
        IReadOnlyList<ReaderResult> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        RequireDistinctReaderScopes(sources.Select(source => source.Reader));

        var rows = new List<FirebasePredictionAuditCostRow>();
        foreach (var source in sources)
        {
            foreach (var row in source.Rows)
            {
                if (row is null
                    || row.IsCurrentAuthoritative
                    || !string.Equals(
                        row.AuthorityLabel, source.Reader.AuthorityLabel, StringComparison.Ordinal)
                    || !string.Equals(
                        row.PhysicalCollection, source.Reader.PhysicalCollection, StringComparison.Ordinal)
                    || !string.Equals(row.ItemKind, source.Reader.ItemKind, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Audit row contradicts its isolated reader authority, collection, kind, or non-current contract.");
                }
                rows.Add(row);
            }
        }

        var duplicate = rows.GroupBy(row =>
                (row.AuthorityLabel, row.PhysicalCollection, row.DocumentId))
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                "Audit report contains a duplicate authority/collection/document identity.");
        }

        var subtotals = sources.Select(source => source.Reader.AuthorityLabel)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(authority => CreateSubtotal(
                authority,
                rows.Where(row => string.Equals(
                    row.AuthorityLabel, authority, StringComparison.Ordinal))))
            .ToImmutableArray();

        long rowCount = 0;
        long knownInput = 0;
        long knownOutput = 0;
        long unknownInput = 0;
        long unknownOutput = 0;
        decimal cost = 0;
        var hasUnknownInput = false;
        var hasUnknownOutput = false;
        checked
        {
            foreach (var subtotal in subtotals)
            {
                rowCount += subtotal.RowCount;
                unknownInput += subtotal.UnknownInputTokenCount;
                unknownOutput += subtotal.UnknownOutputTokenCount;
                hasUnknownInput |= subtotal.InputTokens is null;
                hasUnknownOutput |= subtotal.OutputTokens is null;
                if (subtotal.InputTokens is { } input) knownInput += input;
                if (subtotal.OutputTokens is { } output) knownOutput += output;
                cost += subtotal.CostUsd;
            }
        }

        return new BundesligaPredictionAuditCostReport(
            rows.OrderBy(row => row.AuthorityLabel, StringComparer.Ordinal)
                .ThenBy(row => row.PhysicalCollection, StringComparer.Ordinal)
                .ThenBy(row => row.DocumentId, StringComparer.Ordinal),
            subtotals,
            rowCount,
            hasUnknownInput ? null : knownInput,
            hasUnknownOutput ? null : knownOutput,
            unknownInput,
            unknownOutput,
            cost);
    }

    private static BundesligaPredictionAuditCostSubtotal CreateSubtotal(
        string authority,
        IEnumerable<FirebasePredictionAuditCostRow> rows)
    {
        long count = 0;
        long knownInput = 0;
        long knownOutput = 0;
        long unknownInput = 0;
        long unknownOutput = 0;
        decimal cost = 0;
        checked
        {
            foreach (var row in rows)
            {
                count++;
                if (row.InputTokens is { } input) knownInput += input;
                else unknownInput++;
                if (row.OutputTokens is { } output) knownOutput += output;
                else unknownOutput++;
                cost += row.CostUsd;
            }
        }

        return new BundesligaPredictionAuditCostSubtotal(
            authority,
            count,
            unknownInput == 0 ? knownInput : null,
            unknownOutput == 0 ? knownOutput : null,
            unknownInput,
            unknownOutput,
            cost);
    }

    private static void RequireDistinctReaderScopes(
        IEnumerable<IFirebasePredictionAuditCostReader> readers)
    {
        var materialized = readers.ToArray();
        if (materialized.Length != 4
            || materialized.Any(reader => reader is null
                || string.IsNullOrWhiteSpace(reader.AuthorityLabel)
                || string.IsNullOrWhiteSpace(reader.PhysicalCollection)
                || reader.ItemKind is not ("match" or "bonus")))
        {
            throw new InvalidDataException("Audit report requires four exact labelled reader scopes.");
        }

        var duplicate = materialized.GroupBy(reader =>
                (reader.AuthorityLabel, reader.PhysicalCollection, reader.ItemKind))
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException("Audit reader scopes must be exact and distinct.");
        }
    }

    internal sealed record ReaderResult(
        IFirebasePredictionAuditCostReader Reader,
        IReadOnlyList<FirebasePredictionAuditCostRow> Rows);
}
