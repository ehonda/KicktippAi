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
    [Skip("The real fixture was captured when all bonus questions were locked. " +
          "Re-enable after regenerating fixture during a period with open bonus questions.")]
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
}
