import unittest
from types import SimpleNamespace
from unittest.mock import patch

import experiment_cost_estimator as estimator


class SessionRecoveryTests(unittest.TestCase):
    def observation(self, **changes):
        return dict(traceId='linked-trace', name='predict-match', type='GENERATION', sessionId='', **changes)

    def test_lookup_uses_exact_trace_and_follows_cursor(self):
        observation = self.observation()
        pages = [{'data': [], 'meta': {'cursor': 'next'}}, {'data': [observation], 'meta': {}}]
        with patch.object(estimator, 'langfuse_get_json', side_effect=pages) as api:
            rows = estimator.list_linked_trace_observations(SimpleNamespace(), {}, 'run', 'linked-trace')
        self.assertEqual(1, len(rows))
        self.assertTrue(rows[0]['_sessionIdRecoveryUsed'])
        query = api.call_args_list[0].args[3]
        self.assertEqual('linked-trace', query['traceId'])
        self.assertNotIn('sessionId', query)
        self.assertEqual('next', api.call_args_list[1].args[3]['cursor'])

    def test_conflicting_trace_or_session_is_rejected(self):
        for key, value in [('traceId', 'unlinked'), ('sessionId', 'other-run'), ('name', 'other'), ('type', 'SPAN')]:
            observation = self.observation()
            observation[key] = value
            with self.subTest(key=key), patch.object(estimator, 'langfuse_get_json', return_value={'data': [observation]}):
                with self.assertRaisesRegex(RuntimeError, 'conflicts'):
                    estimator.list_linked_trace_observations(SimpleNamespace(), {}, 'run', 'linked-trace')

    def test_recovery_still_requires_manifest_item_metadata_and_unique_generation(self):
        args = SimpleNamespace(recover_missing_session_observations=True)
        links = [dict(datasetRunId='run-id', datasetRunName='run', datasetItemId='item', traceId='linked-trace')]
        provenance = dict(preparedManifestSha256='a' * 64, preparedManifestSampleSize=1)
        observation = self.observation(metadata={'langfuse.experiment.item.id': 'item'})
        for recovered, expected in [([observation], None), ([observation, observation], 'more than one'),
                                    ([self.observation(metadata={'langfuse.experiment.item.id': 'wrong'})], 'immutable run link')]:
            with patch.object(estimator, 'list_dataset_run_items', return_value=links), \
                 patch.object(estimator, 'validate_manifest_item_identity', return_value=provenance), \
                 patch.object(estimator, 'list_linked_trace_observations', return_value=recovered):
                if expected:
                    with self.assertRaisesRegex(RuntimeError, expected):
                        estimator.select_dataset_run_observations(args, {}, 'run', 'dataset', 'run-id', 'manifest', 1, [])
                else:
                    rows, bound = estimator.select_dataset_run_observations(args, {}, 'run', 'dataset', 'run-id', 'manifest', 1, [])
                    self.assertEqual(1, len(rows))
                    self.assertEqual(provenance['preparedManifestSha256'], bound['preparedManifestSha256'])

    def test_item_identity_accepts_full_nested_attributes_and_rejects_truncation(self):
        for attributes in ({'langfuse.experiment.item.id': 'item'}, '{"langfuse.experiment.item.id":"item"}'):
            self.assertEqual('item', estimator.extract_dataset_item_id({'metadata': {'attributes': attributes}}))
        with self.assertRaisesRegex(RuntimeError, 'truncated'):
            estimator.extract_dataset_item_id({'metadata': {'attributes': '{"langfuse.experiment.item.id":"it'}})

    def test_incomplete_links_do_not_trigger_recovery(self):
        args = SimpleNamespace(recover_missing_session_observations=True)
        with patch.object(estimator, 'list_dataset_run_items', return_value=[]), \
             patch.object(estimator, 'list_linked_trace_observations') as api:
            rows, _ = estimator.select_dataset_run_observations(args, {}, 'run', 'dataset', 'run-id', 'manifest', 1, [])
        self.assertEqual([], rows)
        api.assert_not_called()


if __name__ == '__main__':
    unittest.main()
