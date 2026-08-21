using EHonda.KicktippAi.Core;

namespace OpenAiIntegration.Tests;

public sealed class BundesligaPromptMirrorTests
{
    private const string PromptModel = "bundesliga-2026-27";

    [Test]
    public async Task Match_mirrors_are_schema_aware_identical_and_use_the_current_context_contract()
    {
        var provider = new InstructionsTemplateProvider(PromptsFileProvider.Create());
        var regular = provider.LoadMatchTemplate(PromptModel, includeJustification: false);
        var justification = provider.LoadMatchTemplate(PromptModel, includeJustification: true);

        await Assert.That(regular.template).IsEqualTo(justification.template);
        await Assert.That(regular.template)
            .Contains("Bundesliga 2026/27")
            .And.Contains("{{context_documents}}")
            .And.Contains("club-elo-*.csv")
            .And.Contains("roster-*")
            .And.DoesNotContain("2025/2026")
            .And.DoesNotContain("transfer");
        await Assert.That(regular.path.Replace('\\', '/')).EndsWith("prompts/bundesliga-2026-27/match.md");
        await Assert.That(justification.path.Replace('\\', '/')).EndsWith("prompts/bundesliga-2026-27/match.justification.md");
    }

    [Test]
    public async Task Bonus_mirror_uses_only_the_current_aggregate_and_targeted_roster_contract()
    {
        var provider = new InstructionsTemplateProvider(PromptsFileProvider.Create());
        var bonus = provider.LoadBonusTemplate(PromptModel);

        await Assert.That(bonus.template)
            .Contains("Bundesliga 2026/27")
            .And.Contains("{{context_documents}}")
            .And.Contains("club-elo-rankings")
            .And.Contains("team-squad-summary")
            .And.Contains("roster-*")
            .And.DoesNotContain("2025/2026")
            .And.DoesNotContain("transfer");
        await Assert.That(bonus.path.Replace('\\', '/')).EndsWith("prompts/bundesliga-2026-27/bonus.md");
    }

    [Test]
    public async Task Match_and_bonus_mirrors_reconstruct_context_at_the_exact_placeholder()
    {
        var provider = new InstructionsTemplateProvider(PromptsFileProvider.Create());
        var match = provider.LoadMatchTemplate(PromptModel, includeJustification: true);
        var bonus = provider.LoadBonusTemplate(PromptModel);
        DocumentContext[] documents = [new("club-elo-example.csv", "Club,Elo\nExample,1500")];

        var reconstructedMatch = PredictionPromptComposer.BuildSystemPrompt(match.template, documents);
        var reconstructedBonus = PredictionPromptComposer.BuildSystemPrompt(bonus.template, documents);

        await Assert.That(reconstructedMatch).Contains("club-elo-example.csv").And.DoesNotContain("{{context_documents}}");
        await Assert.That(reconstructedBonus).Contains("club-elo-example.csv").And.DoesNotContain("{{context_documents}}");
    }

    [Test]
    public async Task Content_hash_normalizes_line_endings_and_trailing_whitespace()
    {
        var lf = PromptTemplateContentHash.ComputeSha256("alpha\nbeta\n");
        var crlf = PromptTemplateContentHash.ComputeSha256("alpha\r\nbeta\r\n\r\n");

        await Assert.That(crlf).IsEqualTo(lf);
        await Assert.That(lf).Matches("^[0-9a-f]{64}$");
    }
}
