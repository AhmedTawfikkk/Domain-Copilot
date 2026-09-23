# Business Requirements Document — Domain Copilot (Legal, D1T6)

> **Status:** complete for the evaluation deliverable (objectives, requirement
> set with acceptance criteria, business rules, assumptions, risks, and the
> requirement traceability matrix).
> Requirement IDs `BR-01…BR-15` are stable and referenced from
> `docs/SYSTEM-DESIGN.md` (gap table) and `docs/ARCHITECTURE.md` (components).

---

## 1. Context

An organisation holds a large body of legal contracts. Lawyers spend hours
manually reviewing contracts against internal playbook standards, searching
for risky clauses, and drafting review memos. This system automates the
mechanical parts of that workflow — ingestion, clause inventory, risk
flagging, memo drafting — while keeping a human (Counsel) in control of the
**final decision**.

The product is defined as much by what it refuses to do as by what it does:
it answers only from retrieved contract evidence and it never finalizes a
memo without explicit human approval.

---

## 2. Personas

| Persona | Goals | Pain points addressed |
|---|---|---|
| **Lawyer** — uploads contracts, asks questions, runs reviews | Ask grounded questions about a contract; get a cited, evidence-backed review memo; export it | Manual clause hunting; unverified "AI answers"; slow memo drafting |
| **Counsel** — senior reviewer, owns the sign-off | Trust that a memo is evidence-backed; approve/reject it; keep an audit trail | Reviewing unverified machine output; re-doing pages of drafting by hand |
| **Operator / Evaluator** — runs the system, monitors it | Deploy with minimal friction; verify grounding and injection resistance; observe cost | Untestable "RAG" claims; opaque LLM spend; fragile setup |

---

## 3. Objectives (measurable)

| ID | Objective | Measurable criterion | Current evidence |
|---|---|:---:|---|
| O-1 | Grounded answers only | ≥ 99 % of answered responses carry ≥ 1 validated citation on the golden set | E-01…E-08 + E-13…E-15; 100 % groundedness on recorded run |
| O-2 | Clause capture | ≥ 90 % section coverage on the evaluation corpus | `ClauseExtractorAgent` + `E-01…E-08`; recorded answer-hit rate 13/13 (100 %) |
| O-3 | Injection resistance | Direct **and** indirect injection cases refuse safely or stay grounded (100 % on golden set) | E-16…E-21; recorded **6/6 (100 %)** |
| O-4 | Controlled refusal | Out-of-corpus / ambiguous / unsupported questions return a refusal, never invented facts | E-09…E-15; refusal correctness 8/8 (100 %) |
| O-5 | Approval gate | Zero memos finalized without a `Counsel` decision (enforced by policy + domain guard) | `Counsel` policy; `ReviewMemo` status machine; 403 verified for `Lawyer` |
| O-6 | Review a 50-page agreement in minutes | Full multi-agent review stream completes in < 5 min wall-clock on the Docker quick start corpus | SSE progress timestamps in `ReviewAgentStep` (duration per step) |
| O-7 | Cost visibility | Every LLM call records tokens + `EstimatedCostUsd` once pricing is configured | `LlmRequestTelemetry` + `GET /api/Telemetry` |
| O-8 | 15-minute start | An evaluator with only Docker reaches a working demo in ≤ 15 min | README quick start + `scripts/smoke-docker.ps1` (7/7 green) |
| O-9 | Run traceability | Every review run is replayable with per-agent steps | `ReviewRuns` + `ReviewAgentStep`, `GET /api/ReviewRuns/{id}` |

---

## 4. Functional requirements (with acceptance criteria)

Each requirement has a unique ID, an acceptance criterion (AC), and (in
§9) a status + evidence.

### BR-01 — Document ingestion
The system must ingest PDF, DOCX, and TXT uploads, extract text (OCR fallback
for scanned PDFs), and index clauses for retrieval and review.

**AC-01:** Uploading a supported file returns a success status with a chunk
count; text PDFs extract directly; scanned PDFs fall back to Tesseract OCR;
duplicate uploads return HTTP 409 and do not duplicate the corpus.

### BR-02 — Clause-level structure
The system must split contracts into clause-level chunks that preserve legal
structure and carry per-chunk extraction confidence where OCR was involved.

**AC-02:** ≥ 90 % of expected sections are represented across the golden
corpus (measured by the evaluation harness answer-hit rate on section
expectations); low-confidence OCR chunks are retained but flagged
(`DocumentIngestion.LowConfidenceThreshold = 0.85`).

### BR-03 — Grounded answering with citations
Questions must be answered **only** from retrieved chunks; every answer
includes citations, and citations are validated in code before the UI sees
them.

**AC-03:** `GroundedAnswerService` rejects any citation that did not come from
retrieval; the recorded evaluation run shows 100 % groundedness (14/14).

### BR-04 — Controlled refusal
Questions that are out of corpus, ambiguous, or unsupported must be refused
explicitly — never answered with invented facts.

**AC-04:** Golden cases E-09…E-15 return `Refused`; refusal correctness on the
recorded run is 8/8 (100 %).

### BR-05 — Prompt-injection resistance
Instructions embedded in untrusted contract text must never override the
system's instructions or cause disclosure.

**AC-05:** Direct (E-16…E-18) and indirect (E-19…E-21) injection golden cases
return a safe refusal or a validated grounded answer; recorded run 6/6
(100 %).

### BR-06 — Multi-agent review with live progress
The system must run a clause-extractor → risk-assessor → memo-drafter pipeline,
stream live progress over SSE, allow cancellation, and persist per-agent
trace steps.

**AC-06:** `POST /api/LegalReviews/stream` emits `Started /
AgentStarted / AgentCompleted / Completed` (and `Cancelled` on
`POST /api/ReviewRuns/{id}/cancel`); each agent has a persisted
`ReviewAgentStep` with status and duration.

### BR-07 — Evidence-backed memos
The review memo must include risk findings tied to the playbook and citations
to source chunks.

**AC-07:** `ReviewMemo` rows are persisted with `ReviewMemoRiskFinding` and
`ReviewMemoCitation` child rows, surfaced in the approval desk and DOCX
export.

### BR-08 — Approval gate (no memo leaves without Counsel)
No memo may be finalized without explicit `Counsel` approval. `Lawyer` role
must be rejected from approval operations with HTTP 403.

**AC-08:** approve/reject/edit-and-approve endpoints carry the `Counsel`
policy; the domain (`ReviewMemo`) refuses decisions on non-draft memos;
`Lawyer` attempts return 403 (verified in the Docker smoke test).

### BR-09 — DOCX export
An approved memo must export as a standalone DOCX containing findings and
citations.

**AC-09:** `GET /api/ReviewMemos/{id}/export/docx` returns an OpenXML `.docx`
(rendered by `OpenXmlReviewMemoDocxRenderer`).

### BR-10 — Run traces and LLM telemetry
Every review run and every LLM call must be recorded for replay and cost
estimation.

**AC-10:** `GET /api/ReviewRuns/{id}` returns the full trace; `GET
/api/Telemetry` returns LLM calls with input/output tokens and
`EstimatedCostUsd` (when `LlmTelemetry__*CostPerMillionTokensUsd` is set).

### BR-11 — Evaluation harness
The system must evaluate grounding, refusal, and injection behavior on a
fixed golden set and report structured metrics.

**AC-11:** `POST /api/Evaluation/run` (Counsel only) executes 25 cases
E-01…E-25 and returns outcome/hit/groundedness/refusal/injection metrics;
recorded run: 25/25 completed, 0 failures.

### BR-12 — Authentication and roles
Users must register (self-service, role choice Lawyer/Counsel), sign in with
cookie sessions, and be authorized per role.

**AC-12:** Password policy (≥ 12 chars, upper/lower/digit/symbol) is enforced
at registration; role policies gate endpoints (403 otherwise); cookie is
HttpOnly + SameSite=Strict (Secure over HTTPS).

### BR-13 — Abuse protection
The system must bound expensive operations to protect provider spend.

**AC-13:** Fixed-window limits return HTTP 429: 10/min
(`expensive-operations`), 20/min (`state-changing`), 60/min (`retrieval`).
*(Note: request-bound, in-process — see G-01 in SYSTEM-DESIGN for the
target spend-quota tier.)*

### BR-14 — Docker quick start
An evaluator with only Docker must reach a working demo quickly.

**AC-14:** `docker compose up --build` starts DB + API (health-gated,
migrations applied on boot); `scripts/smoke-docker.ps1` passes 7/7
(health, UI, register, session, protected endpoint).

### BR-15 — Provider resilience
Provider failure or quota exhaustion must not hard-fail the product; a
fallback LLM provider must take over.

**AC-15:** `FallbackLlmProvider` chains Gemini ↔ Ollama per `LLM_PROVIDER`;
telemetry records which provider served each call.

---

## 5. Non-functional requirements

| ID | Requirement | Measure |
|---|---|---|
| NFR-01 | **Security** | OWASP Web + LLM control mapping maintained in `docs/SECURITY.md`; secrets never committed (Gitleaks in CI) |
| NFR-02 | **Observability** | Correlation IDs on requests; structured logs; `/health/live` + `/health/ready` |
| NFR-03 | **Streaming UX** | Answers and reviews stream incrementally (SSE) — first tokens before a full completion |
| NFR-04 | **Testability** | Application layer has no Infrastructure dependency → domain/application tests run without a database |
| NFR-05 | **Portability** | Single Docker image with OCR preinstalled; local Ollama mode runs with $0 provider cost |

---

## 6. Business rules

| Rule | Statement | Enforced where |
|---|---|---|
| BR-01-R | Only `Counsel` may approve, reject, or edit-and-approve a memo | `Counsel` authorization policy |
| BR-02-R | A memo's status machine allows decisions **only** while the memo is `Draft` | `ReviewMemo` domain guard |
| BR-03-R | An answer may cite only chunks returned by retrieval for that request | `GroundedAnswerService` pre-UI validation |
| BR-04-R | Untrusted (document) text is data; it must not be executed as instruction | prompt assembly + injection golden cases |
| BR-05-R | Duplicate uploads are rejected (409) — the corpus is idempotent per file | `IngestController` duplicate detection |
| BR-06-R | Passwords must satisfy the Identity policy (≥ 12 chars, 4 classes) | `AddIdentityCore` options |
| BR-07-R | LLM calls in the expensive tiers are rate-limited to 10/min per instance | `AddRateLimiter` fixed window |
| BR-08-R | A review run can be cancelled by its owner and the memo remains `Draft` | cancellation registry + domain status |

---

## 7. Assumptions

1. Corpus is synthetic/public contracts only, no real client data.
2. Single-firm deployment — tenant isolation across firms is out of scope.
3. Contracts are in English.
4. Users self-register and *self-assert* their role (no admin approval flow in
   the MVP) — documented as a known trust model.
5. LLM output quality varies by provider/model; evaluation re-runs per
   provider are the agreed way to compare (never chunk IDs).
6. The evaluator has Docker; network access for the default provider or a
   local Ollama for zero-key mode.
7. Cost estimates depend on correct `LlmTelemetry__*` pricing inputs.

---

## 8. Risks

| ID | Risk | Likelihood / Impact | Mitigation |
|---|---|---|---|
| R-1 | **Hallucination / grounding failure** — answer not supported by the contract | Med / High | citation validation in code, refusal pattern, golden set E-01…E-15 |
| R-2 | **Prompt injection via contract text** | Med / High | injection golden cases E-16…E-21; text treated as data; refusal metric |
| R-3 | **Abuse-driven token spend** | Med / High | request rate limits (BR-13); target: managed spend quota (G-01) |
| R-4 | **OCR errors on poor scans** propagate into retrieval | Med / Med | per-page confidence, `LowConfidenceThreshold` flagging, evidence links |
| R-5 | **Data loss of legal memos** | Low / Critical | DB persists to a volume; **gap G-09 (PITR/backups) is open** |
| R-6 | **Provider outage / quota** | Med / Med | `FallbackLlmProvider` Gemini ↔ Ollama |
| R-7 | **Role self-assertion** (anyone registers as Counsel) | Med / Med | accepted for MVP; admin-approved roles = future work |
| R-8 | **Model quality drift** over provider versions | Low / Med | evaluation harness results recorded per run/provider |
| R-9 | **Session theft over plain HTTP** | Low / Med | cookie Secure over HTTPS, HttpOnly, SameSite=Strict; Docker path documented |

---

## 9. Traceability matrix — BR → implemented / partial / deferred → evidence

| Requirement | Status | Evidence |
|---|---:|---|
| BR-01 Ingest PDF/DOCX/TXT + OCR | **Implemented** | `DocumentIngestionService`, `TesseractPdfOcrService`, `scripts/seed-corpus.ps1`, smoke test (ingest path), corpus files in `corpus/raw` |
| BR-02 Clause structure | **Implemented** | `ClauseAwareChunker`, `DocumentChunk` (index, confidence), evaluation answer-hit 13/13 |
| BR-03 Grounded answers + citations | **Implemented** | `GroundedAnswerService` validation, `StreamingGroundedAnswerService`, E-01…E-08 |
| BR-04 Controlled refusal | **Implemented** | `Refused` stream event, E-09…E-15, refusal 8/8 |
| BR-05 Injection resistance | **Implemented** | E-16…E-21, injection resistance 6/6 |
| BR-06 Multi-agent review + live progress + cancel | **Implemented** | `LegalReviewOrchestrator` + agents, SSE events, `ReviewRunsController/cancel`, `ReviewAgentStep` |
| BR-07 Evidence-backed memos | **Implemented** | `ReviewMemoRiskFinding`, `ReviewMemoCitation`, approval desk UI |
| BR-08 Approval gate | **Implemented** | `Counsel` policy, `MemoApprovalService`, `ReviewMemo` guard, 403 verified |
| BR-09 DOCX export | **Implemented** | `OpenXmlReviewMemoDocxRenderer`, export endpoint |
| BR-10 Traces + LLM telemetry | **Implemented** | `ReviewRunTracker`, `LlmRequestTelemetry` + price config |
| BR-11 Evaluation harness | **Implemented** | `EvaluationHarness`, `GoldenEvaluationCaseCatalog`, 25 cases, recorded run |
| BR-12 Auth + roles | **Implemented** | Identity cookie auth, roles, `api/Auth/*` |
| BR-13 Abuse protection | **Partial** | in-process fixed windows (10/20/60) → 429; spend-quota target deferred (G-01) |
| BR-14 Docker quick start | **Implemented** | compose files, `.dockerignore`, smoke-docker.ps1 all green (7/7) |
| BR-15 Provider resilience | **Implemented** | `FallbackLlmProvider`, provider-per-call telemetry |

Legend: **Implemented** = shipped and verified; **Partial** = works but with
a documented limitation (link: `docs/SYSTEM-DESIGN.md` gap table G-xx);
**Deferred** = recorded, not shipped.