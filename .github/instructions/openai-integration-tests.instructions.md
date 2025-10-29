---
applyTo: '**/OpenAiIntegration.Tests/**'
---
# Instructions when writing OpenAI integration tests

ALWAYS follow these when writing `OpenAiIntegration.Tests`

## Do the following

* Consult the `README.md` and source code in #githubRepo openai/openai-dotnet for details on any topic related to the OpenAI library.
* When creating instances of types from the OpenAI library (e.g., `ChatMessage`, `ChatCompletionRequest`), use the library provided model factories.
* When mocking OpenAI client calls, consult the library `README.md` for the correct way to set up the mocks.
