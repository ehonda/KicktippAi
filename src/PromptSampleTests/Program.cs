using Microsoft.Extensions.Logging;

namespace PromptSampleTests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var logger = LoggingConfiguration.CreateLogger<Program>();

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: PromptSampleTests <model> <prompt-sample-directory>");
            Console.WriteLine("Example: PromptSampleTests gpt-4o-2024-08-06 \"c:\\path\\to\\2425_md34_rbl_vfb\"");
            return 1;
        }

        var model = args[0];
        var promptSampleDirectory = args[1];

        try
        {
            // Load environment variables from .env file
            EnvironmentHelper.LoadEnvironmentVariables(logger);

            var testRunnerLogger = LoggingConfiguration.CreateLogger<PromptTestRunner>();
            var runner = new PromptTestRunner(testRunnerLogger);
            await runner.RunAsync(model, promptSampleDirectory);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while running prompt test: {Message}", ex.Message);
            return 1;
        }
    }
}
