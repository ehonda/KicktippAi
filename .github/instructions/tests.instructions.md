---
applyTo: '**/tests/**'
---
# Instructions when writing tests

ALWAYS follow these when writing `tests`

## Do the following

* Use the [TUnit Usage Guide](../../tests/tunit_usage_guide/overview.md) as your primary reference for writing tests with TUnit. It is designed to be progressively discoverable at the right level of detail. Start with the overview and drill down into specific topics as needed.
* Ensure your tests are organized according to the [Project Style Guide](../../tests/tunit_usage_guide/general/project_style_guide.md) to maintain consistency across the codebase.
* If information on some topic is missing, use #context7/* to try and find more information about it. When successful, contribute it to the TUnit Usage Guide following the existing structure.

## DO NOT do the following

* Use `--logger "console;verbosity=normal"` with `dotnet test`, as it breaks shell command auto approval
