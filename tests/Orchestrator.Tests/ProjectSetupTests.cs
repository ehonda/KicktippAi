namespace Orchestrator.Tests;

/// <summary>
/// Placeholder test to validate project setup. Remove once real tests are added.
/// </summary>
public class ProjectSetupTests
{
    [Test]
    public async Task Project_builds_and_references_orchestrator()
    {
        // Verify Orchestrator types are accessible
        var programType = typeof(Orchestrator.Program);
        
        await Assert.That(programType).IsNotNull();
        await Assert.That(programType.FullName).IsEqualTo("Orchestrator.Program");
    }
}
