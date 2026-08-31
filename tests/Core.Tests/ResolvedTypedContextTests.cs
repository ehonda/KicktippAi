using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class ResolvedTypedContextTests
{
    private const string Seed = "52ce7ba4430d07ed71528a7ce48fee499e25b9dd303bd7bce22eed17a1921660";
    private const string Content = "f943f4b8f19d69dd1fc378d5684a2fdf7f59596accab4aa25866f81889b3e709";
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Exact_manifest_and_binding_bytes_round_trip_without_a_clock()
    {
        var manifest = Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now);
        var expectedManifest = "{\"seasonPartition\":\"bundesliga-2026-27\",\"communityContext\":\"schadensfresse\",\"bundesligaSeasonSubcompetition\":\"dfb-pokal\",\"profileId\":\"schadensfresse-dfb-pokal-rules-only-v1\",\"routingSeedSha256\":\"" + Seed + "\",\"rulesObservedAt\":\"2026-08-30T12:00:00.0000000Z\",\"rulesSchemaVersion\":\"schadensfresse-live-rules-v1\",\"canonicalRulesSha256\":\"1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90\",\"documents\":[{\"kind\":\"Context\",\"name\":\"community-rules-schadensfresse.md\",\"version\":7,\"contentSha256\":\"" + Content + "\"}]}";
        var bytes = manifest.SerializeCanonical();
        await Assert.That(Encoding.UTF8.GetString(bytes)).IsEqualTo(expectedManifest);
        await Assert.That(ResolvedTypedContextManifest.DeserializeCanonical(bytes)).IsEqualTo(manifest);
        var binding = Binding(Now);
        var expectedBinding = "{\"seasonPartition\":\"bundesliga-2026-27\",\"communityContext\":\"schadensfresse\",\"profileId\":\"schadensfresse-dfb-pokal-rules-only-v1\",\"routingSeedSha256\":\"" + Seed + "\",\"bundesligaSeasonSubcompetition\":\"dfb-pokal\",\"rulesObservedAt\":\"2026-08-30T12:00:00.0000000Z\",\"rulesSchemaVersion\":\"schadensfresse-live-rules-v1\",\"canonicalRulesSha256\":\"1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90\",\"document\":{\"kind\":\"Context\",\"name\":\"community-rules-schadensfresse.md\",\"version\":7,\"contentSha256\":\"" + Content + "\"}}";
        await Assert.That(Encoding.UTF8.GetString(binding.SerializeCanonical())).IsEqualTo(expectedBinding);
        await Assert.That(ResolvedTypedContextPublicationBinding.DeserializeCanonical(binding.SerializeCanonical())).IsEqualTo(binding);
    }

    [Test]
    public async Task Historical_manifest_parses_but_freshness_is_an_explicit_evaluation_gate()
    {
        var stale = Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now.AddDays(-2));
        await Assert.That(ResolvedTypedContextManifest.DeserializeCanonical(stale.SerializeCanonical())).IsEqualTo(stale);
        await Assert.That(() => SchadensfresseTypedContextProfiles.ValidateManifestFreshness(stale, Now)).Throws<InvalidDataException>();
        await Assert.That(() => SchadensfresseTypedContextProfiles.ValidateManifestFreshness(Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now.AddHours(-24)), Now)).ThrowsNothing();
        await Assert.That(() => SchadensfresseTypedContextProfiles.ValidateManifestFreshness(Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now.AddHours(-24).AddTicks(-1)), Now)).Throws<InvalidDataException>();
        await Assert.That(() => SchadensfresseTypedContextProfiles.ValidateManifestFreshness(Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now.AddTicks(1)), Now)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Binding_refresh_accepts_stale_stored_value_but_rejects_stale_candidate_and_future_stored_value()
    {
        var stale = Binding(Now.AddDays(-2)); var fresh = Binding(Now);
        var update = ResolvedTypedContextPublicationBindingContract.SelectEffective(stale, fresh, Now);
        await Assert.That(update.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.Updated);
        await Assert.That(() => ResolvedTypedContextPublicationBindingContract.SelectEffective(stale, Binding(Now.AddHours(-25)), Now)).Throws<InvalidDataException>();
        await Assert.That(() => ResolvedTypedContextPublicationBindingContract.SelectEffective(stale, Binding(Now.AddTicks(1)), Now)).Throws<InvalidDataException>();
        await Assert.That(() => ResolvedTypedContextPublicationBindingContract.SelectEffective(Binding(Now.AddTicks(1)), fresh, Now)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Models_are_deeply_immutable_and_have_sequence_value_equality()
    {
        var source = new List<ResolvedTypedContextDocument> { Doc(7) };
        var left = new ResolvedTypedContextManifest("bundesliga-2026-27", "schadensfresse", BundesligaSeasonSubcompetition.DfbPokal, "schadensfresse-dfb-pokal-rules-only-v1", Seed, Now, SchadensfresseRulesCanonicalJson.SchemaVersion, SchadensfresseRulesCanonicalJson.CanonicalSha256, source);
        var right = Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now);
        source[0] = Doc(8); source.Add(Doc(9));
        await Assert.That(left.Documents.Count).IsEqualTo(1);
        await Assert.That(left.Documents[0].Version).IsEqualTo(7);
        await Assert.That(left).IsEqualTo(right);
        await Assert.That(left.GetHashCode()).IsEqualTo(right.GetHashCode());
    }

    [Test]
    public async Task Both_schemas_systematically_reject_structural_timestamp_and_identity_mutations()
    {
        var manifest = Encoding.UTF8.GetString(Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now).SerializeCanonical());
        var binding = Encoding.UTF8.GetString(Binding(Now).SerializeCanonical());
        var mutations = new[] { " \u0000", "\uFEFF", "\n", "missing-season", "reordered-root", "\"extra\":true,", "\"seasonPartition\":\"bundesliga-2026-27\",", "\"SeasonPartition\"", "\"seasonPartition\":null", "\"routingSeedSha256\":123", "\"rulesObservedAt\":null", "\"rulesObservedAt\":\"2026-02-30T12:00:00.0000000Z\"", "\"rulesObservedAt\":\"2026-08-30 12:00:00.0000000Z\"", "\"rulesObservedAt\":\"2026-08-30T12:00:00.000000Z\"", "\"rulesObservedAt\":\"2026-08-30T12:00:00.00000000Z\"", "\"rulesObservedAt\":\"2026-08-30T12:00:00.0000000+00:00\"", "\"rulesObservedAt\":\"2026-08-30T12:00:00.0000000z\"", "\"rulesObservedAt\":\"2026-08-30T12:00:60.0000000Z\"", "\"rulesObservedAt\":\"２０２６-08-30T12:00:00.0000000Z\"", "\"rulesObservedAt\":\"2026-08-30T12:00:00.0000000 Z\"", "\"canonicalRulesSha256\":\"b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03\"", "\"canonicalRulesSha256\":\"4ea1a5203ec2870141e59aa5573559a3945741984411f0d5cd3c66fb3a5f473e\"", "\"bundesligaSeasonSubcompetition\":\"bundesliga\"", "\"communityContext\":\"pes-squad\"", "\"profileId\":\"SCHADENSFRESSE-DFB-POKAL-RULES-ONLY-V1\"", "\"version\":-1", "\"version\":2147483648", "\"version\":\"7\"", "\"kind\":\"latest\"", "\"name\":\"bundesliga-standings.csv\"", "\"contentSha256\":\"ABC\"" };
        foreach (var mutation in mutations)
        {
            var badManifest = Mutate(manifest, mutation); var badBinding = Mutate(binding, mutation);
            await Assert.That(() => ResolvedTypedContextManifest.DeserializeCanonical(Encoding.UTF8.GetBytes(badManifest))).Throws<InvalidDataException>();
            await Assert.That(() => ResolvedTypedContextPublicationBinding.DeserializeCanonical(Encoding.UTF8.GetBytes(badBinding))).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Profile_matrix_document_budget_and_version_zero_are_exact()
    {
        foreach (var x in new[] { ("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal), ("schadensfresse-champions-league-match-rules-only-v1", BundesligaSeasonSubcompetition.ChampionsLeague), ("schadensfresse-champions-league-bonus-rules-only-v1", BundesligaSeasonSubcompetition.ChampionsLeague) })
            await Assert.That(() => SchadensfresseTypedContextProfiles.ValidateManifestStructure(Manifest(x.Item1, x.Item2, Now), 8192)).ThrowsNothing();
        await Assert.That(() => SchadensfresseTypedContextProfiles.ValidateManifestStructure(Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now, [Doc(0)]), 0)).ThrowsNothing();
        await Assert.That(() => SchadensfresseTypedContextProfiles.ValidateManifestStructure(Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now), 8193)).Throws<InvalidDataException>();
        await Assert.That(() => SchadensfresseTypedContextProfiles.ValidateManifestStructure(Manifest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, Now, [Doc(7), Doc(8)]), 0)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Physical_key_fixture_is_injective_and_rejects_noncanonical_encodings()
    {
        var key = new ResolvedTypedContextPublicationBindingKey("ab", "c", "schadensfresse-dfb-pokal-rules-only-v1", Seed);
        var alias = new ResolvedTypedContextPublicationBindingKey("a", "bc", "schadensfresse-dfb-pokal-rules-only-v1", Seed);
        var tuple = "[\"ab\",\"c\",\"schadensfresse-dfb-pokal-rules-only-v1\",\"" + Seed + "\"]";
        await Assert.That(Encoding.UTF8.GetString(key.CanonicalTupleBytes)).IsEqualTo(tuple);
        await Assert.That(key.PhysicalId).IsEqualTo("WyJhYiIsImMiLCJzY2hhZGVuc2ZyZXNzZS1kZmItcG9rYWwtcnVsZXMtb25seS12MSIsIjUyY2U3YmE0NDMwZDA3ZWQ3MTUyOGE3Y2U0OGZlZTQ5OWUyNWI5ZGQzMDNiZDdiY2UyMmVlZDE3YTE5MjE2NjAiXQ");
        await Assert.That(key.PhysicalId).IsNotEqualTo(alias.PhysicalId);
        await Assert.That(TypedContextCanonicalJson.DeserializePhysicalBindingId(key.PhysicalId)).IsEqualTo(key);
        foreach (var bad in new[] { key.PhysicalId + "=", key.PhysicalId[..10], "!", "_w" })
            await Assert.That(() => TypedContextCanonicalJson.DeserializePhysicalBindingId(bad)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Binding_result_identity_drifts_and_exact_readback_fail_closed()
    {
        var initial = Binding(Now.AddMinutes(-1));
        await Assert.That(ResolvedTypedContextPublicationBindingContract.SelectEffective(null, initial, Now).Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.Created);
        await Assert.That(ResolvedTypedContextPublicationBindingContract.SelectEffective(initial, Binding(Now), Now).Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.Updated);
        var equal = ResolvedTypedContextPublicationBindingContract.SelectEffective(initial, initial, Now);
        var older = ResolvedTypedContextPublicationBindingContract.SelectEffective(initial, Binding(Now.AddMinutes(-2)), Now);
        await Assert.That(equal.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.NoOp);
        await Assert.That(older.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.NoOp);
        await Assert.That(older.Succeeded).IsTrue();
        await Assert.That(older.EffectiveBinding).IsEqualTo(initial);
        foreach (var drift in new[] { NewBinding(initial.SeasonPartition, initial, profile: "schadensfresse-champions-league-match-rules-only-v1", sub: BundesligaSeasonSubcompetition.ChampionsLeague), NewBinding(initial.SeasonPartition, initial, seed: new string('a', 64)), NewBinding(initial.SeasonPartition, initial, document: Doc(8)) })
            await Assert.That(ResolvedTypedContextPublicationBindingContract.SelectEffective(initial, drift, Now).Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.IdentityDrift);
        await Assert.That(() => ResolvedTypedContextPublicationBindingContract.SelectEffective(initial, NewBinding("other", initial), Now)).Throws<InvalidDataException>();
        await Assert.That(() => ResolvedTypedContextPublicationBindingContract.SelectEffective(initial, NewBinding(initial.SeasonPartition, initial, community: "other"), Now)).Throws<InvalidDataException>();
        ResolvedTypedContextPublicationBindingContract.ValidateExactReadback(initial.Key, initial, initial, Now);
        await Assert.That(() => ResolvedTypedContextPublicationBindingContract.ValidateExactReadback(initial.Key, initial, Binding(Now), Now)).ThrowsNothing();
        foreach (var rejected in new[] { Binding(Now.AddMinutes(-2)), Binding(Now.AddHours(-25)), Binding(Now.AddTicks(1)), NewBinding("other", initial), NewBinding(initial.SeasonPartition, initial, community: "other"), NewBinding(initial.SeasonPartition, initial, seed: new string('a', 64)), NewBinding(initial.SeasonPartition, initial, document: Doc(8)) })
            await Assert.That(() => ResolvedTypedContextPublicationBindingContract.ValidateExactReadback(initial.Key, initial, rejected, Now)).Throws<InvalidDataException>();
        foreach (var document in new[] { new ResolvedTypedContextDocument("Other", Doc(7).Name, 7, Content), new ResolvedTypedContextDocument("Context", "other.md", 7, Content), new ResolvedTypedContextDocument("Context", Doc(7).Name, 7, new string('a', 64)) })
            await Assert.That(() => ResolvedTypedContextPublicationBindingContract.ValidateExactReadback(initial.Key, initial, NewBinding(initial.SeasonPartition, initial, document: document), Now)).Throws<InvalidDataException>();
        var schemaDrift = new ResolvedTypedContextPublicationBinding(initial.SeasonPartition, initial.CommunityContext, initial.ProfileId, initial.RoutingSeedSha256, initial.BundesligaSeasonSubcompetition, Now, "other", initial.CanonicalRulesSha256, initial.Document);
        var hashDrift = new ResolvedTypedContextPublicationBinding(initial.SeasonPartition, initial.CommunityContext, initial.ProfileId, initial.RoutingSeedSha256, initial.BundesligaSeasonSubcompetition, Now, initial.RulesSchemaVersion, new string('a', 64), initial.Document);
        await Assert.That(() => ResolvedTypedContextPublicationBindingContract.ValidateExactReadback(initial.Key, initial, schemaDrift, Now)).Throws<InvalidDataException>();
        await Assert.That(() => ResolvedTypedContextPublicationBindingContract.ValidateExactReadback(initial.Key, initial, hashDrift, Now)).Throws<InvalidDataException>();
    }

    private static string Mutate(string json, string mutation) => mutation switch { " \u0000" => " " + json, "\uFEFF" => "\uFEFF" + json, "\n" => json + "\n", "missing-season" => json.Replace("\"seasonPartition\":\"bundesliga-2026-27\",", "", StringComparison.Ordinal), "reordered-root" => json.Replace("\"seasonPartition\":\"bundesliga-2026-27\",\"communityContext\":\"schadensfresse\"", "\"communityContext\":\"schadensfresse\",\"seasonPartition\":\"bundesliga-2026-27\"", StringComparison.Ordinal), "\"extra\":true," => json.Replace("{", "{\"extra\":true,", StringComparison.Ordinal), "\"seasonPartition\":\"bundesliga-2026-27\"," => json.Replace("{", "{\"seasonPartition\":\"bundesliga-2026-27\",", StringComparison.Ordinal), "\"SeasonPartition\"" => json.Replace("\"seasonPartition\"", "\"SeasonPartition\"", StringComparison.Ordinal), _ => ReplaceValue(json, mutation) };
    private static string ReplaceValue(string json, string pair) { var colon = pair.IndexOf(':'); var property = pair[..colon]; var replacement = pair[(colon + 1)..]; var start = json.IndexOf(property, StringComparison.Ordinal); if (start < 0) return json; var valueStart = start + property.Length + 1; var end = json.IndexOfAny([',', '}'], valueStart); return json[..valueStart] + replacement + json[end..]; }
    private static ResolvedTypedContextDocument Doc(int version) => new("Context", "community-rules-schadensfresse.md", version, Content);
    private static ResolvedTypedContextManifest Manifest(string profile, BundesligaSeasonSubcompetition sub, DateTimeOffset observed, IEnumerable<ResolvedTypedContextDocument>? documents = null) => new("bundesliga-2026-27", "schadensfresse", sub, profile, Seed, observed, SchadensfresseRulesCanonicalJson.SchemaVersion, SchadensfresseRulesCanonicalJson.CanonicalSha256, documents ?? [Doc(7)]);
    private static ResolvedTypedContextPublicationBinding Binding(DateTimeOffset observed) => NewBinding("bundesliga-2026-27", null, observed: observed);
    private static ResolvedTypedContextPublicationBinding NewBinding(string season, ResolvedTypedContextPublicationBinding? source, string? community = null, string? profile = null, string? seed = null, BundesligaSeasonSubcompetition? sub = null, ResolvedTypedContextDocument? document = null, DateTimeOffset? observed = null) => new(season, community ?? source?.CommunityContext ?? "schadensfresse", profile ?? source?.ProfileId ?? "schadensfresse-dfb-pokal-rules-only-v1", seed ?? source?.RoutingSeedSha256 ?? Seed, sub ?? source?.BundesligaSeasonSubcompetition ?? BundesligaSeasonSubcompetition.DfbPokal, observed ?? source?.RulesObservedAt ?? Now, source?.RulesSchemaVersion ?? SchadensfresseRulesCanonicalJson.SchemaVersion, source?.CanonicalRulesSha256 ?? SchadensfresseRulesCanonicalJson.CanonicalSha256, document ?? source?.Document ?? Doc(7));
}
