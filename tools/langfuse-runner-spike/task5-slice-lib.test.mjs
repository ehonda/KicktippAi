import test from "node:test";
import assert from "node:assert/strict";

import {
  buildSliceDatasetItemId,
  buildSliceExecutionPlan,
  calculateScores,
  createBatchChunks,
  deriveSliceDatasetName,
  deriveTraceTags,
  summarizeScores
} from "./task5-slice-lib.mjs";

test("buildSliceExecutionPlan keeps one execution per item without warmup", () => {
  const executions = buildSliceExecutionPlan([
    { inputPath: "a.json", exportedItem: { datasetItem: { id: "item-a" } } },
    { inputPath: "b.json", exportedItem: { datasetItem: { id: "item-b" } } }
  ]);

  assert.equal(executions.length, 2);
  assert.deepEqual(executions.map((entry) => entry.repetition), [1, 1]);
  assert.deepEqual(executions.map((entry) => entry.exportedItem.datasetItem.id), ["item-a", "item-b"]);
});

test("createBatchChunks groups slice executions by requested batch size", () => {
  const chunks = createBatchChunks([1, 2, 3, 4, 5], 2);

  assert.deepEqual(chunks, [[1, 2], [3, 4], [5]]);
});

test("deriveTraceTags emits the Task 5 filtering tags", () => {
  const tags = deriveTraceTags({
    communityContext: "pes-squad",
    model: "o3",
    promptKey: "prompt-v1",
    sliceKey: "random-10-seed-20260328"
  });

  assert.deepEqual(tags, [
    "task-5",
    "phase-2",
    "experiment",
    "community:pes-squad",
    "slice:random-10-seed-20260328",
    "model:o3",
    "prompt:prompt-v1"
  ]);
});

test("slice dataset helpers derive stable dataset and item ids", () => {
  assert.equal(
    deriveSliceDatasetName(
      "match-predictions/bundesliga-2025-26/pes-squad",
      "all-matchdays",
      "random-16-seed-20260328"
    ),
    "match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays/random-16-seed-20260328"
  );

  assert.equal(
    buildSliceDatasetItemId(
      "bundesliga-2025-26__pes-squad__ts1423757309",
      "random-16-seed-20260328"
    ),
    "bundesliga-2025-26__pes-squad__ts1423757309__slice__random-16-seed-20260328"
  );
});

test("calculateScores and summarizeScores preserve kicktipp aggregates", () => {
  const entries = [
    calculateScores({ homeGoals: 2, awayGoals: 1 }, { homeGoals: 2, awayGoals: 1 }),
    calculateScores({ homeGoals: 1, awayGoals: 0 }, { homeGoals: 2, awayGoals: 1 })
  ];

  const aggregate = summarizeScores(entries);

  assert.equal(entries[0].kicktipp_points, 4);
  assert.equal(entries[1].kicktipp_points, 3);
  assert.deepEqual(Object.keys(entries[0]), ["kicktipp_points"]);
  assert.equal(aggregate.total_kicktipp_points, 7);
  assert.equal(aggregate.avg_kicktipp_points, 3.5);
  assert.deepEqual(Object.keys(aggregate), ["total_kicktipp_points", "avg_kicktipp_points"]);
});
