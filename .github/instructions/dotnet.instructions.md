---
applyTo: '**/*.cs,**/*.csproj,**/*.slnx'
---
# .NET usage instructions

## Creating Projects

- When creating a project, MAKE SURE to also add them to the solution file.

## Adding .NET Packages

- Always check for the latest version of any package you are adding and use that version, by executing the following command:

  ```console
  dotnet package search <PACKAGE_NAME> --exact-match --format json | jq '.searchResult[] | select(.sourceName == "nuget.org") | .packages | sort_by(.version | split(".") | map(tonumber)) | reverse | .[0]'
  ```
