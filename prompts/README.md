# Model-Specific Prompts

This directory contains model-specific prompts for KicktippAi predictions. The system supports using different prompt templates optimized for different AI models.

## Directory Structure

```text
prompts/
├── o3/
│   ├── match.md      # Match prediction prompts for O3 model
│   └── bonus.md      # Bonus question prompts for O3 model
├── gpt-5/
│   ├── match.md      # Match prediction prompts for GPT-5 model
│   └── bonus.md      # Bonus question prompts for GPT-5 model
└── README.md         # This file
```

## Adding Prompts for a New Model

To add prompts for a new model (e.g., `claude-3.5`):

1. **Create model directory**:

   ```bash
   mkdir prompts/claude-3.5
   ```

2. **Create prompt files**:
   - Copy existing prompts as templates or create new ones
   - `prompts/claude-3.5/match.md` - For match predictions
   - `prompts/claude-3.5/bonus.md` - For bonus question predictions

3. **Update cross-model mappings** (if needed):
   - Edit `src/OpenAiIntegration/PredictionService.cs`
   - Update the `GetPromptModelForModel()` method to add the new model

## Cross-Model Prompt Mappings

Some models are configured to use prompts from other models. Current mappings:

- `o4-mini` → uses `o3` prompts
- `gpt-5-nano` → uses `gpt-5` prompts

### ⚠️ Important: Check for Cross-Model Mappings

Before creating specialized prompts for a model, **always check if it's currently mapped to use another model's prompts**.

**Example**: If you want to create specialized prompts for `o4-mini` (which currently uses `o3` prompts), you need to:

1. **Check current mapping** by running a command in verbose mode:

   ```bash
   dotnet run --project src/Orchestrator -- matchday o4-mini --community ehonda-test-buli --verbose --dry-run
   ```

   Example output showing cross-model mapping:

   ```text
   Matchday command initialized with model: o4-mini
   Verbose mode enabled
   Match prompt: C:\Users\...\KicktippAi\prompts\o3\match.md
   ```

2. **Remove the cross-model mapping** in `src/OpenAiIntegration/PredictionService.cs`:

   ```csharp
   private static string GetPromptModelForModel(string model)
   {
       return model switch
       {
           "o3" => "o3",
           "gpt-5" => "gpt-5",
           
           // Remove this line if creating specialized o4-mini prompts:
           // "o4-mini" => "o3",
           
           "gpt-5-nano" => "gpt-5",
           
           _ => model
       };
   }
   ```

3. **Create the model-specific directory and prompts**:

   ```bash
   mkdir prompts/o4-mini
   # Add match.md and bonus.md files
   ```

4. **Verify the change** by running the command again:

   ```bash
   dotnet run --project src/Orchestrator -- matchday o4-mini --community ehonda-test-buli --verbose --dry-run
   ```

   Expected output after removing cross-model mapping:

   ```text
   Matchday command initialized with model: o4-mini
   Verbose mode enabled
   Match prompt: C:\Users\...\KicktippAi\prompts\o4-mini\match.md
   ```

## Testing Prompt Changes

Always test your changes:

1. **Check verbose output** to confirm correct prompt paths:

   ```bash
   # For match predictions
   dotnet run --project src/Orchestrator -- matchday MODEL --community COMMUNITY --verbose --dry-run
   
   # For bonus predictions  
   dotnet run --project src/Orchestrator -- bonus MODEL --community COMMUNITY --verbose --dry-run
   ```

2. **Verify the prompts work** by running actual predictions:

   ```bash
   # Remove --dry-run to test actual prediction generation
   dotnet run --project src/Orchestrator -- matchday MODEL --community COMMUNITY --verbose
   ```

## Prompt File Format

Each prompt file should be a Markdown file containing:

- Instructions for the AI model
- Input format specification (JSON schema)
- Context document format specification
- Any model-specific optimizations or guidance

See existing prompt files in `o3/` or `gpt-5/` directories for examples.
