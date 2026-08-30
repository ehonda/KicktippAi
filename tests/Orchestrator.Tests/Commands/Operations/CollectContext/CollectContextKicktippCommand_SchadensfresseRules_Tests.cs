using System.Net;
using System.Text;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>Command-boundary tests: the authenticated semantic gate runs before any target context write.</summary>
public class CollectContextKicktippCommand_SchadensfresseRules_Tests : CollectContextKicktippCommandTests_Base
{
    private static readonly string RulesMarkdown = File.ReadAllText(Path.Combine(
        SolutionPathUtility.FindSolutionRoot(),
        "community-rules",
        "schadensfresse.md"));

    [Test]
    public async Task Login_source_fails_before_provider_or_context_publication()
    {
        var context = CreateCollectContextCommandApp(matchesWithHistory: Option.Some(new List<MatchWithHistory>()));
        context.KicktippClientFactory.Setup(factory => factory.CreateAuthenticatedHttpClient())
            .Returns(new HttpClient(new StaticResponseHandler("<html><title>Login</title><form id='loginFormular'></form></html>")));

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "collect-context-kicktipp",
            "--community-context", "schadensfresse",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("login page");
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Ordinary_publication_uses_one_document_atomic_result_and_exact_effective_version_readback()
    {
        var bindingRepository = CreateMockResolvedTypedContextPublicationBindingRepository();
        var documents = CreateMatchContextDocuments(communityContext: "schadensfresse")
            .Values
            .Select(document => new DocumentContext(
                document.DocumentName,
                document.DocumentName == SchadensfresseRulesPublicationGate.DocumentName
                    ? RulesMarkdown
                    : document.Content))
            .ToList();
        var context = CreateCollectContextCommandApp(contextDocuments: documents);
        context.FirebaseServiceFactory.Setup(factory => factory.CreateResolvedTypedContextPublicationBindingRepository())
            .Returns(bindingRepository.Object);
        context.KicktippClientFactory.Setup(factory => factory.CreateAuthenticatedHttpClient())
            .Returns(new HttpClient(new StaticResponseHandler(SanitizedFixture)));
        context.ContextRepository.Setup(repository => repository.SaveContextDocumentsAtomicallyAsync(
                It.Is<IReadOnlyList<ContextDocumentWrite>>(writes =>
                    writes.Count == 1
                    && writes[0].DocumentName == SchadensfresseRulesPublicationGate.DocumentName),
                "schadensfresse",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ContextDocumentSaveResult(
                    SchadensfresseRulesPublicationGate.DocumentName,
                    null,
                    7)
            ]);
        context.ContextRepository.Setup(repository => repository.GetContextDocumentAsync(
                SchadensfresseRulesPublicationGate.DocumentName,
                7,
                "schadensfresse",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(
                SchadensfresseRulesPublicationGate.DocumentName,
                RulesMarkdown,
                7,
                DateTimeOffset.UnixEpoch));

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "collect-context-kicktipp",
            "--community-context", "schadensfresse",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Context collection completed");
        context.ContextRepository.Verify(repository => repository.GetContextDocumentAsync(
            SchadensfresseRulesPublicationGate.DocumentName,
            7,
            "schadensfresse",
            It.IsAny<CancellationToken>()), Times.Once);
        context.ContextRepository.Verify(repository => repository.GetLatestContextDocumentAsync(
            SchadensfresseRulesPublicationGate.DocumentName,
            "schadensfresse",
            It.IsAny<CancellationToken>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            SchadensfresseRulesPublicationGate.DocumentName,
            It.IsAny<string>(),
            "schadensfresse",
            It.IsAny<CancellationToken>()), Times.Never);
        var candidates = bindingRepository.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IResolvedTypedContextPublicationBindingRepository.UpsertExactAsync))
            .Select(invocation => (ResolvedTypedContextPublicationBinding)invocation.Arguments[0])
            .ToArray();
        await Assert.That(candidates.Select(candidate => candidate.ProfileId)).IsEquivalentTo(
        [
            "schadensfresse-dfb-pokal-rules-only-v1",
            "schadensfresse-champions-league-match-rules-only-v1",
            "schadensfresse-champions-league-bonus-rules-only-v1"
        ]);
        await Assert.That(candidates.Select(candidate => candidate.Key).Distinct().Count()).IsEqualTo(3);
        await Assert.That(candidates.All(candidate =>
            candidate.SeasonPartition == CompetitionIds.Bundesliga2026_27
            && candidate.CommunityContext == "schadensfresse"
            && candidate.RoutingSeedSha256 == BundesligaSeasonRoutingSeed.Default.CanonicalSha256
            && candidate.Document.Name == SchadensfresseRulesPublicationGate.DocumentName
            && candidate.Document.Version == 7
            && candidate.Document.ContentSha256 == DocumentPublicationContract.ComputeContentSha256(RulesMarkdown))).IsTrue();
        await Assert.That(candidates.Select(candidate => candidate.RulesObservedAt).Distinct().Count()).IsEqualTo(1);
        bindingRepository.Verify(repository => repository.GetExactAsync(
            It.IsAny<ResolvedTypedContextPublicationBindingKey>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task Interleaved_different_then_original_latest_cannot_substitute_for_transaction_selected_version()
    {
        var documents = CreateMatchContextDocuments(communityContext: "schadensfresse")
            .Values
            .Select(document => new DocumentContext(
                document.DocumentName,
                document.DocumentName == SchadensfresseRulesPublicationGate.DocumentName
                    ? RulesMarkdown
                    : document.Content))
            .ToList();
        var context = CreateCollectContextCommandApp(contextDocuments: documents);
        context.KicktippClientFactory.Setup(factory => factory.CreateAuthenticatedHttpClient())
            .Returns(new HttpClient(new StaticResponseHandler(SanitizedFixture)));
        context.ContextRepository.Setup(repository => repository.SaveContextDocumentsAtomicallyAsync(
                It.Is<IReadOnlyList<ContextDocumentWrite>>(writes =>
                    writes.Count == 1
                    && writes[0].DocumentName == SchadensfresseRulesPublicationGate.DocumentName),
                "schadensfresse",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ContextDocumentSaveResult(
                    SchadensfresseRulesPublicationGate.DocumentName,
                    null,
                    3)
            ]);
        context.ContextRepository.Setup(repository => repository.GetContextDocumentAsync(
                SchadensfresseRulesPublicationGate.DocumentName,
                3,
                "schadensfresse",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(
                SchadensfresseRulesPublicationGate.DocumentName,
                "different bytes selected by the transaction",
                3,
                DateTimeOffset.UnixEpoch));
        context.ContextRepository.Setup(repository => repository.GetLatestContextDocumentAsync(
                SchadensfresseRulesPublicationGate.DocumentName,
                "schadensfresse",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(
                SchadensfresseRulesPublicationGate.DocumentName,
                RulesMarkdown,
                5,
                DateTimeOffset.UnixEpoch));

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "collect-context-kicktipp",
            "--community-context", "schadensfresse",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Immutable community-rules publication readback");
        context.ContextRepository.Verify(repository => repository.GetLatestContextDocumentAsync(
            SchadensfresseRulesPublicationGate.DocumentName,
            "schadensfresse",
            It.IsAny<CancellationToken>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.GetContextDocumentAsync(
            SchadensfresseRulesPublicationGate.DocumentName,
            3,
            "schadensfresse",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Exact_binding_identity_drift_fails_closed_after_only_prior_exact_keys_complete()
    {
        var bindingRepository = new Mock<IResolvedTypedContextPublicationBindingRepository>();
        var successful = new Dictionary<ResolvedTypedContextPublicationBindingKey, ResolvedTypedContextPublicationBinding>();
        bindingRepository.Setup(repository => repository.UpsertExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBinding>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBinding candidate, CancellationToken _) =>
            {
                var result = candidate.ProfileId == "schadensfresse-champions-league-match-rules-only-v1"
                    ? TypedContextPublicationBindingUpsertResult.Drift(candidate)
                    : TypedContextPublicationBindingUpsertResult.Created(candidate);
                if (result.Succeeded)
                {
                    successful[candidate.Key] = candidate;
                }

                return Task.FromResult(result);
            });
        bindingRepository.Setup(repository => repository.GetExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBindingKey key, CancellationToken _) =>
                Task.FromResult(successful.TryGetValue(key, out var binding) ? binding : null));
        var context = CreateRulesPublicationContext(bindingRepository);

        var (exitCode, output) = await RunRulesPublicationCommandAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("binding upsert failed");
        await Assert.That(successful.Count).IsEqualTo(1);
        bindingRepository.Verify(repository => repository.UpsertExactAsync(
            It.IsAny<ResolvedTypedContextPublicationBinding>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        bindingRepository.Verify(repository => repository.GetExactAsync(
            It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Exact_binding_readback_identity_mismatch_fails_closed()
    {
        var bindingRepository = new Mock<IResolvedTypedContextPublicationBindingRepository>();
        bindingRepository.Setup(repository => repository.UpsertExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBinding>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBinding candidate, CancellationToken _) =>
                Task.FromResult(TypedContextPublicationBindingUpsertResult.Created(candidate)));
        bindingRepository.Setup(repository => repository.GetExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBindingKey _, CancellationToken _) =>
                Task.FromResult<ResolvedTypedContextPublicationBinding?>(new ResolvedTypedContextPublicationBinding(
                    CompetitionIds.Bundesliga2026_27,
                    "schadensfresse",
                    "schadensfresse-dfb-pokal-rules-only-v1",
                    BundesligaSeasonRoutingSeed.Default.CanonicalSha256,
                    BundesligaSeasonSubcompetition.DfbPokal,
                    DateTimeOffset.UtcNow,
                    SchadensfresseRulesCanonicalJson.SchemaVersion,
                    SchadensfresseRulesCanonicalJson.CanonicalSha256,
                    new ResolvedTypedContextDocument(
                        SchadensfresseTypedContextProfiles.RulesDocumentKind,
                        SchadensfresseRulesPublicationGate.DocumentName,
                        99,
                        DocumentPublicationContract.ComputeContentSha256(RulesMarkdown)))));
        var context = CreateRulesPublicationContext(bindingRepository);

        var (exitCode, output) = await RunRulesPublicationCommandAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Exact binding readback does not match");
    }

    [Test]
    public async Task Missing_exact_binding_readback_fails_closed()
    {
        var bindingRepository = new Mock<IResolvedTypedContextPublicationBindingRepository>();
        bindingRepository.Setup(repository => repository.UpsertExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBinding>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBinding candidate, CancellationToken _) =>
                Task.FromResult(TypedContextPublicationBindingUpsertResult.Created(candidate)));
        bindingRepository.Setup(repository => repository.GetExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResolvedTypedContextPublicationBinding?)null);
        var context = CreateRulesPublicationContext(bindingRepository);

        var (exitCode, output) = await RunRulesPublicationCommandAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("binding readback is missing");
    }

    [Test]
    public async Task Exact_binding_gate_uses_one_evaluation_instant_for_all_profiles_and_inclusive_24_hour_boundary()
    {
        var observation = new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);
        var evaluation = observation.AddHours(24);
        var timeProvider = new ExhaustingTimeProvider(observation, evaluation, evaluation, evaluation);
        var bindingRepository = CreateEffectiveBindingRepository();
        var context = CreateRulesPublicationContext(bindingRepository, timeProvider);

        var (exitCode, output) = await RunRulesPublicationCommandAsync(context);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Context collection completed");
        await Assert.That(timeProvider.UtcNowCallCount).IsEqualTo(4);
        bindingRepository.Verify(repository => repository.GetExactAsync(
            It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task Updated_equal_no_op_and_older_no_op_returned_effective_bindings_all_pass_exact_readback()
    {
        var bindingRepository = new Mock<IResolvedTypedContextPublicationBindingRepository>();
        var effectiveBindings = new Dictionary<ResolvedTypedContextPublicationBindingKey, ResolvedTypedContextPublicationBinding>();
        var outcomes = new List<TypedContextPublicationBindingUpsertResult>();
        var candidates = new List<ResolvedTypedContextPublicationBinding>();
        bindingRepository.Setup(repository => repository.UpsertExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBinding>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBinding candidate, CancellationToken _) =>
            {
                candidates.Add(candidate);
                var result = candidate.ProfileId switch
                {
                    "schadensfresse-dfb-pokal-rules-only-v1" => TypedContextPublicationBindingUpsertResult.Updated(candidate),
                    "schadensfresse-champions-league-match-rules-only-v1" => TypedContextPublicationBindingUpsertResult.NoOp(candidate),
                    "schadensfresse-champions-league-bonus-rules-only-v1" => TypedContextPublicationBindingUpsertResult.NoOp(
                        WithObservation(candidate, candidate.RulesObservedAt.AddTicks(1))),
                    _ => throw new InvalidDataException("Unexpected binding profile.")
                };
                outcomes.Add(result);
                effectiveBindings[candidate.Key] = result.EffectiveBinding;
                return Task.FromResult(result);
            });
        bindingRepository.Setup(repository => repository.GetExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBindingKey key, CancellationToken _) =>
                Task.FromResult(effectiveBindings.TryGetValue(key, out var effective) ? effective : null));
        var context = CreateRulesPublicationContext(bindingRepository);

        var (exitCode, _) = await RunRulesPublicationCommandAsync(context);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(outcomes.Select(outcome => outcome.Disposition)).IsEquivalentTo(
        [
            TypedContextPublicationBindingUpsertDisposition.Updated,
            TypedContextPublicationBindingUpsertDisposition.NoOp,
            TypedContextPublicationBindingUpsertDisposition.NoOp
        ]);
        var olderCandidate = candidates.Single(candidate => candidate.ProfileId == "schadensfresse-champions-league-bonus-rules-only-v1");
        var noOpEffective = outcomes.Single(outcome => outcome.EffectiveBinding.ProfileId == olderCandidate.ProfileId).EffectiveBinding;
        await Assert.That(noOpEffective.RulesObservedAt).IsEqualTo(olderCandidate.RulesObservedAt.AddTicks(1));
    }

    [Test]
    public async Task Malformed_structural_exact_binding_readback_fails_closed()
    {
        var bindingRepository = new Mock<IResolvedTypedContextPublicationBindingRepository>();
        bindingRepository.Setup(repository => repository.UpsertExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBinding>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBinding candidate, CancellationToken _) =>
                Task.FromResult(TypedContextPublicationBindingUpsertResult.Created(candidate)));
        bindingRepository.Setup(repository => repository.GetExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBindingKey key, CancellationToken _) =>
                Task.FromResult<ResolvedTypedContextPublicationBinding?>(new ResolvedTypedContextPublicationBinding(
                    key.SeasonPartition,
                    key.CommunityContext,
                    key.ProfileId,
                    key.RoutingSeedSha256,
                    BundesligaSeasonSubcompetition.DfbPokal,
                    DateTimeOffset.UtcNow,
                    SchadensfresseRulesCanonicalJson.SchemaVersion,
                    SchadensfresseRulesCanonicalJson.CanonicalSha256,
                    new ResolvedTypedContextDocument("Context", "wrong-document.md", 7, DocumentPublicationContract.ComputeContentSha256(RulesMarkdown)))));
        var context = CreateRulesPublicationContext(bindingRepository);

        var (exitCode, output) = await RunRulesPublicationCommandAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Typed context document");
    }

    [Test]
    public async Task Dry_run_validates_rules_profiles_without_creating_or_reading_bindings()
    {
        var bindingRepository = new Mock<IResolvedTypedContextPublicationBindingRepository>(MockBehavior.Strict);
        var context = CreateRulesPublicationContext(bindingRepository);

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "collect-context-kicktipp",
            "--community-context", "schadensfresse",
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run completed");
        bindingRepository.Verify(repository => repository.UpsertExactAsync(
            It.IsAny<ResolvedTypedContextPublicationBinding>(), It.IsAny<CancellationToken>()), Times.Never);
        bindingRepository.Verify(repository => repository.GetExactAsync(
            It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CollectContextKicktippCommandTestContext CreateRulesPublicationContext(
        Mock<IResolvedTypedContextPublicationBindingRepository> bindingRepository,
        TimeProvider? timeProvider = null)
    {
        var documents = CreateMatchContextDocuments(communityContext: "schadensfresse")
            .Values
            .Select(document => new DocumentContext(
                document.DocumentName,
                document.DocumentName == SchadensfresseRulesPublicationGate.DocumentName
                    ? RulesMarkdown
                    : document.Content))
            .ToList();
        var context = CreateCollectContextCommandApp(
            contextDocuments: documents,
            timeProvider: timeProvider is null ? default : Option.Some(timeProvider));
        context.FirebaseServiceFactory.Setup(factory => factory.CreateResolvedTypedContextPublicationBindingRepository())
            .Returns(bindingRepository.Object);
        context.KicktippClientFactory.Setup(factory => factory.CreateAuthenticatedHttpClient())
            .Returns(new HttpClient(new StaticResponseHandler(SanitizedFixture)));
        context.ContextRepository.Setup(repository => repository.SaveContextDocumentsAtomicallyAsync(
                It.Is<IReadOnlyList<ContextDocumentWrite>>(writes =>
                    writes.Count == 1
                    && writes[0].DocumentName == SchadensfresseRulesPublicationGate.DocumentName),
                "schadensfresse",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ContextDocumentSaveResult(SchadensfresseRulesPublicationGate.DocumentName, null, 7)
            ]);
        context.ContextRepository.Setup(repository => repository.GetContextDocumentAsync(
                SchadensfresseRulesPublicationGate.DocumentName,
                7,
                "schadensfresse",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(
                SchadensfresseRulesPublicationGate.DocumentName,
                RulesMarkdown,
                7,
                DateTimeOffset.UnixEpoch));
        return context;
    }

    private static Mock<IResolvedTypedContextPublicationBindingRepository> CreateEffectiveBindingRepository()
    {
        var effectiveBindings = new Dictionary<ResolvedTypedContextPublicationBindingKey, ResolvedTypedContextPublicationBinding>();
        var repository = new Mock<IResolvedTypedContextPublicationBindingRepository>();
        repository.Setup(value => value.UpsertExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBinding>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBinding candidate, CancellationToken _) =>
            {
                effectiveBindings[candidate.Key] = candidate;
                return Task.FromResult(TypedContextPublicationBindingUpsertResult.Created(candidate));
            });
        repository.Setup(value => value.GetExactAsync(
                It.IsAny<ResolvedTypedContextPublicationBindingKey>(), It.IsAny<CancellationToken>()))
            .Returns((ResolvedTypedContextPublicationBindingKey key, CancellationToken _) =>
                Task.FromResult(effectiveBindings.TryGetValue(key, out var effective) ? effective : null));
        return repository;
    }

    private static ResolvedTypedContextPublicationBinding WithObservation(
        ResolvedTypedContextPublicationBinding binding,
        DateTimeOffset observation) => new(
            binding.SeasonPartition,
            binding.CommunityContext,
            binding.ProfileId,
            binding.RoutingSeedSha256,
            binding.BundesligaSeasonSubcompetition,
            observation,
            binding.RulesSchemaVersion,
            binding.CanonicalRulesSha256,
            binding.Document);

    private sealed class ExhaustingTimeProvider(params DateTimeOffset[] utcInstants) : TimeProvider
    {
        private int _utcNowCallCount;

        public int UtcNowCallCount => _utcNowCallCount;

        public override DateTimeOffset GetUtcNow() => _utcNowCallCount < utcInstants.Length
            ? utcInstants[_utcNowCallCount++]
            : throw new InvalidOperationException("The publication-binding gate must not capture another evaluation instant.");

    }

    private static Task<(int ExitCode, string Output)> RunRulesPublicationCommandAsync(
        CollectContextKicktippCommandTestContext context) =>
        RunCommandAsync(
            context.App,
            context.Console,
            "collect-context-kicktipp",
            "--community-context", "schadensfresse",
            "--competition", CompetitionIds.Bundesliga2026_27);

    private sealed class StaticResponseHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
    }

    private const string SanitizedFixture = """
<!doctype html><html><head><title>Schadensfresse</title></head><body><div class="pagecontent"><h2>Sichtbarkeit der Tipps</h2><p>Die Tipps sind erst sichtbar, wenn die Tippzeit abgelaufen ist.</p><h2>Tippmodus</h2><p>Es wird das genaue Ergebnis getippt.</p><p>Es wird das jeweils folgende Ergebnis gewertet:</p><ul><li>DFB-Pokal 2026/27: nach Elfmeterschießen</li><li>Champions League 2026/27: nach Elfmeterschießen</li><li>1. Bundesliga 2026/27: 90 Minuten</li></ul><h2>Punktegleichstand</h2><p>Soweit nicht etwas anderes vereinbart wurde, entscheidet bei Gleichstand in der Gesamtpunktzahl die Anzahl der Spieltagssiege ("Siege") über die Platzierung der Tipper.</p><h2>Tippabgaberegel: 0 Minuten Vorlaufzeit</h2><p>Die Tippzeit endet 0 Minuten vor dem Termin des jeweiligen Ereignisses.</p><h2>Punkteregel: 2 - 5 Punkte</h2><div><table class="ktable"><thead><tr><th></th><th>Tendenz</th><th>Tordifferenz</th><th>Ergebnis</th></tr></thead><tbody><tr><td>Sieg</td><td>2</td><td>3</td><td>5</td></tr><tr><td>Unentschieden</td><td>3</td><td>-</td><td>5</td></tr></tbody></table></div><h2>Punkteregel: 9 Punkte</h2><div><p>Punkte pro richtiger Antwort: 9</p><p>Punkte gibt es für jeden richtigen Tipp. Bei dieser Regel hat die Reihenfolge keine Bedeutung.</p></div></div></body></html>
""";
}
