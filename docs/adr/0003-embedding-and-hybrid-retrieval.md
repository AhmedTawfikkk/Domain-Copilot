# ADR-0003: Embedding and Hybrid Retrieval

## Status
Accepted

## Context
The system must retrieve contract chunks relevant to a legal question while
preserving citation-ready provenance. Semantic search alone can miss exact
legal terms, while keyword search can miss paraphrases.

## Decision
- Use local Ollama `nomic-embed-text` for 768-dimension embeddings.
- Store embeddings in a dedicated Infrastructure persistence entity using
  PostgreSQL pgvector `vector(768)`.
- Use an HNSW index with cosine distance for dense retrieval.
- Use PostgreSQL generated `tsvector` plus GIN index for keyword retrieval.
- Combine dense and keyword rankings through Reciprocal Rank Fusion with
  constant k = 60.
- Return chunk content together with document, source, version, clause,
  page number, low-OCR-confidence status, and score.

## Consequences
- Domain and Application remain independent of pgvector and Npgsql types.
- Every retrieval result is citation-ready for later RAG answers and memos.
- RRF avoids manually calibrating incomparable dense and keyword scores.
- The embedding model and its 768 dimensions must remain aligned with the
  pgvector schema. Changing the model dimensions requires a new migration
  and re-indexing all chunks.