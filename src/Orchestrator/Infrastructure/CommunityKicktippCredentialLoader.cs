using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure;

public interface ICommunityKicktippCredentialLoader
{
    void Load(string postingCommunity);
}

public sealed class CommunityKicktippCredentialLoader(
    ILogger<CommunityKicktippCredentialLoader> logger) : ICommunityKicktippCredentialLoader
{
    public void Load(string postingCommunity)
    {
        EnvironmentHelper.LoadCommunityKicktippCredentials(logger, postingCommunity);
    }
}
