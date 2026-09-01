using System.Reflection;
using FirebaseAdapter;
using Moq;
using NodaTime;
using Orchestrator.Services;

namespace Orchestrator.Tests.Services;

public sealed class BundesligaPredictionAuditCostReportTests
{
    [Test]
    public async Task Four_isolated_reads_are_combined_from_labelled_subtotals_with_checked_unknown_semantics()
    {
        var legacyMatch = Reader<ILegacyFirebaseMatchPredictionAuditCostReader>(
            "legacy", "legacy-match", "match",
            [Row("legacy", "legacy-match", "z", "match", null, 2, 3m)]);
        var legacyBonus = Reader<ILegacyFirebaseBonusPredictionAuditCostReader>(
            "legacy", "legacy-bonus", "bonus", []);
        var typedMatch = Reader<ITypedFirebaseMatchPredictionAuditCostReader>(
            "typed", "typed-match", "match",
            [Row("typed", "typed-match", "b", "match", 10, 5, 0.25m)]);
        var typedBonus = Reader<ITypedFirebaseBonusPredictionAuditCostReader>(
            "typed", "typed-bonus", "bonus",
            [Row("typed", "typed-bonus", "a", "bonus", 2, 1, 0.10m)]);
        var sut = new BundesligaPredictionAuditCostReportReader(
            legacyMatch.Object, legacyBonus.Object, typedMatch.Object, typedBonus.Object);

        var report = await sut.ReadAsync();

        await Assert.That(report.RowCount).IsEqualTo(3);
        await Assert.That(report.InputTokens).IsNull();
        await Assert.That(report.OutputTokens).IsEqualTo(8);
        await Assert.That(report.UnknownInputTokenCount).IsEqualTo(1);
        await Assert.That(report.UnknownOutputTokenCount).IsEqualTo(0);
        await Assert.That(report.CostUsd).IsEqualTo(3.35m);
        await Assert.That(report.AuthoritySubtotals.Select(value => value.AuthorityLabel)
            .SequenceEqual(["legacy", "typed"], StringComparer.Ordinal)).IsTrue();
        await Assert.That(report.Rows.Select(value => value.DocumentId)
            .SequenceEqual(["z", "a", "b"], StringComparer.Ordinal)).IsTrue();
        await Assert.That(report.AuthoritySubtotals.Single(value => value.AuthorityLabel == "legacy")
            .InputTokens).IsNull();
        await Assert.That(report.AuthoritySubtotals.Single(value => value.AuthorityLabel == "typed")
            .InputTokens).IsEqualTo(12);
        VerifyOnce(legacyMatch, legacyBonus, typedMatch, typedBonus);
    }

    [Test]
    public async Task Empty_authorities_have_exact_zero_subtotals_and_outputs_are_immutable()
    {
        var legacyMatch = Reader<ILegacyFirebaseMatchPredictionAuditCostReader>(
            "legacy", "legacy-match", "match", []);
        var legacyBonus = Reader<ILegacyFirebaseBonusPredictionAuditCostReader>(
            "legacy", "legacy-bonus", "bonus", []);
        var typedMatch = Reader<ITypedFirebaseMatchPredictionAuditCostReader>(
            "typed", "typed-match", "match", []);
        var typedBonus = Reader<ITypedFirebaseBonusPredictionAuditCostReader>(
            "typed", "typed-bonus", "bonus", []);

        var report = await new BundesligaPredictionAuditCostReportReader(
            legacyMatch.Object, legacyBonus.Object, typedMatch.Object, typedBonus.Object).ReadAsync();

        await Assert.That(report.AuthoritySubtotals).Count().IsEqualTo(2);
        await Assert.That(report.AuthoritySubtotals.All(value => value.RowCount == 0
            && value.InputTokens == 0
            && value.OutputTokens == 0
            && value.UnknownInputTokenCount == 0
            && value.UnknownOutputTokenCount == 0
            && value.CostUsd == 0)).IsTrue();
        await Assert.That(report.RowCount).IsEqualTo(0);
        await Assert.That(report.InputTokens).IsEqualTo(0);
        await Assert.That(report.OutputTokens).IsEqualTo(0);
        await Assert.That(report.Rows is IList<FirebasePredictionAuditCostRow>).IsTrue();
        await Assert.That(() => ((IList<FirebasePredictionAuditCostRow>)report.Rows)
                .Add(Row("typed", "typed-match", "x", "match", 0, 0, 0)))
            .Throws<NotSupportedException>();
        await Assert.That(report.AuthoritySubtotals is IList<BundesligaPredictionAuditCostSubtotal>)
            .IsTrue();
    }

    [Test]
    public async Task Label_disagreement_duplicate_identity_and_arithmetic_overflow_fail_atomically()
    {
        var wrongLabel = Reader<ILegacyFirebaseMatchPredictionAuditCostReader>(
            "legacy", "legacy-match", "match",
            [Row("typed", "legacy-match", "x", "match", 1, 1, 0)]);
        var emptyLegacyBonus = Reader<ILegacyFirebaseBonusPredictionAuditCostReader>(
            "legacy", "legacy-bonus", "bonus", []);
        var emptyTypedMatch = Reader<ITypedFirebaseMatchPredictionAuditCostReader>(
            "typed", "typed-match", "match", []);
        var emptyTypedBonus = Reader<ITypedFirebaseBonusPredictionAuditCostReader>(
            "typed", "typed-bonus", "bonus", []);

        await Assert.That(() => new BundesligaPredictionAuditCostReportReader(
                wrongLabel.Object,
                emptyLegacyBonus.Object,
                emptyTypedMatch.Object,
                emptyTypedBonus.Object).ReadAsync())
            .Throws<InvalidDataException>();

        var duplicate = Row("legacy", "legacy-match", "same", "match", 1, 1, 0);
        var duplicateReader = Reader<ILegacyFirebaseMatchPredictionAuditCostReader>(
            "legacy", "legacy-match", "match", [duplicate, duplicate]);
        await Assert.That(() => new BundesligaPredictionAuditCostReportReader(
                duplicateReader.Object,
                emptyLegacyBonus.Object,
                emptyTypedMatch.Object,
                emptyTypedBonus.Object).ReadAsync())
            .Throws<InvalidDataException>();

        var overflowReader = Reader<ITypedFirebaseMatchPredictionAuditCostReader>(
            "typed", "typed-match", "match",
            [
                Row("typed", "typed-match", "max", "match", long.MaxValue, 0, decimal.MaxValue),
                Row("typed", "typed-match", "one", "match", 1, 0, 1)
            ]);
        await Assert.That(() => new BundesligaPredictionAuditCostReportReader(
                Reader<ILegacyFirebaseMatchPredictionAuditCostReader>(
                    "legacy", "legacy-match", "match", []).Object,
                emptyLegacyBonus.Object,
                overflowReader.Object,
                emptyTypedBonus.Object).ReadAsync())
            .Throws<OverflowException>();
    }

    [Test]
    public async Task One_reader_failure_returns_no_partial_report_after_all_four_reads_start()
    {
        var legacyMatch = Reader<ILegacyFirebaseMatchPredictionAuditCostReader>(
            "legacy", "legacy-match", "match", []);
        var legacyBonus = Reader<ILegacyFirebaseBonusPredictionAuditCostReader>(
            "legacy", "legacy-bonus", "bonus", []);
        var typedMatch = Reader<ITypedFirebaseMatchPredictionAuditCostReader>(
            "typed", "typed-match", "match", []);
        var typedBonus = Reader<ITypedFirebaseBonusPredictionAuditCostReader>(
            "typed", "typed-bonus", "bonus", []);
        typedMatch.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>()))
            .Throws(new InvalidDataException("isolated failure"));
        var sut = new BundesligaPredictionAuditCostReportReader(
            legacyMatch.Object, legacyBonus.Object, typedMatch.Object, typedBonus.Object);

        await Assert.That(() => sut.ReadAsync()).Throws<InvalidDataException>();

        VerifyOnce(legacyMatch, legacyBonus, typedMatch, typedBonus);
    }

    [Test]
    public async Task Reader_scopes_must_be_distinct_and_audit_rows_cannot_claim_current_authority()
    {
        var legacyMatch = Reader<ILegacyFirebaseMatchPredictionAuditCostReader>(
            "same", "same", "match", []);
        var legacyBonus = Reader<ILegacyFirebaseBonusPredictionAuditCostReader>(
            "same", "same", "match", []);
        var typedMatch = Reader<ITypedFirebaseMatchPredictionAuditCostReader>(
            "typed", "typed-match", "match", []);
        var typedBonus = Reader<ITypedFirebaseBonusPredictionAuditCostReader>(
            "typed", "typed-bonus", "bonus", []);

        await Assert.That(() => new BundesligaPredictionAuditCostReportReader(
                legacyMatch.Object, legacyBonus.Object, typedMatch.Object, typedBonus.Object))
            .Throws<InvalidDataException>();
        var row = Row("typed", "typed-match", "x", "match", 0, 0, 0);
        await Assert.That(row.IsCurrentAuthoritative).IsFalse();
        await Assert.That(typeof(FirebasePredictionAuditCostRow)
            .GetProperty(nameof(FirebasePredictionAuditCostRow.IsCurrentAuthoritative))!
            .SetMethod).IsNull();
    }

    private static Mock<T> Reader<T>(
        string authority,
        string collection,
        string kind,
        IReadOnlyList<FirebasePredictionAuditCostRow> rows)
        where T : class, IFirebasePredictionAuditCostReader
    {
        var mock = new Mock<T>(MockBehavior.Strict);
        mock.SetupGet(value => value.AuthorityLabel).Returns(authority);
        mock.SetupGet(value => value.PhysicalCollection).Returns(collection);
        mock.SetupGet(value => value.ItemKind).Returns(kind);
        mock.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        return mock;
    }

    private static FirebasePredictionAuditCostRow Row(
        string authority,
        string collection,
        string documentId,
        string kind,
        long? input,
        long? output,
        decimal cost)
    {
        var constructor = typeof(FirebasePredictionAuditCostRow).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (FirebasePredictionAuditCostRow)constructor.Invoke(
        [
            authority,
            collection,
            documentId,
            kind,
            $"{documentId}-r0",
            0,
            Instant.FromUtc(2026, 8, 31, 12, 0),
            input,
            output,
            cost
        ]);
    }

    private static void VerifyOnce(
        Mock<ILegacyFirebaseMatchPredictionAuditCostReader> legacyMatch,
        Mock<ILegacyFirebaseBonusPredictionAuditCostReader> legacyBonus,
        Mock<ITypedFirebaseMatchPredictionAuditCostReader> typedMatch,
        Mock<ITypedFirebaseBonusPredictionAuditCostReader> typedBonus)
    {
        legacyMatch.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Once);
        legacyBonus.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Once);
        typedMatch.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Once);
        typedBonus.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
