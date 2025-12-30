using KicktippIntegration.Tests.Infrastructure;

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
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "bonus-questions", ("bonus", "true"));
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
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "bonus-questions", ("bonus", "true"));
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
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "bonus-questions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert
        var multiSelectQuestion = questions.FirstOrDefault(q => q.Text == "Which teams will be relegated?");
        await Assert.That(multiSelectQuestion).IsNotNull();
        await Assert.That(multiSelectQuestion!.Options).HasCount().EqualTo(3);
    }

    [Test]
    [FixtureRequired]
    public async Task Getting_open_bonus_questions_parses_real_bonus_page()
    {
        // Arrange
        StubWithFixtureAndParams("/test-community/tippabgabe", "tippabgabe-bonus", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert - the fixture may or may not have bonus questions depending on when it was captured
        // We just verify the method executes successfully and returns a list
        await Assert.That(questions).IsNotNull();
    }
}
