using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console.Cli;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Bridges Microsoft.Extensions.DependencyInjection with Spectre.Console.Cli's <see cref="ITypeRegistrar"/>.
/// </summary>
/// <remarks>
/// <para>
/// Based on the canonical implementation from spectreconsole/examples:
/// <see href="https://github.com/spectreconsole/examples/blob/main/examples/Cli/Logging/Infrastructure/TypeRegistrar.cs"/>
/// </para>
/// <para>
/// Extended to start <see cref="IHostedService"/> instances after building the
/// <see cref="ServiceProvider"/>. This is necessary because Spectre.Console.Cli does not use
/// <c>IHost</c>, so hosted services (e.g. OpenTelemetry's <c>TelemetryHostedService</c>)
/// would never be started otherwise.
/// </para>
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
        var provider = _builder.BuildServiceProvider();

        // Start any registered IHostedService instances so they can initialize
        // (e.g. OpenTelemetry builds its TracerProvider during StartAsync).
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        foreach (var service in hostedServices)
        {
            service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        return new TypeResolver(provider, hostedServices);
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
