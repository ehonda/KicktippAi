using TUnit.Core;

namespace Coverage.Tests;

/// <summary>
/// Test that ensures all target assemblies are loaded for coverage instrumentation.
/// </summary>
/// <remarks>
/// This test exists purely to trigger assembly loading. The actual assertion verifies
/// that the expected assemblies were loaded, which serves as both a sanity check and
/// documentation of what this project covers.
/// </remarks>
public class AssemblyLoaderTests
{
    [Test]
    public async Task All_target_assemblies_are_loaded_for_coverage()
    {
        // Act
        var loadedAssemblies = AssemblyLoader.GetLoadedAssemblyNames();

        // Assert - Verify all expected assemblies are loaded
        // These are the assemblies that don't have dedicated test projects
        await Assert.That(loadedAssemblies).Contains("KicktippIntegration");
        await Assert.That(loadedAssemblies).Contains("Orchestrator");
    }
}
