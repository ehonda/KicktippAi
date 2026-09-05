using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.KicktippClientTests;

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
        var handler = new StrictRoundTripHandler(CreateHtml(), CreateHtml(placed: true));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.kicktipp.de/") };
        var client = CreateClient(httpClient);
        var predictions = CreatePredictions();
        var initial = await client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse");

        var final = await client.PlaceChampionsLeagueBonusPredictionsAsync(
            "schadensfresse", initial, predictions, overridePredictions: true);

        ChampionsLeagueBonusRoute.ValidatePlacedSelections(final, predictions);
        await Assert.That(handler.PostBodies.Count).IsEqualTo(1);
        var fields = ParseFormDataMultiValue(handler.PostBodies.Single());
        await Assert.That(fields["kept"].SequenceEqual(new[] { "first", "second" }, StringComparer.Ordinal)).IsTrue();
        await Assert.That(SchadensfresseChampionsLeagueBonusSeed.Default.Questions.SelectMany(question => question.FormKeys)
            .All(fields.ContainsKey)).IsTrue();
        await Assert.That(fields["submitbutton"].Single()).IsEqualTo("save");
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

    private static string CreateHtml(bool placed = false)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("<html><body><form method=\"post\" action=\"https://www.kicktipp.de/schadensfresse/tippabgabe\">")
            .Append("<input type=\"hidden\" name=\"tipperId\" value=\"123\">")
            .Append("<input type=\"checkbox\" name=\"ignored\" value=\"no\">")
            .Append("<input type=\"checkbox\" name=\"kept\" value=\"first\" checked>")
            .Append("<input type=\"checkbox\" name=\"kept\" value=\"second\" checked>")
            .Append("<select name=\"unrelated\"><option value=\"u1\" selected>Other</option></select>")
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

    private sealed class StrictRoundTripHandler(string blankHtml, string placedHtml) : HttpMessageHandler
    {
        private int _getCount;
        public List<string> PostBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string html;
            if (request.Method == HttpMethod.Post)
            {
                PostBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                html = placedHtml;
            }
            else
            {
                _getCount++;
                html = _getCount <= 2 ? blankHtml : placedHtml;
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(html, new System.Text.UTF8Encoding(false), "text/html")
            };
        }
    }
}
