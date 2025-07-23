using DotNetEnv;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Orchestrator;

public static class EnvironmentHelper
{
    public static void LoadEnvironmentVariables(ILogger logger)
    {
        try
        {
            // Use PathUtility to get the correct .env file path
            var envPath = PathUtility.GetEnvFilePath("Orchestrator");
            
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

            // Load Firebase credentials if available
            LoadFirebaseCredentials(logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load .env file: {Message}", ex.Message);
        }
    }

    private static void LoadFirebaseCredentials(ILogger logger)
    {
        try
        {
            // Check if Firebase credentials are already set via environment variables
            var existingFirebaseJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
            var existingProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
            
            if (!string.IsNullOrEmpty(existingFirebaseJson) && !string.IsNullOrEmpty(existingProjectId))
            {
                logger.LogInformation("Firebase credentials already set via environment variables");
                return;
            }

            // Try to load from firebase.json file
            var firebaseJsonPath = PathUtility.GetFirebaseJsonPath();
            
            if (File.Exists(firebaseJsonPath))
            {
                var firebaseJson = File.ReadAllText(firebaseJsonPath);
                
                // Parse the JSON to extract project_id
                try
                {
                    using var document = JsonDocument.Parse(firebaseJson);
                    var root = document.RootElement;
                    
                    if (root.TryGetProperty("project_id", out var projectIdElement))
                    {
                        var projectId = projectIdElement.GetString();
                        
                        if (!string.IsNullOrEmpty(projectId))
                        {
                            // Set both environment variables
                            Environment.SetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON", firebaseJson);
                            Environment.SetEnvironmentVariable("FIREBASE_PROJECT_ID", projectId);
                            
                            logger.LogInformation("Loaded Firebase credentials from: {FirebasePath}", firebaseJsonPath);
                            logger.LogInformation("Firebase project ID: {ProjectId}", projectId);
                        }
                        else
                        {
                            logger.LogWarning("Firebase JSON file is missing or has empty project_id field");
                        }
                    }
                    else
                    {
                        logger.LogWarning("Firebase JSON file is missing project_id field");
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Failed to parse Firebase JSON file: {Message}", ex.Message);
                }
            }
            else
            {
                logger.LogInformation("No Firebase credentials file found at: {FirebasePath}", firebaseJsonPath);
                logger.LogInformation("Firebase integration will be disabled unless FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON are set");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load Firebase credentials: {Message}", ex.Message);
        }
    }
}
