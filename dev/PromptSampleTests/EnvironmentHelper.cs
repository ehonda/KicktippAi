using DotNetEnv;
using Microsoft.Extensions.Logging;

namespace PromptSampleTests;

public static class EnvironmentHelper
{
    public static void LoadEnvironmentVariables(ILogger logger)
    {
        try
        {
            // Use PathUtility to get the correct .env file path
            var envPath = PathUtility.GetEnvFilePath("PromptSampleTests");
            
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                logger.LogInformation("Loaded .env file from: {EnvPath}", envPath);
            }
            else
            {
                logger.LogWarning("No .env file found at: {EnvPath}", envPath);
                logger.LogInformation("Please create a .env file in the secrets directory based on .env.example");
                logger.LogInformation("Alternatively, set environment variables directly");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load .env file: {Message}", ex.Message);
        }
    }
}
