using System.Text.Json;
using EHonda.KicktippAi.Core;

namespace OpenAiIntegration;

/// <summary>
/// Shared helpers for building prompt inputs used by prediction and reconstruction flows.
/// </summary>
public static class PredictionPromptComposer
{
    public static string BuildSystemPrompt(string template, IEnumerable<DocumentContext> contextDocuments)
    {
        var contextList = contextDocuments.ToList();
        if (!contextList.Any())
        {
            return template;
        }

        var contextSection = "\n";
        foreach (var doc in contextList)
        {
            contextSection += "---\n";
            contextSection += $"{doc.Name}\n\n";
            contextSection += $"{doc.Content}\n";
        }

        contextSection += "---";
        return template + contextSection;
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
