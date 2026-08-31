using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaTypedRepositoryContractTests
{
    [Test]
    public async Task Current_request_is_the_complete_compile_time_shape_for_every_operation_family()
    {
        var methods = typeof(IBundesligaTypedPredictionAuthorityRepository).GetMethods();
        await Assert.That(methods.Length).IsEqualTo(16);

        foreach (var method in methods.Where(method => !method.Name.Contains("Copy", StringComparison.Ordinal)))
        {
            var requestType = method.Name.Contains("Match", StringComparison.Ordinal)
                ? typeof(BundesligaTypedCurrentRequest<TypedMatchSnapshot>)
                : typeof(BundesligaTypedCurrentRequest<TypedBonusSnapshot>);
            await Assert.That(method.GetParameters()[0].ParameterType).IsEqualTo(requestType);
            await Assert.That(method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string))).IsFalse();
        }

        foreach (var method in methods.Where(method => method.Name.Contains("Copy", StringComparison.Ordinal)))
        {
            var requestType = method.Name.Contains("Match", StringComparison.Ordinal)
                ? typeof(BundesligaTypedCopyRequest<TypedMatchSnapshot>)
                : typeof(BundesligaTypedCopyRequest<TypedBonusSnapshot>);
            await Assert.That(method.GetParameters()[0].ParameterType).IsEqualTo(requestType);
            await Assert.That(method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(PredictionCopyCompatibilityV2Decision))).IsFalse();
        }
    }

    [Test]
    public async Task Current_request_validates_route_profile_generation_input_and_save_provenance()
    {
        var snapshot = BundesligaPredictionContractTestData.Match();
        var current = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            BundesligaPredictionContractTestData.DirectAuthority(), snapshot,
            BundesligaPredictionContractTestData.Model(),
            BundesligaPredictionContractTestData.CurrentIdentity(),
            BundesligaPredictionContractTestData.Routes());
        current.RequireMatchingProvenance(BundesligaPredictionContractTestData.DirectProvenance(snapshot));

        await Assert.That(current.Identity.GenerationInputContract)
            .IsEqualTo(BundesligaPredictionContractTestData.GenerationInput());
        await Assert.That(() => BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            current.Authority, snapshot, current.ModelConfig,
            BundesligaPredictionContractTestData.CurrentIdentity(BundesligaPredictionContractTestData.BonusRoute),
            BundesligaPredictionContractTestData.Routes())).Throws<InvalidDataException>();

        var wrongProfile = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            current.Authority, snapshot, current.ModelConfig,
            BundesligaPredictionContractTestData.CurrentIdentity(profileId: "wrong-profile"),
            BundesligaPredictionContractTestData.Routes());
        await Assert.That(() => wrongProfile.RequireMatchingProvenance(
            BundesligaPredictionContractTestData.DirectProvenance(snapshot))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Copy_request_contains_source_and_target_authority_binding_current_identity_and_bound_decision()
    {
        var input = BundesligaPredictionContractTestData.MatchCopyInput();
        var decision = PredictionCopyCompatibilityV2.Evaluate(input);
        var request = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(input, decision);
        request.RequireMatchingTargetProvenance(BundesligaPredictionContractTestData.CopyProvenance(input));

        await Assert.That(request.TargetCurrent.Authority.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Copy);
        await Assert.That(request.SourceCurrent.Authority.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Direct);
        await Assert.That(request.TargetCurrent.Identity.RouteId).IsEqualTo(request.BindingEntry.RouteId);
        await Assert.That(request.Binding.Reference).IsEqualTo(request.Decision.Binding);
        await Assert.That(request.SourceCurrent.Identity.GenerationInputContract).IsNotNull();

        var wrongIdentity = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            request.TargetCurrent.Authority, request.TargetCurrent.Snapshot,
            request.TargetCurrent.ModelConfig,
            BundesligaPredictionContractTestData.CurrentIdentity(profileId: "wrong-copy-profile"),
            BundesligaPredictionContractTestData.Routes());
        var wrongInput = PredictionCopyCompatibilityV2Input<TypedMatchSnapshot>.Create(
            wrongIdentity, input.SourceCurrent, input.PostingSeed, input.SourceSeed,
            input.Binding, input.BindingEntry,
            BundesligaPredictionContractTestData.MatchCompatibilityContract(wrongIdentity.Authority.CommunityContext),
            input.SourceContract);
        var wrongRequest = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(
            wrongInput, PredictionCopyCompatibilityV2.Evaluate(wrongInput));
        await Assert.That(() => wrongRequest.RequireMatchingTargetProvenance(
            BundesligaPredictionContractTestData.CopyProvenance(input))).Throws<InvalidDataException>();
    }
}
