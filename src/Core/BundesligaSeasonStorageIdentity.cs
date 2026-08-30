using System.Security.Cryptography;
using System.Text;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Validates the identity required for a row to be current in the
/// <c>bundesliga-2026-27</c> storage partition. This deliberately validates
/// storage shape only; the routing seed remains the authority for deciding
/// whether a live source item is a known canonical route.
/// </summary>
public static class BundesligaSeasonStorageIdentity
{
    public static bool IsTypedMatch(Match match) =>
        match.KicktippFixtureId is not null || match.KicktippRoundName is not null
        || match.BundesligaSeasonSubcompetition is not null || match.ResultBasis is not null;

    public static bool IsTypedBonusQuestion(BonusQuestion question) =>
        question.KicktippQuestionId is not null || question.BundesligaSeasonSubcompetition is not null;

    public static void ValidateMatch(string competition, Match match)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        ArgumentNullException.ThrowIfNull(match);

        if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            if (match.BundesligaSeasonSubcompetition is not null)
            {
                throw new InvalidOperationException(
                    "Bundesliga season subcompetition is invalid outside the bundesliga-2026-27 partition.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(match.KicktippFixtureId)
            || string.IsNullOrWhiteSpace(match.KicktippRoundName)
            || match.BundesligaSeasonSubcompetition is null
            || match.ResultBasis is null)
        {
            throw new InvalidOperationException(
                "Current Bundesliga 2026/27 match rows require a Kicktipp fixture ID, exact round, subcompetition, and result basis.");
        }

        var subcompetition = match.BundesligaSeasonSubcompetition.Value;
        var resultBasis = match.ResultBasis.Value;
        _ = subcompetition.ToSerializedValue();
        _ = resultBasis.ToSerializedValue();
        if ((subcompetition == BundesligaSeasonSubcompetition.Bundesliga
                && resultBasis != ResultBasis.RegularTime90Minutes)
            || (subcompetition is BundesligaSeasonSubcompetition.DfbPokal or BundesligaSeasonSubcompetition.ChampionsLeague
                && resultBasis != ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout))
        {
            throw new InvalidOperationException(
                "The Bundesliga season subcompetition and result basis conflict.");
        }
    }

    public static void ValidateBonusQuestion(string competition, BonusQuestion question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        ArgumentNullException.ThrowIfNull(question);

        if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            if (question.BundesligaSeasonSubcompetition is not null)
            {
                throw new InvalidOperationException(
                    "Bundesliga season subcompetition is invalid outside the bundesliga-2026-27 partition.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(question.KicktippQuestionId)
            || question.BundesligaSeasonSubcompetition is null)
        {
            throw new InvalidOperationException(
                "Current Bundesliga 2026/27 bonus rows require a Kicktipp question ID and subcompetition.");
        }

        _ = question.BundesligaSeasonSubcompetition.Value.ToSerializedValue();
    }

    /// <summary>Builds the ordered, exact identity persisted with typed bonus rows.</summary>
    public static string ComputeBonusQuestionIdentitySha256(BonusQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        ValidateBonusQuestion(CompetitionIds.Bundesliga2026_27, question);
        var builder = new StringBuilder("bundesliga-season-bonus-storage-v1\n");
        Append(builder, question.KicktippQuestionId!);
        Append(builder, question.BundesligaSeasonSubcompetition!.Value.ToSerializedValue());
        Append(builder, question.Text);
        Append(builder, question.Deadline.ToInstant().ToDateTimeOffset().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        Append(builder, question.MaxSelections.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var option in question.Options)
        {
            Append(builder, option.Id);
            Append(builder, option.Text);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(Encoding.UTF8.GetByteCount(value)).Append(':').Append(value).Append('\n');
}

/// <summary>
/// Optional repository capability for current Bundesliga-season bonus rows.
/// The legacy string/text APIs intentionally cannot provide enough identity to
/// make a current-row decision safely.
/// </summary>
public interface IBundesligaSeasonTypedBonusPredictionRepository
{
    Task<BonusPrediction?> GetCurrentBonusPredictionAsync(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default);

    Task<BonusPredictionMetadata?> GetCurrentBonusPredictionMetadataAsync(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default);

    Task<bool> HasCurrentBonusPredictionAsync(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default);

    Task<int> GetCurrentBonusRepredictionIndexAsync(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default);
}

/// <summary>Typed current-row capability for cancelled Bundesliga-season fixtures.</summary>
public interface IBundesligaSeasonTypedCancelledMatchPredictionRepository
{
    Task<Prediction?> GetCurrentCancelledMatchPredictionAsync(Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default);
    Task<PredictionMetadata?> GetCurrentCancelledMatchPredictionMetadataAsync(Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default);
    Task<int> GetCurrentCancelledMatchRepredictionIndexAsync(Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default);
}
