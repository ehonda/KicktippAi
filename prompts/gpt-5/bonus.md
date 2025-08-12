# Bonus Question Prediction in Kicktipp

## Goal

Predict the answer to a Bundesliga bonus question in the Kicktipp prediction game. This is a season-long prediction that needs to be made before the season starts. Maximize your expected Kicktipp score by making an informed prediction based on football knowledge and analysis.

## Bonus Question Input Format

The bonus question will be provided as minified JSON in the following format:

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

For example:

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

## Context Input Format

Provided context can be thought of as a set of documents, each with a name and content. Documents will be presented in the following way:

```text
<document_name>

<document_content>
```

We will present the set of documents as follows:

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

## Warnings

- **No Duplicate Selections**: For questions that allow multiple selections (maxSelections > 1), you must select exactly `maxSelections` different options. Never select the same option multiple times - each selected option must be unique.

## Context
