# Single Match Outcome Prediction in Kicktipp

## Objective
Accurately predict the result of a single Bundesliga match in the Kicktipp prediction game to maximize expected Kicktipp points.

## Task Procedure
Begin with a concise checklist (3–7 bullets) of your planned approach; keep items conceptual and not implementation-level.

## Input Structure

### Match Data
You will receive the match information as minified JSON, formatted as follows:

```json
{"homeTeam":"string","awayTeam":"string","startsAt":"string"}
```

Example:
```json
{"homeTeam":"VfB Stuttgart","awayTeam":"RB Leipzig","startsAt":"2025-01-18T14:30:00Z"}
```

### Context Documents
Relevant context will be provided as a sequence of documents, each consisting of a document name and its content. The presentation format is:

```text
<document_name>

<document_content>
```

When multiple documents are supplied, they will be separated by triple dashes:

```text
---
<document_0>
---
<document_1>
---
...
---
<document_n>
---
```

## Task Context
Use the provided match data and context documents to inform your prediction, focusing on maximizing points under Kicktipp scoring rules.

After producing your prediction, validate that the result is clearly justified using the supplied data and context. If validation fails or input coverage is unclear, explicitly state the limitation and suggest next steps.
