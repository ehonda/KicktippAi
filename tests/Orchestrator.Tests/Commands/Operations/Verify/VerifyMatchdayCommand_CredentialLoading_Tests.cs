using Moq;

namespace Orchestrator.Tests.Commands.Operations.Verify;

public class VerifyMatchdayCommand_CredentialLoading_Tests : VerifyMatchdayCommandTests_Base
{
    [Test]
    public async Task Command_loads_posting_community_once_before_creating_client()
    {
        var calls = new List<string>();
        var context = CreateVerifyMatchdayCommandApp();
        context.CredentialLoader
            .Setup(loader => loader.Load("ehonda-ai-arena"))
            .Callback(() => calls.Add("credentials"));
        context.KicktippClientFactory
            .Setup(factory => factory.CreateClient())
            .Callback(() => calls.Add("client"))
            .Returns(context.KicktippClient.Object);

        await context.App.RunAsync([
            "verify-matchday", "test-model",
            "--community", "ehonda-ai-arena",
            "--community-context", "pes-squad"
        ]);

        await Assert.That(calls).Count().IsEqualTo(2);
        await Assert.That(calls[0]).IsEqualTo("credentials");
        await Assert.That(calls[1]).IsEqualTo("client");
        context.CredentialLoader.Verify(loader => loader.Load("ehonda-ai-arena"), Times.Once);
        context.CredentialLoader.Verify(loader => loader.Load("pes-squad"), Times.Never);
    }

    [Test]
    public async Task Loader_failure_returns_error_before_client_factory_access()
    {
        var context = CreateVerifyMatchdayCommandApp();
        context.CredentialLoader
            .Setup(loader => loader.Load("test-community"))
            .Throws(new InvalidOperationException("credential load failed"));

        var exitCode = await context.App.RunAsync([
            "verify-matchday", "test-model", "--community", "test-community"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        context.KicktippClientFactory.Verify(factory => factory.CreateClient(), Times.Never);
    }

    [Test]
    public async Task Explicit_participant_profile_is_loaded_for_posting_community()
    {
        var context = CreateVerifyMatchdayCommandApp();

        await context.App.RunAsync([
            "verify-matchday", "test-model",
            "--community", "ehonda-ai-arena",
            "--community-context", "pes-squad",
            "--kicktipp-credential-profile", "gpt-5-6-sol-xhigh"
        ]);

        context.CredentialLoader.Verify(
            loader => loader.Load("ehonda-ai-arena", "gpt-5-6-sol-xhigh"),
            Times.Once);
        context.CredentialLoader.Verify(loader => loader.Load("ehonda-ai-arena"), Times.Never);
    }

    [Test]
    public async Task Invalid_competition_does_not_load_credentials_or_create_client()
    {
        var context = CreateVerifyMatchdayCommandApp();

        var exitCode = await context.App.RunAsync([
            "verify-matchday", "test-model", "--community", "test-community",
            "--competition", "unknown-competition"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        context.CredentialLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
        context.KicktippClientFactory.Verify(factory => factory.CreateClient(), Times.Never);
    }
}
