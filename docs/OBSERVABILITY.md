# Observability and Local Operations

## Correlation IDs

Every request receives an `X-Correlation-ID` response header. A caller may supply
its own UUID in that header; otherwise the API generates one. The value is added to
the logging scope, so API, EF Core, agent, and outbound LLM logs for one request can
be traced with the same ID.

Document content, prompts, and API keys are never written to these logs. The review
agents log only workflow metadata such as document ID, chunk count, and result count.

## LLM request telemetry

Every direct Gemini or Ollama call writes one row to `LlmRequestTelemetry`. A fallback
therefore produces two independent rows: one failed primary attempt and, if it succeeds,
one successful fallback attempt. The table records correlation ID, provider, model,
operation, token usage when returned by the provider, duration, outcome, and a bounded
failure classification. It never stores prompts, contract text, model output, or API keys.

`EstimatedCostUsd` is `0` for local Ollama. For hosted providers, it remains `NULL`
until the current account's token pricing is deliberately configured with environment
variables such as `LlmTelemetry__GeminiInputCostPerMillionTokensUsd`; this avoids
presenting an invented or stale price as financial data.

To inspect the newest records locally:

```powershell
docker exec domain-copilot-db psql -U copilot -d domain_copilot -c 'SELECT "CreatedAtUtc", "CorrelationId", "Provider", "Model", "Operation", "InputTokens", "OutputTokens", "TotalTokens", "EstimatedCostUsd", "Succeeded", "WasCancelled", "FailureReason", "DurationMilliseconds" FROM "LlmRequestTelemetry" ORDER BY "CreatedAtUtc" DESC LIMIT 20;'
```

## Health checks

- `GET /health/live` confirms that the API process is alive. It has no dependency
  on PostgreSQL and is appropriate for liveness probes.
- `GET /health/ready` confirms that PostgreSQL is reachable. It returns `503` while
  the service cannot connect to the database and is appropriate for readiness probes.

## Streaming answers

`POST /api/Answers/stream` accepts the same JSON body and Lawyer API key as
`POST /api/Answers`, then returns `text/event-stream`.

The stream uses these SSE event names:

- `started` — retrieval succeeded and generation has begun.
- `delta` — an incremental provider response fragment. These fragments are the
  provider's structured grounded-answer JSON and must be accumulated by a client.
- `completed` — the accumulated response passed citation validation. Its data is the
  final `GroundedAnswerResult` that a client may display as grounded.
- `refused` — no evidence was retrieved or the completed response did not meet the
  grounding contract.
- `error` — invalid input or an unavailable provider.

Disconnecting the client cancels the request token, which stops retrieval/provider
work when the provider honors cancellation.

## Docker Compose

Copy `.env.example` to `.env`, replace the API-key placeholders, configure
`LLM_API_KEY`, and set a non-default `POSTGRES_PASSWORD`. Then run:

```powershell
docker compose up --build
```

Compose starts PostgreSQL, waits for its health check, starts the API on port `8080`,
and applies pending EF Core migrations because it sets
`Database__ApplyMigrationsOnStartup=true`. OCR dependencies are included in the API
container at `/usr/bin/tesseract` and `/usr/bin/pdftoppm`.

To ingest the local corpus after the API is ready:

```powershell
.\scripts\seed-corpus.ps1 `
  -ApiUrl "http://localhost:8080/api/Ingest"
```

The script reads `ApiSecurity__LawyerApiKey` from the repository `.env` file, or an
explicit `-ApiKey` can be passed for CI and other non-interactive callers.
