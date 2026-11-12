"""Attempt ledger. No messages, prompts, key values, response text or tool arguments."""
from datetime import datetime, timezone
import json
import os
import threading
from litellm.integrations.custom_logger import CustomLogger

_lock = threading.Lock()


class GatewayAudit(CustomLogger):
    def record(self, phase, kwargs, response=None, start=None, end=None):
        params = kwargs.get('litellm_params') or {}
        metadata = {**(kwargs.get('metadata') or {}), **(params.get('metadata') or {})}
        standard = kwargs.get('standard_logging_object') or {}
        meta = {**metadata, **(standard.get('metadata') or {})}
        def number(value):
            return value if isinstance(value, (int, float)) and value >= 0 else None
        usage = getattr(response, 'usage', None)
        error = kwargs.get('exception')
        event = {
            'kind': 'gateway_attempt', 'phase': phase, 'at': datetime.now(timezone.utc).isoformat(),
            'organization_id': meta.get('organization_id'), 'request_id': meta.get('request_id'),
            'portal_call_id': meta.get('portal_call_id'), 'attempt_id': kwargs.get('litellm_call_id'),
            'requested_route': meta.get('requested_route'),
            'provider': params.get('custom_llm_provider'), 'model': kwargs.get('model'),
            'effective_model': getattr(response, 'model', None),
            'input_tokens': number(getattr(usage, 'prompt_tokens', None)),
            'output_tokens': number(getattr(usage, 'completion_tokens', None)),
            'estimated_cost_usd': number(kwargs.get('response_cost')),
            'duration_ms': round((end-start).total_seconds()*1000) if start and end else None,
            'error_type': type(error).__name__ if error else None,
            'http_status': getattr(error, 'status_code', None)
        }
        line = json.dumps(event, separators=(',', ':'))
        with _lock:
            with open(os.environ.get('PORTAL_LLM_AUDIT_PATH', '/var/log/portal/attempts.jsonl'), 'a', encoding='utf-8') as output:
                output.write(line + '\n')

    async def async_log_pre_api_call(self, model, messages, kwargs):
        self.record('started', {**kwargs, 'model': model})

    async def async_log_success_event(self, kwargs, response_obj, start_time, end_time):
        self.record('completed', kwargs, response_obj, start_time, end_time)

    async def async_log_failure_event(self, kwargs, response_obj, start_time, end_time):
        self.record('failed', kwargs, response_obj, start_time, end_time)

    def log_success_fallback_event(self, original_model_group, kwargs, original_exception):
        self.record('fallback_completed', {**kwargs, 'exception': original_exception})

    def log_failure_fallback_event(self, original_model_group, kwargs, original_exception):
        self.record('fallback_failed', {**kwargs, 'exception': original_exception})


gateway_audit = GatewayAudit()
