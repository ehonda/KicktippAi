using ContextProviders.Kicktipp;
using FirebaseAdapter;
using KicktippIntegration;
using Orchestrator;

namespace Coverage.Tests;

/// <summary>
/// Forces loading of src/ assemblies that don't have dedicated test projects.
/// This ensures they appear in code coverage reports with 0% coverage rather than being missing entirely.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> dotnet-coverage only instruments and reports on assemblies that are
/// loaded at runtime during test execution. Without explicit type references, assemblies that
/// have no tests would not appear in coverage reports at all - we'd have no visibility into
/// which parts of our codebase lack test coverage.
/// </para>
/// 
/// <para>
/// <b>Design choice:</b> We use simple <c>typeof(T)</c> expressions rather than more complex
/// alternatives like <c>RuntimeHelpers.RunClassConstructor</c> or <c>Assembly.Load</c>.
/// This approach is:
/// <list type="bullet">
///   <item>Sufficient for coverage instrumentation (forces the assembly to load)</item>
///   <item>Simple and readable</item>
///   <item>Compile-time checked (breaks if types are renamed/removed)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Removal criteria:</b> As dedicated test projects are created for these assemblies,
/// remove the corresponding type reference from this class. When all assemblies have tests,
/// delete this entire project.
/// </para>
/// </remarks>
public static class AssemblyLoader
{
    /// <summary>
    /// Type references that force assembly loading. Each type comes from a different assembly.
    /// </summary>
    /// <remarks>
    /// Current assemblies without dedicated test projects:
    /// <list type="bullet">
    ///   <item><see cref="KicktippContextProvider"/> - ContextProviders.Kicktipp.dll</item>
    ///   <item><see cref="FirebaseContextRepository"/> - FirebaseAdapter.dll</item>
    ///   <item><see cref="IKicktippClient"/> - KicktippIntegration.dll</item>
    ///   <item><see cref="Program"/> - Orchestrator.dll</item>
    /// </list>
    /// </remarks>
    public static readonly Type[] LoadedAssemblyTypes =
    [
        // ContextProviders.Kicktipp - No dedicated test project
        typeof(KicktippContextProvider),
        
        // FirebaseAdapter - No dedicated test project
        typeof(FirebaseContextRepository),
        
        // KicktippIntegration - No dedicated test project
        typeof(IKicktippClient),
        
        // Orchestrator - No dedicated test project
        typeof(Program)
    ];

    /// <summary>
    /// Verifies that all expected assemblies are loaded.
    /// </summary>
    /// <returns>The names of assemblies that were loaded.</returns>
    public static string[] GetLoadedAssemblyNames()
    {
        return LoadedAssemblyTypes
            .Select(t => t.Assembly.GetName().Name!)
            .Distinct()
            .OrderBy(n => n)
            .ToArray();
    }
}
