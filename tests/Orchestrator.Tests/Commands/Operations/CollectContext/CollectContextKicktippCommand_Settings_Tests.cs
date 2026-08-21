using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.CollectContext.CollectContextKicktippCommand"/> settings validation.
/// </summary>
public class CollectContextKicktippCommand_Settings_Tests : CollectContextKicktippCommandTests_Base
{
    [Test]
    public async Task Running_command_without_community_context_returns_error()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Community context is required");
    }

    [Test]
    public async Task Running_command_with_empty_community_context_returns_error()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Community context is required");
    }

    [Test]
    public async Task Running_command_with_whitespace_community_context_returns_error()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "   ");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Community context is required");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Invalid_matchdays_fail_before_any_write_capable_outcome_collection(bool matchOutcomesOnly)
    {
        var contextRepository = CreateMockContextRepositoryWithPreviousDocuments([]);
        var outcomeRepository = new Mock<IMatchOutcomeRepository>();
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            contextRepository: contextRepository,
            matchOutcomeRepository: outcomeRepository);
        var ctx = CreateCollectContextCommandApp(firebaseServiceFactory: firebaseFactory);
        var arguments = new List<string>
        {
            "collect-context-kicktipp",
            "--community-context", "ehonda-dev-buli-2627",
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--matchdays", "0"
        };
        if (matchOutcomesOnly) arguments.Add("--match-outcomes-only");

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, arguments.ToArray());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Invalid matchday '0'");
        outcomeRepository.VerifyNoOtherCalls();
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        contextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
