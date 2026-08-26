using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure;

public interface ICommunityKicktippCredentialLoader
{
    void Load(string postingCommunity);
    void Load(string postingCommunity, string credentialProfile);
}

public sealed class CommunityKicktippCredentialLoader(
    ILogger<CommunityKicktippCredentialLoader> logger) : ICommunityKicktippCredentialLoader
{
    public void Load(string postingCommunity)
    {
        EnvironmentHelper.LoadCommunityKicktippCredentials(logger, postingCommunity);
    }

    public void Load(string postingCommunity, string credentialProfile)
    {
        EnvironmentHelper.LoadCommunityKicktippCredentials(logger, postingCommunity, credentialProfile);
    }
}
