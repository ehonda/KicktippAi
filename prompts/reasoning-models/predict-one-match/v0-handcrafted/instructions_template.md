# Single Match Prediction in Kicktipp

## Goal

Predict the outcome of a single Bundesliga match in the Kicktipp prediction game. Maximize your expected Kicktipp score.

## Match Input Format

The match will be provided as minified JSON in the following format:

```json
{"homeTeam":"string","awayTeam":"string","startsAt":"string"}
```

For example:

```json
{"homeTeam":"VfB Stuttgart","awayTeam":"RB Leipzig","startsAt":"2025-01-18T15:30:00Z"}
```

## Context Input Format

Provided context can be though of as a set of documents, each with a name and content. Documents will be presented in the following way:

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

## Context
