using EHonda.KicktippAi.Core;
using KicktippIntegration.Transport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace KicktippIntegration.Tests.KicktippClientTests;

[NotInParallel("ChampionsLeagueStrictRouteLoopback")]
public sealed class ChampionsLeagueBonusRouteTests : KicktippClientTests_Base
{
    [Test]
    public async Task Strict_parser_reads_the_exact_three_seeded_definitions_and_all_six_opaque_keys()
    {
        using var httpClient = new HttpClient(new StaticHtmlHandler(CreateHtml()))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        var client = CreateClient(httpClient);

        var snapshot = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(snapshot.Questions.Count).IsEqualTo(3);
        await Assert.That(snapshot.Questions.Sum(question => question.Question.Options.Count)).IsEqualTo(108);
        await Assert.That(snapshot.Questions.SelectMany(question => question.FormKeys)
            .SequenceEqual(SchadensfresseChampionsLeagueBonusSeed.Default.Questions.SelectMany(question => question.FormKeys), StringComparer.Ordinal)).IsTrue();
        await Assert.That(snapshot.NonTargetControls.SequenceEqual(new[]
        {
            new KeyValuePair<string, string>("tipperId", "123"),
            new KeyValuePair<string, string>("kept", "first"),
            new KeyValuePair<string, string>("kept", "second"),
            new KeyValuePair<string, string>("unrelated", "u1")
        })).IsTrue();
    }

    [Test]
    public async Task Strict_parser_rejects_definition_drift_instead_of_returning_an_empty_or_partial_result()
    {
        var html = CreateHtml().Replace("FC Bayern M&#252;nchen", "FC Bayern Muenchen", StringComparison.Ordinal);
        using var httpClient = new HttpClient(new StaticHtmlHandler(html))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        var client = CreateClient(httpClient);

        await Assert.That(() => client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Strict_parser_rejects_non_target_controls_that_cannot_be_preserved_exactly()
    {
        var targetKey = SchadensfresseChampionsLeagueBonusSeed.Default.Questions[0].FormKeys[0];
        var collisionHtml = CreateHtml().Replace(
            "<table id=\"tippabgabeFragen\">",
            $"<input type=\"hidden\" name=\"{targetKey}\" value=\"collision\"><table id=\"tippabgabeFragen\">",
            StringComparison.Ordinal);
        using var collisionClient = new HttpClient(new StaticHtmlHandler(collisionHtml))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        await Assert.That(() => CreateClient(collisionClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .Throws<InvalidDataException>();

        var fileHtml = CreateHtml().Replace(
            "<table id=\"tippabgabeFragen\">",
            "<input type=\"file\" name=\"upload\"><table id=\"tippabgabeFragen\">",
            StringComparison.Ordinal);
        using var fileClient = new HttpClient(new StaticHtmlHandler(fileHtml))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        await Assert.That(() => CreateClient(fileClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Strict_parser_rejects_disabled_or_malformed_target_controls()
    {
        var key = SchadensfresseChampionsLeagueBonusSeed.Default.Questions[0].FormKeys[0];
        var disabled = CreateHtml().Replace(
            $"<select name=\"{key}\">",
            $"<select name=\"{key}\" disabled>",
            StringComparison.Ordinal);
        using var disabledClient = new HttpClient(new StaticHtmlHandler(disabled))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        await Assert.That(() => CreateClient(disabledClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .Throws<InvalidDataException>();

        var malformed = CreateHtml().Replace(
            "<table id=\"tippabgabeFragen\"><tbody>",
            $"<table id=\"tippabgabeFragen\"><tbody><tr><td>x</td><td>x</td><td><select name=\"fragetippForms[{SchadensfresseChampionsLeagueBonusProfile.OrderedQuestionIds[0]}].antwortIds[x]\"><option value=\"-1\">--</option></select></td></tr>",
            StringComparison.Ordinal);
        using var malformedClient = new HttpClient(new StaticHtmlHandler(malformed))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        await Assert.That(() => CreateClient(malformedClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .Throws<InvalidDataException>();

        var malformedOutsideTable = CreateHtml().Replace(
            "<table id=\"tippabgabeFragen\">",
            $"<select name=\"fragetippForms[{SchadensfresseChampionsLeagueBonusProfile.OrderedQuestionIds[0]}].antwortIds[x]\"><option value=\"-1\">--</option></select><table id=\"tippabgabeFragen\">",
            StringComparison.Ordinal);
        using var outsideClient = new HttpClient(new StaticHtmlHandler(malformedOutsideTable))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        await Assert.That(() => CreateClient(outsideClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Strict_parser_rejects_extra_canonical_slots_and_cross_question_slot_reuse_anywhere_in_form()
    {
        var seed = SchadensfresseChampionsLeagueBonusSeed.Default.Questions[0];
        var extraCanonical = $"fragetippForms[{seed.KicktippQuestionId}].antwortIds[999999999]";
        var extraHtml = CreateHtml().Replace(
            "<table id=\"tippabgabeFragen\">",
            $"<select name=\"{extraCanonical}\"><option value=\"-1\">--</option></select><table id=\"tippabgabeFragen\">",
            StringComparison.Ordinal);
        using var extraClient = new HttpClient(new StaticHtmlHandler(extraHtml))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        await Assert.That(() => CreateClient(extraClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .Throws<InvalidDataException>();

        var reusedKey = seed.FormKeys[0].Replace(seed.KicktippQuestionId, "999999999", StringComparison.Ordinal);
        var reusedHtml = CreateHtml().Replace(
            "<table id=\"tippabgabeFragen\">",
            $"<select name=\"{reusedKey}\"><option value=\"-1\">--</option></select><table id=\"tippabgabeFragen\">",
            StringComparison.Ordinal);
        using var reusedClient = new HttpClient(new StaticHtmlHandler(reusedHtml))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        await Assert.That(() => CreateClient(reusedClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Strict_parser_preserves_unrelated_bonus_question_controls_with_noncanonical_slots()
    {
        var unrelatedKey = "fragetippForms[999999999].antwortIds[888888888]";
        var html = CreateHtml().Replace(
            "<table id=\"tippabgabeFragen\">",
            $"<select name=\"{unrelatedKey}\"><option value=\"unrelated-option\" selected>Other</option></select><table id=\"tippabgabeFragen\">",
            StringComparison.Ordinal);
        using var httpClient = new HttpClient(new StaticHtmlHandler(html))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };

        var snapshot = await CreateClient(httpClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(snapshot.NonTargetControls.Contains(
            new KeyValuePair<string, string>(unrelatedKey, "unrelated-option"))).IsTrue();
    }

    [Test]
    public async Task Strict_parser_omits_controls_disabled_by_an_ancestor_fieldset()
    {
        var html = CreateHtml().Replace(
            "<table id=\"tippabgabeFragen\">",
            "<fieldset disabled><input name=\"disabledAncestor\" value=\"must-not-post\"></fieldset><table id=\"tippabgabeFragen\">",
            StringComparison.Ordinal);
        using var httpClient = new HttpClient(new StaticHtmlHandler(html))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };

        var snapshot = await CreateClient(httpClient).GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(snapshot.NonTargetControls.Any(control => control.Key == "disabledAncestor")).IsFalse();
    }

    [Test]
    public async Task Strict_client_posts_once_then_validates_response_and_fresh_get_readback()
    {
        var origin = new Uri(ServerUrl + "/");
        StubBonusGets(
            CreateHtml(origin: origin),
            CreateHtml(origin: origin),
            CreateHtml(placed: true, origin: origin));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(HtmlResponse(CreateHtml(placed: true, origin: origin)));
        using var client = CreateStrictClient(origin);
        var predictions = CreatePredictions();
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        var final = await client.PlaceChampionsLeagueBonusPredictionsAsync(
            "schadensfresse", initial, predictions, overridePredictions: true);

        await Assert.That(final.Questions.SelectMany(question => question.SelectedOptionIds).All(value => value is not null)).IsTrue();
        var post = Server.LogEntries.Single(entry => entry.RequestMessage.Method == "POST");
        var fields = ParseFormDataMultiValue(post.RequestMessage.Body);
        await Assert.That(SchadensfresseChampionsLeagueBonusSeed.Default.Questions.SelectMany(question => question.FormKeys)
            .All(fields.ContainsKey)).IsTrue();
        await Assert.That(SchadensfresseChampionsLeagueBonusSeed.Default.Questions.SelectMany(question => question.FormKeys)
            .All(key => fields[key].Count == 1)).IsTrue();
        await Assert.That(fields["submitbutton"].Single()).IsEqualTo("save");
    }

    [Test]
    [Arguments(302)]
    [Arguments(303)]
    public async Task Strict_client_follows_one_exact_safe_redirect_as_get_then_performs_final_get(int statusCode)
    {
        var origin = new Uri(ServerUrl + "/");
        StubBonusGets(
            CreateHtml(origin: origin),
            CreateHtml(origin: origin),
            CreateHtml(placed: true, origin: origin));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Location", "/schadensfresse/tippabgabe?bonus=true"));
        using var client = CreateStrictClient(origin);
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        var final = await client.PlaceChampionsLeagueBonusPredictionsAsync(
            "schadensfresse", initial, CreatePredictions(), overridePredictions: true);

        await Assert.That(final.Questions.SelectMany(question => question.SelectedOptionIds).All(value => value is not null)).IsTrue();
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "GET"
                                                   && entry.RequestMessage.Path == "/schadensfresse/tippabgabe"))
            .IsEqualTo(4);
    }

    [Test]
    [Arguments(301)]
    [Arguments(307)]
    [Arguments(308)]
    [Arguments(401)]
    [Arguments(403)]
    public async Task Strict_client_never_retries_or_follows_unsafe_post_statuses(int statusCode)
    {
        var origin = new Uri(ServerUrl + "/");
        StubBonusGets(CreateHtml(origin: origin), CreateHtml(origin: origin));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Location", "/schadensfresse/tippabgabe?bonus=true"));
        using var client = CreateStrictClient(origin);
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(() => client.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", initial, CreatePredictions(), overridePredictions: true))
            .Throws<HttpRequestException>();

        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "GET"
                                                   && entry.RequestMessage.Path == "/schadensfresse/tippabgabe"))
            .IsEqualTo(2);
    }

    [Test]
    public async Task Strict_client_rejects_wrong_redirect_and_login_response_without_another_request()
    {
        var origin = new Uri(ServerUrl + "/");
        StubBonusGets(CreateHtml(origin: origin), CreateHtml(origin: origin));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", "/info/profil/login"));
        using (var wrongRedirectClient = CreateStrictClient(origin))
        {
            var initial = await wrongRedirectClient.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");
            await Assert.That(() => wrongRedirectClient.PlaceChampionsLeagueBonusPredictionsAsync(
                    "schadensfresse", initial, CreatePredictions(), overridePredictions: true))
                .Throws<InvalidDataException>();
        }
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Path == "/info/profil/login")).IsEqualTo(0);

        Server.ResetLogEntries();
        Server.ResetMappings();
        StubBonusGets(CreateHtml(origin: origin), CreateHtml(origin: origin));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(HtmlResponse("<html><body><form id=\"loginFormular\"></form></body></html>"));
        using var loginClient = CreateStrictClient(origin);
        var loginInitial = await loginClient.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");
        await Assert.That(() => loginClient.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", loginInitial, CreatePredictions(), overridePredictions: true))
            .Throws<InvalidDataException>();
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
    }

    [Test]
    public async Task Strict_client_does_not_repost_when_final_get_fails()
    {
        var origin = new Uri(ServerUrl + "/");
        var getCount = 0;
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingGet())
            .RespondWith(Response.Create().WithCallback(_ =>
            {
                getCount++;
                return getCount <= 2
                    ? HtmlResponseMessage(CreateHtml(origin: origin))
                    : new WireMock.ResponseMessage { StatusCode = 500 };
            }));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(HtmlResponse(CreateHtml(placed: true, origin: origin)));
        using var client = CreateStrictClient(origin);
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(() => client.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", initial, CreatePredictions(), overridePredictions: true))
            .Throws<HttpRequestException>();

        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
    }

    [Test]
    public async Task Strict_client_rejects_empty_post_response_before_final_get()
    {
        var origin = new Uri(ServerUrl + "/");
        StubBonusGets(CreateHtml(origin: origin), CreateHtml(origin: origin));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(HtmlResponse(CreateHtml(origin: origin)));
        using var client = CreateStrictClient(origin);
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(() => client.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", initial, CreatePredictions(), overridePredictions: true))
            .Throws<InvalidDataException>();

        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "GET"
                                                   && entry.RequestMessage.Path == "/schadensfresse/tippabgabe"))
            .IsEqualTo(2);
    }

    [Test]
    public async Task Strict_client_rejects_partial_final_readback_without_reposting()
    {
        var origin = new Uri(ServerUrl + "/");
        var lastSeed = SchadensfresseChampionsLeagueBonusSeed.Default.Questions[^1];
        var partial = CreateHtml(placed: true, origin: origin).Replace(
            $"<option value=\"{lastSeed.Options[0].Id}\" selected>",
            $"<option value=\"{lastSeed.Options[0].Id}\">",
            StringComparison.Ordinal);
        StubBonusGets(CreateHtml(origin: origin), CreateHtml(origin: origin), partial);
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(HtmlResponse(CreateHtml(placed: true, origin: origin)));
        using var client = CreateStrictClient(origin);
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(() => client.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", initial, CreatePredictions(), overridePredictions: true))
            .Throws<InvalidDataException>();

        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
    }

    [Test]
    public async Task Strict_client_reports_unknown_post_timeout_without_retry()
    {
        var origin = new Uri(ServerUrl + "/");
        StubBonusGets(CreateHtml(origin: origin), CreateHtml(origin: origin));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingPost())
            .RespondWith(Response.Create().WithCallback(_ =>
            {
                Thread.Sleep(1_000);
                return HtmlResponseMessage(CreateHtml(placed: true, origin: origin));
            }));
        using var client = CreateStrictClient(origin, TimeSpan.FromMilliseconds(250));
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        await Assert.That(() => client.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", initial, CreatePredictions(), overridePredictions: true))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("outcome is unknown");

        await Task.Delay(1_000);
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
    }

    [Test]
    public async Task Legacy_client_fails_strict_mutation_before_any_pre_post_request()
    {
        var handler = new CountingHandler(CreateHtml());
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.kicktipp.de/") };
        using var client = CreateClient(httpClient);

        await Assert.That(() => client.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", CreateSnapshot(), CreatePredictions(), overridePredictions: true))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("not configured");
        await Assert.That(handler.RequestCount).IsEqualTo(0);
    }

    [Test]
    public async Task Exact_payload_preserves_ordered_multimap_controls_and_uses_all_six_target_keys_once()
    {
        var initial = CreateSnapshot();
        var current = CreateSnapshot();
        var predictions = CreatePredictions();

        var payload = ChampionsLeagueBonusRoute.BuildPostPayload(initial, current, predictions, overrideKicktipp: false);

        await Assert.That(payload.Take(3).SequenceEqual(new[]
        {
            new KeyValuePair<string, string>("csrf", "token"),
            new KeyValuePair<string, string>("kept", "first"),
            new KeyValuePair<string, string>("kept", "second")
        })).IsTrue();
        await Assert.That(payload.Skip(3).Take(6).Select(pair => pair.Key)
            .SequenceEqual(SchadensfresseChampionsLeagueBonusSeed.Default.Questions.SelectMany(question => question.FormKeys), StringComparer.Ordinal)).IsTrue();
        await Assert.That(payload[^1]).IsEqualTo(new KeyValuePair<string, string>("submitbutton", "tippsSpeichern"));
    }

    [Test]
    public async Task Pre_post_target_change_fails_before_payload_construction()
    {
        var initial = CreateSnapshot();
        var changed = CreateSnapshot(selected: ("1662326752", 0, FirstOptionId()));

        await Assert.That(() => ChampionsLeagueBonusRoute.BuildPostPayload(
                initial, changed, CreatePredictions(), overrideKicktipp: true))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Partial_existing_selections_require_explicit_override()
    {
        var current = CreateSnapshot(selected: ("1662326752", 0, FirstOptionId()));

        await Assert.That(() => ChampionsLeagueBonusRoute.BuildPostPayload(
                current, current, CreatePredictions(), overrideKicktipp: false))
            .Throws<InvalidOperationException>();
        var payload = ChampionsLeagueBonusRoute.BuildPostPayload(
            current, current, CreatePredictions(), overrideKicktipp: true);
        await Assert.That(payload.Count).IsEqualTo(10);
    }

    [Test]
    public async Task Missing_question_or_wrong_selection_count_fails_closed()
    {
        var snapshot = CreateSnapshot();
        var incomplete = CreatePredictions().Take(2).ToArray();
        var wrongCount = CreatePredictions().ToArray();
        wrongCount[1] = (wrongCount[1].QuestionId, new BonusPrediction(wrongCount[1].Prediction.SelectedOptionIds.Take(3).ToList()));

        await Assert.That(() => ChampionsLeagueBonusRoute.ValidateCompletePredictions(snapshot, incomplete))
            .Throws<InvalidDataException>();
        await Assert.That(() => ChampionsLeagueBonusRoute.ValidateCompletePredictions(snapshot, wrongCount))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Readback_requires_the_exact_complete_selection_sets()
    {
        var predictions = CreatePredictions();
        var selected = predictions.SelectMany(result => result.Prediction.SelectedOptionIds.Select((option, slot) =>
            (result.QuestionId, slot, option))).ToArray();
        var exact = CreateSnapshot(selected);
        ChampionsLeagueBonusRoute.ValidatePlacedSelections(exact, predictions);

        var wrong = selected.ToArray();
        wrong[0] = (wrong[0].QuestionId, wrong[0].slot, SchadensfresseChampionsLeagueBonusSeed.Default.Questions[0].Options[1].Id);
        await Assert.That(() => ChampionsLeagueBonusRoute.ValidatePlacedSelections(CreateSnapshot(wrong), predictions))
            .Throws<InvalidDataException>();
    }

    private static ChampionsLeagueBonusFormSnapshot CreateSnapshot(
        params (string QuestionId, int slot, string option)[] selected)
    {
        var selectedBySlot = selected.ToDictionary(value => (value.QuestionId, value.slot), value => value.option);
        var questions = SchadensfresseChampionsLeagueBonusSeed.Default.Questions.Select(seed =>
        {
            var question = new BonusQuestion(
                seed.Text,
                NodaTime.Text.InstantPattern.ExtendedIso.Parse(seed.Deadline).Value.InUtc(),
                seed.Options.Select(option => new BonusQuestionOption(option.Id, option.Text)).ToList(),
                seed.MaxSelections,
                seed.FormKeys[0]);
            return new ChampionsLeagueBonusQuestionSnapshot(
                seed.KicktippQuestionId,
                question,
                seed.FormKeys,
                seed.FormKeys.Select((_, slot) => selectedBySlot.GetValueOrDefault((seed.KicktippQuestionId, slot))).ToArray());
        }).ToArray();
        return new ChampionsLeagueBonusFormSnapshot(
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe"),
            "POST",
            questions,
            [new("csrf", "token"), new("kept", "first"), new("kept", "second")],
            "submitbutton",
            "tippsSpeichern",
            true);
    }

    private static IReadOnlyList<(string QuestionId, BonusPrediction Prediction)> CreatePredictions() =>
        SchadensfresseChampionsLeagueBonusSeed.Default.Questions.Select(question => (
            question.KicktippQuestionId,
            new BonusPrediction(question.Options.Take(question.MaxSelections).Select(option => option.Id).ToList())))
        .ToArray();

    private static string FirstOptionId() =>
        SchadensfresseChampionsLeagueBonusSeed.Default.Questions[0].Options[0].Id;

    private static string CreateHtml(bool placed = false, Uri? origin = null)
    {
        origin ??= new Uri("https://www.kicktipp.de/");
        var builder = new System.Text.StringBuilder();
        builder.Append("<html><body><form method=\"post\" action=\"")
            .Append(new Uri(origin, "/schadensfresse/tippabgabe"))
            .Append("\">")
            .Append("<input type=\"hidden\" name=\"tipperId\" value=\"123\">")
            .Append("<input type=\"checkbox\" name=\"ignored\" value=\"no\">");
        if (string.Equals(origin.Host, "www.kicktipp.de", StringComparison.Ordinal))
        {
            builder.Append("<input type=\"checkbox\" name=\"kept\" value=\"first\" checked>")
                .Append("<input type=\"checkbox\" name=\"kept\" value=\"second\" checked>");
        }
        builder.Append("<select name=\"unrelated\"><option value=\"u1\" selected>Other</option></select>")
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
        return builder.Append("</tbody></table><button type=\"button\" name=\"submitbutton\" value=\"save\"></button>")
            .Append("<button type=\"button\" name=\"otherbutton\" value=\"ignored\"></button></form></body></html>")
            .ToString();
    }

    private sealed class StaticHtmlHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(html, new System.Text.UTF8Encoding(false), "text/html")
            };
            return Task.FromResult(response);
        }
    }

    private KicktippClient CreateStrictClient(Uri origin, TimeSpan? strictTimeout = null)
    {
        var cookies = new System.Net.CookieContainer();
        var genericHandler = new HttpClientHandler
        {
            CookieContainer = cookies,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        var genericClient = new HttpClient(genericHandler) { BaseAddress = origin };
        var strictTransport = new ChampionsLeagueBonusStrictTransport(origin, cookies, strictTimeout);
        return new KicktippClient(
            genericClient,
            new FakeLogger<KicktippClient>(),
            new MemoryCache(new MemoryCacheOptions()),
            strictTransport);
    }

    private void StubBonusGets(params string[] bodies)
    {
        var index = 0;
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingGet())
            .RespondWith(Response.Create().WithCallback(_ =>
                HtmlResponseMessage(bodies[Math.Min(index++, bodies.Length - 1)])));
    }

    private static IResponseBuilder HtmlResponse(string html) =>
        Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "text/html; charset=utf-8")
            .WithBody(html);

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

    private sealed class CountingHandler(string html) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(html, new System.Text.UTF8Encoding(false), "text/html")
            });
        }
    }
}
