using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Shared;
using Spectre.Console.Testing;

namespace Orchestrator.Tests.Commands.Shared;

public class JustificationConsoleWriterTests
{
    [Test]
    public async Task Null_justification_writes_only_fallback_markup()
    {
        var console = new TestConsole();
        var writer = new JustificationConsoleWriter(console);

        writer.WriteJustification(
            null,
            "[blue]Prediction justification[/]",
            "  ",
            "[grey]No justification available[/]");

        var output = console.Output;

        await Assert.That(output).Contains("No justification available");
        await Assert.That(output).DoesNotContain("Prediction justification");
    }

    [Test]
    public async Task Blank_reasoning_sources_and_uncertainties_fall_back_to_default_message()
    {
        var console = new TestConsole();
        var writer = new JustificationConsoleWriter(console);

        writer.WriteJustification(
            new PredictionJustification(
                "   ",
                new PredictionJustificationContextSources(
                    [new PredictionJustificationContextSource(" ", " ")],
                    [new PredictionJustificationContextSource("", "")]),
                [" ", "\t"]),
            "[blue]Prediction justification[/]",
            "  ",
            "[grey]No justification available[/]");

        var output = console.Output;

        await Assert.That(output).Contains("No justification available");
        await Assert.That(output).DoesNotContain("Key reasoning:");
        await Assert.That(output).DoesNotContain("Most valuable context sources");
        await Assert.That(output).DoesNotContain("Least valuable context sources");
        await Assert.That(output).DoesNotContain("Uncertainties:");
    }

    [Test]
    public async Task Null_context_sources_and_uncertainties_without_reasoning_fall_back_to_default_message()
    {
        var console = new TestConsole();
        var writer = new JustificationConsoleWriter(console);

        writer.WriteJustification(
            new PredictionJustification("", null!, null!),
            "[blue]Prediction justification[/]",
            "  ",
            "[grey]No justification available[/]");

        var output = console.Output;

        await Assert.That(output).Contains("No justification available");
        await Assert.That(output).DoesNotContain("Prediction justification");
    }

    [Test]
    public async Task Null_source_entries_are_ignored_when_evaluating_content()
    {
        var console = new TestConsole();
        var writer = new JustificationConsoleWriter(console);

        writer.WriteJustification(
            new PredictionJustification(
                "",
                new PredictionJustificationContextSources(
                    [null!],
                    [null!]),
                []),
            "[blue]Prediction justification[/]",
            "  ",
            "[grey]No justification available[/]");

        var output = console.Output;

        await Assert.That(output).Contains("No justification available");
        await Assert.That(output).DoesNotContain("Most valuable context sources");
        await Assert.That(output).DoesNotContain("Least valuable context sources");
    }

    [Test]
    public async Task Reasoning_with_null_collections_skips_optional_sections()
    {
        var console = new TestConsole();
        var writer = new JustificationConsoleWriter(console);

        writer.WriteJustification(
            new PredictionJustification(
                "Clinical finishing decided the match",
                null!,
                null!),
            "[blue]Prediction justification[/]",
            "  ",
            "[grey]No justification available[/]");

        var output = console.Output;

        await Assert.That(output).Contains("Prediction justification");
        await Assert.That(output).Contains("Key reasoning:");
        await Assert.That(output).Contains("Clinical finishing decided the match");
        await Assert.That(output).DoesNotContain("Most valuable context sources");
        await Assert.That(output).DoesNotContain("Least valuable context sources");
        await Assert.That(output).DoesNotContain("Uncertainties:");
    }

    [Test]
    public async Task Most_valuable_sources_count_as_content_even_without_reasoning()
    {
        var console = new TestConsole();
        var writer = new JustificationConsoleWriter(console);

        writer.WriteJustification(
            new PredictionJustification(
                "",
                new PredictionJustificationContextSources(
                    [new PredictionJustificationContextSource("form-guide.csv", "Recent wins")],
                    []),
                []),
            "[blue]Prediction justification[/]",
            "  ",
            "[grey]No justification available[/]");

        var output = console.Output;

        await Assert.That(output).Contains("Prediction justification");
        await Assert.That(output).Contains("Most valuable context sources");
        await Assert.That(output).Contains("form-guide.csv");
        await Assert.That(output).DoesNotContain("No justification available");
    }

    [Test]
    public async Task Least_valuable_sources_count_as_content_even_without_reasoning()
    {
        var console = new TestConsole();
        var writer = new JustificationConsoleWriter(console);

        writer.WriteJustification(
            new PredictionJustification(
                "",
                new PredictionJustificationContextSources(
                    [],
                    [new PredictionJustificationContextSource("noise.csv", "Low signal")]),
                []),
            "[blue]Prediction justification[/]",
            "  ",
            "[grey]No justification available[/]");

        var output = console.Output;

        await Assert.That(output).Contains("Prediction justification");
        await Assert.That(output).Contains("Least valuable context sources");
        await Assert.That(output).Contains("noise.csv");
        await Assert.That(output).DoesNotContain("No justification available");
    }

    [Test]
    public async Task Sources_and_uncertainties_use_default_labels_and_escape_markup()
    {
        var console = new TestConsole();
        var writer = new JustificationConsoleWriter(console);

        writer.WriteJustification(
            new PredictionJustification(
                " [bold]Momentum[/] favored the home team ",
                new PredictionJustificationContextSources(
                [
                    new PredictionJustificationContextSource("", "   Shot volume [red]spiked[/] "),
                    new PredictionJustificationContextSource("home-history-[fcb].csv", " Strong home form ")
                ],
                [
                    new PredictionJustificationContextSource("injuries.md", ""),
                    new PredictionJustificationContextSource(" ", " ")
                ]),
                [" [italic]Late lineup changes[/] ", " "]),
            "[blue]Prediction justification[/]",
            "  ",
            "[grey]No justification available[/]");

        var output = console.Output;

        await Assert.That(output).Contains("Prediction justification");
        await Assert.That(output).Contains("Key reasoning:");
        await Assert.That(output).Contains("[bold]Momentum[/] favored the home team");
        await Assert.That(output).Contains("Most valuable context sources");
        await Assert.That(output).Contains("Least valuable context sources");
        await Assert.That(output).Contains("Unnamed document");
        await Assert.That(output).Contains("No details provided");
        await Assert.That(output).Contains("home-history-[fcb].csv");
        await Assert.That(output).Contains("Shot volume [red]spiked[/]");
        await Assert.That(output).Contains("Uncertainties:");
        await Assert.That(output).Contains("[italic]Late lineup changes[/]");
    }
}
