using Microsoft.Extensions.Logging;

namespace PromptSampleTests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var logger = LoggingConfiguration.CreateLogger<Program>();

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: PromptSampleTests <model> <mode>");
            Console.WriteLine("Modes:");
            Console.WriteLine("  file <prompt-sample-directory>  - Load instructions.md and match.json from directory");
            Console.WriteLine("  live [homeTeam] [awayTeam]      - Use live Kicktipp data and instructions template");
            Console.WriteLine("Examples:");
            Console.WriteLine("  PromptSampleTests gpt-4o-2024-08-06 file \"c:\\path\\to\\2425_md34_rbl_vfb\"");
            Console.WriteLine("  PromptSampleTests o4-mini live \"FC Bayern München\" \"RB Leipzig\"");
            return 1;
        }

        var model = args[0];
        var mode = args[1];

        try
        {
            // Load environment variables from .env file
            EnvironmentHelper.LoadEnvironmentVariables(logger);

            var testRunnerLogger = LoggingConfiguration.CreateLogger<PromptTestRunner>();
            var runner = new PromptTestRunner(testRunnerLogger);

            if (mode == "file")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Error: file mode requires a prompt-sample-directory argument");
                    return 1;
                }
                var promptSampleDirectory = args[2];
                await runner.RunFileMode(model, promptSampleDirectory);
            }
            else if (mode == "live")
            {
                string homeTeam = args.Length > 2 ? args[2] : "FC Bayern München";
                string awayTeam = args.Length > 3 ? args[3] : "RB Leipzig";
                await runner.RunLiveMode(model, homeTeam, awayTeam);
            }
            else
            {
                Console.WriteLine($"Error: Unknown mode '{mode}'. Use 'file' or 'live'.");
                return 1;
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while running prompt test: {Message}", ex.Message);
            return 1;
        }
    }
}
