using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaTypedRepositoryContractTests
{
    [Test]
    public async Task Repository_exposes_only_complete_typed_authority_families()
    {
        var methods = typeof(IBundesligaTypedPredictionAuthorityRepository).GetMethods();
        var expected = new[]
        {
            "GetCurrentTypedMatchPredictionAsync", "GetCurrentTypedMatchPredictionMetadataAsync",
            "HasCurrentTypedMatchPredictionAsync", "GetCurrentTypedMatchRepredictionIndexAsync",
            "SaveCurrentTypedMatchPredictionAsync", "SaveCurrentTypedMatchRepredictionAsync",
            "GetTypedMatchCopyCandidateAsync", "SaveCurrentTypedMatchCopyAsync",
            "GetCurrentTypedBonusPredictionAsync", "GetCurrentTypedBonusPredictionMetadataAsync",
            "HasCurrentTypedBonusPredictionAsync", "GetCurrentTypedBonusRepredictionIndexAsync",
            "SaveCurrentTypedBonusPredictionAsync", "SaveCurrentTypedBonusRepredictionAsync",
            "GetTypedBonusCopyCandidateAsync", "SaveCurrentTypedBonusCopyAsync"
        };

        await Assert.That(methods.Select(method => method.Name).Order().ToArray())
            .IsEquivalentTo(expected.Order().ToArray());
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            await Assert.That(parameters.Any(parameter => parameter.ParameterType == typeof(BundesligaPredictionAuthority))).IsTrue();
            await Assert.That(parameters.Any(parameter => parameter.ParameterType == typeof(PredictionModelConfig))).IsTrue();
            await Assert.That(parameters.Any(parameter =>
                parameter.ParameterType == typeof(TypedMatchSnapshot)
                || parameter.ParameterType == typeof(TypedBonusSnapshot))).IsTrue();
            await Assert.That(parameters.Any(parameter => parameter.ParameterType == typeof(string))).IsFalse();
        }
    }

    [Test]
    public async Task Every_save_requires_complete_provenance_and_copy_saves_require_compatibility()
    {
        var saves = typeof(IBundesligaTypedPredictionAuthorityRepository).GetMethods()
            .Where(method => method.Name.StartsWith("Save", StringComparison.Ordinal)).ToArray();
        foreach (var save in saves)
        {
            await Assert.That(save.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(PredictionGenerationProvenanceV2))).IsTrue();
            if (save.Name.EndsWith("CopyAsync", StringComparison.Ordinal))
            {
                await Assert.That(save.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(PredictionCopyCompatibilityV2Result))).IsTrue();
            }
        }
    }
}
