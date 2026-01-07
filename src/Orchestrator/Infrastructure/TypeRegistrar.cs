using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Bridges Microsoft.Extensions.DependencyInjection with Spectre.Console.Cli's <see cref="ITypeRegistrar"/>.
/// </summary>
/// <remarks>
/// Based on the canonical implementation from spectreconsole/examples:
/// <see href="https://github.com/spectreconsole/examples/blob/main/examples/Cli/Logging/Infrastructure/TypeRegistrar.cs"/>
/// </remarks>
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _builder;

    public TypeRegistrar(IServiceCollection builder)
    {
        _builder = builder;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_builder.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _builder.AddSingleton(service, _ => func());
    }
}
