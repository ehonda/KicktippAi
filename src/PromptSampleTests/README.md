# Prompt Sample Tests

This project allows you to test prompt samples with different OpenAI models by loading instructions and match data from disk.

## Prerequisites

1. Set up your OpenAI API key using one of these methods:
   - **Recommended**: Create a `.env` file in `KicktippAi.Secrets/src/PromptSampleTests/.env` (sibling to solution directory)
   - **Alternative**: Set the `OPENAI_API_KEY` environment variable directly
2. Ensure you have a prompt sample directory with the required files

### .env File Setup

Create a `.env` file at `KicktippAi.Secrets/src/PromptSampleTests/.env` with:

```env
OPENAI_API_KEY=your_openai_api_key_here
```

## Usage

```bash
dotnet run --project src/PromptSampleTests/PromptSampleTests.csproj <model> <prompt-sample-directory>
```

### Example

```bash
dotnet run --project src/PromptSampleTests/PromptSampleTests.csproj gpt-4o-2024-08-06 "c:\Users\dennis\source\repos\ehonda\KicktippAi\prompts\reasoning-models\predict-one-match\v0-handcrafted\samples\2425_md34_rbl_vfb"
```

## Prompt Sample Directory Structure

The prompt sample directory must contain:

- `instructions.md` - The system instructions for the model
- `match.json` - The match data in JSON format

Example `match.json`:
```json
{"homeTeam":"RB Leipzig","awayTeam":"VfB Stuttgart","startsAt":"2025-05-17T13:30:00Z"}
```

## Output

The tool will output:
1. Model and directory information
2. The model's prediction response
3. Token usage statistics in JSON format (similar to OpenAI API response format)

## Features

- **Flexible API Key Loading**: Automatically loads API key from `.env` file with fallback to environment variables
- **Comprehensive Logging**: Uses structured logging with clear information and warning messages
- **Error Handling**: Detailed error messages for missing files, directories, and configuration
- **Token Usage Logging**: Comprehensive token usage statistics in JSON format (similar to OpenAI API response format)
- **Hard-coded Safety Limits**: Built-in safeguards for API usage
