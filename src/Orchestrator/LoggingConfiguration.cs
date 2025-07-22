using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Orchestrator;

public static class LoggingConfiguration
{
    public static ILogger<T> CreateLogger<T>()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => 
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = false;
                options.TimestampFormat = null;
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            })
            .SetMinimumLevel(LogLevel.Information)); // Show info level for .env loading feedback
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider.GetRequiredService<ILogger<T>>();
    }
}
