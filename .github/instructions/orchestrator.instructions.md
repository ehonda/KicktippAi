---
applyTo: 'src/Orchestrator/**'
---
When making changes to the `src/Orchestrator` project or our core logic, follow these guidelines:

* Test using the orchestrator project, example:
  
  ```powershell
  dotnet run --project src/Orchestrator/Orchestrator.csproj -- matchday o4-mini --community ehonda-test-buli
  ```

* Use `o4-mini` for testing to save costs, and always use `--estimated-costs o1` because that is our production model and we want to see cost estimates for it.
* Use `ehonda-test-buli` as the community for testing
* There are different matchday options available that make sense in different scenarios like e.g. `--dry-run`
* Use the following commands to get more information about the available commands and options:
  
  ```powershell
  dotnet run --project src/Orchestrator/Orchestrator.csproj -- help
  dotnet run --project src/Orchestrator/Orchestrator.csproj -- matchday --help
  ```
