import { readFile } from "node:fs/promises";
import process from "node:process";

import { LangfuseClient } from "@langfuse/client";

const INPUT_SCHEMA = {
  type: "object",
  properties: {
    awayTeam: {
      type: "string",
      minLength: 1,
      description: "Exact away team name from the persisted match outcome"
    },
    homeTeam: {
      type: "string",
      minLength: 1,
      description: "Exact home team name from the persisted match outcome"
    },
    startsAt: {
      type: "string",
      minLength: 1,
      description: "Localized match start timestamp string emitted by the .NET exporter"
    }
  },
  required: ["awayTeam", "homeTeam", "startsAt"],
  additionalProperties: false
};

const EXPECTED_OUTPUT_SCHEMA = {
  type: "object",
  properties: {
    awayGoals: {
      type: "integer",
      minimum: 0,
      description: "Actual away goals scored in the completed match"
    },
    homeGoals: {
      type: "integer",
      minimum: 0,
      description: "Actual home goals scored in the completed match"
    }
  },
  required: ["awayGoals", "homeGoals"],
  additionalProperties: false
};

const REQUIRED_METADATA_KEYS = [
  "awayTeam",
  "communityContext",
  "competition",
  "homeTeam",
  "matchday",
  "matchdayLabel",
  "season",
  "tippSpielId"
];

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

function isNonNegativeInteger(value) {
  return Number.isInteger(value) && value >= 0;
}

function validateCanonicalInput(input, itemId) {
  assert(isPlainObject(input), `Dataset item '${itemId}' input must be an object.`);

  const keys = Object.keys(input).sort();
  assert(JSON.stringify(keys) === JSON.stringify(["awayTeam", "homeTeam", "startsAt"]),
    `Dataset item '${itemId}' input must contain exactly awayTeam, homeTeam, and startsAt.`);
  assert(isNonEmptyString(input.homeTeam), `Dataset item '${itemId}' input.homeTeam must be a non-empty string.`);
  assert(isNonEmptyString(input.awayTeam), `Dataset item '${itemId}' input.awayTeam must be a non-empty string.`);
  assert(isNonEmptyString(input.startsAt), `Dataset item '${itemId}' input.startsAt must be a non-empty string.`);
}

function validateCanonicalExpectedOutput(expectedOutput, itemId) {
  assert(isPlainObject(expectedOutput), `Dataset item '${itemId}' expectedOutput must be an object.`);

  const keys = Object.keys(expectedOutput).sort();
  assert(JSON.stringify(keys) === JSON.stringify(["awayGoals", "homeGoals"]),
    `Dataset item '${itemId}' expectedOutput must contain exactly awayGoals and homeGoals.`);
  assert(isNonNegativeInteger(expectedOutput.homeGoals),
    `Dataset item '${itemId}' expectedOutput.homeGoals must be a non-negative integer.`);
  assert(isNonNegativeInteger(expectedOutput.awayGoals),
    `Dataset item '${itemId}' expectedOutput.awayGoals must be a non-negative integer.`);
}

function validateCanonicalMetadata(metadata, itemId) {
  assert(isPlainObject(metadata), `Dataset item '${itemId}' metadata must be an object.`);

  const keys = Object.keys(metadata).sort();
  const expectedKeys = [...REQUIRED_METADATA_KEYS].sort();
  assert(JSON.stringify(keys) === JSON.stringify(expectedKeys),
    `Dataset item '${itemId}' metadata must contain exactly ${expectedKeys.join(", ")}.`);

  assert(isNonEmptyString(metadata.competition), `Dataset item '${itemId}' metadata.competition must be a non-empty string.`);
  assert(isNonEmptyString(metadata.season), `Dataset item '${itemId}' metadata.season must be a non-empty string.`);
  assert(isNonEmptyString(metadata.communityContext), `Dataset item '${itemId}' metadata.communityContext must be a non-empty string.`);
  assert(Number.isInteger(metadata.matchday) && metadata.matchday >= 1,
    `Dataset item '${itemId}' metadata.matchday must be a positive integer.`);
  assert(isNonEmptyString(metadata.matchdayLabel), `Dataset item '${itemId}' metadata.matchdayLabel must be a non-empty string.`);
  assert(isNonEmptyString(metadata.homeTeam), `Dataset item '${itemId}' metadata.homeTeam must be a non-empty string.`);
  assert(isNonEmptyString(metadata.awayTeam), `Dataset item '${itemId}' metadata.awayTeam must be a non-empty string.`);
  assert(isNonEmptyString(metadata.tippSpielId), `Dataset item '${itemId}' metadata.tippSpielId must be a non-empty string.`);
}

function validateArtifact(exportArtifact, datasetName) {
  assert(isPlainObject(exportArtifact), "Artifact root must be an object.");
  assert(isNonEmptyString(datasetName), "Dataset name must be a non-empty string.");
  assert(Array.isArray(exportArtifact.items), "Artifact must contain an 'items' array.");

  const seenItemIds = new Set();
  for (const item of exportArtifact.items) {
    assert(isPlainObject(item), "Each dataset item must be an object.");
    assert(isNonEmptyString(item.id), "Each dataset item must have a non-empty string id.");
    assert(!seenItemIds.has(item.id), `Duplicate dataset item id '${item.id}' found in artifact.`);
    seenItemIds.add(item.id);

    validateCanonicalInput(item.input, item.id);
    validateCanonicalExpectedOutput(item.expectedOutput, item.id);
    validateCanonicalMetadata(item.metadata, item.id);
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

function buildDatasetDefinition(datasetName, items) {
  const metadata = items[0]?.metadata;
  const competition = metadata?.competition ?? "bundesliga-2025-26";
  const season = metadata?.season ?? "2025/2026";
  const communityContext = metadata?.communityContext ?? datasetName.split("/").at(-1);

  return {
    name: datasetName,
    description: `Canonical hosted dataset for ${season} ${communityContext} ${competition} match experiments`,
    metadata: {
      competition,
      communityContext,
      scope: "match-centric",
      season
    },
    inputSchema: INPUT_SCHEMA,
    expectedOutputSchema: EXPECTED_OUTPUT_SCHEMA
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
  const datasetDefinition = buildDatasetDefinition(datasetName, exportArtifact.items);

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
