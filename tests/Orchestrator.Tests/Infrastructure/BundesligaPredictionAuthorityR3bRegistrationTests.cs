using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Infrastructure;
using Orchestrator.Services;
using Orchestrator.Tests.Services;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Tests.Infrastructure;

public sealed class BundesligaPredictionAuthorityR3bRegistrationTests
{
    [Test]
    public async Task Registration_is_explicit_idempotent_and_adds_no_observed_default()
    {
        var selection = BundesligaPredictionAuthorityKernelTestData.MatchSelection(
            "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var services = new ServiceCollection();

        services.AddBundesligaPredictionAuthorityR3b([selection]);
        services.AddBundesligaPredictionAuthorityR3b([selection]);

        await Assert.That(services.Count(value =>
            value.ServiceType == typeof(IBundesligaPredictionProvenanceAssembler))).IsEqualTo(1);
        await Assert.That(services.Any(value =>
            value.ServiceType == typeof(OpenAiIntegration.IObservedPredictionService))).IsFalse();
        await Assert.That(services.Any(value =>
            value.ServiceType == typeof(OpenAiIntegration.IObservedInstructionsTemplateProvider))).IsFalse();
    }

    [Test]
    public async Task Observed_factory_surface_requires_complete_model_options_and_both_provider_capabilities()
    {
        var method = typeof(IOpenAiServiceFactory).GetMethod("CreateObservedPredictionService");
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType)
            .IsEqualTo(typeof(OpenAiIntegration.IObservedPredictionService));
        await Assert.That(method.GetParameters().Select(value => value.ParameterType).ToArray())
            .IsEquivalentTo([
                typeof(EHonda.KicktippAi.Core.PredictionModelConfig),
                typeof(OpenAiIntegration.PredictionServiceOptions),
                typeof(OpenAiIntegration.IInstructionsTemplateProvider),
                typeof(OpenAiIntegration.IObservedInstructionsTemplateProvider)]);
        await Assert.That(method.GetParameters().Any(value => value.HasDefaultValue)).IsFalse();
    }
}
