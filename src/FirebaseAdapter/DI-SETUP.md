# Firebase Adapter - Dependency Injection Setup

This document explains how to configure and use the Firebase adapter with dependency injection.

## Quick Start

### 1. Basic Setup with Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Firebase services using configuration
builder.Services.AddFirebaseDatabase(builder.Configuration);

var app = builder.Build();
```

**Configuration (appsettings.json):**
```json
{
  "Firebase": {
    "ProjectId": "your-firebase-project-id",
    "ServiceAccountJson": "{\"type\":\"service_account\",\"project_id\":\"your-project\",...}"
  }
}
```

### 2. Setup with Service Account File

```csharp
services.AddFirebaseDatabaseWithFile(
    projectId: "your-firebase-project-id",
    serviceAccountPath: "/path/to/service-account-key.json"
);
```

### 3. Setup with Direct JSON

```csharp
services.AddFirebaseDatabase(
    projectId: "your-firebase-project-id", 
    serviceAccountJson: jsonContent
);
```

### 4. Manual Configuration

```csharp
services.AddFirebaseDatabase(options =>
{
    options.ProjectId = "your-firebase-project-id";
    options.ServiceAccountJson = jsonContent;
    // OR
    options.ServiceAccountPath = "/path/to/service-account-key.json";
});
```

## Configuration Options

### FirebaseOptions Properties

| Property | Type | Description | Required |
|----------|------|-------------|----------|
| `ProjectId` | `string` | Firebase project ID | ✅ |
| `ServiceAccountJson` | `string` | Complete service account JSON content | ✅* |
| `ServiceAccountPath` | `string` | Path to service account JSON file | ✅* |

*Either `ServiceAccountJson` OR `ServiceAccountPath` must be provided.

### Environment Variables

You can also use environment variables:
- `GOOGLE_APPLICATION_CREDENTIALS` - Path to service account file
- When using file path, this will be set automatically

## Usage in Your Code

Once configured, inject `IPredictionRepository` anywhere in your application:

```csharp
public class PredictionService
{
    private readonly IPredictionRepository _repository;

    public PredictionService(IPredictionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Prediction?> GetPredictionAsync(Match match)
    {
        return await _repository.GetPredictionAsync(match);
    }
}
```

## Error Handling

The setup includes validation and proper error handling:

- ✅ Configuration validation at startup
- ✅ Detailed error logging for connection issues  
- ✅ Graceful fallbacks for authentication methods

## Troubleshooting

### Common Issues

1. **Invalid JSON Format**
   ```
   Error: Failed to initialize Firebase Firestore
   ```
   - Ensure service account JSON is valid and complete
   - Check that the JSON hasn't been truncated or corrupted

2. **Authentication Errors**
   ```
   Error: The Application Default Credentials are not available
   ```
   - Verify the service account has Firestore permissions
   - Check that the project ID matches your Firebase project

3. **Permission Errors**
   ```
   Error: Missing or insufficient permissions
   ```
   - Ensure the service account has `Cloud Datastore User` role
   - Verify Firestore is enabled in your Firebase project

### Service Account Setup

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Select your project → Project Settings → Service Accounts
3. Click "Generate new private key"
4. Download the JSON file and use it as `ServiceAccountJson` or `ServiceAccountPath`

### Required Permissions

Your service account needs these Firebase/Google Cloud permissions:
- `Cloud Datastore User` (for read/write access)
- `Cloud Datastore Index Admin` (for query optimization)
