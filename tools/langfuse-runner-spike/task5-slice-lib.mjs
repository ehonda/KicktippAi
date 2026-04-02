export function parsePositiveInteger(value, optionName) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed < 1) {
    throw new Error(`${optionName} must be a positive integer.`);
  }

  return parsed;
}

export function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

export function stableJson(value) {
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

export function deriveSliceDatasetName(canonicalDatasetName, sliceSourceKey, sliceKey) {
  return `${canonicalDatasetName}/slices/${sliceSourceKey}/${sliceKey}`;
}

export function buildSliceDatasetItemId(canonicalItemId, sliceKey) {
  return `${canonicalItemId}__slice__${sliceKey}`;
}

export function calculateScores(prediction, expectedOutput) {
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

export function summarizeScores(scoreEntries) {
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

export function createBatchChunks(items, batchSize) {
  const chunks = [];
  for (let index = 0; index < items.length; index += batchSize) {
    chunks.push(items.slice(index, index + batchSize));
  }

  return chunks;
}

export function buildSliceExecutionPlan(exportedItems) {
  return exportedItems.map(({ inputPath, exportedItem }) => ({
    inputPath,
    exportedItem,
    repetition: 1
  }));
}

export function deriveTraceTags(runMetadata) {
  const tags = [
    "task-5",
    "phase-2",
    "experiment"
  ];

  if (runMetadata?.communityContext) {
    tags.push(`community:${runMetadata.communityContext}`);
  }

  if (runMetadata?.sliceKey) {
    tags.push(`slice:${runMetadata.sliceKey}`);
  }

  if (runMetadata?.model) {
    tags.push(`model:${runMetadata.model}`);
  }

  if (runMetadata?.promptKey) {
    tags.push(`prompt:${runMetadata.promptKey}`);
  }

  return [...new Set(tags)];
}

export function derivePropagatedMetadata(runMetadata) {
  const metadata = {};
  const candidates = {
    communityContext: runMetadata?.communityContext,
    evaluationTimestampPolicyKey: runMetadata?.evaluationTimestampPolicyKey,
    model: runMetadata?.model,
    promptKey: runMetadata?.promptKey,
    selectedItemIdsHash: runMetadata?.selectedItemIdsHash,
    sliceKey: runMetadata?.sliceKey,
    startedAtUtc: runMetadata?.startedAtUtc,
    task: runMetadata?.task
  };

  for (const [key, value] of Object.entries(candidates)) {
    if (typeof value === "string" && value.length > 0 && value.length <= 200) {
      metadata[key] = value;
    }
  }

  return metadata;
}
