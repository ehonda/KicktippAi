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
    [Skip("The 'tippabgabe-bonus' fixture was captured when no open bonus questions were available. " +
          "This test needs to be re-enabled after regenerating the fixture during a period when " +
          "the Kicktipp community has open bonus questions to answer.")]
    public async Task Getting_open_bonus_questions_parses_real_bonus_page()
    {
        // Arrange
        StubWithFixtureAndParams("/test-community/tippabgabe", "tippabgabe-bonus", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert - once the fixture is regenerated with open bonus questions,
        // update this test with precise assertions for the expected questions
        await Assert.That(questions).IsNotEmpty();
    }

    [Test]
    public async Task Getting_open_bonus_questions_returns_empty_for_locked_questions_snapshot()
    {
        // Arrange - use real snapshot from kicktipp-snapshots directory
        // The tippabgabe-bonus.html snapshot was captured when all bonus questions
        // were already answered and locked (showing "nichttippbar" divs with answers).
        // This is the expected behavior - no OPEN questions to return.
        StubWithSnapshotAndParams("/test-community/tippabgabe", "tippabgabe-bonus", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var questions = await client.GetOpenBonusQuestionsAsync("test-community");

        // Assert - returns empty because all questions are locked (already answered)
        // The snapshot shows 8 bonus questions, all with "nichttippbar" class
        // meaning they cannot be edited - they are past their deadline
        await Assert.That(questions).IsEmpty();
    }
}
