import { readFile } from "node:fs/promises";
import process from "node:process";

import { LangfuseClient } from "@langfuse/client";
import { LangfuseSpanProcessor } from "@langfuse/otel";
import { propagateAttributes, startObservation } from "@langfuse/tracing";
import { NodeSDK } from "@opentelemetry/sdk-node";
import OpenAI from "openai";
import { zodResponseFormat } from "openai/helpers/zod";
import { z } from "zod";

import {
  assert,
  buildSliceExecutionPlan,
  calculateScores,
  createBatchChunks,
  derivePropagatedMetadata,
  deriveTraceTags,
  parsePositiveInteger,
  stableJson,
  summarizeScores
} from "./task5-slice-lib.mjs";

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
  let batchSize = 10;
  let datasetName;
  let runMetadataFile;
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

    if (current === "--run-metadata-file") {
      runMetadataFile = args[index + 1];
      index += 1;
      continue;
    }

    if (current === "--replace-run") {
      replaceRun = true;
    }
  }

  if (inputPaths.length === 0) {
    throw new Error("At least one --input argument is required.");
  }

  if (!model) {
    throw new Error("Missing required --model argument.");
  }

  if (!runName) {
    throw new Error("Missing required --run-name argument.");
  }

  if (!runMetadataFile) {
    throw new Error("Missing required --run-metadata-file argument.");
  }

  return {
    inputPaths,
    model,
    runName,
    runDescription,
    batchSize,
    datasetName,
    runMetadataFile,
    replaceRun
  };
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

async function loadRunMetadata(runMetadataFile) {
  return JSON.parse(await readFile(runMetadataFile, "utf8"));
}

function deriveDatasetName(datasetNameOverride, runMetadata, exportedItems) {
  if (datasetNameOverride) {
    return datasetNameOverride;
  }

  if (runMetadata?.datasetName) {
    return runMetadata.datasetName;
  }

  const communityContext = exportedItems[0].exportedItem.datasetItem.metadata.communityContext;
  return `match-predictions/bundesliga-2025-26/${communityContext}`;
}

function resolveHostedDatasetItemId(exportedItem, runMetadata) {
  const canonicalDatasetItemId = exportedItem.datasetItem.id;
  return runMetadata?.datasetItemIdMap?.[canonicalDatasetItemId] ?? canonicalDatasetItemId;
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

function isNotFoundError(error) {
  if (!(error instanceof Error)) {
    return false;
  }

  const message = error.message.toLowerCase();
  return message.includes("404") || message.includes("not found");
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

function reportProgress(message) {
  console.error(`[progress] ${message}`);
}

async function runPrediction({ openai, model, exportedItem, runName, runMetadata }) {
  const metadata = exportedItem.datasetItem.metadata;
  const includeJustification = Boolean(metadata.includeJustification ?? runMetadata.includeJustification);
  const messages = createMessages(exportedItem);
  const schema = selectPredictionSchema(includeJustification);
  const traceTags = deriveTraceTags(runMetadata);

  return await propagateAttributes(
    {
      traceName: runName,
      tags: traceTags,
      metadata: derivePropagatedMetadata(runMetadata)
    },
    async () => {
      const observation = startObservation(
        "predict-match-experiment",
        {
          model,
          input: messages,
          modelParameters: {
            responseFormat: includeJustification ? "match_prediction_with_justification" : "match_prediction"
          },
          metadata: stableJson({
            ...runMetadata,
            datasetItemId: exportedItem.datasetItem.id,
            homeTeam: metadata.homeTeam,
            awayTeam: metadata.awayTeam,
            matchday: metadata.matchday,
            tippSpielId: metadata.tippSpielId,
            promptTimestamp: metadata.predictionCreatedAt ?? null,
            runName,
            traceTags
          })
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
          metadata: stableJson({
            ...runMetadata,
            ...metadata,
            datasetItemId: exportedItem.datasetItem.id,
            traceTags
          })
        });

        return {
          prediction,
          usage,
          traceId: observation.traceId,
          observationId: observation.id,
          traceTags
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
  );
}

async function postItemScores(langfuse, datasetRunId, hostedDatasetItemId, executionResult, exportedItem) {
  const scoreValues = calculateScores(executionResult.prediction, exportedItem.datasetItem.expectedOutput);

  for (const [name, value] of Object.entries(scoreValues)) {
    await langfuse.api.legacy.scoreV1.create({
      traceId: executionResult.traceId,
      observationId: executionResult.observationId,
      name,
      value,
      comment: `Task 5 slice score for ${exportedItem.datasetItem.metadata.homeTeam} vs ${exportedItem.datasetItem.metadata.awayTeam}`,
      metadata: stableJson({
        datasetRunId,
        datasetItemId: hostedDatasetItemId,
        sourceDatasetItemId: exportedItem.datasetItem.id,
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
      comment: `Aggregate score for ${runMetadata.sampleSize} item(s)`,
      metadata: stableJson(runMetadata)
    });
  }

  return aggregateScores;
}

function sleep(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
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
  const runMetadata = await loadRunMetadata(argumentsResult.runMetadataFile);
  const datasetName = deriveDatasetName(argumentsResult.datasetName, runMetadata, exportedItems);

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
    const deletedExistingRun = await deleteExistingRunIfRequested(
      langfuse,
      datasetName,
      argumentsResult.runName,
      argumentsResult.replaceRun
    );

    const executions = buildSliceExecutionPlan(exportedItems);
    const batches = createBatchChunks(executions, argumentsResult.batchSize);
    const scoreEntries = [];
    const executionSummaries = [];
    let datasetRunId = null;
    let completedExecutionCount = 0;

    reportProgress(
      `Starting Task 5 slice run '${argumentsResult.runName}' for model '${argumentsResult.model}' with sample size ${exportedItems.length} and batch size ${argumentsResult.batchSize}.`
    );

    for (let batchIndex = 0; batchIndex < batches.length; batchIndex += 1) {
      const batch = batches[batchIndex];
      const batchStart = completedExecutionCount + 1;
      const batchEnd = completedExecutionCount + batch.length;

      reportProgress(
        `Batch ${batchIndex + 1}/${batches.length}: executions ${batchStart}-${batchEnd} of ${executions.length}.`
      );

      const batchResults = await Promise.all(batch.map(async (execution) => {
        const hostedDatasetItemId = resolveHostedDatasetItemId(execution.exportedItem, runMetadata);
        const result = await runPrediction({
          openai,
          model: argumentsResult.model,
          exportedItem: execution.exportedItem,
          runName: argumentsResult.runName,
          runMetadata
        });

        const datasetRunItem = await langfuse.api.datasetRunItems.create({
          runName: argumentsResult.runName,
          runDescription: argumentsResult.runDescription,
          metadata: stableJson(runMetadata),
          datasetItemId: hostedDatasetItemId,
          observationId: result.observationId,
          traceId: result.traceId
        });

        const scoreValues = await postItemScores(
          langfuse,
          datasetRunItem.datasetRunId,
          hostedDatasetItemId,
          result,
          execution.exportedItem
        );

        return {
          inputPath: execution.inputPath,
          datasetItemId: hostedDatasetItemId,
          sourceDatasetItemId: execution.exportedItem.datasetItem.id,
          runName: argumentsResult.runName,
          traceId: result.traceId,
          observationId: result.observationId,
          prediction: result.prediction,
          usage: result.usage,
          scores: scoreValues,
          traceTags: result.traceTags,
          datasetRunId: datasetRunItem.datasetRunId
        };
      }));

      for (const batchResult of batchResults) {
        datasetRunId ??= batchResult.datasetRunId;
        scoreEntries.push(batchResult.scores);
        executionSummaries.push(batchResult);
      }

      completedExecutionCount += batchResults.length;
      reportProgress(
        `Completed batch ${batchIndex + 1}/${batches.length}: ${completedExecutionCount}/${executions.length} executions finished.`
      );
    }

    if (!datasetRunId) {
      throw new Error(`Dataset run '${argumentsResult.runName}' did not return a datasetRunId.`);
    }

    const aggregateScores = await postRunScores(langfuse, datasetRunId, scoreEntries, runMetadata);
    const datasetRun = await langfuse.api.datasets.getRun(datasetName, argumentsResult.runName);
    const datasetRunItems = await waitForDatasetRunItems(
      langfuse,
      datasetRun.datasetId,
      argumentsResult.runName,
      exportedItems.length
    );

    console.log(JSON.stringify({
      datasetName,
      runName: argumentsResult.runName,
      runFamilyName: argumentsResult.runName,
      model: argumentsResult.model,
      deletedExistingRun,
      sampleSize: exportedItems.length,
      batchSize: argumentsResult.batchSize,
      executionCount: executionSummaries.length,
      runCount: 1,
      aggregateScores,
      datasetRuns: [{
        repetition: 1,
        runName: argumentsResult.runName,
        datasetRunId,
        runItemCount: datasetRunItems.data.length,
        aggregateScores,
        firstExecution: executionSummaries[0] ?? null,
        lastExecution: executionSummaries.at(-1) ?? null
      }],
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
