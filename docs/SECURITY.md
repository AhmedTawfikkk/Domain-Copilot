# Security Controls

## Scope

Domain Copilot processes uploaded legal documents and calls configured LLM
providers. The system treats uploaded document text, user questions, and model
output as untrusted inputs. This document records the controls implemented in
the application as of Day 10.

## Input and upload controls

- `POST /api/Ingest` accepts only `.pdf`, `.docx`, and `.txt` files.
- The upload endpoint limits request bodies to 20 MB and returns `413` for a
  larger file.
- Empty files and sources longer than 500 characters are rejected before
  ingestion.
- Extraction is allow-listed by file extension in the ingestion pipeline; no
  uploaded file is executed.
- File hashes provide idempotent ingestion. A duplicate file returns its
  existing document rather than creating duplicate chunks.

## Prompt-injection controls

- System instructions are isolated from document evidence in all LLM calls.
- Grounded-answer evidence is wrapped in `<evidence-chunk>` elements and the
  system prompt explicitly states that questions and evidence are untrusted.
- Clause extraction and memo drafting use separate versioned system and user
  prompt artifacts. Contract chunks are treated as data, never instructions.
- Grounded answers must cite chunk IDs retrieved for the request. Any missing,
  malformed, or unretrieved citation causes a controlled refusal.
- Legal review rejects malformed extraction output, duplicate IDs, omitted
  chunks, and unsupported structural results before risk assessment.

## Rate limiting

The API uses ASP.NET Core fixed-window rate limiting with no request queue:

| Policy | Limit | Protected endpoints |
|---|---:|---|
| `expensive-operations` | 10 requests/minute | Ingest, Answers, LegalReviews, Embeddings index |
| `state-changing` | 20 requests/minute | Review memo approval and export endpoints |
| `retrieval` | 60 requests/minute | Retrieval search |

Requests above a policy limit receive `429 Too Many Requests`. These limits
protect the database and LLM providers from accidental bursts. The current
policies are application-wide; identity-aware quotas are introduced with roles
in Day 11.

## Authentication

- Every non-Swagger route requires an `X-Api-Key` header.
- `ApiSecurity:ApiKey` is read from `API_SECURITY__API_KEY` and must contain at
  least 24 characters; the API fails at startup when the secret is missing.
- The supplied value is compared in constant time. The key is never returned
  by an endpoint or logged by this application.
- This is a service-level authentication gate, not a user identity system.
  Lawyer/Counsel roles and role-based authorization remain Day 11 work.

## Data access and output controls

- Persistence uses EF Core LINQ queries and generated parameterized commands;
  the application does not concatenate user input into SQL.
- `POST /api/LegalReviews` terminates before risk assessment when extraction
  cannot be validated.
- Review memos can be exported only after the explicit counsel approval state.
  The DOCX renderer reads persisted citations and risk findings rather than
  trusting model-provided source labels.
- OCR confidence is persisted for every chunk and appears in exported source
  references.

## Secrets and transport

- Connection strings and provider keys are read from environment variables via
  `.env`; real `.env` files are not committed. `.env.example` contains empty
  placeholders only.
- HTTPS redirection is enabled in the API pipeline.
- Provider failures are logged without logging API keys or complete document
  content.

## OWASP-oriented control mapping

| Risk area | Implemented control |
|---|---|
| Broken access control / API exposure | API-key authentication for all non-Swagger routes; counsel approval gate before export |
| Unrestricted resource consumption | Upload size limit and fixed-window rate limits with `429` rejection |
| Injection | EF Core generated parameterized commands; no user-input SQL concatenation |
| Prompt injection | System/evidence separation, untrusted-data rules, versioned prompts, citation validation, and E-11 indirect-injection corpus case |
| Insecure output handling | Strict JSON contracts, chunk-ID validation, controlled refusals, and deterministic memo citations |
| Sensitive configuration exposure | Environment-based secrets and committed empty placeholders only |

## Known limitations and next steps

- Current fixed-window policies are application-wide. Day 11 adds
  Lawyer/Counsel roles, identity-aware authorization, and identity-based
  quotas.
- File extension validation is not a malware scanner. Production deployment
  should add upstream malware scanning and content-type inspection.
- LLM output remains probabilistic. Structural validation, citation checks,
  deterministic risk rules, and human approval reduce but do not eliminate the
  need for counsel review.
