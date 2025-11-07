---
applyTo: '**/OpenAiIntegration.Tests/**'
---
# Instructions when writing OpenAI integration tests

ALWAYS follow these when writing `OpenAiIntegration.Tests`

## Do the following

* Refer to the OpenAI .NET API library [usage instructions](./openai-dotnet-usage.instructions.md) for general guidelines on using the OpenAI library.
* When creating instances of types from the OpenAI library (e.g., `ChatMessage`, `ChatCompletionRequest`), use the library provided model factories.
* When mocking OpenAI client calls, consult the library `README.md` for the correct way to set up the mocks.
