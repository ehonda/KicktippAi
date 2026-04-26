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

    public static string BuildSystemPrompt(string template, IEnumerable<DocumentContext> contextDocuments)
    {
        var contextList = contextDocuments.ToList();
        if (template.Contains(ContextDocumentsPlaceholder, StringComparison.Ordinal))
        {
            return template.Replace(
                ContextDocumentsPlaceholder,
                BuildContextDocumentsSection(contextList, includeLeadingNewLine: false),
                StringComparison.Ordinal);
        }

        if (contextList.Count == 0)
        {
            return template;
        }

        return template + BuildContextDocumentsSection(contextList, includeLeadingNewLine: true);
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
        return JsonSerializer.Serialize(new
        {
            homeTeam = match.HomeTeam,
            awayTeam = match.AwayTeam,
            startsAt = match.StartsAt.ToString()
        }, new JsonSerializerOptions
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
