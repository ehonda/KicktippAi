import { readFile } from "node:fs/promises";
import process from "node:process";

import { LangfuseClient } from "@langfuse/client";
import { LangfuseSpanProcessor } from "@langfuse/otel";
import { NodeSDK } from "@opentelemetry/sdk-node";
import { startActiveObservation } from "@langfuse/tracing";

function readInputPath() {
  const args = process.argv.slice(2);
  for (let index = 0; index < args.length; index += 1) {
    if (args[index] === "--input") {
      return args[index + 1];
    }
  }

  throw new Error("Missing required --input argument.");
}

function createTask(exportedItem) {
  return async (item) => startActiveObservation("predict-match-runner-spike", async (span) => {
    span.update({
      input: {
        datasetInput: item.input,
        systemPromptLength: exportedItem.runnerPayload.systemPrompt.length,
        promptTemplatePath: item.metadata.promptTemplatePath
      },
      metadata: {
        communityContext: item.metadata.communityContext,
        competition: item.metadata.competition,
        matchday: item.metadata.matchday,
        modelAnchor: item.metadata.model,
        spikeRunner: "js-ts-fallback"
      }
    });

    const output = {
      homeGoals: item.expectedOutput.homeGoals,
      awayGoals: item.expectedOutput.awayGoals,
      note: "runner-spike echo output"
    };

    span.update({ output });
    return output;
  });
}

async function main() {
  const inputPath = readInputPath();
  const raw = await readFile(inputPath, "utf8");
  const exportedItem = JSON.parse(raw);
  const localExperimentItem = {
    input: exportedItem.datasetItem.input,
    expectedOutput: exportedItem.datasetItem.expectedOutput,
    metadata: exportedItem.datasetItem.metadata
  };

  const sdk = new NodeSDK({
    spanProcessors: [new LangfuseSpanProcessor()]
  });

  sdk.start();

  const langfuse = new LangfuseClient();

  try {
    const result = await langfuse.experiment.run({
      name: "Task 3 Runner Spike",
      description: "Minimal JS fallback spike consuming one .NET-exported experiment item",
      data: [localExperimentItem],
      task: createTask(exportedItem),
      metadata: {
        spike: "task-3-runner-spike",
        runner: "js-ts",
        source: "dotnet-export"
      }
    });

    console.log(await result.format());
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
