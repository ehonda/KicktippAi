# OpenAI Integration - MVP Setup Guide

## Overview

The OpenAI Integration provides AI-powered football match predictions using OpenAI's GPT models. This MVP implementation allows you to replace random predictions with intelligent AI predictions.

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
2. **AI Predictions**: The `OpenAiPredictor` class implements `IPredictor<PredictorContext>`
3. **Intelligent Prompting**: The AI receives context about the match and provides structured predictions
4. **Fallback Safety**: If AI prediction fails, the system falls back to a 1-1 prediction
5. **Logging**: Comprehensive logging shows the AI prediction process

## Usage in POC

The POC application now automatically uses AI predictions instead of random predictions:

```csharp
// The POC automatically uses OpenAI predictor when configured
var aiPrediction = await openAiPredictor.PredictAsync(match, predictorContext);
```

## Features

- ✅ **Dependency Injection**: Easy setup with service collection extensions
- ✅ **Logging**: Comprehensive logging for debugging and monitoring
- ✅ **Error Handling**: Robust error handling with fallback predictions
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
