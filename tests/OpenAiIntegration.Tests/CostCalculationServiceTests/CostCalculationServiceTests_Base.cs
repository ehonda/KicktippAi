using Microsoft.Extensions.Logging.Testing;
using TestUtilities;

namespace OpenAiIntegration.Tests.CostCalculationServiceTests;

/// <summary>
/// Base class for CostCalculationService tests providing shared helper functionality
/// </summary>
public abstract class CostCalculationServiceTests_Base
{
    protected FakeLogger<CostCalculationService> Logger = null!;
    protected CostCalculationService Service = null!;

    [Before(Test)]
    public void SetupServiceAndLogger()
    {
        Logger = new FakeLogger<CostCalculationService>();
        Service = new CostCalculationService(Logger);
    }
}
