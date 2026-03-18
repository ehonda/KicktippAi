using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;

namespace Orchestrator.Tests.Infrastructure;

public class LoggingAndTypeInfrastructureTests
{
    [Test]
    public async Task CreateLogger_returns_usable_logger_instance()
    {
        var logger = LoggingConfiguration.CreateLogger<LoggingAndTypeInfrastructureTests>();

        logger.LogInformation("Testing logger creation");

        await Assert.That(logger).IsNotNull();
    }

    [Test]
    public async Task TypeResolver_constructor_throws_for_null_dependencies()
    {
        await Assert.That(() => new TypeResolver(null!, []))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new TypeResolver(new ServiceCollection().BuildServiceProvider(), null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task TypeResolver_resolve_returns_null_for_missing_or_null_type()
    {
        var provider = new ServiceCollection()
            .AddSingleton("registered")
            .BuildServiceProvider();
        using var resolver = new TypeResolver(provider, []);

        await Assert.That(resolver.Resolve(null)).IsNull();
        await Assert.That(resolver.Resolve(typeof(int))).IsNull();
        await Assert.That(resolver.Resolve(typeof(string))).IsEqualTo("registered");
    }

    [Test]
    public async Task TypeResolver_dispose_stops_hosted_services_and_disposes_provider()
    {
        var hostedService = new RecordingHostedService();
        var provider = new DisposableServiceProvider();
        var resolver = new TypeResolver(provider, [hostedService]);

        resolver.Dispose();

        await Assert.That(hostedService.StopCalls).IsEqualTo(1);
        await Assert.That(provider.DisposeCalls).IsEqualTo(1);
    }

    [Test]
    public async Task TypeResolver_dispose_skips_provider_disposal_when_not_supported()
    {
        var hostedService = new RecordingHostedService();
        var provider = new NonDisposableServiceProvider();
        var resolver = new TypeResolver(provider, [hostedService]);

        resolver.Dispose();

        await Assert.That(hostedService.StopCalls).IsEqualTo(1);
    }

    [Test]
    public async Task TypeRegistrar_build_starts_hosted_services_and_resolves_registered_services()
    {
        var services = new ServiceCollection();
        var hostedService = new RecordingHostedService();
        var lazyCreateCalls = 0;

        services.AddSingleton<IHostedService>(hostedService);

        var registrar = new TypeRegistrar(services);
        registrar.Register(typeof(IDisposable), typeof(MemoryStream));
        registrar.RegisterInstance(typeof(string), "registered-instance");
        registrar.RegisterLazy(typeof(Uri), () =>
        {
            lazyCreateCalls++;
            return new Uri("https://example.com");
        });

        var resolver = registrar.Build();

        try
        {
            await Assert.That(hostedService.StartCalls).IsEqualTo(1);
            await Assert.That(resolver.Resolve(typeof(IDisposable))).IsTypeOf<MemoryStream>();
            await Assert.That(resolver.Resolve(typeof(string))).IsEqualTo("registered-instance");
            await Assert.That(resolver.Resolve(typeof(Uri))).IsTypeOf<Uri>();
            await Assert.That(lazyCreateCalls).IsEqualTo(1);
            await Assert.That(hostedService.StopCalls).IsEqualTo(0);
        }
        finally
        {
            ((IDisposable)resolver).Dispose();
        }
    }

    [Test]
    public async Task TypeRegistrar_register_lazy_throws_for_null_factory()
    {
        var registrar = new TypeRegistrar(new ServiceCollection());

        await Assert.That(() => registrar.RegisterLazy(typeof(string), null!))
            .Throws<ArgumentNullException>();
    }

    private sealed class RecordingHostedService : IHostedService
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalls++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class DisposableServiceProvider : IServiceProvider, IDisposable
    {
        public int DisposeCalls { get; private set; }

        public object? GetService(Type serviceType) => null;

        public void Dispose()
        {
            DisposeCalls++;
        }
    }

    private sealed class NonDisposableServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
