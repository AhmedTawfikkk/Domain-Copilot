# AI Usage Log

## Purpose

This log records material AI assistance used during development, the
developer's independent decisions and verification, and AI suggestions that
were corrected after testing or review.

AI was used as an engineering assistant for design discussion, code review,
debugging, and documentation drafting. All implementation choices, source-code
changes, test execution, and acceptance decisions were reviewed and performed
by the developer.

## Day 4 - LLM Provider Abstraction

- AI-assisted: discussed the provider-abstraction design and reviewed the
  `ILlmProvider` contract for completion, streaming, and embeddings.
- Independent decision: initially selected Groq as the hosted provider and
  Ollama as the local fallback provider, preserving provider selection through
  configuration.
- AI mistake caught: an initially suggested Groq model name was deprecated.
  A runtime 404 exposed the issue; the model configuration was corrected after
  checking the currently available provider model.
- AI mistake caught: the initial environment-variable loading order caused
  configuration values to be read before `.env` was loaded. The startup order
  was corrected and verified locally.
- Documentation: recorded the provider decision and fallback rationale in
  ADR-0001.

## Provider Decision Update - Gemini Primary

- Developer verification: Groq responded correctly to small calls, but the
  contract-review pipeline exceeded its rolling token rate limit under repeated
  structured extraction calls. The resulting HTTP 429 responses triggered the
  local fallback as designed.
- Independent decision: changed the active hosted provider to Gemini and kept
  Ollama as fallback. Gemini was selected because it supports both completion
  and embeddings compatible with the project's 768-dimension vector schema.
- AI mistake caught: Gemini initially returned prose or Markdown around the
  required grounded-answer JSON. The provider request was updated to request
  `application/json`, after which the grounded-answer endpoint returned a
  validated answer with citations.
- Groq remains as an adapter for future comparison or reactivation; it is not
  the active provider chain. ADR-0001 was amended to record the reversible
  decision and operational reason.

## Day 5 - Ingestion Pipeline

- AI-assisted: reviewed the ingestion design: extraction for PDF, DOCX, and
  TXT; legal-text cleaning; clause-aware chunking; metadata; and SHA-256
  idempotency.
- Developer implementation and verification: implemented the ingestion flow,
  applied EF Core migrations, ingested corpus files, and inspected the
  `Documents` and `DocumentChunks` tables in PostgreSQL.
- Independent decision: used incremental migrations as the data model evolved,
  rather than creating every future table before a feature required it.
- AI-assisted: discussed Clean Architecture placement for database-dependent
  adapters and application-level contracts.
- Documentation: recorded the chunking decision in ADR-0002.

## Day 6 - Embeddings and Hybrid Retrieval

- AI-assisted: reviewed dense retrieval with pgvector, PostgreSQL full-text
  search, and Reciprocal Rank Fusion (RRF) for hybrid retrieval.
- Developer implementation and verification: stored embeddings for ingested
  chunks, applied the database migration, and tested dense, keyword, and hybrid
  retrieval through the API.
- AI mistake caught: an initial EF Core full-text-search approach caused
  `WebSearchToTsQuery` to switch to client evaluation at runtime. It was
  replaced with parameterized PostgreSQL SQL so full-text search executes on
  the database server.
- AI mistake caught: dense and keyword database queries were initially run in
  parallel through one scoped DbContext, causing Npgsql to report
  "A command is already in progress". The hybrid retrieval flow was changed to
  execute the two database queries sequentially before RRF fusion.
- Documentation: recorded the pgvector and hybrid-retrieval decision in
  ADR-0003.

## Day 7 - Grounded Answers and Citations

- AI-assisted: reviewed the grounded-answer design, including retrieval before
  generation, citation validation, controlled refusal, and prompt-injection
  resistance.
- Developer implementation and verification: tested a liability question
  against the ingested corpus and confirmed that the API returned retrieved
  source citations.
- Independent decision: an answer is accepted only when every returned chunk ID
  exists in the retrieved evidence for that request. Missing, malformed, or
  unknown citations result in a controlled refusal.
- AI mistake caught: a first prompt design combined system rules, the user
  question, and retrieved contract text in one user message. This weakened the
  instruction/data boundary. Prompts were split into separate versioned system
  and user prompt artifacts; retrieved contract text is treated as untrusted
  data.
- Documentation: started `docs/EVALUATION.md` as a candidate evaluation plan.
  Cases remain pending until manually verified against the actual corpus.

## Day 8 - Clause Extractor and Risk Assessor

- AI-assisted: reviewed the typed contracts and sequential pipeline design for
  the Clause Extractor, Risk Assessor, and orchestrator.
- Developer implementation: added a Clause Extractor that classifies supplied
  chunks, a deterministic Risk Assessor using a synthetic legal playbook, and
  an orchestrator with explicit termination conditions and per-step timeouts.
- Independent decision: the Risk Assessor does not access the LLM or database
  directly. Its only permitted capability is the review playbook. The
  orchestrator is responsible for document access and agent sequencing.
- AI mistake caught: the initial extraction batch size was too large for
  consistent structured output. The model returned incomplete JSON or omitted
  one or more required chunk results. The workflow correctly terminated rather
  than assessing incomplete data; batch sizing and prompt instructions were
  then adjusted during testing.
- AI mistake caught: the model returned an unsupported clause type. Strict
  contract validation rejected it instead of silently mapping an invented type
  into the legal-review workflow. Prompt guidance was refined to use
  `Unknown` or `Other` for non-target clauses.
- Documentation: recorded the pipeline orchestration decision, restricted tool
  sets, timeout policy, and termination conditions in ADR-0005.

## Day 9 - Memo Drafter and Human Approval Gate

- AI-assisted: reviewed the typed Memo Drafter contract, structured-output
  validation, source-citation validation, retry/backoff strategy, and approval
  state model.
- Developer implementation and verification: added draft memo persistence and
  explicit counsel actions: approve, reject, and edit-and-approve.
- Independent decision: a generated memo is stored as `Draft`; it is never
  automatically approved. Only an approved memo can be retrieved for a future
  finalization, export, or send action.
- AI mistake caught: early orchestration tests still constructed the
  orchestrator with its previous dependencies after Memo Drafter and memo
  persistence were added. The tests were updated with explicit mocks for the
  new dependencies.
- Documentation: recorded the approval-gate decision in ADR-0006.

## Day 10 - OCR, Export, and Basic API Security

- Developer implementation and verification: added a Tesseract/Poppler OCR
  fallback for scanned PDFs, persisted extraction-confidence values per chunk,
  and verified OCR-generated chunks against PostgreSQL.
- Developer implementation and verification: added counsel-gated DOCX export
  with persisted memo citations, persisted risk findings, a risk table, and a
  source-reference table. Manual API testing confirmed export is rejected
  before approval and enabled after approval.
- Independent decision: memo citations are derived deterministically from typed
  risk findings and extracted clauses rather than trusting model-generated GUID
  lists. This preserves grounding when a local model produces an invalid ID.
- AI mistake caught: the default `HttpClient` timeout of 100 seconds was
  shorter than the configured 120-second Memo Drafter timeout, causing a
  misleading provider timeout. The Ollama client now leaves cancellation to the
  orchestrator policy.
- AI mistake caught: the first Memo Drafter prompt repeated raw evidence after
  clause extraction, making local inference unnecessarily slow. The prompt now
  uses typed summaries and typed risk findings only, with a bounded output.
- AI-assisted: added fixed-window rate limits, upload size/type validation,
  API-key authentication, an indirect prompt-injection regression test, and
  `docs/SECURITY.md` to describe the actual controls and remaining limits.
