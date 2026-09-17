# ADR-0001: LLM Provider Abstraction and Active Provider Chain

## Status

Accepted - amended after runtime rate-limit evaluation.

## Context

Domain Copilot needs completion, streaming, and embedding capabilities without
the Domain or Application layers depending on a provider SDK. The assessment
also requires at least two working provider implementations, selected through
configuration, with a documented fallback chain.

The initial hosted provider was Groq (`openai/gpt-oss-20b`) with Ollama as the
local fallback. Groq is fast for short calls, but contract review sends several
structured requests with large evidence prompts. During manual review testing,
the Groq free tier repeatedly returned HTTP 429 because its rolling token limit
was exceeded, even when its request-per-minute limit was not.

Gemini supports both completion and embeddings. Its embedding output is
configured to match the project's `vector(768)` pgvector schema, allowing the
same hosted provider to support both answer generation and indexing.

## Decision

- `ILlmProvider` remains the single Application-layer abstraction for
  `CompleteAsync`, `StreamCompleteAsync`, and `EmbedAsync`.
- `GeminiProvider` is the active hosted primary provider. It uses
  `gemini-3.5-flash-lite` for short structured completion workloads and Gemini
  embeddings for dense retrieval.
- `OllamaProvider` remains the local/free fallback provider, using
  `llama3.1:8b` for completion and `nomic-embed-text` for embeddings.
- `FallbackLlmProvider` provides the active chain:

  ```text
  Gemini -> Ollama
  ```

- Setting `LLM_PROVIDER=ollama` reverses the active chain for local-first
  development:

  ```text
  Ollama -> Gemini
  ```

- `GroqProvider` remains an Infrastructure adapter in the codebase, but is not
  the active provider chain at this time. It can be re-enabled after supplying
  a dedicated Groq credential and reviewing the runtime quota trade-off.

## Alternatives Considered

- Keep Groq as the active hosted primary: rejected for current development
  workloads because token-rate limits caused review calls to fall back
  unpredictably during manual testing.
- Ollama only: rejected because the project must demonstrate a hosted provider
  and because a hosted embedding implementation provides a realistic swap test.
- Provider SDK references in Application: rejected because they would violate
  the Clean Architecture boundary.

## Consequences

- Completion and embeddings can both run through Gemini when it is available.
- Ollama remains a functional local fallback, so a hosted-provider outage does
  not prevent local development or demonstrations.
- The provider identity is observable through API/HTTP logs; `FallbackLlmProvider`
  is the DI wrapper name and is not itself the provider that generated text.
- Provider quotas remain external constraints. The workflow retains chunk,
  timeout, retry, and output-validation controls rather than assuming an
  unlimited hosted quota.
