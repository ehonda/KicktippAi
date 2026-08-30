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
