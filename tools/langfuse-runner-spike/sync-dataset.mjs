import { readFile } from "node:fs/promises";
import process from "node:process";

import { LangfuseClient } from "@langfuse/client";

const INPUT_SCHEMA = {
  type: "object",
  properties: {
    homeTeam: {
      type: "string",
      minLength: 1,
      description: "Exact home team name from the persisted match outcome"
    },
    awayTeam: {
      type: "string",
      minLength: 1,
      description: "Exact away team name from the persisted match outcome"
    },
    startsAt: {
      type: "string",
      minLength: 1,
      description: "Localized match start timestamp string emitted by the .NET exporter"
    }
  },
  required: ["homeTeam", "awayTeam", "startsAt"],
  additionalProperties: false
};

const EXPECTED_OUTPUT_SCHEMA = {
  type: "object",
  properties: {
    homeGoals: {
      type: "integer",
      minimum: 0,
      description: "Actual home goals scored in the completed match"
    },
    awayGoals: {
      type: "integer",
      minimum: 0,
      description: "Actual away goals scored in the completed match"
    }
  },
  required: ["homeGoals", "awayGoals"],
  additionalProperties: false
};

const METADATA_SCHEMA = {
  type: "object",
  properties: {
    competition: {
      type: "string",
      minLength: 1,
      description: "Competition identifier"
    },
    season: {
      type: "string",
      minLength: 1,
      description: "Season label"
    },
    communityContext: {
      type: "string",
      minLength: 1,
      description: "Community context slug"
    },
    matchday: {
      type: "integer",
      minimum: 1,
      description: "Bundesliga matchday number"
    },
    matchdayLabel: {
      type: "string",
      minLength: 1,
      description: "Human-readable matchday label"
    },
    homeTeam: {
      type: "string",
      minLength: 1,
      description: "Exact home team name"
    },
    awayTeam: {
      type: "string",
      minLength: 1,
      description: "Exact away team name"
    },
    tippSpielId: {
      type: "string",
      minLength: 1,
      description: "Kicktipp match identifier"
    }
  },
  required: ["competition", "season", "communityContext", "matchday", "matchdayLabel", "homeTeam", "awayTeam", "tippSpielId"],
  additionalProperties: false
};

function readArguments() {
  const args = process.argv.slice(2);
  let inputPath;
  let datasetName;
  let dryRun = false;

  for (let index = 0; index < args.length; index += 1) {
    if (args[index] === "--input") {
      inputPath = args[index + 1];
      index += 1;
      continue;
    }

    if (args[index] === "--dataset-name") {
      datasetName = args[index + 1];
      index += 1;
      continue;
    }

    if (args[index] === "--dry-run") {
      dryRun = true;
    }
  }

  if (!inputPath) {
    throw new Error("Missing required --input argument.");
  }

  return { inputPath, datasetName, dryRun };
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function isPlainObject(value) {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isNonEmptyString(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function validateValueAgainstSchema(value, schema, label) {
  assert(isPlainObject(schema), `${label} schema must be an object.`);

  if (schema.type === "object") {
    assert(isPlainObject(value), `${label} must be an object.`);

    const properties = schema.properties ?? {};
    const required = schema.required ?? [];
    const actualKeys = Object.keys(value);
    const allowedKeys = Object.keys(properties);

    if (schema.additionalProperties === false) {
      const unexpectedKeys = actualKeys.filter((key) => !allowedKeys.includes(key));
      assert(unexpectedKeys.length === 0,
        `${label} must not contain unexpected keys: ${unexpectedKeys.join(", ")}.`);
    }

    for (const requiredKey of required) {
      assert(Object.hasOwn(value, requiredKey), `${label}.${requiredKey} is required.`);
    }

    for (const key of actualKeys) {
      if (properties[key]) {
        validateValueAgainstSchema(value[key], properties[key], `${label}.${key}`);
      }
    }

    return;
  }

  if (schema.type === "string") {
    assert(isNonEmptyString(value), `${label} must be a non-empty string.`);
    if (typeof schema.minLength === "number") {
      assert(value.length >= schema.minLength, `${label} must be at least ${schema.minLength} characters long.`);
    }

    if (typeof schema.maxLength === "number") {
      assert(value.length <= schema.maxLength, `${label} must be at most ${schema.maxLength} characters long.`);
    }

    return;
  }

  if (schema.type === "integer") {
    assert(Number.isInteger(value), `${label} must be an integer.`);
    if (typeof schema.minimum === "number") {
      assert(value >= schema.minimum, `${label} must be at least ${schema.minimum}.`);
    }

    if (typeof schema.maximum === "number") {
      assert(value <= schema.maximum, `${label} must be at most ${schema.maximum}.`);
    }

    return;
  }

  if (schema.type === "number") {
    assert(typeof value === "number", `${label} must be a number.`);
    if (typeof schema.minimum === "number") {
      assert(value >= schema.minimum, `${label} must be at least ${schema.minimum}.`);
    }

    if (typeof schema.maximum === "number") {
      assert(value <= schema.maximum, `${label} must be at most ${schema.maximum}.`);
    }

    return;
  }

  if (schema.type === "boolean") {
    assert(typeof value === "boolean", `${label} must be a boolean.`);
    return;
  }

  throw new Error(`Unsupported schema type '${schema.type}' for ${label}.`);
}

function validateArtifact(exportArtifact, datasetName) {
  assert(isPlainObject(exportArtifact), "Artifact root must be an object.");
  assert(isNonEmptyString(datasetName), "Dataset name must be a non-empty string.");
  assert(Array.isArray(exportArtifact.items), "Artifact must contain an 'items' array.");

  const inputSchema = exportArtifact.inputSchema ?? INPUT_SCHEMA;
  const expectedOutputSchema = exportArtifact.expectedOutputSchema ?? EXPECTED_OUTPUT_SCHEMA;

  const seenItemIds = new Set();
  for (const item of exportArtifact.items) {
    assert(isPlainObject(item), "Each dataset item must be an object.");
    assert(isNonEmptyString(item.id), "Each dataset item must have a non-empty string id.");
    assert(!seenItemIds.has(item.id), `Duplicate dataset item id '${item.id}' found in artifact.`);
    seenItemIds.add(item.id);

    validateValueAgainstSchema(item.input, inputSchema, `Dataset item '${item.id}' input`);
    validateValueAgainstSchema(item.expectedOutput, expectedOutputSchema, `Dataset item '${item.id}' expectedOutput`);
    validateValueAgainstSchema(item.metadata, METADATA_SCHEMA, `Dataset item '${item.id}' metadata`);
  }
}

function stableJson(value) {
  if (Array.isArray(value)) {
    return value.map(stableJson);
  }

  if (isPlainObject(value)) {
    return Object.keys(value)
      .sort()
      .reduce((result, key) => {
        result[key] = stableJson(value[key]);
        return result;
      }, {});
  }

  return value;
}

function buildDatasetDefinition(exportArtifact, datasetName) {
  const metadata = exportArtifact.items[0]?.metadata;
  const competition = metadata?.competition ?? "bundesliga-2025-26";
  const season = metadata?.season ?? "2025/2026";
  const communityContext = metadata?.communityContext ?? datasetName.split("/").at(-1);

  return {
    name: datasetName,
    description: exportArtifact.datasetDescription
      ?? `Hosted dataset for ${season} ${communityContext} ${competition} match experiments`,
    metadata: exportArtifact.datasetMetadata ?? {
      competition,
      communityContext,
      scope: "match-centric",
      season
    },
    inputSchema: exportArtifact.inputSchema ?? INPUT_SCHEMA,
    expectedOutputSchema: exportArtifact.expectedOutputSchema ?? EXPECTED_OUTPUT_SCHEMA
  };
}

async function upsertDataset(langfuse, datasetDefinition, dryRun) {
  if (!dryRun) {
    await langfuse.api.datasets.create(datasetDefinition);

    return langfuse.api.datasets.get(encodeURIComponent(datasetDefinition.name));
  }

  return {
    id: null,
    name: datasetDefinition.name,
    inputSchema: datasetDefinition.inputSchema,
    expectedOutputSchema: datasetDefinition.expectedOutputSchema
  };
}

function isNotFoundError(error) {
  if (!(error instanceof Error)) {
    return false;
  }

  const message = error.message.toLowerCase();
  return message.includes("404") || message.includes("not found");
}

async function getExistingDatasetItem(langfuse, itemId) {
  try {
    return await langfuse.api.datasetItems.get(itemId);
  }
  catch (error) {
    if (isNotFoundError(error)) {
      return null;
    }

    throw error;
  }
}

function hasSameCanonicalContent(existingItem, item, datasetName) {
  if (!existingItem) {
    return false;
  }

  if (existingItem.datasetName !== datasetName) {
    throw new Error(
      `Dataset item '${item.id}' already exists in dataset '${existingItem.datasetName}', not '${datasetName}'.`);
  }

  return JSON.stringify(stableJson(existingItem.input)) === JSON.stringify(stableJson(item.input))
    && JSON.stringify(stableJson(existingItem.expectedOutput)) === JSON.stringify(stableJson(item.expectedOutput))
    && JSON.stringify(stableJson(existingItem.metadata)) === JSON.stringify(stableJson(item.metadata));
}

async function syncDatasetItem(langfuse, datasetName, item, dryRun) {
  const existingItem = await getExistingDatasetItem(langfuse, item.id);

  if (hasSameCanonicalContent(existingItem, item, datasetName)) {
    return { disposition: "unchanged", itemId: item.id };
  }

  if (!dryRun) {
    await langfuse.api.datasetItems.create({
      id: item.id,
      datasetName,
      input: item.input,
      expectedOutput: item.expectedOutput,
      metadata: item.metadata
    });
  }

  return {
    disposition: existingItem ? "updated" : "created",
    itemId: item.id
  };
}

async function main() {
  const { inputPath, datasetName: datasetNameOverride, dryRun } = readArguments();
  const raw = await readFile(inputPath, "utf8");
  const exportArtifact = JSON.parse(raw);
  const datasetName = datasetNameOverride ?? exportArtifact.datasetName;

  if (!datasetName) {
    throw new Error("Dataset name missing from artifact and no --dataset-name override was provided.");
  }

  validateArtifact(exportArtifact, datasetName);

  const langfuse = dryRun ? null : new LangfuseClient();
  const datasetDefinition = buildDatasetDefinition(exportArtifact, datasetName);

  try {
    const dataset = await upsertDataset(langfuse, datasetDefinition, dryRun);

    let created = 0;
    let updated = 0;
    let unchanged = 0;
    const failures = [];

    for (const item of exportArtifact.items) {
      if (dryRun) {
        created += 1;
        continue;
      }

      try {
        const result = await syncDatasetItem(langfuse, datasetName, item, dryRun);

        if (result.disposition === "created") {
          created += 1;
        }
        else if (result.disposition === "updated") {
          updated += 1;
        }
        else {
          unchanged += 1;
        }
      }
      catch (error) {
        failures.push({
          itemId: item.id,
          message: error instanceof Error ? error.message : String(error)
        });
      }
    }

    if (failures.length > 0) {
      throw new Error(`Dataset sync failed for ${failures.length} item(s): ${JSON.stringify(failures, null, 2)}`);
    }

    console.log(JSON.stringify({
      datasetName,
      datasetId: dataset.id,
      datasetInputSchemaKeys: Object.keys(dataset.inputSchema ?? {}),
      datasetExpectedOutputSchemaKeys: Object.keys(dataset.expectedOutputSchema ?? {}),
      dryRun,
      itemCount: exportArtifact.items.length,
      created,
      updated,
      unchanged,
      firstItemId: exportArtifact.items[0]?.id ?? null,
      lastItemId: exportArtifact.items.at(-1)?.id ?? null
    }, null, 2));
  }
  finally {
    if (langfuse) {
      await langfuse.flush();
    }
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  process.exitCode = 1;
});
