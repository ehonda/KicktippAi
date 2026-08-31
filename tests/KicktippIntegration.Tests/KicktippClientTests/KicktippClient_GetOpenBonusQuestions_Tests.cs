using KicktippIntegration.Tests.Infrastructure;
using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetOpenBonusQuestionsAsync method.
/// </summary>
public class KicktippClient_GetOpenBonusQuestions_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_open_bonus_questions_returns_empty_list_on_404()
    {
        // Arrange - must use bonus=true query param
        StubNotFoundWithParams("/test-community/tippabgabe", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert
        await Assert.That(questions).IsEmpty();
    }

    [Test]
    public async Task Getting_open_bonus_questions_returns_empty_list_when_table_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
                <div class="content"><p>No bonus questions</p></div>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams("/test-community/tippabgabe", html, ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert
        await Assert.That(questions).IsEmpty();
    }

    [Test]
    public async Task Getting_open_bonus_questions_uses_bonus_true_parameter()
    {
        // Arrange - only respond if bonus=true is present
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert
        await Assert.That(questions).IsNotEmpty();
    }

    [Test]
    public async Task Getting_open_bonus_questions_parses_single_select_options()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert
        var singleSelectQuestion = questions.FirstOrDefault(q => q.Text == "Who will win the championship?");
        await Assert.That(singleSelectQuestion).IsNotNull();
        await Assert.That(singleSelectQuestion!.Options).HasCount().EqualTo(3);
        await Assert.That(singleSelectQuestion.Options[0].Id).IsEqualTo("101");
        await Assert.That(singleSelectQuestion.Options[0].Text).IsEqualTo("Team A");
    }

    [Test]
    public async Task Getting_open_bonus_questions_parses_multi_select_options()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert
        var multiSelectQuestion = questions.FirstOrDefault(q => q.Text == "Which teams will be relegated?");
        await Assert.That(multiSelectQuestion).IsNotNull();
        await Assert.That(multiSelectQuestion!.Options).HasCount().EqualTo(3);
    }

    [Test]
    public async Task Getting_open_bonus_questions_retains_exact_stable_question_id_without_classifying_by_text()
    {
        var html = """
            <table id="tippabgabeFragen"><tbody><tr>
              <td>08.09.26 18:45</td><td>CL: Display text is not routing evidence</td><td>
              <select name="fragetippForms[1662326752].antwortIds[1795788]"><option value="-1">Choose</option><option value="15413244">AEK Athen</option></select>
              </td></tr></tbody></table>
            """;
        StubHtmlResponseWithParams("/test-community/tippabgabe", html, ("bonus", "true"));
        var client = CreateClient();

        var question = (await client.GetOpenBonusQuestionsAsync("test-community")).Single();

        await Assert.That(question.KicktippQuestionId).IsEqualTo("1662326752");
        await Assert.That(question.BundesligaSeasonSubcompetition).IsNull();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_fail_closed_when_a_stable_ID_is_missing()
    {
        var html = """
            <table id="tippabgabeFragen"><tbody><tr>
              <td>08.09.26 18:45</td><td>Question</td><td>
              <select name="bonus_q"><option value="-1">Choose</option><option value="1">One</option></select>
              </td></tr></tbody></table>
            """;
        StubHtmlResponseWithParams("/schadensfresse/tippabgabe", html, ("bonus", "true"));
        var client = CreateClient();

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_fail_closed_on_duplicate_stable_IDs()
    {
        var html = """
            <table id="tippabgabeFragen"><tbody>
              <tr><td>08.09.26 18:45</td><td>First</td><td><select name="fragetippForms[1662326752].antwortIds[0]"><option value="1">One</option></select></td></tr>
              <tr><td>08.09.26 18:45</td><td>Second</td><td><select name="fragetippForms[1662326752].antwortIds[0]"><option value="2">Two</option></select></td></tr>
            </tbody></table>
            """;
        StubHtmlResponseWithParams("/schadensfresse/tippabgabe", html, ("bonus", "true"));
        var client = CreateClient();

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_fail_closed_when_multi_select_IDs_or_options_conflict()
    {
        var html = """
            <table id="tippabgabeFragen"><tbody><tr>
              <td>08.09.26 18:45</td><td>Question</td><td>
                <select name="fragetippForms[1662326752].antwortIds[0]"><option value="1">One</option><option value="2">Two</option></select>
                <select name="fragetippForms[1662326753].antwortIds[1]"><option value="1">One</option><option value="3">Three</option></select>
              </td></tr></tbody></table>
            """;
        StubHtmlResponseWithParams("/schadensfresse/tippabgabe", html, ("bonus", "true"));
        var client = CreateClient();

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_fail_closed_on_duplicate_tables_or_extra_rows()
    {
        var html = """
            <table id="tippabgabeFragen"><tbody><tr><td>08.09.26 18:45</td><td>One</td><td><select name="fragetippForms[1].antwortIds[0]"><option value="1">One</option></select></td></tr></tbody></table>
            <table id="tippabgabeFragen"><tbody><tr><td>08.09.26 18:45</td><td>Two</td><td><select name="fragetippForms[2].antwortIds[0]"><option value="2">Two</option></select></td></tr></tbody></table>
            """;
        StubHtmlResponseWithParams("/schadensfresse/tippabgabe", html, ("bonus", "true"));
        var client = CreateClient();

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_fail_closed_when_the_source_table_is_ambiguous()
    {
        StubHtmlResponseWithParams("/schadensfresse/tippabgabe", "<p>No questions</p>", ("bonus", "true"));
        var client = CreateClient();

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_fail_closed_when_the_exact_table_body_has_no_direct_rows()
    {
        StubHtmlResponseWithParams(
            "/schadensfresse/tippabgabe",
            "<table id=\"tippabgabeFragen\"><tbody></tbody></table>",
            ("bonus", "true"));
        var client = CreateClient();

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_accepts_only_an_exact_canonical_final_target()
    {
        using var client = CreateCanonicalTargetClient(
            HttpStatusCode.OK,
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
            CanonicalTargetQuestionHtml);

        var questions = await client.GetOpenBonusQuestionsAsync("schadensfresse");

        await Assert.That(questions).HasCount().EqualTo(1);
        await Assert.That(questions[0].KicktippQuestionId).IsEqualTo("1662326752");
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_non_200_target_response()
    {
        using var client = CreateCanonicalTargetClient(
            HttpStatusCode.NoContent,
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
            CanonicalTargetQuestionHtml);

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_missing_final_uri()
    {
        using var client = CreateCanonicalTargetClient(HttpStatusCode.OK, null, CanonicalTargetQuestionHtml);

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_noncanonical_final_uri_variants()
    {
        var invalidUris = new[]
        {
            "http://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true",
            "https://kicktipp.de/schadensfresse/tippabgabe?bonus=true",
            "https://www.kicktipp.de.evil.test/schadensfresse/tippabgabe?bonus=true",
            "https://www.kicktipp.de:444/schadensfresse/tippabgabe?bonus=true",
            "https://www.kicktipp.de/schadensfresse/Tippabgabe?bonus=true",
            "https://www.kicktipp.de/schadensfresse/tippabgabe/?bonus=true",
            "https://www.kicktipp.de/schadensfresse/other?bonus=true",
            "https://www.kicktipp.de/schadensfresse/tippabgabe",
            "https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true&extra=value",
            "https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true&bonus=true",
            "https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=false",
            "https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true#fragment"
        };

        foreach (var invalidUri in invalidUris)
        {
            using var client = CreateCanonicalTargetClient(
                HttpStatusCode.OK,
                new Uri(invalidUri),
                CanonicalTargetQuestionHtml);

            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_login_and_malformed_target_dom_or_select_identity()
    {
        var malformedBodies = new[]
        {
            "<title>Login</title>",
            "<form id=\"loginFormular\"></form>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>Only two cells</td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><div><tr><td>08.09.26 18:45</td><td>Nested</td><td><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"1\">One</option></select></td></tr></div></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td></td><td>Missing deadline</td><td><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"1\">One</option></select></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>not-a-deadline</td><td>Bad deadline</td><td><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"1\">One</option></select></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>No select</td><td></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>No name</td><td><select><option value=\"1\">One</option></select></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>Malformed name</td><td><select name=\"fragetippForms[1].antwortIds[x]\"><option value=\"1\">One</option></select></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>Duplicate selection index</td><td><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"1\">One</option></select><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"1\">One</option></select></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>Empty option value</td><td><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"\">One</option></select></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>Empty option text</td><td><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"1\"></option></select></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>Duplicate options</td><td><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"1\">One</option><option value=\"1\">Two</option></select></td></tr></tbody></table>",
            "<table id=\"tippabgabeFragen\"><tbody><tr><td>08.09.26 18:45</td><td>Conflicting later options</td><td><select name=\"fragetippForms[1].antwortIds[0]\"><option value=\"1\">One</option></select><select name=\"fragetippForms[1].antwortIds[1]\"><option value=\"2\">Two</option></select></td></tr></tbody></table>"
        };

        foreach (var html in malformedBodies)
        {
            using var client = CreateCanonicalTargetClient(
                HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
                html);

            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_ignores_unrelated_noncanonical_tables_outside_the_target()
    {
        var unrelated = "<table><tbody><tr><th>unrelated</th></tr></tbody></table>";
        using var client = CreateCanonicalTargetClient(
            HttpStatusCode.OK,
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
            unrelated + CanonicalTargetQuestionHtml + unrelated);

        var questions = await client.GetOpenBonusQuestionsAsync("schadensfresse");

        await Assert.That(questions).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_every_rogue_direct_target_tbody_child_before_between_or_after_rows()
    {
        var variants = new[]
        {
            CanonicalTargetQuestionHtml.Replace("<tbody>", "<tbody><div></div>"),
            CanonicalTargetQuestionHtml.Replace("</tr></tbody>", "</tr><div></div></tbody>"),
            CanonicalTargetQuestionHtml.Replace("</tbody>", "rogue</tbody>")
        };
        foreach (var html in variants)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_every_rogue_direct_target_row_child_before_between_or_after_cells()
    {
        var variants = new[]
        {
            CanonicalTargetQuestionHtml.Replace("<tr>", "<tr><span></span>"),
            CanonicalTargetQuestionHtml.Replace("</td><td>Canonical question", "</td><span></span><td>Canonical question"),
            CanonicalTargetQuestionHtml.Replace("</td></tr>", "</td>rogue</tr>")
        };
        foreach (var html in variants)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_handles_comments_case_and_quoted_angle_brackets_only_inside_the_real_target()
    {
        var commentedFake = "<!-- <table id=\"tippabgabeFragen\"><tbody></tbody></table> -->";
        var commentedRowsAndCells = CanonicalTargetQuestionHtml
            .Replace("<tbody>", "<tbody><!-- before row -->")
            .Replace("</tr></tbody>", "</tr><!-- after row --></tbody>")
            .Replace("<tr>", "<tr><!-- before cell -->")
            .Replace("</td><td>Canonical question", "</td><!-- between cells --><td>Canonical question")
            .Replace("</td></tr>", "</td><!-- after cell --></tr>");
        var uppercase = CanonicalTargetQuestionHtml
            .Replace("<table id=\"tippabgabeFragen\">", "<TABLE ID=\"tippabgabeFragen\" data-note=\"x > y\">")
            .Replace("</table>", "</TABLE>")
            .Replace("<tbody>", "<TBODY>").Replace("</tbody>", "</TBODY>")
            .Replace("<tr>", "<TR>").Replace("</tr>", "</TR>")
            .Replace("<td>", "<TD>").Replace("</td>", "</TD>");

        foreach (var html in new[] { commentedFake + commentedRowsAndCells, uppercase, CanonicalTargetQuestionHtml.Replace("</tr>", "<!-- </table> --></tr>") })
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            var questions = await client.GetOpenBonusQuestionsAsync("schadensfresse");
            await Assert.That(questions).HasCount().EqualTo(1);
        }

        using var noRealTarget = CreateCanonicalTargetClient(HttpStatusCode.OK,
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), commentedFake);
        await Assert.That(() => noRealTarget.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<KicktippBonusQuestionIdentityException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_unbalanced_mismatched_or_extra_target_structure()
    {
        var invalid = new[]
        {
            CanonicalTargetQuestionHtml.Replace("</tbody>", "</tbody></bogus>"),
            CanonicalTargetQuestionHtml.Replace("</td></tr>", "</td></bogus></tr>"),
            CanonicalTargetQuestionHtml.Replace("</td></tr>", "</tr></td>"),
            CanonicalTargetQuestionHtml.Replace("</table>", string.Empty),
            CanonicalTargetQuestionHtml.Replace("</tbody>", string.Empty),
            CanonicalTargetQuestionHtml.Replace("</tr>", string.Empty),
            CanonicalTargetQuestionHtml.Replace("</td>", string.Empty),
            CanonicalTargetQuestionHtml + "</table>"
        };
        foreach (var html in invalid)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_duplicate_target_ID_attributes_and_target_end_tag_attributes()
    {
        var invalid = new[]
        {
            CanonicalTargetQuestionHtml.Replace("id=\"tippabgabeFragen\"", "id=\"tippabgabeFragen\" id=\"tippabgabeFragen\""),
            CanonicalTargetQuestionHtml.Replace("id=\"tippabgabeFragen\"", "id=\"tippabgabeFragen\" id=\"other\""),
            CanonicalTargetQuestionHtml.Replace("id=\"tippabgabeFragen\"", "id=\"other\" id=\"tippabgabeFragen\""),
            CanonicalTargetQuestionHtml.Replace("</td>", "</td bogus>"),
            CanonicalTargetQuestionHtml.Replace("</table>", "</table bogus>")
        };

        foreach (var html in invalid)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_rejects_self_closing_target_end_tags()
    {
        var invalid = new[]
        {
            CanonicalTargetQuestionHtml.Replace("</td>", "</td/>"),
            CanonicalTargetQuestionHtml.Replace("</tr>", "</tr/>"),
            CanonicalTargetQuestionHtml.Replace("</tbody>", "</tbody/>"),
            CanonicalTargetQuestionHtml.Replace("</table>", "</table/>")
        };

        foreach (var html in invalid)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_limits_parse_errors_to_the_exact_balanced_target_range()
    {
        var validSuffixes = new[]
        {
            "<!--",
            "<div",
            "<div data-note=\"",
            "</span>"
        };
        foreach (var suffix in validSuffixes)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), CanonicalTargetQuestionHtml + suffix);
            var questions = await client.GetOpenBonusQuestionsAsync("schadensfresse");
            await Assert.That(questions).HasCount().EqualTo(1);
        }

        var invalidWithinTarget = new[]
        {
            CanonicalTargetQuestionHtml.Replace("</td></tr>", "</td><!--</tr>"),
            CanonicalTargetQuestionHtml.Replace("</td></tr>", "</td><div</tr>"),
            CanonicalTargetQuestionHtml.Replace("</td></tr>", "</td><input value=\"</tr>"),
            CanonicalTargetQuestionHtml.Replace("</td></tr>", "</td></span></tr>")
        };
        foreach (var html in invalidWithinTarget)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_applies_AngleSharp_token_semantics_only_to_the_target_table()
    {
        var valid = new[]
        {
            CanonicalTargetQuestionHtml.Replace("id=\"tippabgabeFragen\"", "id=\"tippabgabe&#70;ragen\""),
            CanonicalTargetQuestionHtml.Replace("<tbody>", "<tbody> \t\r\n\f").Replace("<tr>", "<tr> \t\r\n\f"),
            "<script>\"<table id='tippabgabeFragen'><tbody></tbody></table>\"</script>" + CanonicalTargetQuestionHtml +
                "<style>/* </table><table id='tippabgabeFragen'> */</style>",
            "<div></span></div>" + CanonicalTargetQuestionHtml
        };
        foreach (var html in valid)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            var questions = await client.GetOpenBonusQuestionsAsync("schadensfresse");
            await Assert.That(questions).HasCount().EqualTo(1);
        }

        var invalid = new[]
        {
            CanonicalTargetQuestionHtml.Replace("<tbody>", "<tbody>\u00a0"),
            CanonicalTargetQuestionHtml.Replace("<tbody>", "<tbody>\u2003"),
            CanonicalTargetQuestionHtml.Replace("<tr>", "<tr>\u00a0"),
            CanonicalTargetQuestionHtml.Replace("<tr>", "<tr>\u2003")
        };
        foreach (var html in invalid)
        {
            using var client = CreateCanonicalTargetClient(HttpStatusCode.OK,
                new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"), html);
            await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
                .Throws<KicktippBonusQuestionIdentityException>();
        }
    }

    [Test]
    public async Task Getting_open_bonus_questions_propagates_requested_cancellation()
    {
        var client = CreateClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse", cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_propagates_cancellation_from_http_send()
    {
        using var client = CreateCanonicalTargetClient(
            HttpStatusCode.OK,
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
            CanonicalTargetQuestionHtml,
            new CancellingSendHandler());

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Schadensfresse_open_bonus_questions_propagates_cancellation_from_body_read()
    {
        using var client = CreateCanonicalTargetClient(
            HttpStatusCode.OK,
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
            CanonicalTargetQuestionHtml,
            new CancellingBodyHandler());

        await Assert.That(() => client.GetOpenBonusQuestionsAsync("schadensfresse"))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Getting_open_bonus_questions_with_real_fixture_returns_questions()
    {
        // Arrange - use encrypted real fixture for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        // 
        // NOTE: This test is skipped because the current fixture was captured when all bonus 
        // questions were locked. It needs to be re-enabled after regenerating the fixture
        // during a period when the community has open bonus questions to answer.
        const string community = "ehonda-test-buli";
        StubWithRealFixtureAndParams($"/{community}/tippabgabe", community, "tippabgabe-bonus",
            ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync(community);

        // Assert - should have questions with valid structure
        await Assert.That(questions.Count).IsGreaterThan(0);
        
        foreach (var question in questions)
        {
            await Assert.That(question.Text).IsNotEmpty();
            await Assert.That(question.Options).IsNotEmpty();
            await Assert.That(question.MaxSelections).IsGreaterThan(0);
            
            // Each option should have valid data
            foreach (var option in question.Options)
            {
                await Assert.That(option.Id).IsNotEmpty();
                await Assert.That(option.Text).IsNotEmpty();
            }
        }
    }

    private const string CanonicalTargetQuestionHtml = """
        <table id="tippabgabeFragen"><tbody><tr>
          <td>08.09.26 18:45</td><td>Canonical question</td><td>
          <select name="fragetippForms[1662326752].antwortIds[0]"><option value="-1">Choose</option><option value="1">One</option></select>
          </td></tr></tbody></table>
        """;

    private static KicktippClient CreateCanonicalTargetClient(
        HttpStatusCode status,
        Uri? finalUri,
        string html,
        HttpMessageHandler? handler = null)
    {
        var httpClient = new HttpClient(handler ?? new FinalResponseHandler(status, finalUri, html))
        {
            BaseAddress = new Uri("https://www.kicktipp.de/")
        };
        return new KicktippClient(
            httpClient,
            NullLogger<KicktippClient>.Instance,
            new MemoryCache(new MemoryCacheOptions()));
    }

    private sealed class FinalResponseHandler(HttpStatusCode status, Uri? finalUri, string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = finalUri is null ? new HttpRequestMessage() : new HttpRequestMessage(HttpMethod.Get, finalUri),
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
    }

    private sealed class CancellingSendHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(new CancellationToken(canceled: true));
    }

    private sealed class CancellingBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
                Content = new CancellingHttpContent()
            });
    }

    private sealed class CancellingHttpContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromCanceled(new CancellationToken(canceled: true));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
