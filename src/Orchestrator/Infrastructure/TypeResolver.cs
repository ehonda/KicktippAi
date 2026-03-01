using Microsoft.Extensions.Hosting;
using Spectre.Console.Cli;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Resolves types from the DI container for Spectre.Console.Cli.
/// </summary>
/// <remarks>
/// <para>
/// Based on the canonical implementation from spectreconsole/examples:
/// <see href="https://github.com/spectreconsole/examples/blob/main/examples/Cli/Logging/Infrastructure/TypeResolver.cs"/>
/// </para>
/// <para>
/// Extended to stop <see cref="IHostedService"/> instances on disposal, ensuring
/// graceful shutdown of services started by <see cref="TypeRegistrar.Build"/>.
/// </para>
/// </remarks>
public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly IReadOnlyList<IHostedService> _hostedServices;

    public TypeResolver(IServiceProvider provider, IReadOnlyList<IHostedService> hostedServices)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _hostedServices = hostedServices ?? throw new ArgumentNullException(nameof(hostedServices));
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
        // Stop hosted services before disposing the provider to allow graceful shutdown
        // (e.g. OpenTelemetry TracerProvider flush).
        //
        // Blocking wait rationale: Spectre.Console.Cli disposes our resolver through
        // TypeResolverAdapter.Dispose(), which only checks for IDisposable — not IAsyncDisposable.
        // The enclosing `using` block in CommandExecutor.ExecuteAsync is synchronous, so there is no
        // async disposal path we can participate in. IHostedService only exposes async lifecycle
        // methods, so the .GetAwaiter().GetResult() bridge is required here.
        // See: spectre.console.cli/src/Spectre.Console.Cli/Internal/TypeResolverAdapter.cs
        //      spectre.console.cli/src/Spectre.Console.Cli/Internal/CommandExecutor.cs (~line 88)
        foreach (var service in _hostedServices)
        {
            service.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
