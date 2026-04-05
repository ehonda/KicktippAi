import { readFile } from "node:fs/promises";
import process from "node:process";

import { LangfuseClient } from "@langfuse/client";

function readArguments() {
  const args = process.argv.slice(2);
  let inputPath;
  let datasetName;

  for (let index = 0; index < args.length; index += 1) {
    if (args[index] === "--input") {
      inputPath = args[index + 1];
      index += 1;
      continue;
    }

    if (args[index] === "--dataset-name") {
      datasetName = args[index + 1];
      index += 1;
    }
  }

  if (!inputPath) {
    throw new Error("Missing required --input argument.");
  }

  return { inputPath, datasetName };
}

async function ensureDataset(langfuse, datasetName) {
  try {
    return await langfuse.dataset.get(encodeURIComponent(datasetName));
  }
  catch {
    await langfuse.api.datasets.create({
      name: datasetName,
      description: "Canonical hosted dataset for Bundesliga 2025/2026 pes-squad match experiments",
      metadata: {
        competition: "bundesliga-2025-26",
        scope: "match-centric",
        communityContext: datasetName.split("/").at(-1)
      }
    });

    return await langfuse.dataset.get(encodeURIComponent(datasetName));
  }
}

async function main() {
  const { inputPath, datasetName: datasetNameOverride } = readArguments();
  const raw = await readFile(inputPath, "utf8");
  const exportArtifact = JSON.parse(raw);
  const datasetName = datasetNameOverride ?? exportArtifact.datasetName;

  if (!datasetName) {
    throw new Error("Dataset name missing from artifact and no --dataset-name override was provided.");
  }

  if (!Array.isArray(exportArtifact.items)) {
    throw new Error("Artifact must contain an 'items' array.");
  }

  const langfuse = new LangfuseClient();

  try {
    await ensureDataset(langfuse, datasetName);

    for (const item of exportArtifact.items) {
      await langfuse.api.datasetItems.create({
        id: item.id,
        datasetName,
        input: item.input,
        expectedOutput: item.expectedOutput,
        metadata: item.metadata
      });
    }

    console.log(JSON.stringify({
      datasetName,
      itemCount: exportArtifact.items.length,
      firstItemId: exportArtifact.items[0]?.id ?? null,
      lastItemId: exportArtifact.items.at(-1)?.id ?? null
    }, null, 2));
  }
  finally {
    await langfuse.flush();
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  process.exitCode = 1;
});
