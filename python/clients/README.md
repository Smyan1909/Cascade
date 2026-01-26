# clients

Provider-agnostic clients used by the Python Brain.

## What’s here

- `llm_client.py`: Pluggable LLM client interface loaded from environment variables.

## Environment variables

The LLM client is intentionally **env-driven** (do not hardcode model IDs).

Primary model (required):
- `CASCADE_MODEL_PROVIDER` (e.g. `openai`)
- `CASCADE_MODEL_NAME`
- `CASCADE_MODEL_API_KEY`
- `CASCADE_MODEL_ENDPOINT` (optional)

Summarization model (optional; defaults to lightweight OpenAI settings if present):
- `CASCADE_SUMMARY_MODEL_PROVIDER`
- `CASCADE_SUMMARY_MODEL_NAME`
- `CASCADE_SUMMARY_MODEL_API_KEY`
- `CASCADE_SUMMARY_MODEL_ENDPOINT`

## Extending providers

To add a new provider:
- Add a new internal client class implementing `generate(...)` semantics.
- Extend `load_llm_client_from_env()` and (optionally) `load_summarization_client_from_env()`.
- Add/extend tests under `python/tests/unit/` to validate env parsing and request shape.


