using System.Collections.Generic;
using System.Linq;
using EHonda.KicktippAi.Core;
using Spectre.Console;

namespace Orchestrator.Commands.Shared;

internal sealed class JustificationConsoleWriter
{
    private readonly IAnsiConsole _console;

    public JustificationConsoleWriter(IAnsiConsole console)
    {
        _console = console;
    }

    public void WriteJustification(
        PredictionJustification? justification,
        string headingMarkup,
        string indent,
        string fallbackMarkup)
    {
        if (justification == null || !HasContent(justification))
        {
            _console.MarkupLine(fallbackMarkup);
            return;
        }

        _console.MarkupLine(headingMarkup);

        if (!string.IsNullOrWhiteSpace(justification.KeyReasoning))
        {
            _console.MarkupLine($"{indent}[white]Key reasoning:[/] {Markup.Escape(justification.KeyReasoning.Trim())}");
        }

        WriteSources("Most valuable context sources", justification.ContextSources?.MostValuable, indent);
        WriteSources("Least valuable context sources", justification.ContextSources?.LeastValuable, indent);
        WriteUncertainties(justification.Uncertainties, indent);
    }

    private static bool HasContent(PredictionJustification justification)
    {
        if (!string.IsNullOrWhiteSpace(justification.KeyReasoning))
        {
            return true;
        }

        if (justification.ContextSources?.MostValuable != null &&
            justification.ContextSources.MostValuable.Any(HasSourceContent))
        {
            return true;
        }

        if (justification.ContextSources?.LeastValuable != null &&
            justification.ContextSources.LeastValuable.Any(HasSourceContent))
        {
            return true;
        }

        return justification.Uncertainties != null &&
               justification.Uncertainties.Any(item => !string.IsNullOrWhiteSpace(item));
    }

    private static bool HasSourceContent(PredictionJustificationContextSource source)
    {
        return !string.IsNullOrWhiteSpace(source?.DocumentName) ||
               !string.IsNullOrWhiteSpace(source?.Details);
    }

    private void WriteSources(
        string heading,
        IReadOnlyList<PredictionJustificationContextSource>? sources,
        string indent)
    {
        if (sources == null)
        {
            return;
        }

        var entries = sources
            .Where(HasSourceContent)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        _console.MarkupLine($"{indent}[white]{heading}[/]");

        foreach (var entry in entries)
        {
            var documentName = string.IsNullOrWhiteSpace(entry.DocumentName)
                ? "Unnamed document"
                : entry.DocumentName.Trim();

            var details = string.IsNullOrWhiteSpace(entry.Details)
                ? "No details provided"
                : entry.Details.Trim();

            _console.MarkupLine($"{indent}  • [yellow]{Markup.Escape(documentName)}[/]: {Markup.Escape(details)}");
        }
    }

    private void WriteUncertainties(IReadOnlyList<string>? uncertainties, string indent)
    {
        if (uncertainties == null)
        {
            return;
        }

        var items = uncertainties
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

        if (items.Count == 0)
        {
            return;
        }

        _console.MarkupLine($"{indent}[white]Uncertainties:[/]");

        foreach (var item in items)
        {
            _console.MarkupLine($"{indent}  • {Markup.Escape(item)}");
        }
    }
}
