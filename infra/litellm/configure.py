"""Compile explicitly authorized capability routes, never copy secrets into config."""
import json
import os
from pathlib import Path
import re


def compile_config(policy, env):
    models, fallbacks = [], []
    paid_routes = {route.strip() for route in env.get('LITELLM_PAID_ROUTES', '').split(',') if route.strip()}
    budgets = {}
    for route, rule in policy['routes'].items():
        if not re.fullmatch(r'[a-z][a-z0-9-]{0,39}', route):
            raise ValueError('Invalid route identifier')
        if rule['privacy'] not in ('local-only', 'external-approved'):
            raise ValueError('Explicit privacy policy required')
        limit = rule['max_tokens']
        if not isinstance(limit, int) or not 1 <= limit <= 8192:
            raise ValueError('Bounded route output budget required')
        chain = []
        for name in rule['destinations']:
            d = policy['destinations'][name]
            provider, billing = d['provider'], d['billing']
            model = env.get(d['model_env'], '').strip()
            if not model or (d.get('key_env') and not env.get(d['key_env'])):
                continue
            if len(model) > 150 or any(ord(c) < 32 for c in model) or '|' in model:
                raise ValueError('Invalid model identifier')
            if not re.fullmatch(r'[a-z_]{1,40}', provider):
                raise ValueError('Invalid provider identifier')
            if rule['privacy'] == 'local-only' and billing != 'local':
                raise ValueError('External destination forbidden by local-only policy')
            if billing == 'free-account' and (provider != 'groq' or env.get('LITELLM_GROQ_FREE_ACCOUNT') != 'true'):
                continue
            if billing == 'free-model' and (provider != 'openrouter' or not (model.endswith(':free') or model == 'openrouter/free')):
                raise ValueError('Only explicit OpenRouter free models allowed')
            if billing == 'local' and (provider != 'ollama_chat' or d.get('api_base') not in ('http://host.docker.internal:11434', 'http://ollama:11434')):
                raise ValueError('Local destination must be an approved local Ollama')
            if billing == 'paid':
                if route not in paid_routes:
                    raise ValueError('Paid destination requires explicit route authorization')
                budget = float(env.get('LITELLM_PAID_DAILY_BUDGET_USD', '0'))
                if not 0 < budget <= 1000:
                    raise ValueError('Paid provider requires a bounded daily budget')
                budgets[provider] = {'budget_limit': budget, 'time_period': '1d'}
            elif billing not in ('free-account', 'free-model', 'local'):
                raise ValueError('Explicit billing policy required')
            alias = route if not chain else f'{route}-fallback-{len(chain)}'
            params = {'model': f'{provider}/{model}', 'max_tokens': limit, 'max_retries': 0, 'cooldown_time': 30}
            # Deployment-specific capability tuning; never allow overriding credentials or budgets.
            tuning = d.get('parameters', {})
            if not isinstance(tuning, dict) or set(tuning) - {'reasoning_effort'}:
                raise ValueError('Unsupported deployment tuning parameter')
            if 'reasoning_effort' in tuning and tuning['reasoning_effort'] not in ('low', 'medium', 'high'):
                raise ValueError('Invalid reasoning effort')
            params.update(tuning)
            if d.get('key_env'):
                params['api_key'] = 'os.environ/' + d['key_env']
            if d.get('api_base'):
                params['api_base'] = d['api_base']
            models.append({'model_name': alias, 'litellm_params': params, 'model_info': {'id': f'{provider}|{model}|{alias}'}})
            chain.append(alias)
        if len(chain) > 1:
            fallbacks.append({route: chain[1:]})
    if not models:
        raise ValueError('No authorized configured destinations; configure credentials/models or local Ollama')
    return {
        'model_list': models,
        'general_settings': {'master_key': 'os.environ/LITELLM_MASTER_KEY', 'disable_spend_logs': True},
        'litellm_settings': {'callbacks': ['/app/portal/audit.gateway_audit'], 'set_verbose': False, 'turn_off_message_logging': True},
        'router_settings': {
            'num_retries': 1, 'max_fallbacks': 2, 'timeout': 25, 'stream_timeout': 25,
            'allowed_fails': 2, 'cooldown_time': 30,
            'fallbacks': fallbacks, 'provider_budget_config': budgets,
            'retry_policy': {'RateLimitErrorRetries': 1, 'TimeoutErrorRetries': 1, 'InternalServerErrorRetries': 1,
                             'AuthenticationErrorRetries': 0, 'BadRequestErrorRetries': 0, 'ContentPolicyViolationErrorRetries': 0}
        }
    }


if __name__ == '__main__':
    if len(os.environ.get('LITELLM_MASTER_KEY', '')) < 35 or not os.environ['LITELLM_MASTER_KEY'].startswith('sk-'):
        raise ValueError('Configure a strong sk- prefixed master key')
    config = compile_config(json.loads(Path('/app/portal/routes.json').read_text()), os.environ)
    Path('/tmp/portal-litellm.yaml').write_text(json.dumps(config))  # JSON is valid YAML.
    os.execvp('litellm', ['litellm', '--config', '/tmp/portal-litellm.yaml', '--host', '0.0.0.0', '--port', '4000'])
