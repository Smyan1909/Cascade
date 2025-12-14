# Explorer Agent
LangGraph-based explorer that reads manuals, probes UI, discovers APIs, and writes Skill Maps to Firestore. See `docs/phase4-explorer.md`.

## Modules
- `graph.py`: LangGraph wiring and checkpoints
- `manual_reader.py`: manual parsing and chunking
- `api_discovery.py` / `api_evaluator.py`: discover and rank API usage
- `planner.py`: map tasks to API/UI hypotheses
- `observer.py` / `verifier.py`: probe UI and validate selectors/endpoints
- `skill_map.py`: schema with API/UI preferences
- `code_generator.py`: emit code artifacts to Firestore
- `embeddings.py`: env-configurable embedding loader
- `cli.py`: entrypoint to run the Explorer graph
- `tools/`: web search + API tester helpers

## Running
```bash
python -m agents.explorer.cli --app-name "myapp" --grpc-endpoint "localhost:50051"
```

Environment (env-first):
- `CASCADE_APP_ID`, `CASCADE_USER_ID`, `CASCADE_AUTH_TOKEN`
- `CASCADE_GRPC_ENDPOINT`
- `CASCADE_MODEL_PROVIDER`, `CASCADE_MODEL_NAME`, `CASCADE_MODEL_ENDPOINT`, `CASCADE_MODEL_API_KEY`
- `CASCADE_WEB_SEARCH_PROVIDER`, `CASCADE_WEB_SEARCH_API_KEY`
- `CASCADE_EMBED_PROVIDER`, `CASCADE_EMBED_MODEL`, `CASCADE_EMBED_API_KEY` (optional)

