using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using KicktippIntegration;
using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_Constructor_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Passing_null_kicktippClient_throws_ArgumentNullException()
    {
        await Assert.That(() => CreateProvider(NullableOption.Some<IKicktippClient>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("kicktippClient");
    }

    [Test]
    public async Task Passing_null_communityRulesFileProvider_throws_ArgumentNullException()
    {
        await Assert.That(() => CreateProvider(communityRulesFileProvider: NullableOption.Some<IFileProvider>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("communityRulesFileProvider");
    }

    [Test]
    public async Task Passing_null_community_throws_ArgumentNullException()
    {
        await Assert.That(() => CreateProvider(community: NullableOption.Some<string>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("community");
    }

    [Test]
    public async Task Passing_null_communityContext_uses_community_as_default()
    {
        // Arrange - create provider directly without using factory since we need to pass null for communityContext
        var mockClient = CreateMockKicktippClient();
        var mockFileProvider = CreateMockCommunityRulesFileProvider();
        var provider = new KicktippContextProvider(
            mockClient.Object,
            mockFileProvider.Object,
            "test-community",
            CompetitionIds.Bundesliga2026_27,
            communityContext: null);

        // Act & Assert - CommunityScoringRules uses communityContext for file lookup
        // If communityContext defaults to community, it should look for "test-community.md"
        // which doesn't exist in our mock, so we expect FileNotFoundException mentioning "test-community"
        await Assert.That(async () => await provider.CommunityScoringRules())
            .Throws<FileNotFoundException>()
            .WithMessageContaining("test-community");
    }

    [Test]
    public async Task Passing_valid_parameters_creates_provider()
    {
        // Arrange & Act
        var provider = CreateProvider();

        // Assert - provider was created successfully (no exception)
        await Assert.That(provider).IsNotNull();
    }

    [Test]
    public async Task Passing_null_competition_throws_ArgumentNullException()
    {
        await Assert.That(() => CreateProvider(competition: NullableOption.Some<string>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("competition");
    }

    [Test]
    public async Task Passing_blank_competition_throws_ArgumentException()
    {
        await Assert.That(() => CreateProvider(competition: NullableOption.Some(" ")))
            .Throws<ArgumentException>()
            .WithParameterName("competition");
    }
}
