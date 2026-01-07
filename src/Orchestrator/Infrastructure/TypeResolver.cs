using Spectre.Console.Cli;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Resolves types from the DI container for Spectre.Console.Cli.
/// </summary>
/// <remarks>
/// Based on the canonical implementation from spectreconsole/examples:
/// <see href="https://github.com/spectreconsole/examples/blob/main/examples/Cli/Logging/Infrastructure/TypeResolver.cs"/>
/// </remarks>
public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public object? Resolve(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        return _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
