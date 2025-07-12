using DotNetEnv;
using Microsoft.Extensions.Logging;

namespace PromptSampleTests;

public static class EnvironmentHelper
{
    public static void LoadEnvironmentVariables(ILogger logger)
    {
        try
        {
            // Try to find .env file in secrets directory
            var envPath = FindEnvFile();
            
            if (envPath != null && File.Exists(envPath))
            {
                Env.Load(envPath);
                logger.LogInformation("Loaded .env file from: {EnvPath}", envPath);
            }
            else
            {
                logger.LogWarning("No .env file found. Please create one in the secrets directory based on .env.example");
                logger.LogInformation("Expected location: KicktippAi.Secrets/src/PromptSampleTests/.env (sibling to solution directory)");
                logger.LogInformation("Alternatively, set OPENAI_API_KEY environment variable directly");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load .env file");
        }
    }
    
    private static string? FindEnvFile()
    {
        // Start from the current assembly location and work our way up to find the solution directory
        var currentDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        
        while (currentDir != null)
        {
            // Look for solution file indicators (like .slnx or .sln files)
            var solutionFiles = Directory.GetFiles(currentDir, "*.sln*", SearchOption.TopDirectoryOnly);
            
            if (solutionFiles.Length > 0)
            {
                // Found solution directory, now look for sibling secrets directory
                var parentDir = Path.GetDirectoryName(currentDir);
                if (parentDir != null)
                {
                    var secretsDir = Path.Combine(parentDir, "KicktippAi.Secrets");
                    var envPath = Path.Combine(secretsDir, "src", "PromptSampleTests", ".env");
                    
                    if (File.Exists(envPath))
                    {
                        return envPath;
                    }
                }
                break; // Don't continue searching beyond the solution directory
            }
            
            currentDir = Path.GetDirectoryName(currentDir);
        }
        
        // Fallback: try relative to current working directory
        var currentWorkingDir = Directory.GetCurrentDirectory();
        var fallbackPath = Path.Combine(currentWorkingDir, "..", "..", "..", "..", "KicktippAi.Secrets", "src", "PromptSampleTests", ".env");
        
        if (File.Exists(fallbackPath))
        {
            return Path.GetFullPath(fallbackPath);
        }
        
        return null;
    }
}
