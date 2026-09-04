using System.Text.Json;
using System.Text.Json.Serialization;

namespace EHonda.KicktippAi.Core;

/// <summary>Subcompetitions valid only inside the bundesliga-2026-27 season partition.</summary>
[JsonConverter(typeof(BundesligaSeasonSubcompetitionJsonConverter))]
public enum BundesligaSeasonSubcompetition
{
    Bundesliga,
    DfbPokal,
    ChampionsLeague
}

/// <summary>Generic result interpretation retained independently from competition-specific match data.</summary>
[JsonConverter(typeof(ResultBasisJsonConverter))]
public enum ResultBasis
{
    RegularTime90Minutes,
    FinalScoreIncludingExtraTimeAndPenaltyShootout
}

public sealed class BundesligaSeasonSubcompetitionJsonConverter : JsonConverter<BundesligaSeasonSubcompetition>
{
    public override BundesligaSeasonSubcompetition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && BundesligaSeasonRoutingIdentityValues.TryParseBundesligaSeasonSubcompetition(reader.GetString(), out var value)
            ? value : throw new JsonException("Unknown Bundesliga season subcompetition.");

    public override void Write(Utf8JsonWriter writer, BundesligaSeasonSubcompetition value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToSerializedValue());
}

public sealed class ResultBasisJsonConverter : JsonConverter<ResultBasis>
{
    public override ResultBasis Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && BundesligaSeasonRoutingIdentityValues.TryParseResultBasis(reader.GetString(), out var value)
            ? value : throw new JsonException("Unknown result basis.");

    public override void Write(Utf8JsonWriter writer, ResultBasis value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToSerializedValue());
}

public static class BundesligaSeasonRoutingIdentityValues
{
    public const string BundesligaSeasonPartition = CompetitionIds.Bundesliga2026_27;

    public static string ToSerializedValue(this BundesligaSeasonSubcompetition value) => value switch
    {
        BundesligaSeasonSubcompetition.Bundesliga => "bundesliga",
        BundesligaSeasonSubcompetition.DfbPokal => "dfb-pokal",
        BundesligaSeasonSubcompetition.ChampionsLeague => "uefa-champions-league",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Bundesliga season subcompetition.")
    };

    public static bool TryParseBundesligaSeasonSubcompetition(
        string? value,
        out BundesligaSeasonSubcompetition subcompetition)
    {
        subcompetition = value switch
        {
            "bundesliga" => BundesligaSeasonSubcompetition.Bundesliga,
            "dfb-pokal" => BundesligaSeasonSubcompetition.DfbPokal,
            "uefa-champions-league" => BundesligaSeasonSubcompetition.ChampionsLeague,
            _ => default
        };
        return value is "bundesliga" or "dfb-pokal" or "uefa-champions-league";
    }

    public static string ToSerializedValue(this ResultBasis value) => value switch
    {
        ResultBasis.RegularTime90Minutes => "regularTime90Minutes",
        ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout =>
            "finalScoreIncludingExtraTimeAndPenaltyShootout",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown result basis.")
    };

    public static bool TryParseResultBasis(string? value, out ResultBasis resultBasis)
    {
        resultBasis = value switch
        {
            "regularTime90Minutes" => ResultBasis.RegularTime90Minutes,
            "finalScoreIncludingExtraTimeAndPenaltyShootout" =>
                ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout,
            _ => default
        };
        return value is "regularTime90Minutes" or "finalScoreIncludingExtraTimeAndPenaltyShootout";
    }
}
