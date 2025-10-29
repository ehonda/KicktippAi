---
applyTo: '**/OpenAiIntegration.Tests/**'
---
# Instructions when writing OpenAI integration tests

ALWAYS follow these when writing `OpenAiIntegration.Tests`

## Creating instances of library types

* When creating instances of types from the OpenAI library (e.g., `ChatMessage`, `ChatCompletionRequest`), use the library provided model factories. Consult the `README.md` and source code in #githubRepo openai/openai-dotnet for details on available factory methods and their usage.
