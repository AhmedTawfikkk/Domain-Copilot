# Review Run Trace

Each `POST /api/LegalReviews` request creates a persisted review run before any
agent executes. The API response includes `reviewRunId`; use that value to
inspect the workflow after completion or termination.

## Endpoints

- `GET /api/ReviewRuns` returns newest review runs first.
- `GET /api/ReviewRuns/{runId}` returns the run and its ordered agent steps.
- `POST /api/LegalReviews/stream` emits live Server-Sent Events for the
  review lifecycle. It emits `started`, then `progress` for each agent
  transition, and finishes with either `completed`, `terminated`, or `error`.

The trace stores the initiating user, document, correlation ID, timestamps,
terminal status, termination reason, generated memo ID, and safe operational
counts for each agent step. It intentionally does not store contract text,
prompts, or model output.

## Current scope

Clause extraction, risk assessment, and memo drafting are persisted as ordered
steps and emitted live by the streaming endpoint. Cancellation is propagated to
the workflow both when the client disconnects and when a Lawyer explicitly
requests it for an active run.

## Cancelling an active review

After the `started` event provides a `reviewRunId`, a Lawyer can request
cancellation with `POST /api/ReviewRuns/{runId}/cancel`. The active agent is
cancelled through its token, the run and active step are persisted as
`Cancelled`, and the stream ends with a `cancelled` event.
