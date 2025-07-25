using Core;
using FirebaseAdapter.Configuration;
using Google.Cloud.Firestore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace FirebaseAdapter;

/// <summary>
/// Extension methods for configuring Firebase services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Firebase Firestore database services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">Optional configuration delegate for Firebase options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFirebaseDatabase(
        this IServiceCollection services, 
        Action<FirebaseOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Register FirestoreDb as a singleton
        services.AddSingleton<FirestoreDb>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FirebaseOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<FirebasePredictionRepository>>();

            try
            {
                options.Validate();

                FirestoreDb firestoreDb;

                if (!string.IsNullOrWhiteSpace(options.ServiceAccountPath))
                {
                    // Use service account file path
                    logger.LogInformation("Initializing Firebase with service account file: {Path}", options.ServiceAccountPath);
                    
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", options.ServiceAccountPath);
                    firestoreDb = FirestoreDb.Create(options.ProjectId);
                }
                else
                {
                    // Use service account JSON content
                    logger.LogInformation("Initializing Firebase with service account JSON content for project: {ProjectId}", options.ProjectId);
                    
                    // Create FirestoreDb from JSON content
                    var credentialsBytes = Encoding.UTF8.GetBytes(options.ServiceAccountJson);
                    using var stream = new MemoryStream(credentialsBytes);
                    
                    var firestoreDbBuilder = new FirestoreDbBuilder
                    {
                        ProjectId = options.ProjectId,
                        JsonCredentials = options.ServiceAccountJson
                    };
                    
                    firestoreDb = firestoreDbBuilder.Build();
                }

                logger.LogInformation("Firebase Firestore successfully initialized for project: {ProjectId}", options.ProjectId);
                return firestoreDb;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Firebase Firestore for project: {ProjectId}", options.ProjectId);
                throw;
            }
        });

        // Register the prediction repository
        services.AddScoped<IPredictionRepository, FirebasePredictionRepository>();

        return services;
    }

    /// <summary>
    /// Adds Firebase Firestore database services with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration to bind Firebase options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFirebaseDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.SectionName));
        return services.AddFirebaseDatabase((Action<FirebaseOptions>?)null);
    }

    /// <summary>
    /// Adds Firebase Firestore database services with explicit project ID and service account JSON.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="projectId">Firebase project ID.</param>
    /// <param name="serviceAccountJson">Service account JSON content.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFirebaseDatabase(
        this IServiceCollection services,
        string projectId,
        string serviceAccountJson)
    {
        return services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = projectId;
            options.ServiceAccountJson = serviceAccountJson;
        });
    }

    /// <summary>
    /// Adds Firebase Firestore database services with explicit project ID and service account file path.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="projectId">Firebase project ID.</param>
    /// <param name="serviceAccountPath">Path to service account JSON file.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFirebaseDatabaseWithFile(
        this IServiceCollection services,
        string projectId,
        string serviceAccountPath)
    {
        return services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = projectId;
            options.ServiceAccountPath = serviceAccountPath;
        });
    }
}
