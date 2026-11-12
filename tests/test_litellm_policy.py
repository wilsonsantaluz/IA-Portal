import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('configure', ROOT / 'infra/litellm/configure.py')
configure = importlib.util.module_from_spec(spec)
spec.loader.exec_module(configure)


class PolicyTests(unittest.TestCase):
    def setUp(self):
        self.policy = json.loads((ROOT / 'infra/litellm/routes.json').read_text())
        self.env = {'GROQ_API_KEY': 'SECRET', 'GROQ_FAST_MODEL': 'model-a', 'GROQ_BALANCED_MODEL': 'model-b',
                    'GROQ_REASONING_MODEL': 'model-c', 'LITELLM_GROQ_FREE_ACCOUNT': 'true',
                    'OPENROUTER_API_KEY': 'SECRET', 'OPENROUTER_FREE_MODEL': 'openrouter/free'}

    def test_aliases_and_order_are_configurable_without_secrets(self):
        config = configure.compile_config(self.policy, self.env)
        self.assertEqual(config['model_list'][0]['litellm_params']['model'], 'groq/model-a')
        self.assertEqual(config['router_settings']['fallbacks'][0], {'fast': ['fast-fallback-1']})
        self.assertNotIn('SECRET', json.dumps(config))
        self.env['GROQ_FAST_MODEL'] = 'replacement'
        self.assertEqual(configure.compile_config(self.policy, self.env)['model_list'][0]['litellm_params']['model'], 'groq/replacement')

    def test_paid_fallback_requires_route_authorization_and_budget(self):
        self.policy['routes']['fast']['destinations'].append('deepseek')
        self.env.update(DEEPSEEK_API_KEY='SECRET', DEEPSEEK_DEFAULT_MODEL='deepseek-chat')
        with self.assertRaisesRegex(ValueError, 'authorization'):
            configure.compile_config(self.policy, self.env)
        self.env['LITELLM_PAID_ROUTES'] = 'fast'
        with self.assertRaisesRegex(ValueError, 'budget'):
            configure.compile_config(self.policy, self.env)
        self.env['LITELLM_PAID_DAILY_BUDGET_USD'] = '1'
        self.assertIn('deepseek', configure.compile_config(self.policy, self.env)['router_settings']['provider_budget_config'])

    def test_privacy_blocks_external_fallback(self):
        self.policy['routes']['fast']['privacy'] = 'local-only'
        with self.assertRaisesRegex(ValueError, 'local-only'):
            configure.compile_config(self.policy, self.env)

    def test_openrouter_cannot_silently_switch_to_paid(self):
        self.env['OPENROUTER_FREE_MODEL'] = 'paid-model'
        with self.assertRaisesRegex(ValueError, 'free models'):
            configure.compile_config(self.policy, self.env)

    def test_groq_is_not_free_without_account_attestation(self):
        self.env['LITELLM_GROQ_FREE_ACCOUNT'] = 'false'
        config = configure.compile_config(self.policy, self.env)
        self.assertTrue(all(m['litellm_params']['model'].startswith('openrouter/') for m in config['model_list']))

    def test_unconfigured_sources_do_not_load(self):
        with self.assertRaisesRegex(ValueError, 'No authorized'):
            configure.compile_config(self.policy, {})

    def test_deployment_tuning_cannot_override_budget_or_credentials(self):
        config = configure.compile_config(self.policy, self.env)
        self.assertEqual(config['model_list'][0]['litellm_params']['reasoning_effort'], 'low')
        self.policy['destinations']['groq-fast']['parameters'] = {'max_tokens': 100000}
        with self.assertRaisesRegex(ValueError, 'tuning'):
            configure.compile_config(self.policy, self.env)


if __name__ == '__main__':
    unittest.main()
