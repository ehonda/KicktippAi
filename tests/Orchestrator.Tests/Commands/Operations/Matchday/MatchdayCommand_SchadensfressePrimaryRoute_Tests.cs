using EHonda.KicktippAi.Core;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

public sealed class MatchdayCommand_SchadensfressePrimaryRoute_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Schadensfresse_fails_closed_before_factories_or_source_prediction_reads()
    {
        var context = CreateMatchdayCommandApp();

        var exitCode = await context.App.RunAsync(
        [
            "matchday", "test-model",
            "--community", "schadensfresse",
            "--community-context", "pes-squad",
            "--competition", CompetitionIds.Bundesliga2026_27
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output)
            .Contains("schadensfresse predictions are disabled");
        await Assert.That(context.FirebaseServiceFactory.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.KicktippClientFactory.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.OpenAiServiceFactory.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.ContextProviderFactory.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.CredentialLoader.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.KicktippClient.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.PredictionRepository.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.ContextRepository.Invocations).Count().IsEqualTo(0);
    }
}
