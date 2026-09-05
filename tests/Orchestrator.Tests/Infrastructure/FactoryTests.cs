using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.Snapshots;
using Orchestrator.Infrastructure.Factories;
using OpenAiIntegration;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Orchestrator.Tests.Infrastructure;

[NotInParallel("ProcessState")]
public class FactoryTests
{
    private const string OpenAiApiKeyEnvVar = "OPENAI_API_KEY";
    private const string KicktippUsernameEnvVar = "KICKTIPP_USERNAME";
    private const string KicktippPasswordEnvVar = "KICKTIPP_PASSWORD";
    private const string FirebaseProjectIdEnvVar = "FIREBASE_PROJECT_ID";
    private const string FirebaseServiceAccountJsonEnvVar = "FIREBASE_SERVICE_ACCOUNT_JSON";

    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new();

    [Before(Test)]
    public void SaveEnvironmentVariables()
    {
        RememberEnvironmentVariable(OpenAiApiKeyEnvVar);
        RememberEnvironmentVariable(KicktippUsernameEnvVar);
        RememberEnvironmentVariable(KicktippPasswordEnvVar);
        RememberEnvironmentVariable(FirebaseProjectIdEnvVar);
        RememberEnvironmentVariable(FirebaseServiceAccountJsonEnvVar);
    }

    [After(Test)]
    public void RestoreEnvironmentVariables()
    {
        foreach (var (name, value) in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Test]
    public async Task OpenAiServiceFactory_requires_api_key_and_caches_services()
    {
        Environment.SetEnvironmentVariable(OpenAiApiKeyEnvVar, null);
        var loggerFactory = CreateLoggerFactory();
        var missingKeyFactory = new OpenAiServiceFactory(loggerFactory);

        await Assert.That(() => missingKeyFactory.CreatePredictionService("gpt-5-nano"))
            .Throws<InvalidOperationException>();

        Environment.SetEnvironmentVariable(OpenAiApiKeyEnvVar, "test-openai-key");
        var sut = new OpenAiServiceFactory(loggerFactory);

        var first = sut.CreatePredictionService("gpt-5-nano");
        var second = sut.CreatePredictionService("gpt-5-nano");
        var explicitDefault = sut.CreatePredictionService("gpt-5-nano", PredictionServiceOptions.Default);
        var standardProcessing = sut.CreatePredictionService("gpt-5-nano", PredictionServiceOptions.StandardProcessing);
        var differentModel = sut.CreatePredictionService("o4-mini");
        var tracker1 = sut.GetTokenUsageTracker();
        var tracker2 = sut.GetTokenUsageTracker();

        await Assert.That(first).IsTypeOf<PredictionService>();
        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(explicitDefault).IsSameReferenceAs(first);
        await Assert.That(standardProcessing).IsTypeOf<PredictionService>();
        await Assert.That(standardProcessing).IsNotSameReferenceAs(first);
        await Assert.That(differentModel).IsTypeOf<PredictionService>();
        await Assert.That(differentModel).IsNotSameReferenceAs(first);
        await Assert.That(tracker2).IsSameReferenceAs(tracker1);
    }

    [Test]
    public async Task KicktippClientFactory_requires_credentials_and_applies_http_client_defaults()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerFactory = CreateLoggerFactory();

        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, null);
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, null);
        var missingCredentialsFactory = new KicktippClientFactory(memoryCache, loggerFactory);

        await Assert.That(() => missingCredentialsFactory.CreateAuthenticatedHttpClient())
            .Throws<InvalidOperationException>();

        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, "user@example.com");
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, "secret");

        var sut = new KicktippClientFactory(memoryCache, loggerFactory);
        using var httpClient = sut.CreateAuthenticatedHttpClient();

        await Assert.That(httpClient.BaseAddress).IsEqualTo(new Uri("https://www.kicktipp.de"));
        await Assert.That(httpClient.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
        await Assert.That(httpClient.DefaultRequestHeaders.UserAgent.ToString()).Contains("Mozilla/5.0");
        await Assert.That(sut.CreateClient()).IsSameReferenceAs(sut.CreateClient());
        await Assert.That(sut.CreateSnapshotClient()).IsTypeOf<SnapshotClient>();
    }

    [Test]
    public async Task KicktippClientFactory_production_builder_shares_authenticated_cookie_with_single_send_strict_route()
    {
        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, "user@example.com");
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, "secret");
        using var server = WireMockServer.Start();
        var origin = new Uri(server.Urls[0] + "/");
        var blank = CreateChampionsLeagueHtml(placed: false);
        var placed = CreateChampionsLeagueHtml(placed: true);
        StubFactoryAuthentication(server);
        var bonusGetCount = 0;
        server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingGet())
            .RespondWith(Response.Create().WithCallback(_ =>
            {
                var html = bonusGetCount++ < 2 ? blank : placed;
                return HtmlResponseMessage(html);
            }));
        server.Given(Request.Create().WithPath("/schadensfresse/tippabgabeForm").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(placed));
        var sut = new KicktippClientFactory(
            new MemoryCache(new MemoryCacheOptions()), CreateLoggerFactory());
        using var client = sut.BuildClient(origin);
        var predictions = SchadensfresseChampionsLeagueBonusSeed.Default.Questions.Select(question => (
            question.KicktippQuestionId,
            new BonusPrediction(question.Options.Take(question.MaxSelections).Select(option => option.Id).ToList())))
            .ToArray();
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        var final = await client.PlaceChampionsLeagueBonusPredictionsAsync(
            "schadensfresse", initial, predictions, overridePredictions: true);

        await Assert.That(final.Questions.SelectMany(question => question.SelectedOptionIds)
            .All(option => option is not null)).IsTrue();
        var actionPosts = server.LogEntries.Where(entry =>
            entry.RequestMessage.Method == "POST"
            && entry.RequestMessage.Path == "/schadensfresse/tippabgabeForm").ToArray();
        await Assert.That(actionPosts.Length).IsEqualTo(1);
        await Assert.That(server.LogEntries.Count(entry =>
            entry.RequestMessage.Method == "POST"
            && entry.RequestMessage.Path == "/schadensfresse/tippabgabe")).IsEqualTo(0);
        await Assert.That(actionPosts[0].RequestMessage.Headers!["Cookie"].Single()).Contains("factory-session=shared");
        var formValues = ParseFormDataMultiValue(actionPosts[0].RequestMessage.Body);
        await Assert.That(SchadensfresseChampionsLeagueBonusSeed.Default.Questions
            .SelectMany(question => question.FormKeys).All(formValues.ContainsKey)).IsTrue();
        foreach (var (questionId, prediction) in predictions)
        {
            var seed = SchadensfresseChampionsLeagueBonusSeed.Default.GetQuestion(questionId);
            for (var index = 0; index < seed.FormKeys.Count; index++)
            {
                await Assert.That(formValues[seed.FormKeys[index]])
                    .IsEquivalentTo([prediction.SelectedOptionIds[index]]);
            }
        }
        await Assert.That(server.LogEntries.Count(entry =>
            entry.RequestMessage.Method == "POST"
            && entry.RequestMessage.Path == "/info/profil/loginaction")).IsEqualTo(1);
        await Assert.That(server.LogEntries.Count(entry =>
            entry.RequestMessage.Method == "GET"
            && entry.RequestMessage.Path == "/schadensfresse/tippabgabe")).IsEqualTo(3);
    }

    [Test]
    public async Task KicktippClientFactory_production_builder_does_not_redirect_or_reauthenticate_strict_post()
    {
        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, "user@example.com");
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, "secret");
        using var server = WireMockServer.Start();
        var origin = new Uri(server.Urls[0] + "/");
        var blank = CreateChampionsLeagueHtml(placed: false);
        StubFactoryAuthentication(server);
        server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(blank));
        server.Given(Request.Create().WithPath("/schadensfresse/tippabgabeForm").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(307)
                .WithHeader("Location", "/schadensfresse/tippabgabe?bonus=true"));
        var sut = new KicktippClientFactory(
            new MemoryCache(new MemoryCacheOptions()), CreateLoggerFactory());
        using var client = sut.BuildClient(origin);
        var predictions = SchadensfresseChampionsLeagueBonusSeed.Default.Questions.Select(question => (
            question.KicktippQuestionId,
            new BonusPrediction(question.Options.Take(question.MaxSelections).Select(option => option.Id).ToList())))
            .ToArray();
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(() => client.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", initial, predictions, overridePredictions: true))
            .Throws<HttpRequestException>();

        await Assert.That(server.LogEntries.Count(entry =>
            entry.RequestMessage.Method == "POST"
            && entry.RequestMessage.Path == "/schadensfresse/tippabgabeForm")).IsEqualTo(1);
        await Assert.That(server.LogEntries.Count(entry =>
            entry.RequestMessage.Method == "POST"
            && entry.RequestMessage.Path == "/schadensfresse/tippabgabe")).IsEqualTo(0);
        await Assert.That(server.LogEntries.Count(entry =>
            entry.RequestMessage.Method == "POST"
            && entry.RequestMessage.Path == "/info/profil/loginaction")).IsEqualTo(1);
    }

    [Test]
    public async Task ContextProviderFactory_creates_expected_provider_types_and_caches_community_rules_provider()
    {
        var kpiRepository = new Mock<IKpiRepository>();
        var publicationRepository = new Mock<IDocumentPublicationRepository>();
        var firebaseFactory = new Mock<IFirebaseServiceFactory>();
        firebaseFactory.Setup(factory => factory.CreateKpiRepository(CompetitionIds.Bundesliga2026_27)).Returns(kpiRepository.Object);
        firebaseFactory.Setup(factory => factory.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27))
            .Returns(publicationRepository.Object);

        var sut = new ContextProviderFactory(
            firebaseFactory.Object,
            new FakeLogger<FirebaseKpiContextProvider>());

        var kicktippClient = new Mock<IKicktippClient>();

        var kicktippContextProvider = sut.CreateKicktippContextProvider(
            kicktippClient.Object,
            "community-name",
            CompetitionIds.Bundesliga2026_27,
            "community-context");
        var kpiContextProvider = sut.CreateKpiContextProvider(CompetitionIds.Bundesliga2026_27);

        await Assert.That(kicktippContextProvider).IsTypeOf<KicktippContextProvider>();
        await Assert.That(kpiContextProvider).IsTypeOf<FirebaseKpiContextProvider>();
        await Assert.That(sut.CommunityRulesFileProvider).IsSameReferenceAs(sut.CommunityRulesFileProvider);
        firebaseFactory.Verify(factory => factory.CreateKpiRepository(CompetitionIds.Bundesliga2026_27), Times.Once);
        firebaseFactory.Verify(
            factory => factory.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27),
            Times.Once);

        await Assert.That(() => sut.CreateKpiContextProvider(" "))
            .Throws<ArgumentException>()
            .WithParameterName("competition");
    }

    [Test]
    public async Task ContextProviderFactory_keeps_world_cup_bonus_context_off_the_Bundesliga_publication_boundary()
    {
        var kpiRepository = new Mock<IKpiRepository>();
        var firebaseFactory = new Mock<IFirebaseServiceFactory>();
        firebaseFactory.Setup(factory => factory.CreateKpiRepository(CompetitionIds.FifaWorldCup2026))
            .Returns(kpiRepository.Object);
        var sut = new ContextProviderFactory(
            firebaseFactory.Object,
            new FakeLogger<FirebaseKpiContextProvider>());

        var provider = sut.CreateKpiContextProvider(CompetitionIds.FifaWorldCup2026.ToUpperInvariant());

        await Assert.That(provider).IsTypeOf<FirebaseKpiContextProvider>();
        firebaseFactory.Verify(factory => factory.CreateKpiRepository(CompetitionIds.FifaWorldCup2026), Times.Once);
        firebaseFactory.Verify(
            factory => factory.CreateDocumentPublicationRepository(It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task FirebaseServiceFactory_requires_environment_variables()
    {
        var loggerFactory = CreateLoggerFactory();

        Environment.SetEnvironmentVariable(FirebaseProjectIdEnvVar, null);
        Environment.SetEnvironmentVariable(FirebaseServiceAccountJsonEnvVar, "{}");
        var missingProjectFactory = new FirebaseServiceFactory(loggerFactory);

        await Assert.That(() => missingProjectFactory.FirestoreDb)
            .Throws<InvalidOperationException>();

        Environment.SetEnvironmentVariable(FirebaseProjectIdEnvVar, "firebase-project");
        Environment.SetEnvironmentVariable(FirebaseServiceAccountJsonEnvVar, null);
        var missingCredentialsFactory = new FirebaseServiceFactory(loggerFactory);

        await Assert.That(() => missingCredentialsFactory.FirestoreDb)
            .Throws<InvalidOperationException>();

        await Assert.That(() => missingCredentialsFactory.CreatePredictionRepository(" "))
            .Throws<ArgumentException>()
            .WithParameterName("competition");
    }

    private static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
    }

    private static void StubFactoryAuthentication(WireMockServer server)
    {
        server.Given(Request.Create().WithPath("/info/profil/login").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><body><form action=\"/info/profil/loginaction\"><input type=\"hidden\" name=\"_charset_\" value=\"UTF-8\"></form></body></html>"));
        server.Given(Request.Create().WithPath("/info/profil/loginaction").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", "/authenticated")
                .WithHeader("Set-Cookie", "factory-session=shared; Path=/"));
        server.Given(Request.Create().WithPath("/authenticated").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><body>authenticated</body></html>"));
    }

    private static string CreateChampionsLeagueHtml(bool placed)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("<html><body><form method=\"post\" action=\"")
            .Append("tippabgabeForm")
            .Append("\"><input type=\"hidden\" name=\"tipperId\" value=\"123\">")
            .Append("<table id=\"tippabgabeFragen\"><tbody>");
        foreach (var seed in SchadensfresseChampionsLeagueBonusSeed.Default.Questions)
        {
            builder.Append("<tr><td>08.09.26 18:45</td><td>")
                .Append(System.Net.WebUtility.HtmlEncode(seed.Text))
                .Append("</td><td>");
            for (var slot = 0; slot < seed.FormKeys.Count; slot++)
            {
                builder.Append("<select name=\"").Append(seed.FormKeys[slot]).Append("\"><option value=\"-1\"");
                if (!placed) builder.Append(" selected");
                builder.Append(">--</option>");
                foreach (var option in seed.Options)
                {
                    builder.Append("<option value=\"").Append(option.Id).Append('"');
                    if (placed && option.Id == seed.Options[slot].Id) builder.Append(" selected");
                    builder.Append('>')
                        .Append(System.Net.WebUtility.HtmlEncode(option.Text)).Append("</option>");
                }
                builder.Append("</select>");
            }
            builder.Append("</td></tr>");
        }
        return builder.Append("</tbody></table><button type=\"button\" name=\"submitbutton\" value=\"save\"></button></form></body></html>")
            .ToString();
    }

    private static WireMock.ResponseMessage HtmlResponseMessage(string html) => new()
    {
        StatusCode = 200,
        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
        {
            ["Content-Type"] = new("text/html; charset=utf-8")
        },
        BodyData = new WireMock.Util.BodyData
        {
            DetectedBodyType = WireMock.Types.BodyType.String,
            BodyAsString = html
        }
    };

    private static Dictionary<string, List<string>> ParseFormDataMultiValue(string? body) =>
        (body ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(pair => pair.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .GroupBy(parts => Uri.UnescapeDataString(parts[0]))
        .ToDictionary(
            group => group.Key,
            group => group.Select(parts => Uri.UnescapeDataString(parts[1])).ToList());

    private void RememberEnvironmentVariable(string name)
    {
        if (!_originalEnvironmentVariables.ContainsKey(name))
        {
            _originalEnvironmentVariables[name] = Environment.GetEnvironmentVariable(name);
        }
    }
}
