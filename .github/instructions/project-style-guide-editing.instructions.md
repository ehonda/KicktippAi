---
name: Project Style Guide Editing Instructions
description: These instructions provide guidelines to follow when editing the project style guide.
applyTo: '**/project_style_guide.md'
---
# Project Style Guide Editing Instructions

ENSURE COMPLIANCE with these guidelines when editing the project style guide.

## Writing Style

- **Be concise**: Write clear and to-the-point instructions, without leaving out necessary details.
- **Clear structure**: Keep a consistent overall document structure. When adding new sections, follow the existing format, and look for the best place to insert them.

## Code Examples

- When adding or updating code examples, check whether they follow the established conventions in the style guide.
  - If not, modify them to align with the guide's standards.
  - **Exception**: If the example is intentionally demonstrating a "bad" practice, ensure it is clearly labeled as such. It is then necessary and fine that it violates the style guide.

## Factory Methods and Utilities

- **Do not enumerate methods**: Avoid listing all available factory / utility methods (e.g., `CreateMatch`, `CreatePrediction`) in the style guide. Instead, reference the source file (`src/TestUtilities/CoreTestFactories.cs`) and let users discover available methods dynamically.
- **Focus on patterns**: Document the patterns and conventions for using factory / utility methods, not an exhaustive list of what's available.
