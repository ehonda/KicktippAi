# OpenAI Integration

## Overview

The OpenAI Integration provides AI-powered football match predictions using OpenAI's GPT models. This integration includes both a low-level predictor and a high-level prediction service.

## Components

### 1. IPredictionService (Recommended)

The `IPredictionService` is the main service for generating match predictions with context documents.

**Features:**
- Service-oriented design with dependency injection
- Accepts match data and context documents
- Returns structured predictions
- Comprehensive logging
- Automatic instructions template loading
- Error handling with fallback predictions

**Usage:**
```csharp
// Dependency injection setup
services.AddOpenAiPredictor(apiKey, model);

// Service usage
var prediction = await predictionService.PredictMatchAsync(match, contextDocuments);
```

### 2. OpenAiPredictor (Legacy)

The lower-level `OpenAiPredictor` implements `IPredictor<PredictorContext>` for backwards compatibility.

## Prerequisites

1. **OpenAI API Key**: You need an OpenAI API key to use this integration
2. **Environment Variables**: Set up your `.env` file with the required keys

## Environment Setup

Add the following to your `.env` file:

```env
# Existing Kicktipp credentials
KICKTIPP_USERNAME=your_username
KICKTIPP_PASSWORD=your_password

# NEW: OpenAI API Key
OPENAI_API_KEY=your_openai_api_key_here
```

## Configuration

The integration uses `gpt-4o-mini` by default for cost-effective predictions. You can customize this in the service registration.

## How It Works

1. **Dependency Injection**: The `AddOpenAiPredictor()` extension method registers all required services
2. **Instructions Template**: Automatically loads the prediction instructions from the prompts folder
3. **Context Integration**: Combines the instructions template with provided context documents
4. **Structured Output**: Uses OpenAI's structured output format for reliable JSON responses
5. **Intelligent Prompting**: The AI receives context about the match and provides structured predictions
6. **Fallback Safety**: If AI prediction fails, the system falls back to a 1-1 prediction
7. **Logging**: Comprehensive logging shows the AI prediction process and token usage

## Usage Examples

### Basic Service Usage

```csharp
// Register services
services.AddOpenAiPredictor("your-api-key");

// Inject and use
public class MyService
{
    private readonly IPredictionService _predictionService;
    
    public MyService(IPredictionService predictionService)
    {
        _predictionService = predictionService;
    }
    
    public async Task<Prediction> PredictMatch(Match match, IEnumerable<DocumentContext> context)
    {
        return await _predictionService.PredictMatchAsync(match, context);
    }
}
```

### With Configuration

```csharp
// From configuration
services.AddOpenAiPredictor(configuration);

// With custom model
services.AddOpenAiPredictor("your-api-key", "gpt-4o");
```

## Features

- ✅ **Service-Oriented Design**: High-level `IPredictionService` for easy integration
- ✅ **Dependency Injection**: Easy setup with service collection extensions
- ✅ **Context Documents**: Support for providing match context from various sources
- ✅ **Structured Output**: JSON schema validation for reliable predictions
- ✅ **Instructions Template**: Automatically loads and combines instructions with context
- ✅ **Logging**: Comprehensive logging for debugging and monitoring
- ✅ **Error Handling**: Robust error handling with fallback predictions
- ✅ **Token Usage Tracking**: Logs token consumption for cost monitoring
- ✅ **Cost Safeguards**: Maximum token limits to prevent excessive API costs
- ✅ **Cost Optimization**: Uses `gpt-4o-mini` for affordable predictions
- ✅ **Structured Parsing**: Parses AI responses into structured predictions
- ✅ **Context Support**: Extensible context system for future enhancements

## Future Enhancements

- Add team statistics and historical data to predictions
- Implement caching for similar matches
- Add support for different prediction strategies
- Include confidence scores in predictions

## Cost Considerations

Using `gpt-4o-mini` keeps costs low while providing good prediction quality. Monitor your OpenAI usage through the OpenAI dashboard.

## Troubleshooting

1. **API Key Issues**: Ensure your OpenAI API key is valid and has sufficient credits
2. **Rate Limits**: The integration handles rate limiting gracefully
3. **Parsing Errors**: Check logs for AI response parsing issues
4. **Network Issues**: Ensure stable internet connection for API calls

## Testing

To test the integration:

1. Set up your `.env` file with valid credentials
2. Run the POC application
3. Check the logs for AI prediction process
4. Verify predictions are generated for each match
