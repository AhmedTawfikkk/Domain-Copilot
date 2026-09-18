# Agentic Workflow

## Purpose

This document explains how AI assistance is guided, constrained, reviewed, and
verified during development of Domain Copilot.

## Architecture Rules

The project follows Clean Architecture:

```text
Domain <- Application <- Infrastructure <- API
```

- Domain and Application do not reference LLM SDKs, vector-store SDKs, EF Core,
  or web frameworks.
- `ILlmProvider` is the provider abstraction used by Application services.
- Gemini, Groq, Ollama, PostgreSQL, pgvector, EF Core, and HTTP clients remain
  Infrastructure concerns.
- The active hosted provider chain is `Gemini -> Ollama`; `LLM_PROVIDER=ollama`
  reverses it for local-first testing. Groq is retained as an inactive adapter.
- API controllers call Application contracts and do not contain business logic.
- Prompts are versioned files, not C# string literals.
- Agent-to-agent data uses typed C# contracts rather than unvalidated text.

## Versioned Prompt Assets

Prompts are stored as separate, versioned files:

```text
Documents/Answering/Prompts/
  GroundedAnswer.System.v1.md
  GroundedAnswer.User.v1.md

Documents/Review/Prompts/
  ClauseExtraction.System.v1.md
  ClauseExtraction.User.v1.md
  MemoDraft.System.v1.md
  MemoDraft.User.v1.md
```

System prompts contain fixed rules, security constraints, and output contracts.
User prompts contain the question and retrieved contract data only.

Retrieved contract text is treated as untrusted data. Instructions contained in
a contract must not override system instructions.

## Grounded Answering Quality Gate

```text
Question
  -> Hybrid Retrieval
  -> Retrieved Evidence Chunks
  -> LLM Generation
  -> JSON Validation
  -> Citation Validation
  -> Answer with Citations OR Controlled Refusal
```

The application refuses an answer when:

- no evidence chunks are retrieved;
- the model returns malformed output;
- the answer is empty;
- the answer has no citations; or
- a cited chunk ID was not retrieved for the request.

## Runtime Agent Roles

### Clause Extractor

- Input: typed source chunks from one document.
- Output: typed `ExtractedClause` records.
- Allowed tool: `ILlmProvider`.
- Does not access the database directly.
- Does not assess risk or make legal recommendations.

### Risk Assessor

- Input: typed extracted clauses.
- Output: typed `RiskFinding` records.
- Allowed tool: synthetic legal playbook only.
- Does not access the LLM or database directly.
- Does not draft a memo or perform side effects.

### Legal Review Orchestrator

- Loads the requested document through `IDocumentReviewRepository`.
- Calls Clause Extractor, then Risk Assessor.
- Enforces batching, timeouts, and termination conditions.
- Does not bypass typed agent contracts.

### Memo Drafter

- Input: typed extracted clauses and typed `RiskFinding` records.
- Output: a draft memo. Source chunk IDs are derived deterministically by the
  Application layer from typed risk findings and extracted clauses.
- Allowed tool: `ILlmProvider`.
- Treats Risk Assessor findings as the complete risk list; it must not invent
  additional findings or recommendations.

### Counsel Approval Gate

- A memo is persisted as `Draft` after review.
- Counsel can approve, reject, or edit-and-approve the draft through typed API
  requests.
- No future finalization, export, or send operation may use a memo unless its
  status is `Approved`.

```text
Document Chunks
  -> Clause Extractor
  -> Extracted Clauses
  -> Risk Assessor
  -> Risk Findings
  -> Memo Drafter
  -> Draft Memo
  -> Counsel Approval Gate
```

## Workflow Controls

The review workflow currently applies:

- Maximum document chunk count.
- Maximum chunks per extraction batch.
- Maximum extraction batch count.
- Clause Extractor timeout.
- Risk Assessor timeout.
- Memo Drafter timeout.
- Bounded retry with exponential backoff for transient agent failures.
- Explicit workflow termination reasons.
- Strict validation that every supplied chunk has one valid extraction result.

The workflow terminates before Risk Assessment if the Clause Extractor returns:

- malformed JSON;
- an unknown source chunk ID;
- a duplicate chunk ID;
- an omitted chunk;
- an unsupported clause type; or
- an invalid confidence value.

This avoids producing a risk assessment from incomplete or untrusted extraction
output.

## Testing and Verification

- Unit tests use mocked dependencies for deterministic Application-layer tests.
- Grounded-answer tests verify citations and refusal behavior.
- Retrieval tests verify retrieval logic separately from live LLM behavior.
- Orchestrator tests verify successful sequencing and termination conditions.
- Manual API testing is performed against the local PostgreSQL database and
  ingested contract corpus.
- OCR tests cover direct-text and scanned-page fallback behavior. Export tests
  verify that a draft cannot be exported before counsel approval.
- An indirect prompt-injection regression test verifies that an instruction in
  retrieved document text remains inside an untrusted evidence boundary.

## Security Controls

- Expensive API operations (ingest, answer generation, legal review, and
  embedding indexing) use a 10-request-per-minute fixed-window limiter.
- Uploaded documents are restricted to PDF, DOCX, or TXT and limited to 20 MB.
- Memo export is fail-closed: it requires `Approved` status plus persisted
  citations and risk findings.
- `docs/SECURITY.md` records the implemented controls, threat model, and
  limitations. API-key authentication is implemented; Lawyer/Counsel role
  authorization is deliberately tracked for Day 11.

`docs/EVALUATION.md` contains candidate evaluation cases. A case is marked as
passed only after the output and cited source chunks are manually reviewed.

## Documentation Process

Architecture decisions are captured in ADRs:

- ADR-0001: Provider abstraction.
- ADR-0002: Chunking strategy.
- ADR-0003: Embeddings and hybrid retrieval.
- ADR-0004: Grounded answers and citations.
- ADR-0005: Pipeline agent orchestration.
- ADR-0006: Memo drafting and counsel approval.

Material AI assistance, developer verification, and corrected suggestions are
recorded in `docs/AI-USAGE-LOG.md`.

## Current Limitations

The following are not implemented yet:

- Graceful fallback from an agentic-review failure to plain grounded RAG.
- GitHub Actions automated quality gate.
- Repository-level coding-agent instruction file.
- Complete evaluation harness with 25 verified cases and 5 prompt-injection
  cases.

## Lessons Learned

During manual testing, larger Clause Extractor batches caused some LLM responses
to contain malformed JSON, omit chunk results, or use unsupported clause types.

The strict typed-output validation correctly stopped the workflow before Risk
Assessment. The current mitigation is smaller extraction batches, clearer
prompt instructions, JSON output mode for Gemini, and bounded retry/backoff.
Graceful RAG fallback remains a planned improvement.
