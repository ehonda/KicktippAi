using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Shared;

namespace Orchestrator.Tests.Commands.Shared;

public sealed class SchadensfresseTypedContextResolverTests
{
    private static readonly DateTimeOffset Evaluation = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private const string Seed = "52ce7ba4430d07ed71528a7ce48fee499e25b9dd303bd7bce22eed17a1921660";

    [Test]
    [Arguments("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal)]
    [Arguments("schadensfresse-champions-league-match-rules-only-v1", BundesligaSeasonSubcompetition.ChampionsLeague)]
    [Arguments("schadensfresse-champions-league-bonus-rules-only-v1", BundesligaSeasonSubcompetition.ChampionsLeague)]
    public async Task Exact_current_binding_and_document_resolve_all_profiles(string profile, BundesligaSeasonSubcompetition subcompetition)
    {
        var binding = CreateBinding(profile, subcompetition);
        var published = CreateDocument(binding.Document, "exact rules");
        binding = WithDocument(binding, new ResolvedTypedContextDocument(binding.Document.Kind, binding.Document.Name, binding.Document.Version, DocumentPublicationContract.ComputeContentSha256(published.Content)));
        var (resolver, bindings, documents) = CreateResolver(binding, published);

        var result = await resolver.ResolveAsync(CreateRequest(profile, subcompetition));

        await Assert.That(result.Documents).HasCount().EqualTo(1);
        await Assert.That(result.Documents[0]).IsEqualTo(new DocumentContext(published.DocumentName, published.Content));
        await Assert.That(result.GenerationManifest.Documents).IsEquivalentTo([binding.Document]);
        await Assert.That(result.GenerationManifest.SeasonPartition).IsEqualTo(binding.SeasonPartition);
        await Assert.That(result.GenerationManifest.CommunityContext).IsEqualTo(binding.CommunityContext);
        await Assert.That(result.GenerationManifest.BundesligaSeasonSubcompetition).IsEqualTo(binding.BundesligaSeasonSubcompetition);
        await Assert.That(result.GenerationManifest.ProfileId).IsEqualTo(binding.ProfileId);
        await Assert.That(result.GenerationManifest.RoutingSeedSha256).IsEqualTo(binding.RoutingSeedSha256);
        await Assert.That(result.GenerationManifest.RulesObservedAt).IsEqualTo(binding.RulesObservedAt);
        await Assert.That(result.GenerationManifest.RulesSchemaVersion).IsEqualTo(binding.RulesSchemaVersion);
        await Assert.That(result.GenerationManifest.CanonicalRulesSha256).IsEqualTo(binding.CanonicalRulesSha256);
        await Assert.That(result.Measurement).IsEqualTo(BonusContextBudgetEstimator.Measure(result.Documents));
        await Assert.That(result.QualityLimitation).Contains("Rules-only");
        bindings.Verify(repository => repository.GetExactAsync(binding.Key, It.IsAny<CancellationToken>()), Times.Once);
        documents.Verify(repository => repository.GetContextDocumentAsync(binding.Document.Name, binding.Document.Version, binding.CommunityContext, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Noncanonical_request_fails_before_clock_or_repository_activity()
    {
        var bindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
        var documents = new Mock<IContextRepository>(MockBehavior.Strict);
        var clock = new CountingTimeProvider(Evaluation);
        var resolver = new SchadensfresseTypedContextResolver(bindings.Object, documents.Object, clock);

        await Assert.That(() => resolver.ResolveAsync(CreateRequest("wrong", BundesligaSeasonSubcompetition.DfbPokal)))
            .Throws<InvalidDataException>();
        await Assert.That(clock.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Every_noncanonical_request_identity_fails_before_clock_or_repository_activity()
    {
        var canonical = CreateRequest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal);
        var requests = new[]
        {
            canonical with { SeasonPartition = "wm26" },
            canonical with { CommunityContext = "pes-squad" },
            canonical with { BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague },
            canonical with { ProfileId = "schadensfresse-champions-league-match-rules-only-v1" },
            canonical with { RoutingSeedSha256 = "A" + Seed[1..] },
            canonical with { RoutingSeedSha256 = "not-a-sha256" }
        };

        foreach (var request in requests)
        {
            var bindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
            var documents = new Mock<IContextRepository>(MockBehavior.Strict);
            var clock = new CountingTimeProvider(Evaluation);
            var resolver = new SchadensfresseTypedContextResolver(bindings.Object, documents.Object, clock);
            await Assert.That(() => resolver.ResolveAsync(request)).Throws<InvalidDataException>();
            await Assert.That(clock.Calls).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Missing_or_stale_binding_fails_closed_before_document_activity()
    {
        var missingBindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
        var documents = new Mock<IContextRepository>(MockBehavior.Strict);
        var request = CreateRequest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal);
        var key = new ResolvedTypedContextPublicationBindingKey(request.SeasonPartition, request.CommunityContext, request.ProfileId, request.RoutingSeedSha256);
        missingBindings.Setup(repository => repository.GetExactAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync((ResolvedTypedContextPublicationBinding?)null);
        var resolver = new SchadensfresseTypedContextResolver(missingBindings.Object, documents.Object, new CountingTimeProvider(Evaluation));
        await Assert.That(() => resolver.ResolveAsync(request)).Throws<InvalidDataException>();

        var stale = CreateBinding(request.ProfileId, request.BundesligaSeasonSubcompetition, Evaluation.AddHours(-24).AddTicks(-1));
        var (staleResolver, _, staleDocuments) = CreateResolver(stale, CreateDocument(stale.Document, "rules"));
        await Assert.That(() => staleResolver.ResolveAsync(request)).Throws<InvalidDataException>();
        staleDocuments.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Future_malformed_and_cross_key_binding_responses_fail_closed_before_document_activity()
    {
        var request = CreateRequest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal);
        var future = CreateBinding(request.ProfileId, request.BundesligaSeasonSubcompetition, Evaluation.AddTicks(1));
        var malformed = new ResolvedTypedContextPublicationBinding(
            future.SeasonPartition, future.CommunityContext, future.ProfileId, future.RoutingSeedSha256,
            future.BundesligaSeasonSubcompetition, Evaluation, "wrong-schema", future.CanonicalRulesSha256, future.Document);
        var crossKey = new ResolvedTypedContextPublicationBinding(
            future.SeasonPartition, future.CommunityContext, future.ProfileId, new string('a', 64),
            future.BundesligaSeasonSubcompetition, Evaluation, future.RulesSchemaVersion, future.CanonicalRulesSha256, future.Document);

        foreach (var response in new[] { future, malformed, crossKey })
        {
            var bindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
            var documents = new Mock<IContextRepository>(MockBehavior.Strict);
            bindings.Setup(repository => repository.GetExactAsync(It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var resolver = new SchadensfresseTypedContextResolver(bindings.Object, documents.Object, new CountingTimeProvider(Evaluation));
            await Assert.That(() => resolver.ResolveAsync(request)).Throws<InvalidDataException>();
            documents.VerifyNoOtherCalls();
        }
    }

    [Test]
    public async Task Wrong_exact_document_identity_or_hash_drift_fails_closed()
    {
        var binding = CreateBinding("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal);
        var wrong = new ContextDocument(binding.Document.Name, "different bytes", binding.Document.Version, Evaluation);
        var (resolver, _, _) = CreateResolver(binding, wrong);

        await Assert.That(() => resolver.ResolveAsync(CreateRequest(binding.ProfileId, binding.BundesligaSeasonSubcompetition)))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Missing_wrong_name_wrong_version_and_hash_mismatched_documents_fail_closed()
    {
        var binding = CreateBoundBinding("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, "rules");
        var missing = (ContextDocument?)null;
        var wrongName = new ContextDocument("other.md", "rules", binding.Document.Version, Evaluation);
        var wrongVersion = new ContextDocument(binding.Document.Name, "rules", binding.Document.Version + 1, Evaluation);
        var wrongHash = new ContextDocument(binding.Document.Name, "other bytes", binding.Document.Version, Evaluation);
        foreach (var document in new[] { missing, wrongName, wrongVersion, wrongHash })
        {
            var bindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
            var documents = new Mock<IContextRepository>(MockBehavior.Strict);
            bindings.Setup(repository => repository.GetExactAsync(binding.Key, It.IsAny<CancellationToken>())).ReturnsAsync(binding);
            documents.Setup(repository => repository.GetContextDocumentAsync(binding.Document.Name, binding.Document.Version, binding.CommunityContext, It.IsAny<CancellationToken>())).ReturnsAsync(document);
            var resolver = new SchadensfresseTypedContextResolver(bindings.Object, documents.Object, new CountingTimeProvider(Evaluation));
            await Assert.That(() => resolver.ResolveAsync(CreateRequest(binding.ProfileId, binding.BundesligaSeasonSubcompetition))).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Exact_2048_token_render_is_accepted_and_one_token_over_is_rejected_without_truncation()
    {
        var accepted = CreateBinding("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal);
        var acceptedDocument = CreateDocument(accepted.Document, ContentForEstimatedTokens(2048));
        accepted = WithDocument(accepted, new ResolvedTypedContextDocument(accepted.Document.Kind, accepted.Document.Name, accepted.Document.Version, DocumentPublicationContract.ComputeContentSha256(acceptedDocument.Content)));
        var (acceptedResolver, _, _) = CreateResolver(accepted, acceptedDocument);
        var acceptedResult = await acceptedResolver.ResolveAsync(CreateRequest(accepted.ProfileId, accepted.BundesligaSeasonSubcompetition));
        await Assert.That(acceptedResult.Measurement.EstimatedTokens).IsEqualTo(2048);

        var over = WithDocument(accepted, new ResolvedTypedContextDocument(accepted.Document.Kind, accepted.Document.Name, accepted.Document.Version, DocumentPublicationContract.ComputeContentSha256(ContentForEstimatedTokens(2049))));
        var overDocument = CreateDocument(over.Document, ContentForEstimatedTokens(2049));
        var (overResolver, _, _) = CreateResolver(over, overDocument);
        await Assert.That(() => overResolver.ResolveAsync(CreateRequest(over.ProfileId, over.BundesligaSeasonSubcompetition)))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Historical_generation_manifest_is_retained_when_every_non_observation_field_matches()
    {
        var binding = CreateBinding("schadensfresse-champions-league-match-rules-only-v1", BundesligaSeasonSubcompetition.ChampionsLeague);
        var document = CreateDocument(binding.Document, "rules");
        binding = WithDocument(binding, new ResolvedTypedContextDocument(binding.Document.Kind, binding.Document.Name, binding.Document.Version, DocumentPublicationContract.ComputeContentSha256(document.Content)));
        var historical = new ResolvedTypedContextManifest(binding.SeasonPartition, binding.CommunityContext, binding.BundesligaSeasonSubcompetition, binding.ProfileId, binding.RoutingSeedSha256, Evaluation.AddDays(-2), binding.RulesSchemaVersion, binding.CanonicalRulesSha256, [binding.Document]);
        var (resolver, _, _) = CreateResolver(binding, document);

        var result = await resolver.ResolveAsync(CreateRequest(binding.ProfileId, binding.BundesligaSeasonSubcompetition, historical));

        await Assert.That(ReferenceEquals(result.GenerationManifest, historical)).IsTrue();
        await Assert.That(result.GenerationManifest.RulesObservedAt).IsEqualTo(Evaluation.AddDays(-2));
    }

    [Test]
    public async Task Generation_manifest_drift_and_cancellation_fail_without_fallback_activity()
    {
        var binding = CreateBinding("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal);
        var document = CreateDocument(binding.Document, "rules");
        binding = WithDocument(binding, new ResolvedTypedContextDocument(binding.Document.Kind, binding.Document.Name, binding.Document.Version, DocumentPublicationContract.ComputeContentSha256(document.Content)));
        var drifted = new ResolvedTypedContextManifest(binding.SeasonPartition, binding.CommunityContext, binding.BundesligaSeasonSubcompetition, binding.ProfileId, new string('a', 64), Evaluation.AddDays(-10), binding.RulesSchemaVersion, binding.CanonicalRulesSha256, [binding.Document]);
        var (resolver, _, _) = CreateResolver(binding, document);
        await Assert.That(() => resolver.ResolveAsync(CreateRequest(binding.ProfileId, binding.BundesligaSeasonSubcompetition, drifted))).Throws<InvalidDataException>();

        var cancelledBindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
        var cancelledDocuments = new Mock<IContextRepository>(MockBehavior.Strict);
        var cancelled = new CancellationToken(canceled: true);
        var cancelledResolver = new SchadensfresseTypedContextResolver(cancelledBindings.Object, cancelledDocuments.Object, new CountingTimeProvider(Evaluation));
        await Assert.That(() => cancelledResolver.ResolveAsync(CreateRequest(binding.ProfileId, binding.BundesligaSeasonSubcompetition), cancelled)).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Every_non_observation_generation_manifest_field_or_document_drift_fails_closed()
    {
        var binding = CreateBoundBinding("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, "rules");
        var original = new ResolvedTypedContextManifest(binding.SeasonPartition, binding.CommunityContext, binding.BundesligaSeasonSubcompetition, binding.ProfileId, binding.RoutingSeedSha256, Evaluation.AddDays(-10), binding.RulesSchemaVersion, binding.CanonicalRulesSha256, [binding.Document]);
        var wrongKind = new ResolvedTypedContextDocument("Other", binding.Document.Name, binding.Document.Version, binding.Document.ContentSha256);
        var wrongName = new ResolvedTypedContextDocument(binding.Document.Kind, "other.md", binding.Document.Version, binding.Document.ContentSha256);
        var wrongVersion = new ResolvedTypedContextDocument(binding.Document.Kind, binding.Document.Name, binding.Document.Version + 1, binding.Document.ContentSha256);
        var wrongHash = new ResolvedTypedContextDocument(binding.Document.Kind, binding.Document.Name, binding.Document.Version, new string('a', 64));
        var drifts = new[]
        {
            new ResolvedTypedContextManifest("wrong", original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, original.Documents),
            new ResolvedTypedContextManifest(original.SeasonPartition, "wrong", original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, original.Documents),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, BundesligaSeasonSubcompetition.ChampionsLeague, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, original.Documents),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, "schadensfresse-champions-league-match-rules-only-v1", original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, original.Documents),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, new string('a', 64), original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, original.Documents),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, "wrong", original.CanonicalRulesSha256, original.Documents),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, new string('a', 64), original.Documents),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, [wrongKind]),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, [wrongName]),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, [wrongVersion]),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, [wrongHash]),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, []),
            new ResolvedTypedContextManifest(original.SeasonPartition, original.CommunityContext, original.BundesligaSeasonSubcompetition, original.ProfileId, original.RoutingSeedSha256, original.RulesObservedAt, original.RulesSchemaVersion, original.CanonicalRulesSha256, [binding.Document, binding.Document])
        };

        foreach (var drift in drifts)
        {
            var (resolver, _, _) = CreateResolver(binding, CreateDocument(binding.Document, "rules"));
            await Assert.That(() => resolver.ResolveAsync(CreateRequest(binding.ProfileId, binding.BundesligaSeasonSubcompetition, drift))).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Clock_is_read_once_result_is_value_safe_and_exact_cancellation_token_is_forwarded()
    {
        var binding = CreateBoundBinding("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal, "rules");
        var published = CreateDocument(binding.Document, "rules");
        var bindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
        var documents = new Mock<IContextRepository>(MockBehavior.Strict);
        using var source = new CancellationTokenSource();
        bindings.Setup(repository => repository.GetExactAsync(binding.Key, source.Token)).ReturnsAsync(binding);
        documents.Setup(repository => repository.GetContextDocumentAsync(binding.Document.Name, binding.Document.Version, binding.CommunityContext, source.Token)).ReturnsAsync(published);
        var clock = new CountingTimeProvider(Evaluation);
        var resolver = new SchadensfresseTypedContextResolver(bindings.Object, documents.Object, clock);

        var result = await resolver.ResolveAsync(CreateRequest(binding.ProfileId, binding.BundesligaSeasonSubcompetition), source.Token);
        published.DocumentName = "mutated.md";
        published.Content = "mutated";
        var expanded = result.Documents.Add(new DocumentContext("extra", "not retained"));

        await Assert.That(clock.Calls).IsEqualTo(1);
        await Assert.That(result.Documents).HasCount().EqualTo(1);
        await Assert.That(expanded).HasCount().EqualTo(2);
        await Assert.That(result.Documents[0]).IsEqualTo(new DocumentContext(binding.Document.Name, "rules"));
        await Assert.That(result.GenerationManifest).IsEqualTo(new ResolvedTypedContextManifest(binding.SeasonPartition, binding.CommunityContext, binding.BundesligaSeasonSubcompetition, binding.ProfileId, binding.RoutingSeedSha256, binding.RulesObservedAt, binding.RulesSchemaVersion, binding.CanonicalRulesSha256, [binding.Document]));
    }

    [Test]
    public async Task Repository_originated_cancellation_propagates_without_document_or_fallback_activity()
    {
        var request = CreateRequest("schadensfresse-dfb-pokal-rules-only-v1", BundesligaSeasonSubcompetition.DfbPokal);
        var key = new ResolvedTypedContextPublicationBindingKey(request.SeasonPartition, request.CommunityContext, request.ProfileId, request.RoutingSeedSha256);
        var bindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
        var documents = new Mock<IContextRepository>(MockBehavior.Strict);
        using var source = new CancellationTokenSource();
        bindings.Setup(repository => repository.GetExactAsync(key, source.Token)).ThrowsAsync(new OperationCanceledException(source.Token));
        var resolver = new SchadensfresseTypedContextResolver(bindings.Object, documents.Object, new CountingTimeProvider(Evaluation));

        await Assert.That(() => resolver.ResolveAsync(request, source.Token)).Throws<OperationCanceledException>();
        documents.VerifyNoOtherCalls();
    }

    private static (SchadensfresseTypedContextResolver Resolver, Mock<IResolvedTypedContextPublicationBindingRepository> Bindings, Mock<IContextRepository> Documents) CreateResolver(ResolvedTypedContextPublicationBinding binding, ContextDocument document)
    {
        var bindings = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
        var documents = new Mock<IContextRepository>(MockBehavior.Strict);
        bindings.Setup(repository => repository.GetExactAsync(binding.Key, It.IsAny<CancellationToken>())).ReturnsAsync(binding);
        documents.Setup(repository => repository.GetContextDocumentAsync(binding.Document.Name, binding.Document.Version, binding.CommunityContext, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        return (new SchadensfresseTypedContextResolver(bindings.Object, documents.Object, new CountingTimeProvider(Evaluation)), bindings, documents);
    }

    private static SchadensfresseTypedContextResolutionRequest CreateRequest(string profile, BundesligaSeasonSubcompetition subcompetition, ResolvedTypedContextManifest? existing = null) =>
        new(SchadensfresseTypedContextProfiles.SeasonPartition, SchadensfresseTypedContextProfiles.CommunityContext, subcompetition, profile, Seed, existing);

    private static ResolvedTypedContextPublicationBinding CreateBinding(string profile, BundesligaSeasonSubcompetition subcompetition, DateTimeOffset? observedAt = null) =>
        new(SchadensfresseTypedContextProfiles.SeasonPartition, SchadensfresseTypedContextProfiles.CommunityContext, profile, Seed, subcompetition, observedAt ?? Evaluation, SchadensfresseRulesCanonicalJson.SchemaVersion, SchadensfresseRulesCanonicalJson.CanonicalSha256, new ResolvedTypedContextDocument(SchadensfresseTypedContextProfiles.RulesDocumentKind, SchadensfresseTypedContextProfiles.RulesDocumentName, 7, DocumentPublicationContract.ComputeContentSha256(string.Empty)));

    private static ResolvedTypedContextPublicationBinding CreateBoundBinding(string profile, BundesligaSeasonSubcompetition subcompetition, string content)
    {
        var binding = CreateBinding(profile, subcompetition);
        return WithDocument(binding, new ResolvedTypedContextDocument(binding.Document.Kind, binding.Document.Name, binding.Document.Version, DocumentPublicationContract.ComputeContentSha256(content)));
    }

    private static ResolvedTypedContextPublicationBinding WithDocument(ResolvedTypedContextPublicationBinding binding, ResolvedTypedContextDocument document) =>
        new(binding.SeasonPartition, binding.CommunityContext, binding.ProfileId, binding.RoutingSeedSha256, binding.BundesligaSeasonSubcompetition, binding.RulesObservedAt, binding.RulesSchemaVersion, binding.CanonicalRulesSha256, document);

    private static ContextDocument CreateDocument(ResolvedTypedContextDocument identity, string content) =>
        new(identity.Name, content, identity.Version, Evaluation);

    private static string ContentForEstimatedTokens(int tokens)
    {
        var overhead = BonusContextBudgetEstimator.Measure([new DocumentContext(SchadensfresseTypedContextProfiles.RulesDocumentName, string.Empty)]).Utf8Bytes;
        return new string('x', checked((tokens * 4) - overhead));
    }

    private sealed class CountingTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public int Calls { get; private set; }
        public override DateTimeOffset GetUtcNow() { Calls++; return value; }
    }
}
