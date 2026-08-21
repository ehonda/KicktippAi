using Moq;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

public class BonusCommand_CredentialLoading_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Command_loads_posting_community_once_before_creating_client()
    {
        var calls = new List<string>();
        var context = CreateBonusCommandApp();
        context.CredentialLoader
            .Setup(loader => loader.Load("ehonda-ai-arena"))
            .Callback(() => calls.Add("credentials"));
        context.KicktippClientFactory
            .Setup(factory => factory.CreateClient())
            .Callback(() => calls.Add("client"))
            .Returns(context.KicktippClient.Object);

        await context.App.RunAsync([
            "bonus", "test-model",
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
        var context = CreateBonusCommandApp();
        context.CredentialLoader
            .Setup(loader => loader.Load("test-community"))
            .Throws(new InvalidOperationException("credential load failed"));

        var exitCode = await context.App.RunAsync([
            "bonus", "test-model", "--community", "test-community"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        context.KicktippClientFactory.Verify(factory => factory.CreateClient(), Times.Never);
    }

    [Test]
    public async Task Invalid_competition_does_not_load_credentials_or_create_client()
    {
        var context = CreateBonusCommandApp();

        var exitCode = await context.App.RunAsync([
            "bonus", "test-model", "--community", "test-community",
            "--competition", "unknown-competition"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        context.CredentialLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
        context.KicktippClientFactory.Verify(factory => factory.CreateClient(), Times.Never);
    }

    [Test]
    public async Task Dev_command_delegates_one_load_after_its_validation()
    {
        var context = CreateBonusCommandApp();

        await context.App.RunAsync([
            "bonus-dev", "--community", "ehonda-dev-buli-2627"
        ]);

        context.CredentialLoader.Verify(loader => loader.Load("ehonda-dev-buli-2627"), Times.Once);
    }

    [Test]
    public async Task Invalid_dev_target_does_not_load_credentials_or_create_client()
    {
        var context = CreateBonusCommandApp();

        var exitCode = await context.App.RunAsync([
            "bonus-dev", "--community", "pes-squad"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        context.CredentialLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
        context.KicktippClientFactory.Verify(factory => factory.CreateClient(), Times.Never);
    }
}
