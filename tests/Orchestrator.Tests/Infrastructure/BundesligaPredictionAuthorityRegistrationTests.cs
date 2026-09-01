using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using OpenAiIntegration;
using Orchestrator.Infrastructure;
using Orchestrator.Services;
using Orchestrator.Tests.Services;

namespace Orchestrator.Tests.Infrastructure;

public sealed class BundesligaPredictionAuthorityRegistrationTests
{
    [Test]
    public async Task R3a_registration_is_explicit_idempotent_and_has_no_default_selection()
    {
        var selection = BundesligaPredictionAuthorityKernelTestData.MatchSelection(
            "explicit-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var services = new ServiceCollection();

        services.AddBundesligaPredictionAuthorityR3a([selection]);
        services.AddBundesligaPredictionAuthorityR3a([selection]);

        await Assert.That(services.Count(descriptor =>
            descriptor.ServiceType == typeof(BundesligaPredictionRouteRegistry))).IsEqualTo(1);
        await Assert.That(services.Count(descriptor =>
            descriptor.ServiceType == typeof(IBundesligaPredictionAuthorityKernel))).IsEqualTo(1);
        await Assert.That(services.Count(descriptor =>
            descriptor.ServiceType == typeof(IBundesligaPredictionAuditCostReportReader))).IsEqualTo(1);
        await Assert.That(() => new ServiceCollection()
                .AddBundesligaPredictionAuthorityR3a([]))
            .Throws<InvalidDataException>();
        await Assert.That(typeof(ServiceRegistrationExtensions).GetMethods()
            .Where(method => method.Name == nameof(
                ServiceRegistrationExtensions.AddBundesligaPredictionAuthorityR3a))
            .All(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(IEnumerable<BundesligaPredictionRouteSelection>))))
            .IsTrue();
    }

    [Test]
    public async Task Current_commands_do_not_register_or_wire_the_R3a_kernel()
    {
        var services = new ServiceCollection();

        services.AddAllCommandServices();

        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(BundesligaPredictionRouteRegistry))).IsFalse();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IBundesligaPredictionAuthorityKernel))).IsFalse();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IBundesligaPredictionAuditCostReportReader))).IsFalse();
    }

    [Test]
    public async Task Registry_rejects_duplicate_conflicting_and_wrong_context_selections()
    {
        var selection = BundesligaPredictionAuthorityKernelTestData.MatchSelection(
            "duplicate", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);

        await Assert.That(() => new BundesligaPredictionRouteRegistry([selection, selection]))
            .Throws<InvalidDataException>();
        var conflictingKind = BundesligaPredictionRouteSelection.Create(
            "conflicting-kind",
            new BundesligaPredictionRouteContract(
                BundesligaPredictionAuthorityKernelTestData.MatchRoute,
                BundesligaPredictionItemKind.Bonus,
                BundesligaSeasonSubcompetition.Bundesliga),
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            "bonus-profile-v1",
            BundesligaPredictionAuthorityKernelTestData.GenerationInput(),
            BundesligaPredictionAuthorityKernelTestData.PromptRequirement());
        await Assert.That(() => new BundesligaPredictionRouteRegistry([selection, conflictingKind]))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaPredictionRouteSelection.Create(
                " noncanonical",
                BundesligaPredictionAuthorityKernelTestData.MatchRouteContract(),
                BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
                "profile-v1",
                BundesligaPredictionAuthorityKernelTestData.GenerationInput(),
                BundesligaPredictionAuthorityKernelTestData.PromptRequirement()))
            .Throws<ArgumentException>();
        await Assert.That(typeof(BundesligaPredictionRouteSelection).GetConstructors())
            .Count().IsEqualTo(0);
        var factoryParameters = typeof(BundesligaPredictionRouteSelection)
            .GetMethod(nameof(BundesligaPredictionRouteSelection.Create))!
            .GetParameters();
        await Assert.That(factoryParameters.Any(parameter => parameter.ParameterType
            == typeof(PredictionPromptExecutionRequirement))).IsTrue();
        await Assert.That(factoryParameters.Any(parameter => parameter.ParameterType
            == typeof(PredictionModelConfig))).IsFalse();
        await Assert.That(selection.PromptRequirement).IsNotNull();

        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var wrongRoute = BundesligaPredictionAuthorityKernelTestData.BonusSelection(
            "wrong-route", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var registry = new BundesligaPredictionRouteRegistry([wrongRoute]);
        await Assert.That(() => registry.GetRequiredSelection(
                wrongRoute.SelectionId, items.TargetAuthority, items.Target))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Selection_rejects_copy_contract_prompt_inconsistent_with_registered_requirement()
    {
        var compatibility = BundesligaPredictionAuthorityKernelTestData.MatchCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var wrongRequirement = PredictionPromptExecutionRequirement.Create(
            BundesligaPredictionAuthorityKernelTestData.Model(),
            BundesligaPredictionAuthorityKernelTestData.ShaA,
            "production");

        await Assert.That(() => BundesligaPredictionAuthorityKernelTestData.MatchSelection(
                "inconsistent-prompt",
                BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
                compatibility,
                wrongRequirement))
            .Throws<InvalidDataException>();
    }
}
