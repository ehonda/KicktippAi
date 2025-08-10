---
applyTo: 'src/**'
---
When making changes to the `src/Orchestrator` project or our core logic, follow these guidelines:

* Test using the orchestrator project, example:
  
  ```powershell
  dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli
  ```

* Use `gpt-5-nano` for testing to save costs and execute quickly, we don't care about the quality of the predictions for development
* When the command is generating predictions, i.e. `-override-database` is specified, use `--estimated-costs o3` because that is our production model and we want to see cost estimates for it.
* Use `ehonda-test-buli` as the community for testing
* Use the following commands to get more information about the available commands and options:
  
  ```powershell
  dotnet run --project src/Orchestrator -- --help
  # Likewise for the other subcommands
  dotnet run --project src/Orchestrator -- matchday --help
  ```
