using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace EHonda.KicktippAi.Core;

public static class BundesligaRosterPublicationContract
{
    public const string AggregateRosterDocumentName = "team-rosters";
    public const string SquadSummaryDocumentName = "team-squad-summary";

    public static IReadOnlyList<(BundesligaRosterPublicationDocumentKind Kind, string Name)> GetRequiredDocuments(
        IReadOnlyList<BundesligaTeamManifestEntry>? teams = null)
    {
        teams ??= BundesligaTeamManifest.Default.Entries;
        var orderedTeams = teams.OrderBy(team => team.TeamSlug, StringComparer.Ordinal).ToArray();
        var documents = orderedTeams
            .Select(team => (BundesligaRosterPublicationDocumentKind.Context, $"roster-{team.TeamSlug}"))
            .ToList();
        documents.Add((BundesligaRosterPublicationDocumentKind.Context, AggregateRosterDocumentName));
        documents.Add((BundesligaRosterPublicationDocumentKind.Kpi, SquadSummaryDocumentName));
        return documents;
    }

    public static IReadOnlyList<BundesligaRosterPublicationDocument> ValidateAndOrder(
        IReadOnlyList<BundesligaRosterPublicationDocument> documents,
        IReadOnlyList<BundesligaTeamManifestEntry>? teams = null)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var required = GetRequiredDocuments(teams);
        var actualByKey = new Dictionary<(BundesligaRosterPublicationDocumentKind Kind, string Name), BundesligaRosterPublicationDocument>();
        foreach (var document in documents)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(document.Name);
            ArgumentNullException.ThrowIfNull(document.Content);
            if (!actualByKey.TryAdd((document.Kind, document.Name), document))
            {
                throw new InvalidDataException($"Duplicate roster publication document '{document.Kind}:{document.Name}'.");
            }
        }

        var requiredSet = required.ToHashSet();
        var missing = required.Where(key => !actualByKey.ContainsKey(key)).ToArray();
        var extra = actualByKey.Keys.Where(key => !requiredSet.Contains(key)).ToArray();
        if (missing.Length > 0 || extra.Length > 0)
        {
            throw new InvalidDataException(
                $"Roster publication document set is incomplete. " +
                $"Missing=[{string.Join(',', missing.Select(FormatKey))}], " +
                $"Extra=[{string.Join(',', extra.Select(FormatKey))}].");
        }

        var ordered = required.Select(key => actualByKey[key]).ToArray();
        foreach (var document in ordered)
        {
            var expectedHeaders = document.Name == SquadSummaryDocumentName
                ? BundesligaRosterCsv.SummaryHeaders
                : BundesligaRosterCsv.RosterHeaders;
            ValidateCsvBytes(document, expectedHeaders);
        }

        return ordered;
    }

    public static string ComputeSnapshotId(
        IReadOnlyList<BundesligaRosterPublicationDocument> documents,
        IReadOnlyList<BundesligaTeamManifestEntry>? teams = null)
    {
        var ordered = ValidateAndOrder(documents, teams);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var document in ordered)
        {
            AppendLengthPrefixed(hash, Encoding.UTF8.GetBytes(document.Kind.ToString()));
            AppendLengthPrefixed(hash, Encoding.UTF8.GetBytes(document.Name));
            AppendLengthPrefixed(hash, document.Content);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void ValidateCsvBytes(
        BundesligaRosterPublicationDocument document,
        IReadOnlyList<string> expectedHeaders)
    {
        if (document.Content.Length == 0)
        {
            throw new InvalidDataException($"Roster publication document '{document.Name}' is empty.");
        }

        if (document.Content.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            throw new InvalidDataException($"Roster publication document '{document.Name}' must not contain a UTF-8 BOM.");
        }

        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(document.Content);
        var expectedPrefix = string.Join(',', expectedHeaders) + "\r\n";
        if (!content.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Roster publication document '{document.Name}' must start with the exact contracted header.");
        }

        if (content.Length == expectedPrefix.Length)
        {
            throw new InvalidDataException(
                $"Roster publication document '{document.Name}' must contain at least one data row.");
        }

        if (!content.EndsWith("\r\n", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Roster publication document '{document.Name}' must end with a final CRLF.");
        }

        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] == '\r'
                && (index + 1 == content.Length || content[index + 1] != '\n'))
            {
                throw new InvalidDataException(
                    $"Roster publication document '{document.Name}' contains a bare carriage return.");
            }

            if (content[index] == '\n' && (index == 0 || content[index - 1] != '\r'))
            {
                throw new InvalidDataException(
                    $"Roster publication document '{document.Name}' contains a bare line feed.");
            }
        }
    }

    private static void AppendLengthPrefixed(IncrementalHash hash, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }

    private static string FormatKey((BundesligaRosterPublicationDocumentKind Kind, string Name) key)
    {
        return $"{key.Kind}:{key.Name}";
    }
}
