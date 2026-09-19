# Security Controls

## Scope

Domain Copilot processes uploaded legal documents and calls configured LLM
providers. Uploaded document text, user questions, and provider output are
untrusted inputs. This document records controls implemented through Day 11.

## Authentication and authorization

- Every API route requires the `X-Api-Key` authentication scheme. Swagger UI
  remains reachable so an authorized caller can enter a key, but API actions
  still require authentication.
- Two distinct configuration values map to authenticated roles:
  `ApiSecurity__LawyerApiKey` maps to `Lawyer` and
  `ApiSecurity__CounselApiKey` maps to `Counsel`.
- API keys are required to be different and at least 24 characters. Values are
  compared in constant time and are never logged or returned in responses.
- Lawyer can ingest documents, index embeddings, retrieve evidence, ask
  grounded questions, initiate legal review, and read review memos.
- Counsel can read review memos and is exclusively authorized to approve,
  reject, edit-and-approve, export a memo, and run the evaluation harness.
- A valid key with an insufficient role returns `403 Forbidden`; a missing or
  invalid key returns `401 Unauthorized`.

## Input and upload controls

- `POST /api/Ingest` accepts only `.pdf`, `.docx`, and `.txt` files.
- Uploads are limited to 20 MB; oversized uploads receive `413`.
- Empty files and sources longer than 500 characters are rejected before
  ingestion.
- Extraction is allow-listed by extension; uploaded files are never executed.
- File hashes provide idempotent ingestion and avoid duplicate chunks.

## Prompt-injection and AI controls

- System instructions are passed separately from user content and retrieved
  evidence.
- Versioned prompt artifacts define that user questions and contract content
  are untrusted data, never executable instructions.
- `GroundedAnswer.System.v2.md` explicitly refuses requests for system
  prompts, hidden instructions, credentials, configuration, internal
  reasoning, and requests to bypass citations or change role.
- Evidence is wrapped in identified `<evidence-chunk>` elements.
- Answer citations are checked against the exact chunks retrieved for the
  request. An unknown, missing, malformed, or uncited answer becomes a
  controlled refusal.
- Legal review terminates before risk assessment if clause extraction output
  is malformed, omits chunks, duplicates IDs, or violates its structure.
- The golden set includes direct and indirect prompt-injection cases, using a
  synthetic contract whose malicious text must be treated as document data.

## Rate limiting and resource controls

The API uses fixed-window rate limiting with no queue:

| Policy | Limit | Protected endpoints |
|---|---:|---|
| `expensive-operations` | 10 requests/minute | Ingest, Answers, LegalReviews, Embeddings index, Evaluation run |
| `state-changing` | 20 requests/minute | Review memo approval and export |
| `retrieval` | 60 requests/minute | Retrieval search |

Requests over a limit receive `429 Too Many Requests`. Evaluation cases run
sequentially to avoid a burst of provider calls.

## Data access and output controls

- Persistence uses EF Core LINQ queries and parameterized Npgsql commands;
  no user input is concatenated into SQL.
- A review memo may be exported only after explicit Counsel approval.
- The DOCX renderer uses persisted citations and risk findings, not
  model-supplied source labels.
- OCR confidence is persisted per chunk and appears in source references.

## Secrets and transport

- Database and provider secrets are supplied through ignored `.env` files or
  environment variables. `.env.example` has placeholders only.
- HTTPS redirection is enabled.
- Provider failures are logged without API keys or full contract text.

## OWASP Web and LLM control mapping

| Risk area | Domain Copilot control |
|---|---|
| Broken access control | Authentication handler assigns Lawyer/Counsel claims; policy authorization restricts approval, export, and evaluation to Counsel |
| Authentication failures | Separate long API keys, startup validation, constant-time comparison, `401` challenge behavior |
| Injection | EF Core parameterization and Npgsql parameters; no SQL string concatenation from request content |
| Insecure file handling | Extension allow-list, upload size limit, non-executable text extraction, and planned external malware scanning for production |
| Unrestricted resource consumption | Fixed-window API rate limits and sequential evaluation execution |
| Security misconfiguration | Required startup validation for secrets and explicit role policies; empty example values only in source control |
| Prompt injection | Instruction/data separation, versioned v2 safety prompt, untrusted evidence handling, direct/indirect golden cases |
| Insecure output handling | Strict JSON contracts, citation-ID validation, controlled refusal, and deterministic memo evidence rendering |
| Sensitive information disclosure | Refusal rules for system prompts, hidden instructions, credentials, and configuration; no secret logging |
| Excessive agency | LLM roles have no direct database or side-effecting tool access; memo export requires explicit human Counsel approval |

## Known limitations and next steps

- API keys are suitable for the supervised project environment, not a
  complete production identity lifecycle. Production should use managed
  identity/OIDC, key rotation, revocation, and per-user auditing.
- Rate limits are application-wide. Identity-aware quotas can be added using
  the authenticated role/identity claims.
- Extension validation is not malware scanning. Production deployment should
  add malware scanning and file-signature/content-type verification.
- LLM outputs remain probabilistic. Structural validation, citations,
  evaluation, deterministic risk rules, and Counsel approval reduce but do
  not eliminate the need for legal review.
