---
applyTo: '**'
---
# Prompt Development Instructions

The following instructions are crucial for the agent to follow when developing or testing prompts in this project.

## When to Use

* When running the `PromptSampleTests` project.
* When testing or developing prompts with OpenAI models.

## ✅ Do's

* Run the project directly from it's subdirectory (i.e. `dev/PromptSampleTests`)
* Use `o4-mini` for testing and development purposes. Examples Usages:

```powershell
# "Semi integrated mode"
dotnet run -- live o4-mini --match 0

# Using the prediction service to predict one match - PREFER TO USE THIS ONE
dotnet run -- service o4-mini --home "Bayern München" --away "RB Leipzig"
```
