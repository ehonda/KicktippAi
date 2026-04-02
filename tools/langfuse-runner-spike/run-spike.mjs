import { readFile } from "node:fs/promises";
import process from "node:process";

import { LangfuseClient } from "@langfuse/client";
import { LangfuseSpanProcessor } from "@langfuse/otel";
import { startObservation } from "@langfuse/tracing";
import { NodeSDK } from "@opentelemetry/sdk-node";
import OpenAI from "openai";
import { zodResponseFormat } from "openai/helpers/zod";
import { z } from "zod";

const BASE_PREDICTION_SCHEMA = z.object({
  home: z.number().int().nonnegative(),
  away: z.number().int().nonnegative()
});

const JUSTIFICATION_CONTEXT_SOURCE_SCHEMA = z.object({
  documentName: z.string(),
  details: z.string()
});

const JUSTIFICATION_SCHEMA = z.object({
  keyReasoning: z.string(),
  contextSources: z.object({
    mostValuable: z.array(JUSTIFICATION_CONTEXT_SOURCE_SCHEMA),
    leastValuable: z.array(JUSTIFICATION_CONTEXT_SOURCE_SCHEMA)
  }),
  uncertainties: z.array(z.string())
});

const JUSTIFIED_PREDICTION_SCHEMA = BASE_PREDICTION_SCHEMA.extend({
  justification: JUSTIFICATION_SCHEMA
});

function readArguments() {
  const args = process.argv.slice(2);
  const inputPaths = [];
  let model;
  let runName;
  let runDescription;
  let repetitions = 1;
  let batchSize = 8;
  let datasetName;
  let replaceRun = false;

  for (let index = 0; index < args.length; index += 1) {
    const current = args[index];

    if (current === "--input") {
      inputPaths.push(args[index + 1]);
      index += 1;
      continue;
    }

    if (current === "--model") {
      model = args[index + 1];
      index += 1;
      continue;
    }

    if (current === "--run-name") {
      runName = args[index + 1];
      index += 1;
      continue;
    }

    if (current === "--run-description") {
      runDescription = args[index + 1];
      index += 1;
      continue;
    }

    if (current === "--repetitions") {
      repetitions = parsePositiveInteger(args[index + 1], "--repetitions");
      index += 1;
      continue;
    }

    if (current === "--batch-size") {
      batchSize = parsePositiveInteger(args[index + 1], "--batch-size");
      index += 1;
      continue;
    }

    if (current === "--dataset-name") {
      datasetName = args[index + 1];
      index += 1;
      continue;
    }

    if (current === "--replace-run") {
      replaceRun = true;
      continue;
    }
  }

  if (inputPaths.length === 0) {
    throw new Error("At least one --input argument is required.");
  }

  if (!model) {
    throw new Error("Missing required --model argument.");
  }

  return {
    inputPaths,
    model,
    runName,
    runDescription,
    repetitions,
    batchSize,
    datasetName,
    replaceRun
  };
}

function parsePositiveInteger(value, optionName) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed < 1) {
    throw new Error(`${optionName} must be a positive integer.`);
  }

  return parsed;
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function slugify(value) {
  return value
    .normalize("NFD")
    .replace(/[^\p{Letter}\p{Number}]+/gu, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
}

function stableJson(value) {
  if (Array.isArray(value)) {
    return value.map(stableJson);
  }

  if (value && typeof value === "object") {
    return Object.keys(value)
      .sort()
      .reduce((result, key) => {
        result[key] = stableJson(value[key]);
        return result;
      }, {});
  }

  return value;
}

function validateExportedItem(exportedItem, inputPath) {
  assert(exportedItem && typeof exportedItem === "object", `Export artifact '${inputPath}' must be an object.`);
  assert(exportedItem.datasetItem?.id, `Export artifact '${inputPath}' is missing datasetItem.id.`);
  assert(exportedItem.datasetItem?.input, `Export artifact '${inputPath}' is missing datasetItem.input.`);
  assert(exportedItem.datasetItem?.expectedOutput, `Export artifact '${inputPath}' is missing datasetItem.expectedOutput.`);
  assert(exportedItem.datasetItem?.metadata, `Export artifact '${inputPath}' is missing datasetItem.metadata.`);
  assert(exportedItem.runnerPayload?.systemPrompt, `Export artifact '${inputPath}' is missing runnerPayload.systemPrompt.`);
  assert(exportedItem.runnerPayload?.matchJson, `Export artifact '${inputPath}' is missing runnerPayload.matchJson.`);
}

async function loadExportedItems(inputPaths) {
  const exportedItems = [];

  for (const inputPath of inputPaths) {
    const raw = await readFile(inputPath, "utf8");
    const exportedItem = JSON.parse(raw);
    validateExportedItem(exportedItem, inputPath);
    exportedItems.push({ inputPath, exportedItem });
  }

  return exportedItems;
}

function deriveDatasetName(datasetNameOverride, exportedItems) {
  if (datasetNameOverride) {
    return datasetNameOverride;
  }

  const communityContext = exportedItems[0].exportedItem.datasetItem.metadata.communityContext;
  return `match-predictions/bundesliga-2025-26/${communityContext}`;
}

function deriveRunName(explicitRunName, exportedItems, model, repetitions) {
  if (explicitRunName) {
    return explicitRunName;
  }

  const firstMetadata = exportedItems[0].exportedItem.datasetItem.metadata;
  const matchSegment = exportedItems.length === 1
    ? `${slugify(firstMetadata.homeTeam)}-vs-${slugify(firstMetadata.awayTeam)}`
    : `sample-${exportedItems.length}`;

  const timestampSegment = slugify((firstMetadata.predictionCreatedAt ?? "").replace(/[:+]/g, "-"));
  return [
    "task-5",
    slugify(firstMetadata.communityContext),
    slugify(model),
    matchSegment,
    `r${repetitions}`,
    timestampSegment || "prompt-time"
  ].join("__");
}

function deriveRepetitionRunName(runFamilyName, repetition, totalRepetitions) {
  if (totalRepetitions === 1) {
    return runFamilyName;
  }

  return `${runFamilyName}__rep${String(repetition).padStart(2, "0")}`;
}

function createRunMetadata(exportedItems, model, repetitions, batchSize) {
  const firstMetadata = exportedItems[0].exportedItem.datasetItem.metadata;

  return {
    runner: "task-5-first-experiment",
    model,
    communityContext: firstMetadata.communityContext,
    competition: firstMetadata.competition,
    includeJustification: Boolean(firstMetadata.includeJustification),
    promptTimestamp: firstMetadata.predictionCreatedAt,
    sampleSize: exportedItems.length,
    repetitions,
    batchSize,
    executionStrategy: {
      warmupSerialPass: 1,
      parallelBatchSize: batchSize
    }
  };
}

function createMessages(exportedItem) {
  return [
    { role: "system", content: exportedItem.runnerPayload.systemPrompt },
    { role: "user", content: exportedItem.runnerPayload.matchJson }
  ];
}

function selectPredictionSchema(includeJustification) {
  return includeJustification ? JUSTIFIED_PREDICTION_SCHEMA : BASE_PREDICTION_SCHEMA;
}

function mapPrediction(parsedPrediction) {
  return {
    homeGoals: parsedPrediction.home,
    awayGoals: parsedPrediction.away,
    justification: parsedPrediction.justification ?? null
  };
}

function normalizeUsage(usage) {
  return {
    promptTokens: usage?.prompt_tokens ?? 0,
    completionTokens: usage?.completion_tokens ?? 0,
    totalTokens: usage?.total_tokens ?? 0
  };
}

function calculateScores(prediction, expectedOutput) {
  const predictedDifference = prediction.homeGoals - prediction.awayGoals;
  const expectedDifference = expectedOutput.homeGoals - expectedOutput.awayGoals;
  const predictedTendency = Math.sign(predictedDifference);
  const expectedTendency = Math.sign(expectedDifference);

  let kicktippPoints = 0;
  const exactHit = prediction.homeGoals === expectedOutput.homeGoals
    && prediction.awayGoals === expectedOutput.awayGoals;
  const outcomeCorrect = predictedTendency === expectedTendency;

  if (exactHit) {
    kicktippPoints = 4;
  }
  else if (outcomeCorrect && predictedDifference === expectedDifference && expectedTendency !== 0) {
    kicktippPoints = 3;
  }
  else if (outcomeCorrect) {
    kicktippPoints = 2;
  }

  return {
    kicktipp_points: kicktippPoints
  };
}

function summarizeScores(scoreEntries) {
  const total = scoreEntries.length;
  const sums = scoreEntries.reduce((result, entry) => {
    for (const [key, value] of Object.entries(entry)) {
      result[key] = (result[key] ?? 0) + value;
    }

    return result;
  }, {});

  return {
    total_kicktipp_points: sums.kicktipp_points ?? 0,
    avg_kicktipp_points: total === 0 ? 0 : (sums.kicktipp_points ?? 0) / total
  };
}

async function deleteExistingRunIfRequested(langfuse, datasetName, runName, replaceRun) {
  if (!replaceRun) {
    return false;
  }

  try {
    await langfuse.api.datasets.deleteRun(datasetName, runName);
    return true;
  }
  catch (error) {
    if (isNotFoundError(error)) {
      return false;
    }

    throw error;
  }
}

async function deleteExistingRunsIfRequested(langfuse, datasetName, runFamilyName, repetitions, replaceRun) {
  if (!replaceRun) {
    return [];
  }

  const deletedRunNames = [];

  for (let repetition = 1; repetition <= repetitions; repetition += 1) {
    const runName = deriveRepetitionRunName(runFamilyName, repetition, repetitions);
    const deleted = await deleteExistingRunIfRequested(langfuse, datasetName, runName, true);
    if (deleted) {
      deletedRunNames.push(runName);
    }
  }

  return deletedRunNames;
}

function isNotFoundError(error) {
  if (!(error instanceof Error)) {
    return false;
  }

  const message = error.message.toLowerCase();
  return message.includes("404") || message.includes("not found");
}

function reportProgress(message) {
  console.error(`[progress] ${message}`);
}

async function runPrediction({ openai, model, exportedItem, runName, repetition, totalRepetitions }) {
  const metadata = exportedItem.datasetItem.metadata;
  const includeJustification = Boolean(metadata.includeJustification);
  const messages = createMessages(exportedItem);
  const schema = selectPredictionSchema(includeJustification);
  const observation = startObservation(
    "predict-match-experiment",
    {
      model,
      input: messages,
      modelParameters: {
        responseFormat: includeJustification ? "match_prediction_with_justification" : "match_prediction"
      },
      metadata: {
        runName,
        repetition,
        totalRepetitions,
        datasetItemId: exportedItem.datasetItem.id,
        communityContext: metadata.communityContext,
        matchday: metadata.matchday,
        homeTeam: metadata.homeTeam,
        awayTeam: metadata.awayTeam,
        promptTimestamp: metadata.predictionCreatedAt
      }
    },
    { asType: "generation" }
  );

  try {
    const completion = await openai.chat.completions.parse({
      model,
      messages,
      response_format: zodResponseFormat(
        schema,
        includeJustification ? "match_prediction_with_justification" : "match_prediction"
      )
    });

    const choice = completion.choices[0]?.message;
    if (!choice?.parsed) {
      const refusal = choice?.refusal ? ` Refusal: ${choice.refusal}` : "";
      throw new Error(`OpenAI did not return parsed structured output.${refusal}`);
    }

    const prediction = mapPrediction(choice.parsed);
    const usage = normalizeUsage(completion.usage);

    observation.update({
      output: prediction,
      usageDetails: usage,
      metadata: {
        ...metadata,
        repetition,
        totalRepetitions
      }
    });

    return {
      prediction,
      usage,
      traceId: observation.traceId,
      observationId: observation.id
    };
  }
  catch (error) {
    observation.update({
      level: "ERROR",
      statusMessage: error instanceof Error ? error.message : String(error),
      output: {
        error: error instanceof Error ? error.message : String(error)
      }
    });
    throw error;
  }
  finally {
    observation.end();
  }
}

async function postItemScores(langfuse, datasetRunId, executionResult, exportedItem, repetition) {
  const scoreValues = calculateScores(executionResult.prediction, exportedItem.datasetItem.expectedOutput);

  for (const [name, value] of Object.entries(scoreValues)) {
    await langfuse.api.legacy.scoreV1.create({
      traceId: executionResult.traceId,
      observationId: executionResult.observationId,
      name,
      value,
      comment: `Repetition ${repetition} for ${exportedItem.datasetItem.metadata.homeTeam} vs ${exportedItem.datasetItem.metadata.awayTeam}`,
      metadata: stableJson({
        datasetRunId,
        repetition,
        datasetItemId: exportedItem.datasetItem.id,
        prediction: executionResult.prediction,
        expectedOutput: exportedItem.datasetItem.expectedOutput
      })
    });
  }

  return scoreValues;
}

async function postRunScores(langfuse, datasetRunId, scoreEntries, runMetadata) {
  const aggregateScores = summarizeScores(scoreEntries);

  for (const [name, value] of Object.entries(aggregateScores)) {
    await langfuse.api.legacy.scoreV1.create({
      datasetRunId,
      name,
      value,
      comment: `Aggregate score for ${runMetadata.sampleSize} item(s) x ${runMetadata.repetitions} repetition(s)`,
      metadata: stableJson(runMetadata)
    });
  }

  return aggregateScores;
}

function buildExecutionPlan(exportedItems, repetitions) {
  const warmupExecutions = exportedItems.map(({ inputPath, exportedItem }) => ({
    inputPath,
    exportedItem,
    repetition: 1
  }));

  const batchedExecutions = [];
  for (let repetition = 2; repetition <= repetitions; repetition += 1) {
    for (const { inputPath, exportedItem } of exportedItems) {
      batchedExecutions.push({
        inputPath,
        exportedItem,
        repetition
      });
    }
  }

  return { warmupExecutions, batchedExecutions };
}

function createBatchChunks(items, batchSize) {
  const chunks = [];
  for (let index = 0; index < items.length; index += batchSize) {
    chunks.push(items.slice(index, index + batchSize));
  }

  return chunks;
}

function sleep(milliseconds) {
  return new Promise((resolve) => {
    setTimeout(resolve, milliseconds);
  });
}

async function waitForDatasetRunItems(langfuse, datasetId, runName, expectedCount) {
  for (let attempt = 0; attempt < 6; attempt += 1) {
    const datasetRunItems = await langfuse.api.datasetRunItems.list({
      datasetId,
      runName
    });

    if (datasetRunItems.data.length >= expectedCount) {
      return datasetRunItems;
    }

    await sleep(2000);
  }

  return await langfuse.api.datasetRunItems.list({
    datasetId,
    runName
  });
}

async function main() {
  const argumentsResult = readArguments();
  const exportedItems = await loadExportedItems(argumentsResult.inputPaths);
  const datasetName = deriveDatasetName(argumentsResult.datasetName, exportedItems);
  const runFamilyName = deriveRunName(
    argumentsResult.runName,
    exportedItems,
    argumentsResult.model,
    argumentsResult.repetitions
  );
  const runMetadata = createRunMetadata(
    exportedItems,
    argumentsResult.model,
    argumentsResult.repetitions,
    argumentsResult.batchSize
  );

  const sdk = new NodeSDK({
    spanProcessors: [new LangfuseSpanProcessor({
      additionalHeaders: {
        "x-langfuse-ingestion-version": "4"
      }
    })]
  });

  sdk.start();

  const langfuse = new LangfuseClient();
  const openai = new OpenAI({ apiKey: process.env.OPENAI_API_KEY });

  try {
    const deletedExistingRuns = await deleteExistingRunsIfRequested(
      langfuse,
      datasetName,
      runFamilyName,
      argumentsResult.repetitions,
      argumentsResult.replaceRun
    );

    const { warmupExecutions, batchedExecutions } = buildExecutionPlan(
      exportedItems,
      argumentsResult.repetitions
    );

    const scoreEntries = [];
    const executionSummaries = [];
    const runsByName = new Map();

    function getRunRecord(repetition) {
      const runName = deriveRepetitionRunName(runFamilyName, repetition, argumentsResult.repetitions);
      let runRecord = runsByName.get(runName);

      if (!runRecord) {
        runRecord = {
          repetition,
          runName,
          datasetRunId: null,
          scoreEntries: [],
          executionSummaries: []
        };

        runsByName.set(runName, runRecord);
      }

      return runRecord;
    }

    const totalExecutionCount = warmupExecutions.length + batchedExecutions.length;
    let completedExecutionCount = 0;

    reportProgress(
      `Starting experiment family '${runFamilyName}' for model '${argumentsResult.model}' with ${argumentsResult.repetitions} repetition(s), sample size ${exportedItems.length}, batch size ${argumentsResult.batchSize}.`
    );

    // The first repetition for every match stays serial on purpose.
    // These first calls populate provider-side prompt caches for an identical prompt shape,
    // so batching only the later repetitions preserves the user-requested 1 + 2x8 pattern
    // for the first experiment while still keeping the total runtime reasonable.
    for (let warmupIndex = 0; warmupIndex < warmupExecutions.length; warmupIndex += 1) {
      const execution = warmupExecutions[warmupIndex];
      const runRecord = getRunRecord(execution.repetition);

      reportProgress(
        `Warm-up ${warmupIndex + 1}/${warmupExecutions.length}: repetition ${execution.repetition}/${argumentsResult.repetitions}, item ${completedExecutionCount + 1}/${totalExecutionCount}, run '${runRecord.runName}'.`
      );

      const result = await runPrediction({
        openai,
        model: argumentsResult.model,
        exportedItem: execution.exportedItem,
        runName: runRecord.runName,
        repetition: execution.repetition,
        totalRepetitions: argumentsResult.repetitions
      });

      const datasetRunItem = await langfuse.api.datasetRunItems.create({
        runName: runRecord.runName,
        runDescription: argumentsResult.runDescription,
        metadata: stableJson(runMetadata),
        datasetItemId: execution.exportedItem.datasetItem.id,
        observationId: result.observationId,
        traceId: result.traceId
      });

      runRecord.datasetRunId ??= datasetRunItem.datasetRunId;
      const scoreValues = await postItemScores(
        langfuse,
        datasetRunItem.datasetRunId,
        result,
        execution.exportedItem,
        execution.repetition
      );

      scoreEntries.push(scoreValues);
      const executionSummary = {
        inputPath: execution.inputPath,
        datasetItemId: execution.exportedItem.datasetItem.id,
        repetition: execution.repetition,
        runName: runRecord.runName,
        traceId: result.traceId,
        observationId: result.observationId,
        prediction: result.prediction,
        usage: result.usage,
        scores: scoreValues
      };

      runRecord.scoreEntries.push(scoreValues);
      runRecord.executionSummaries.push(executionSummary);
      executionSummaries.push(executionSummary);
      completedExecutionCount += 1;
    }

    const batches = createBatchChunks(batchedExecutions, argumentsResult.batchSize);
    for (let batchIndex = 0; batchIndex < batches.length; batchIndex += 1) {
      const batch = batches[batchIndex];
      const batchStart = completedExecutionCount + 1;
      const batchEnd = completedExecutionCount + batch.length;

      reportProgress(
        `Batch ${batchIndex + 1}/${batches.length}: executions ${batchStart}-${batchEnd} of ${totalExecutionCount}.`
      );

      const batchResults = await Promise.all(batch.map(async (execution) => {
        const runRecord = getRunRecord(execution.repetition);
        const result = await runPrediction({
          openai,
          model: argumentsResult.model,
          exportedItem: execution.exportedItem,
          runName: runRecord.runName,
          repetition: execution.repetition,
          totalRepetitions: argumentsResult.repetitions
        });

        const datasetRunItem = await langfuse.api.datasetRunItems.create({
          runName: runRecord.runName,
          runDescription: argumentsResult.runDescription,
          metadata: stableJson(runMetadata),
          datasetItemId: execution.exportedItem.datasetItem.id,
          observationId: result.observationId,
          traceId: result.traceId
        });

        const scoreValues = await postItemScores(
          langfuse,
          datasetRunItem.datasetRunId,
          result,
          execution.exportedItem,
          execution.repetition
        );

        return {
          inputPath: execution.inputPath,
          datasetItemId: execution.exportedItem.datasetItem.id,
          repetition: execution.repetition,
          runName: runRecord.runName,
          traceId: result.traceId,
          observationId: result.observationId,
          prediction: result.prediction,
          usage: result.usage,
          scores: scoreValues,
          datasetRunId: datasetRunItem.datasetRunId
        };
      }));

      for (const batchResult of batchResults) {
        const runRecord = runsByName.get(batchResult.runName);
        runRecord.datasetRunId ??= batchResult.datasetRunId;
        scoreEntries.push(batchResult.scores);
        runRecord.scoreEntries.push(batchResult.scores);
        runRecord.executionSummaries.push(batchResult);
        executionSummaries.push(batchResult);
      }

      completedExecutionCount += batchResults.length;
      reportProgress(
        `Completed batch ${batchIndex + 1}/${batches.length}: ${completedExecutionCount}/${totalExecutionCount} executions finished.`
      );
    }

    if (runsByName.size === 0) {
      throw new Error("No dataset runs were created.");
    }

    const datasetRuns = [];

    for (const runRecord of [...runsByName.values()].sort((left, right) => left.repetition - right.repetition)) {
      if (!runRecord.datasetRunId) {
        throw new Error(`Dataset run '${runRecord.runName}' did not return a datasetRunId.`);
      }

      const repetitionRunMetadata = {
        ...runMetadata,
        runFamilyName,
        repetition: runRecord.repetition
      };
      const repetitionAggregateScores = await postRunScores(
        langfuse,
        runRecord.datasetRunId,
        runRecord.scoreEntries,
        repetitionRunMetadata
      );
      const datasetRun = await langfuse.api.datasets.getRun(datasetName, runRecord.runName);
      const datasetRunItems = await waitForDatasetRunItems(
        langfuse,
        datasetRun.datasetId,
        runRecord.runName,
        exportedItems.length
      );

      datasetRuns.push({
        repetition: runRecord.repetition,
        runName: runRecord.runName,
        datasetRunId: runRecord.datasetRunId,
        runItemCount: datasetRunItems.data.length,
        aggregateScores: repetitionAggregateScores,
        firstExecution: runRecord.executionSummaries[0] ?? null,
        lastExecution: runRecord.executionSummaries.at(-1) ?? null
      });
    }

    const aggregateScores = summarizeScores(scoreEntries);

    console.log(JSON.stringify({
      datasetName,
      runFamilyName,
      model: argumentsResult.model,
      deletedExistingRuns,
      sampleSize: exportedItems.length,
      repetitions: argumentsResult.repetitions,
      batchSize: argumentsResult.batchSize,
      executionCount: executionSummaries.length,
      warmupCount: warmupExecutions.length,
      batchedCount: batchedExecutions.length,
      runCount: datasetRuns.length,
      aggregateScores,
      datasetRuns,
      firstExecution: executionSummaries[0] ?? null,
      lastExecution: executionSummaries.at(-1) ?? null
    }, null, 2));
  }
  finally {
    await langfuse.flush();
    await sdk.shutdown();
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  process.exitCode = 1;
});
