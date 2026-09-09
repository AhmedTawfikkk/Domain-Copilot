# AI Usage Log



## Day 4 — LLM Provider Abstraction
- AI mistake caught: suggested a Groq model name that had since been
  deprecated. Found via a runtime 404, fixed after verifying the
  current model list with a live search instead of relying on
  outdated training data.
- AI mistake caught: initial environment-variable loading order was
  wrong, causing config values to silently read as empty. Corrected.
- Independent decision: kept Groq over switching to Gemini, documented
  as a reversible choice in ADR-0001.

## Day 5 — Ingestion Pipeline
- Built ITextExtractor abstraction
  with PDF/DOCX/TXT implementations, LegalTextCleaner, ClauseAwareChunker
  with nested clause numbering support (e.g. 4.1, 4.1.2), and
  DocumentIngestionService with SHA-256-based idempotent re-ingestion all reviewed by me.
- AI-assisted: reviewed the layer placement question for services that
  need database access (DocumentIngestionService in Infrastructure vs.
  agent-style services in Application needing a repository
  abstraction) — clarified the architectural rule to apply going
  forward rather than asking per-class each time.
- AI-assisted: generated unit tests for ClauseAwareChunker and the
  POST /api/ingest endpoint wiring.

## Day 5 (addendum) — Initial Corpus
- AI-assisted: generated 10 synthetic legal contracts (Service
  Agreements, NDAs, Employment, Software License, MSA, DPA) covering
  common clause types (indemnification, limitation of liability,
  termination, confidentiality, governing law).
- Deliberately included both playbook-compliant and playbook-violating
  contracts (e.g. uncapped indemnification, one-sided NDAs, missing
  liability caps) to support later Risk Assessor Agent testing (Day 8)
  and the evaluation harness (Day 11).
- No real personal or company data used — all entity names, addresses,
  and terms are fictional, per §2 of the assessment brief.
