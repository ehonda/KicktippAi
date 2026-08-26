using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;

namespace OpenAiIntegration;

/// <summary>
/// Shared helpers for building prompt inputs used by prediction and reconstruction flows.
/// </summary>
public static class PredictionPromptComposer
{
    private const string ContextDocumentsPlaceholder = "{{context_documents}}";
    private const string JustificationExplainerPlaceholder = "{{justification_explainer}}";
    private const string JustificationExplainer =
        " Populate the `justification` object concisely with neutral paraphrases of the evidence, " +
        "important uncertainties, and the context documents used.";

    public static string BuildSystemPrompt(
        string template,
        IEnumerable<DocumentContext> contextDocuments,
        bool includeJustification = false)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(contextDocuments);

        var renderedTemplate = ReplaceOptionalSinglePlaceholder(
            template,
            JustificationExplainerPlaceholder,
            includeJustification ? JustificationExplainer : string.Empty);
        var contextList = contextDocuments.ToList();
        var contextPlaceholderCount = CountOccurrences(renderedTemplate, ContextDocumentsPlaceholder);
        if (contextPlaceholderCount > 1)
        {
            throw new InvalidOperationException(
                $"Prompt template contains {contextPlaceholderCount} occurrences of {ContextDocumentsPlaceholder}; exactly zero or one is supported.");
        }

        if (contextPlaceholderCount == 1)
        {
            EnsureNoUnresolvedTemplatePlaceholders(
                renderedTemplate.Replace(ContextDocumentsPlaceholder, string.Empty, StringComparison.Ordinal));
            renderedTemplate = renderedTemplate.Replace(
                ContextDocumentsPlaceholder,
                BuildContextDocumentsSection(contextList, includeLeadingNewLine: false),
                StringComparison.Ordinal);
            return renderedTemplate;
        }

        EnsureNoUnresolvedTemplatePlaceholders(renderedTemplate);
        if (contextList.Count == 0)
        {
            return renderedTemplate;
        }

        renderedTemplate += BuildContextDocumentsSection(contextList, includeLeadingNewLine: true);
        return renderedTemplate;
    }

    private static string ReplaceOptionalSinglePlaceholder(string template, string placeholder, string replacement)
    {
        var count = CountOccurrences(template, placeholder);
        if (count > 1)
        {
            throw new InvalidOperationException(
                $"Prompt template contains {count} occurrences of {placeholder}; exactly zero or one is supported.");
        }

        return count == 1
            ? template.Replace(placeholder, replacement, StringComparison.Ordinal)
            : template;
    }

    private static int CountOccurrences(string value, string searchValue)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(searchValue, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += searchValue.Length;
        }

        return count;
    }

    private static void EnsureNoUnresolvedTemplatePlaceholders(string renderedTemplate)
    {
        if (renderedTemplate.Contains("{{", StringComparison.Ordinal)
            || renderedTemplate.Contains("}}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Prompt rendering left a template placeholder unresolved.");
        }
    }

    private static string BuildContextDocumentsSection(
        IReadOnlyList<DocumentContext> contextDocuments,
        bool includeLeadingNewLine)
    {
        if (contextDocuments.Count == 0)
        {
            return string.Empty;
        }

        var contextSection = new StringBuilder();
        if (includeLeadingNewLine)
        {
            contextSection.Append('\n');
        }

        foreach (var doc in contextDocuments)
        {
            contextSection.Append("---\n");
            contextSection.Append(doc.Name);
            contextSection.Append("\n\n");
            contextSection.Append(doc.Content);
            contextSection.Append('\n');
        }

        contextSection.Append("---");
        return contextSection.ToString();
    }

    public static string CreateMatchJson(Match match)
    {
        object payload = match.CompetitionSpecificData is FifaWorldCup2026MatchData worldCupData
            ? new
            {
                homeTeam = match.HomeTeam,
                awayTeam = match.AwayTeam,
                startsAt = match.StartsAt.ToString(),
                competitionSpecificData = new
                {
                    competition = worldCupData.Competition,
                    isKnockoutStage = worldCupData.IsKnockoutStage,
                    stage = worldCupData.Stage.ToValue(),
                    kicktippRoundName = worldCupData.KicktippRoundName,
                    resultBasis = FifaWorldCup2026MatchDataValues.FinalScoreIncludingExtraTimeAndPenaltyShootout
                }
            }
            : new
            {
                homeTeam = match.HomeTeam,
                awayTeam = match.AwayTeam,
                startsAt = match.StartsAt.ToString()
            };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    public static string CreateBonusQuestionJson(BonusQuestion question)
    {
        var questionData = new
        {
            text = question.Text,
            options = question.Options.Select(o => new { id = o.Id, text = o.Text }).ToArray(),
            maxSelections = question.MaxSelections
        };

        return JsonSerializer.Serialize(questionData, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}
