# ADR-0001: LLM Provider Abstraction

## Status
Accepted

## Context
The system needs access to LLM completion, streaming, and embedding
capabilities, without the Domain/Application layers depending on any
specific LLM SDK (per §4 of the assessment brief). At least 2 working
provider implementations are required (one hosted API, one
local/free alternative), selected by configuration, with a
documented fallback chain.

## Decision
- Single interface: ILlmProvider (Application layer) — CompleteAsync,
  StreamCompleteAsync, EmbedAsync.
- OllamaProvider (local, llama3.1:8b + nomic-embed-text) — fully free,
  runs offline.
- GroqProvider (hosted, openai/gpt-oss-20b) — fast, free tier, but has
  no embeddings endpoint. All embedding calls route through Ollama
  regardless of which provider is active for completion.
- FallbackLlmProvider wraps both — if the primary provider throws, the
  request automatically retries against the secondary provider.

## Alternatives Considered
- Using a ready-made framework (Semantic Kernel/LangChain): rejected
  for now, to fully understand and control the abstraction while
  still learning RAG fundamentals.
- Gemini instead of Groq: supports embeddings, but has a much tighter
  free-tier rate limit (~10-15 requests/minute), which would slow down
  active development. Kept as a documented, reversible option.

## Consequences
- Embeddings currently depend on Ollama only.
- Hosted provider model names can be deprecated without notice
  (happened during development: Groq dropped support for
  llama-3.1-8b-instant, requiring a model name change).
