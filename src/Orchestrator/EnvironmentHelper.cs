using DotNetEnv;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Orchestrator;

public static class EnvironmentHelper
{
    private const string KicktippUsernameEnvVar = "KICKTIPP_USERNAME";
    private const string KicktippPasswordEnvVar = "KICKTIPP_PASSWORD";

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

    public static void LoadCommunityKicktippCredentials(ILogger logger, string community)
    {
        LoadCommunityKicktippCredentials(logger, community, credentialProfile: null);
    }

    public static void LoadCommunityKicktippCredentials(
        ILogger logger,
        string community,
        string? credentialProfile)
    {
        var postingCommunity = ValidatePostingCommunity(community);
        var normalizedCredentialProfile = string.IsNullOrWhiteSpace(credentialProfile)
            ? null
            : ValidateCredentialProfile(credentialProfile);
        var credentialFileSuffix = normalizedCredentialProfile is null
            ? postingCommunity
            : $"{postingCommunity}.{normalizedCredentialProfile}";

        var envPath = PathUtility.GetEnvFilePath("Orchestrator", credentialFileSuffix);
        if (!File.Exists(envPath))
        {
            logger.LogWarning(
                "No community-specific Kicktipp credentials file found at: {EnvPath}. Existing environment variables will be used.",
                envPath);
            return;
        }

        var variables = ReadCommunityCredentialFile(envPath);

        var username = GetRequiredCredential(variables, KicktippUsernameEnvVar, envPath);
        var password = GetRequiredCredential(variables, KicktippPasswordEnvVar, envPath);

        // Change the credential pair only after both sibling-file values have passed validation.
        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, username);
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, password);

        logger.LogInformation("Loaded community-specific Kicktipp credentials from: {EnvPath}", envPath);
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

    private static string GetRequiredCredential(
        IReadOnlyDictionary<string, string> variables,
        string variableName,
        string envPath)
    {
        if (!variables.TryGetValue(variableName, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{Path.GetFileName(envPath)} must define {variableName}.");
        }

        return value;
    }

    private static string ValidatePostingCommunity(string community)
    {
        if (string.IsNullOrWhiteSpace(community)
            || !string.Equals(community, community.Trim(), StringComparison.Ordinal)
            || community[0] == '-'
            || community[^1] == '-')
        {
            throw CreateInvalidPostingCommunityException();
        }

        var previousWasHyphen = false;
        foreach (var character in community)
        {
            var isLowercaseAsciiLetter = character is >= 'a' and <= 'z';
            var isAsciiDigit = character is >= '0' and <= '9';
            var isHyphen = character == '-';
            if ((!isLowercaseAsciiLetter && !isAsciiDigit && !isHyphen)
                || (isHyphen && previousWasHyphen))
            {
                throw CreateInvalidPostingCommunityException();
            }

            previousWasHyphen = isHyphen;
        }

        return community;
    }

    private static string ValidateCredentialProfile(string credentialProfile)
    {
        if (string.IsNullOrWhiteSpace(credentialProfile)
            || !string.Equals(credentialProfile, credentialProfile.Trim(), StringComparison.Ordinal)
            || credentialProfile[0] == '-'
            || credentialProfile[^1] == '-')
        {
            throw CreateInvalidCredentialProfileException();
        }

        var previousWasHyphen = false;
        foreach (var character in credentialProfile)
        {
            var isLowercaseAsciiLetter = character is >= 'a' and <= 'z';
            var isAsciiDigit = character is >= '0' and <= '9';
            var isHyphen = character == '-';
            if ((!isLowercaseAsciiLetter && !isAsciiDigit && !isHyphen)
                || (isHyphen && previousWasHyphen))
            {
                throw CreateInvalidCredentialProfileException();
            }

            previousWasHyphen = isHyphen;
        }

        return credentialProfile;
    }

    private static ArgumentException CreateInvalidPostingCommunityException()
    {
        return new ArgumentException(
            "Posting community must be an exact lowercase Kicktipp slug containing only letters, digits, and single hyphens (for example, 'pes-squad').",
            "community");
    }

    private static ArgumentException CreateInvalidCredentialProfileException()
    {
        return new ArgumentException(
            "Kicktipp credential profile must be an exact lowercase participant slug containing only letters, digits, and single hyphens (for example, 'gpt-5-6-sol-xhigh').",
            "credentialProfile");
    }

    private static IReadOnlyDictionary<string, string> ReadCommunityCredentialFile(string envPath)
    {
        var credentials = new Dictionary<string, string>(StringComparer.Ordinal);
        var lineNumber = 0;
        foreach (var line in File.ReadLines(envPath))
        {
            lineNumber++;
            var assignment = StripAssignmentPrefix(line.TrimStart());
            if (assignment.Length == 0 || assignment[0] == '#')
            {
                continue;
            }

            var separatorIndex = assignment.IndexOf('=');
            if (separatorIndex < 0)
            {
                throw InvalidCredentialFile(envPath, lineNumber, "expected an assignment");
            }

            var key = assignment[..separatorIndex].Trim();
            if (!string.Equals(key, KicktippUsernameEnvVar, StringComparison.Ordinal)
                && !string.Equals(key, KicktippPasswordEnvVar, StringComparison.Ordinal))
            {
                continue;
            }

            if (credentials.ContainsKey(key))
            {
                throw InvalidCredentialFile(envPath, lineNumber, $"defines {key} more than once");
            }

            credentials.Add(
                key,
                ParseCredentialValue(assignment[(separatorIndex + 1)..], envPath, lineNumber));
        }

        return credentials;
    }

    private static string StripAssignmentPrefix(string assignment)
    {
        foreach (var prefix in new[] { "export ", "set -x ", "set ", "SET " })
        {
            if (assignment.StartsWith(prefix, StringComparison.Ordinal))
            {
                return assignment[prefix.Length..].TrimStart();
            }
        }

        return assignment;
    }

    private static string ParseCredentialValue(
        string assignmentValue,
        string envPath,
        int lineNumber)
    {
        var value = assignmentValue.TrimStart();
        if (value.Length == 0 || value[0] == '#')
        {
            return string.Empty;
        }

        if (value[0] is '\'' or '"')
        {
            return ParseQuotedCredentialValue(value, envPath, lineNumber);
        }

        var commentIndex = -1;
        for (var index = 1; index < value.Length; index++)
        {
            if (value[index] == '#' && char.IsWhiteSpace(value[index - 1]))
            {
                commentIndex = index;
                break;
            }
        }

        var parsed = (commentIndex < 0 ? value : value[..commentIndex]).TrimEnd();
        if (parsed.Contains('$'))
        {
            throw InvalidCredentialFile(
                envPath,
                lineNumber,
                "uses interpolation; use a concrete value or single quotes for a literal dollar sign");
        }

        return parsed;
    }

    private static string ParseQuotedCredentialValue(
        string value,
        string envPath,
        int lineNumber)
    {
        var quote = value[0];
        var parsed = new StringBuilder();
        var closingQuoteIndex = -1;
        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (character == quote)
            {
                closingQuoteIndex = index;
                break;
            }

            if (quote == '"' && character == '\\')
            {
                if (++index >= value.Length)
                {
                    throw InvalidCredentialFile(envPath, lineNumber, "ends with an incomplete escape sequence");
                }

                var escaped = value[index];
                parsed.Append(escaped switch
                {
                    '\\' => '\\',
                    '"' => '"',
                    '$' => '$',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => throw InvalidCredentialFile(envPath, lineNumber, "contains an unsupported escape sequence")
                });
                continue;
            }

            if (quote == '"' && character == '$')
            {
                throw InvalidCredentialFile(
                    envPath,
                    lineNumber,
                    "uses interpolation; use a concrete value or escape a literal dollar sign");
            }

            parsed.Append(character);
        }

        if (closingQuoteIndex < 0)
        {
            throw InvalidCredentialFile(envPath, lineNumber, "has an unterminated quoted value");
        }

        var remainder = value[(closingQuoteIndex + 1)..].TrimStart();
        if (remainder.Length > 0 && remainder[0] != '#')
        {
            throw InvalidCredentialFile(envPath, lineNumber, "contains content after its quoted value");
        }

        return parsed.ToString();
    }

    private static InvalidOperationException InvalidCredentialFile(
        string envPath,
        int lineNumber,
        string reason)
    {
        return new InvalidOperationException(
            $"{Path.GetFileName(envPath)} is invalid at line {lineNumber}: {reason}.");
    }
}
