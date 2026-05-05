using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Orchestrator;

public static class LoggingConfiguration
{
    private static readonly HashSet<string> QuietExperimentRunCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "run-slice",
        "run-repeated-match",
        "run-community-to-date"
    };

    public static LogLevel GetMinimumLevelForCommandLine(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Any(QuietExperimentRunCommands.Contains)
            ? LogLevel.Warning
            : LogLevel.Information;
    }

    public static ILogger<T> CreateLogger<T>(LogLevel minimumLevel = LogLevel.Information)
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
            .SetMinimumLevel(minimumLevel));
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider.GetRequiredService<ILogger<T>>();
    }
}
