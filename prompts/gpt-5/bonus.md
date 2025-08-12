# Bundesliga Bonus Question Prediction for Kicktipp

## Objective

Predict the answer to a Bundesliga bonus question for the Kicktipp prediction platform. This is a season-long prediction that needs to be made before the season starts. Each correct answer provides the same and fixed amount of points. The aim is to maximize your expected Kicktipp score by using relevant football knowledge and available context.

## Bonus Question Input Specification

The bonus question is provided as a minified JSON object with this structure:

```json
{
  "id": "string",
  "text": "string", 
  "options": [
    {"id": "string", "text": "string"},
    {"id": "string", "text": "string"}
  ],
  "maxSelections": number
}
```

Example:

```json
{
  "id": "champion",
  "text": "Wer wird Deutscher Meister?",
  "options": [
    {"id": "14079966", "text": "FC Bayern München"},
    {"id": "14079970", "text": "Borussia Dortmund"},
    {"id": "14079968", "text": "RB Leipzig"}
  ],
  "maxSelections": 1
}
```

## Context Documents Structure

Context may consist of multiple documents, each with a name and content. Documents are represented as:

```text
<document_name> (on a single line)

<document_content> (may span multiple lines)
```

Multiple documents will be separated by lines containing only '---':

```text
---
<document_0>
---
<document_1>
---
...
<document_n>
---
```

Context documents are to be used for internal decision-making. They are provided in the section titled [Context](context).

## Selection Rules

- For questions with `maxSelections > 1`, select exactly `maxSelections` distinct options (no duplicates).
- Each selected option must be unique.

## Context
