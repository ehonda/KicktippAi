# OpenAI Structured Output Schema Instructions

These instructions define how to author JSON Schemas for OpenAI "structured outputs" so the model reliably returns machine‑parseable data. Provide this file as system / context input whenever the model is asked to DESIGN or USE a structured output schema.

## Core Contract
We give OpenAI a (subset) JSON Schema and (optionally) `strict: true`. The model must emit ONE JSON object that validates against the schema. No prose, code fences, or extra keys.

## Authoring Rules (UPDATED)
1. Root Must Be Object:
  * Top-level schema: `{ "type": "object", "properties": { ... }, "required": [ ... ], "additionalProperties": false }`.
2. All Defined Fields MUST Be Required:
  * Every property appearing under any `properties` MUST also appear in that object's `required` array.
  * There are NO optional (omittable) properties. Apparent optionality is represented via nullable union types.
3. Represent Optional / Unknown Values via Nullability:
  * Use a union type form: `"type": ["string", "null"]` (or number/boolean + null) to indicate a field may be unknown. Field STILL remains in `required` and must appear with either a concrete value or `null`.
4. `additionalProperties`: false ALWAYS:
  * Every object level MUST include `"additionalProperties": false`. The only exception is an intentional key/value map; this pattern is currently disallowed for simplicity—prefer explicit properties.
5. Supported Keywords ONLY (subset):
  * Allowed: `type`, `properties`, `required`, `items`, `additionalProperties`, `enum`, `description`, `minItems`, `maxItems`, `minLength`, `maxLength`, `minimum`, `maximum`, `format`, `default` (advisory only), `nullable` (only if explicitly supported; prefer union `type: [.., "null"]`).
  * Disallowed: `$ref`, `oneOf`, `anyOf`, `allOf`, `not`, `patternProperties`, `dependencies`, `if/then/else`, `const`, recursion, circular refs.
6. Enumerations:
  * Closed sets MUST use `enum`. Keep literals lowercase snake_case unless domain demands different. Explain semantics succinctly in `description`.
7. Strings:
  * Constrain with `enum`, `minLength`, `maxLength` where possible. Cap free text (e.g. summaries) with a `maxLength` (e.g. 280) to reduce verbosity.
8. Numbers:
  * Provide `minimum` / `maximum`. Use `integer` when decimals are not required. Percentages: prefer 0–100 integers.
9. Arrays:
  * Always specify `items`. Constrain with `minItems` / `maxItems` if knowable. State uniqueness expectations in `description` (no keyword used here).
10. Nested Objects:
  * Apply the same rules recursively (all nested properties required, include `additionalProperties: false`).
11. Descriptions:
  * Each property SHOULD have a one‑sentence description clarifying semantics, units, constraints, or invariants.
12. Date / Time / IDs:
  * Timestamps: `type: "string", format: "date-time"`, description should say `ISO 8601 UTC`.
  * Date only: `format: "date"`.
13. Booleans:
  * Use `type: "boolean"` with a yes/no phrasing in description.
14. Stable Ordering:
  * Alphabetize properties unless a strong logical grouping supersedes; consistency aids review.
15. No Extraneous Narrative in Output:
  * Model returns ONLY raw JSON object (no code fences, no commentary).
16. Strict Mode:
  * Include / ensure `"strict": true` at call site (avoid duplication if our integration injects it already).
17. Keep Schemas Minimal:
  * Do not add speculative fields. Remove unused ones promptly.
18. Versioning:
  * (REMOVED) We do not include `schema_version` unless a future migration explicitly requires it.

## Quality Patterns
GOOD property example (nullable string field):
```json
"stadium_name": {
  "type": ["string", "null"],
  "minLength": 1,
  "maxLength": 80,
  "description": "Official stadium name or null if unknown"
}
```

Numeric example:
```json
"confidence": {
  "type": "number",
  "minimum": 0,
  "maximum": 1,
  "description": "Model confidence (0.0–1.0) in the primary prediction"
}
```

Array + nested objects example:
```json
"matches": {
  "type": "array",
  "minItems": 1,
  "items": {
   "type": "object",
   "properties": {
    "home_team": { "type": "string", "description": "Exact team name as in source data" },
    "away_team": { "type": "string", "description": "Exact team name as in source data" },
    "predicted_score": {
      "type": "object",
      "properties": {
       "home": { "type": "integer", "minimum": 0, "description": "Home goals predicted" },
       "away": { "type": "integer", "minimum": 0, "description": "Away goals predicted" }
      },
      "required": ["home", "away"],
      "additionalProperties": false
    }
   },
   "required": ["away_team", "home_team", "predicted_score"],
   "additionalProperties": false
  },
  "description": "Per-match predictions in fixture order"
}
```

Nullable numeric example:
```json
"attendance": {
  "type": ["integer", "null"],
  "minimum": 0,
  "description": "Expected attendance or null if unknown"
}
```

## Do's
* Do make every declared property required (nullability handles absence of knowledge).
* Do use null unions ONLY when the value may be unknown or inapplicable.
* Do enforce `additionalProperties: false` everywhere.
* Do constrain value spaces tightly (enums, ranges, lengths) to reduce hallucinations.

## Minimal Template (Copy & Adapt)
```json
{
  "name": "<snake_case_schema_name>",
  "schema": {
   "type": "object",
   "properties": {
    // All properties go here; every property listed MUST appear below in required
    "example_field": { "type": "string", "description": "Example field (replace)" }
   },
   "required": ["example_field"],
   "additionalProperties": false
  },
  "strict": true
}
```
(If our integration provides `name` / `strict`, only emit the inner `schema` object.)

## Acceptance Checklist (Mentally Verify Before Use)
- Root object defined (`type: object`).
- Every object: `additionalProperties: false`.
- Every declared property included in `required`.
- Nullable fields use union `type: [<base>, "null"]` (only when truly optional in meaning).
- Each property has `type` + helpful `description` (and constraints where sensible).
- Enums / numeric and length bounds defined where possible.
- No unsupported keywords present.
- All value ranges & length caps validated logically.

Following these rules produces stable, deterministic structured outputs suitable for downstream automation.
