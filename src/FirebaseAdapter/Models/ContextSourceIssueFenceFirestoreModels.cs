using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;

namespace FirebaseAdapter.Models;

internal static class ContextSourceIssueFenceFirestoreModels
{
    private static readonly string[] Fields = ["contract", "repository", "competition", "scope", "source", "marker", "revision", "state",
        "attemptToken", "bodySha256", "watermarkSequence", "watermarkCycleId", "desiredState", "startedAtUtc", "issueNumber", "updatedAtUtc"];

    internal static Dictionary<string, object?> ToMap(BundesligaContextSourceIssueFence value)
    {
        value.Validate(); var attempt = value.Attempt;
        return new()
        {
            ["contract"] = BundesligaContextSourceIssueFence.Contract, ["repository"] = value.Identity.Repository,
            ["competition"] = value.Identity.Competition, ["scope"] = BundesligaContextSourceContract.ScopeValue(value.Identity.Scope),
            ["source"] = BundesligaContextSourceContract.SourceValue(value.Identity.Source), ["marker"] = value.Identity.Marker,
            ["revision"] = value.Revision, ["state"] = value.State.ToString(), ["attemptToken"] = attempt?.Token,
            ["bodySha256"] = attempt?.BodySha256, ["watermarkSequence"] = attempt?.Watermark.Sequence,
            ["watermarkCycleId"] = attempt?.Watermark.CycleId, ["desiredState"] = attempt?.DesiredState.ToString(),
            ["startedAtUtc"] = attempt is null ? null : BundesligaContextSourceContract.FormatUtc(attempt.StartedAtUtc),
            ["issueNumber"] = value.IssueNumber, ["updatedAtUtc"] = BundesligaContextSourceContract.FormatUtc(value.UpdatedAtUtc)
        };
    }

    internal static BundesligaContextSourceIssueFence Parse(DocumentSnapshot snapshot, BundesligaContextSourceIssueFenceIdentity identity)
    {
        identity.Validate();
        if (!snapshot.Exists || snapshot.Id != identity.StorageId || snapshot.Reference.Parent.Id != FirebaseContextSourceIssueFenceRepository.Collection)
            throw new InvalidDataException("ISSUE_FENCE_STORED_IDENTITY_INVALID");
        var map = snapshot.ToDictionary();
        if (!map.Keys.Order(StringComparer.Ordinal).SequenceEqual(Fields.Order(StringComparer.Ordinal))) throw new InvalidDataException("ISSUE_FENCE_FIELDS_INVALID");
        string Text(string name) => map[name] as string ?? throw new InvalidDataException("ISSUE_FENCE_TYPE_INVALID");
        long Integer(string name) => map[name] is long integer ? integer : throw new InvalidDataException("ISSUE_FENCE_TYPE_INVALID");
        T EnumValue<T>(string name) where T : struct, Enum => Enum.TryParse<T>(Text(name), false, out var value)
            && Enum.IsDefined(value) && value.ToString() == Text(name) ? value : throw new InvalidDataException("ISSUE_FENCE_ENUM_INVALID");
        if (Text("contract") != BundesligaContextSourceIssueFence.Contract || Text("repository") != identity.Repository
            || Text("competition") != identity.Competition || Text("scope") != BundesligaContextSourceContract.ProductionScope
            || Text("source") != "club-elo" || Text("marker") != identity.Marker) throw new InvalidDataException("ISSUE_FENCE_STORED_IDENTITY_INVALID");
        BundesligaContextSourceIssueFenceAttempt? attempt = null;
        var attemptFields = new[] { "attemptToken", "bodySha256", "watermarkSequence", "watermarkCycleId", "desiredState", "startedAtUtc" };
        if (attemptFields.Any(field => map[field] is not null))
            attempt = new(Text("attemptToken"), Text("bodySha256"), new(Integer("watermarkSequence"), Text("watermarkCycleId")),
                EnumValue<BundesligaContextSourceIssueState>("desiredState"), BundesligaContextSourceContract.ParseUtc(Text("startedAtUtc")));
        var fence = new BundesligaContextSourceIssueFence(identity, Integer("revision"), EnumValue<BundesligaContextSourceIssueFenceState>("state"), attempt,
            map["issueNumber"] is null ? null : Integer("issueNumber"), BundesligaContextSourceContract.ParseUtc(Text("updatedAtUtc")));
        fence.Validate(); return fence;
    }
}
