# Phase 4 — Explorer Agent (Teacher) with Firestore Persistence

Purpose: define how the Explorer reads manuals, probes UI, and emits Skill Maps stored per user in Firestore. No code yet—only design and steps.

## Deliverables
- Directory scaffold: `python/agents/explorer/`
  - `graph.py` (LangGraph wiring)
  - `manual_reader.py`
  - `planner.py`
  - `observer.py`
  - `verifier.py`
  - `skill_map.py` (schema definition)
  - `storage/` (Firestore helpers shared across agents)
- Skill Map schema (documented here) and Firestore paths.

## Firestore Persistence
- Root: `/artifacts/{__app_id}/users/{userId}`
- Skill Maps: `/skill_maps/{skillId}` — fields: `metadata` (skillId, appId, userId, version, provenance, confidence), `steps[]`, `selectors`, `assets`, timestamps.
- Explorer checkpoints: `/explorer_checkpoints/{runId}` — LangGraph state for resumability.
- All writes require `__initial_auth_token` and carry user/app context.

## Explorer Flow (LangGraph)
1) ManualReader: chunk/parse PDF/manual; embed sections; extract task candidates.
2) Planner: map manual steps to hypotheses about UI targets and actions.
3) Observer: fetch semantic tree + OCR text via Body; align text to UI; propose selectors.
4) Verifier: run non-destructive checks/dry-runs; confirm selectors; adjust hypotheses.
5) Memory: accumulate confirmed mappings and confidence scores.
6) Output: produce Skill Map with selectors and guard conditions; persist to Firestore.

## Skill Map Schema (conceptual)
- `metadata`: skillId, appId, userId, version, provenance, confidence, created_at, updated_at.
- `steps[]`:
  - `action`: Click/Type/Select/etc.
  - `selector`: normalized selector (path + filters + platform_source).
  - `inputs`: optional payload (text/number/json).
  - `guards`: conditions to verify before/after.
  - `fallbacks`: alternate selectors or recovery actions.
  - `waits`: visibility/time conditions.
- `assets`: optional text snippets, OCR hints, manual references.

## Design Notes
- Keep manual alignment explainable: store evidence (matched text, OCR snippets) in Skill Map metadata for debugging.
- Prefer least-privilege actions (hover/inspect) before committing to clicks.
- Track confidence per step; allow downstream Worker to choose fallbacks.

## Testing Plan
- Unit:
  - Skill Map schema validation (pydantic).
  - Planner heuristics with fixture manuals.
  - Selector ranking logic with synthetic semantic trees.
- Integration (Firestore emulator + mock Body):
  - Full loop: manual → plan → observe → verify → skill saved to Firestore.
  - Checkpoint/resume: interrupt mid-run, then resume from `/explorer_checkpoints/{runId}`.
  - Ensure no local file writes (assert paths untouched).

