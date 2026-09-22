# System Design — Domain Copilot

> **Status:** final for the evaluation deliverable, living document.
> **Scope:** this document is the *system design* reference. It has two parts:
> **Part A — Target architecture** (unconstrained, what the system should look
> like when it grows), and **Part B — the implemented MVP**, with an explicit
> gap table, the reasoning behind every significant decision, and a cost model.
> The companion document `docs/ARCHITECTURE.md` carries the C4 diagrams,
> sequence/data-flow/ER diagrams, and the ADR register. `docs/BRD.md` owns the
> functional requirements (`BR-xx`) referenced here.

---

## 0. System in one paragraph

Domain Copilot ingests legal contracts (PDF/DOCX/TXT, with OCR fallback for
scanned pages), clause-chunks them, embeds and indexes them for hybrid
retrieval (full-text + vector), answers questions with **enforced citations**,
and runs a **multi-agent legal review** (clause extractor → risk assessor →
memo drafter) whose output cannot leave the workspace until a human with the
`Counsel` role approves it. Every review run and every LLM call is persisted
for traceability and cost estimation. A 25-case golden harness verifies
controlled refusal behaviour, including prompt-injection resistance.

The design is centered on two principles:

1. **The LLM is untrusted until grounded** — answers and memos must cite
   chunks that actually came back from retrieval; unsupported questions are
   refused, not guessed.
2. **Machine output is not a legal deliverable without a human gate** — the
   approval gate is a first-class architectural boundary, not a UI button.

Both principles are enforced in application code (`GroundedAnswerService`
validates citations before they reach the UI) and in authorization policies
(`Counsel` policy on memo decision endpoints), not merely by prompt wording.

---

## PART A — TARGET ARCHITECTURE (UNCONSTRAINED)

This part describes the architecture the product *should* evolve into for
multi-tenant, production traffic. Each section names the target component,
why it matters, and what problem it solves. Part B then compares each line
against what the MVP actually shipped.

```
                        Internet / corporate network
                                   │
                        ┌──────────▼──────────┐
                        │   API Gateway       │  TLS termination, WAF, authN
                        │   + managed rate    │  token bucket / quota per
                        │   limiting (azure   │  tenant, LLM-cost guard
                        │   api mgmt / CFAAQ) │
                        └──────────┬──────────┘
                                   ▼
                        ┌──────────────────────┐
                        │  App Service / AKS   │  horizontal autoscaling on
                        │  (stateless API)     │  CPU + request queue depth
                        └───┬─────────┬────────┘
                    ┌───────┘         └───────┐
        ┌───────────▼──────────┐ ┌─────────────▼───────────┐
        │  Secrets manager     │ │  Message broker         │ long-running
        │  (Azure Key Vault /  │ │  (Service Bus / Rabbit) │ extraction,
        │  Doppler / Vault)    │ │  job orchestration,     │ review runs,
        │  LLM_API_KEY etc.    │ │  retries, DLQs          │ eval runs
        └───────────┬──────────┘ └─────────────┬───────────┘
                    │                          ▼
                    │               ┌──────────────────────┐
                    │               │  Managed vector DB   │ Azure AI Search /
                    │               │  + PG (PITR, geo)    │ managed PG / Qdrant
                    │               └──────────┬───────────┘
                    │                          │
        ┌───────────┴──────────┐               │
        │  Cache (Redis)       │◄──────────────┘ embedding + retrieval
        │  chunk/embedding     │   warm reads, provider-result cache
        │  result cache        │
        └──────────────────────┘
        Observability: OpenTelemetry → Metrics/Logs/Traces →
        Grafana / Azure Monitor; alerting on token spend abuse
```

### A.1 — Edge: API gateway and managed rate limiting

**Target:** an API gateway (Azure API Management, Cloudflare API Gateway, or
Kong) in front of the API that terminates TLS, applies a WAF, and enforces
**managed, distributed rate limits** per tenant/plan with quotas and burst
allowances.

**Why:** the #1 operational risk of an LLM product is **abuse-driven token
spend** — a single misbehaving client burning expensive provider minutes. A
gateway-level limit is shared across all replicas (unlike an in-process
limiter), can be changed without a deployment, and can refuse cheaply at the
edge before a request ever reaches a replica.

**What it must provide:**

- Per-tenant token-bucket with `X-RateLimit-*` response headers.
- Daily/weekly **spend quotas** (not just request counts) — the cost knob, not
  the request knob, is the one that protects the P&L.
- WAF rules for path traversal / oversized uploads; request body size caps.

### A.2 — Secrets manager

**Target:** Azure Key Vault (or Doppler/Vault). Runtime secrets
(`LLM_API_KEY`, database passwords) live in a managed store with rotation,
audit, and per-environment versions. App code reads them via a provider
abstraction, never from a checked-in file.

**Why:** the MVP reads `.env` at startup (see Part B). That is acceptable for
a local/dev box but wrong for multi-tenant production: a `.env` on disk is a
single point of compromise, has no rotation story, and no audit trail of who
read what when.

### A.3 — Message broker

**Target:** Azure Service Bus (or RabbitMQ) as the backbone for long-running
work: document ingestion, embedding/indexing jobs, review runs, evaluation
runs. Producers enqueue jobs; stateless workers consume them; retries and
dead-letter queues make partial failure recoverable.

**Why:** the MVP runs ingestion and review synchronously in the request path
(SFE streaming is used, but the *work* is in-process). At scale, a 120 s OCR
+ chunking + embedding job should not occupy an API replica; a broker lets
workers scale independently, and an upload can be acknowledged before the
document is fully processed.

### A.4 — Compute and autoscaling

**Target:** containerized stateless API replicas on Azure Container Apps /
AKS with HPA on CPU, memory, and broker queue depth; zero-downtime rolling
deploys; the API itself holds **no** session state beyond the auth cookie
(which is already the case).

**Why:** review runs / answer streams are long-lived HTTP connections; a
replica going away mid-stream must be survivable (client reconnect + cancel +
resume, or at least graceful SSE `retry` events). Stateless replicas make that
simple. Autoscaling on queue depth is the correct signal because unit cost of
an LLM call varies with payload size, not just request count.

### A.5 — Caching

**Target:** Redis shared cache for:

- **Embedding results** of unchanged chunks (a document re-indexed on
  retry/version bump should not re-embed every chunk).
- Frequent **retrieval queries** with a short TTL (5 min) at low risk.
- Provider **completion results** only where determinism is provable (e.g.,
  evaluation harness golden cases with temperature 0) — cache in the harness,
  not in the interactive path.

**Why:** embedding is the cheapest-to-cache, most-repeated operation. The
interactive answer path should *not* cache completions in general (legal
answers should be fresh against the current corpus), which is why the cache
design is deliberately narrow.

### A.6 — Managed vector database

**Target:** managed, horizontally scalable vector store (Azure AI Search with
semantic ranker, or managed Postgres + `pgvector` with PITR) rather than a
self-hosted Postgres container on a single VM.

**Why:** `pgvector` on a single node is limited by RAM/disk and has **no
native HA or PITR** in the current compose setup. A managed option adds:
replicas, point-in-time restore, and (with Azure AI Search) hybrid + RRF
ranking built in, plus quota-friendly pricing.

### A.7 — Observability stack

**Target:** OpenTelemetry SDK → OTLP → metrics/logs/traces in a single
backplane (Azure Monitor / Grafana stack). Custom domain metrics: tokens per
request, cost per request, retrieval latency, groundedness score, refusal
rate, review-run duration by agent. Alerting on cost spikes, refusal-rate
drops (regression signal for injection resistance), and error budgets.

**Why:** the MVP has excellent *domain* telemetry in PostgreSQL (run traces,
LLM call records, cost estimates — see Part B), but no metrics/traces
backplane. Alerting on **refusal rate** is the early-warning system for a
grounding regression — the single most important product-health signal.

### A.8 — CI/CD environments

**Target:** three environments — `dev` (per-PR preview), `staging` (merge to
`main`, full deploy incl. broker + managed services), `production` (promoted
from staging, gated). Promotions are the same image artifact (content-addressable).

**Why:** the MVP has CI (build, format, tests, vulnerability scan, secret
scan) but **no CD**. The approval gate in the product should mirror the
pipeline: production changes go through staging first — same discipline, same
tooling.

### A.9 — DR / backup

**Target:**

- Postgres: PITR (WAL archiving) + daily snapshots, cross-region replica.
- Object-store backups of corpuses, exported memos, and evaluation golden set.
- RTO ≤ 1 h, RPO ≤ 15 min for the data plane; API is stateless, so recovery
  is rehydration + deploy.

**Why:** today the only copy of the database is a named Docker volume
(`pgdata`) on one machine. A disk failure is data loss. For a product whose
artifacts are *legal memos*, that is unacceptable at any scale beyond a demo.

### A.10 — Cost model at scale

The dominant cost line is **LLM tokens** (embeddings + completions), then
compute. Assuming Gemini-class pricing and the free/Ollama fallback (see the
appendix for current indicative pricing):

| Scale | Docs/month | Vector+PG | API compute | LLM spend (est.) | Managed services | **Total/month** |
|---|---:|---:|---:|---:|---:|---:|
| Hobby (dev) | 50 | $0 (self-hosted) | $0 (2 replicas @ free tier) | $0–5 (free tier / Ollama) | $0 | **$0–5** |
| Small firm | 1,000 | $25–50 (managed AI Search S1/I2 or PG) | $40–80 (2 app-service small) | $60–150 (mostly free-tier + pay-as-you-go) | $30 (gateway+redis) | **$155–310** |
| Mid firm (25 lawyers) | 10,000 | $150–300 | $200–400 (4 replicas) | $600–1,500 | $120 (gateway+redis+queues+secrets) | **$1,070–2,320** |

Per-review token budget (typical 50-page agreement) ≈ **200–400 K input /
20–50 K output tokens** across extraction, assessment, and drafting
(measured from `LlmRequestTelemetry`, not hypothetical). The free-tier caps of
the primary provider absorb hobby load; the Ollama fallback absorbs the rest
at zero marginal $.

> The cost ceiling is **not** compute — it is abuse. That is why managed rate
> limiting with *spend quotas* is the first item in the gap table, not the
> vector DB.

---

## PART B — IMPLEMENTED MVP

### B.1 — What actually shipped (component map)

| Component | Implementation | Where |
|---|---|---|
| **API host** | ASP.NET Core 9 (Kestrel), static `wwwroot` UI, SSE streaming | `src/DomainCopilot.Api` |
| **Persistence** | PostgreSQL 16 + `pgvector` (HNSW) | `docker-compose.yml`, EF Core, 9 migrations |
| **AuthN/AuthZ** | ASP.NET Identity (cookie, HttpOnly/SameSite=Strict, 8 h sliding), roles `Lawyer` / `Counsel`, policy-based endpoints | `Program.cs`, `ApiAuthorization.cs` |
| **Rate limiting** | **In-process** fixed-window: 10/min `expensive-operations`, 20/min `state-changing`, 60/min `retrieval` | `Program.cs` |
| **Ingestion** | PDF text extract → Tesseract OCR fallback (per-page confidence) → legal text cleaner → clause-aware chunker → persistence | `TesseractPdfOcrService`, `CompositeTextExtractor`, `ClauseAwareChunker` |
| **Embeddings** | Provider-native (Gemini embedding or Ollama `nomic-embed-text`) → batch index into `pgvector` HNSW + GIN full-text | `EmbeddingIndexingService`, `ChunkRetrievalRepository` |
| **Retrieval** | Hybrid: full-text (`ts_rank`) + vector (`<=>`), merged with weights | `ChunkRetrievalService` |
| **Grounded answering** | Citation-enforced `GroundedAnswerService`, streaming SSE, LLM provider abstraction with **fallback chain** (Gemini ↔ Ollama) | `StreamingGroundedAnswerService`, `FallbackLlmProvider` |
| **Multi-agent review** | Clause extractor → risk assessor (against playbook) → memo drafter; live SSE progress; cancellation registry (in-process `ConcurrentDictionary`) | `LegalReviewOrchestrator`, `ClauseExtractorAgent`, `RiskAssessorAgent`, `MemoDrafterAgent` |
| **Approval gate** | `Counsel`-only approve / reject / edit-and-approve; memo evidence links; OpenXML DOCX export | `MemoApprovalService`, `ReviewMemosController` |
| **Trace & telemetry** | Review run trace table + `LlmRequestTelemetry` (input/output/total tokens, `EstimatedCostUsd` from configured $/M prices); correlation-ID middleware; `health/live` + `health/ready` | `ReviewRunTracker`, `LlmRequestTelemetryRecorder`, `CorrelationIdMiddleware` |
| **Evaluation** | 25 golden cases (`E-01…E-25`): grounding, refusal, direct/indirect injection; `POST /api/Evaluation/run` (Counsel only), read-only | `EvaluationHarness`, `GoldenEvaluationCaseCatalog` |
| **Packaging** | Multi-stage Dockerfile (SDK→runtime, Tesseract + Poppler installed), compose with health-gated Postgres, migrations on boot | `Dockerfile`, `docker-compose.yml`, `.dockerignore` |
| **CI** | GitHub Actions: build, format verification, tests, vulnerable-package scan, Gitleaks secret scan | `.github/workflows/ci.yml` |

### B.2 — Decisions already made and why (short form)

The full ADR register is in `docs/adr/` (0001–0008) and expanded in
`docs/ARCHITECTURE.md`. Headline calls that shaped Part B:

| Decision | Alternative rejected | Why |
|---|---|---|
| **Self-hosted `pgvector` now** | managed vector DB | zero marginal $, full SQL, good enough for the corpus sizes here; HNSW index configurable |
| **Provider abstraction + fallback chain** | single provider | availability (Gemini out / quota) must not be a hard failure; telemetry works for both paths |
| **Citation enforced in code** | prompt-only grounding | a prompt can be overridden by untrusted document text (the injection cases prove it); `GroundedAnswerService` *rejects* answers whose citations didn't come from retrieval |
| **In-process fixed-window rate limiter** | managed gateway | 8 h to build, zero ops; see gap G-01 for the honest limits |
| **Synchronous ingestion + SSE for review** | job broker + workers | simplest end-to-end path for a single-user MVP; see gap G-03 |
| **Direct-export DOCX in OpenXML** | a document cloud / template service | zero external dependency, deterministic output |
| **Cookies (Secure on HTTPS, plain on HTTP) with roles** | JWT / BFF pattern | first-class Identity integration, HttpOnly+SameSite=Strict, trivially correct for hosted UI; JWT adds storage/XSS surface without benefit here |

### B.3 — Gap table: target vs. shipped

Legend — **Done** = shipped and working today; **Partial** = shipped with a
recognized limitation; **Missing** = not shipped.

| # | Target component (Part A) | Implemented? | Why deferred / status | Interim mitigation now | Effort & cost to close |
|---|---|---|---|---|---|
| **G-01** | Managed rate limiting with **spend quota** | **Partial** (requests only) | In-process **fixed-window** limiter; no distributed state, no per-tenant or spend-based quota. One mauve replica means the limiter is per-instance, so horizontal scaling silently weakens it | 10/20/60 per-min limits + `429` rejection; abuse *cannot* blow a single request budget, but a fleet can | Gateway in front (APIM/Cloudflare) **≈ 4 h setup + $20–40/month** at small scale; per-tenant JSON in gateway policy. Top operational risk → **do this first** |
| **G-02** | Secrets manager | **Missing** | MVP reads `.env` at startup (`DotNetEnv`) + compose `env_file` | `.env` is gitignored; no secrets in git (Gitleaks in CI guards regressions); `.env.example` documented | Key Vault / Doppler: **≈ 3 h wiring** + negligible $; rotation policy for `LLM_API_KEY` |
| **G-03** | Message broker / async jobs | **Missing** | Ingestion + review run synchronously in the request path (SSE carries progress but the work is in-process) | SSE streaming keeps UX responsive; `CancellationToken` registry allows cancel | Service Bus / Rabbit **≈ 1–2 days** + $0–30/month; decouples long OCR/embedding jobs from replicas |
| **G-04** | Autoscaling | **Missing** | Single Docker host, fixed replicas | API is stateless (cookie auth, DB-backed state) — ready for horizontal scale by construction | ACA/AKS ingress + HPA **≈ 1 day** + compute cost only when scaled |
| **G-05** | Caching (embeddings/retrieval) | **Missing** | No cache layer today; every re-index re-embeds, every retrieval re-runs both indexes | Re-ingest is idempotent (409 on duplicate); tiny corpus makes warm reads cheap | Redis (managed) **≈ 4–6 h** + $15–50/month; embedding result cache first |
| **G-06** | Managed vector DB | **Missing** | Self-hosted `pgvector` on the compose host; no HA/PITR, RAM-bound | HNSW `m=16, ef_construction=64` + GIN full-text; corpus is single-VM-sized today | Azure AI Search S1/I2 **(≈ 4 h migration query parity) + $50–300/month** at the small-firm scale |
| **G-07** | Observability backplane (OTel → metrics/alerting) | **Partial** | Domain telemetry in PostgreSQL (run traces, LLM cost) is excellent; **no** metrics/traces backplane, no alerting | `CorrelationIdMiddleware` + structured console logs (scopes, timestamps); health endpoints /live /ready; cost per request retrievable via `GET /api/Telemetry` | OpenTelemetry → Grafana Cloud free tier **≈ 1 day**; alert on refusal-rate drop + spend spike |
| **G-08** | CI/CD environments | **Partial** (CI) | CI runs build/format/tests/vuln/secret scans on PR & push; **no CD, no environments** | Image is fully self-contained (build → compose up); deploys are manual `docker compose` today | GitHub Actions CD → ACA **≈ half a day**; preview deploys per PR optional |
| **G-09** | DR / backup | **Missing** | Single `pgdata` volume — a disk failure is data loss | Manual `pg_dump` guidance; `corpus/raw` is version-controlled (re-ingestable); no automated backup | PITR (WAL archiving) **≈ 3–4 h + storage** (~$0.05/GB/mo); restore drill documented |
| **G-10** | TLS at the edge | **Partial** | No gateway → the Docker path serves plain HTTP on :8080 (cookie policy is `SameAsRequest`, so it *works*; Secure only over HTTPS) | Dev profile uses HTTPS (secure cookies); prod-grade TLS is a TLS-terminating proxy one hop away | Traefik/Caddy ingress in compose **≈ 1–2 h**, or gateway (G-01) solves it |

### B.4 — Where we cut corners under time pressure (candour)

The reviewer asked for candour, so here it is — the choices that were
expedient and the cost they exacted:

1. **The Docker quick start was untested and broken until this deliverable.**
   Two real bugs shipped: `COPY . .` copied Windows-generated `obj/` (with
   Visual-Studio NuGet paths) into the Linux image, failing the build, and the
   cookie `SecurePolicy.Always` made auth impossible over plain-HTTP Docker.
   Both were found by *actually running the containerized stack* (see
   `scripts/smoke-docker.ps1` — now green 7/7) and fixed in
   `51ea90c`. Lesson: this project ran on IIS Express only; the CLI-first
   path nobody exercised was the one a new reviewer would follow first.
2. **Rate limiting began as a cost-control fiction.** It limits *request
   counts*, not *tokens*, and it is per-instance. It stops a naive flood but
   does not stop a sophisticated spend attack (G-01 is the fix).
3. **`RiskAssessorTimeoutSeconds: 5`** (in `appsettings.json`) is a pragmatic
   clamp — a risk assessor that answers in 5 s is not doing deep legal work;
   it flags *obvious* deviations for human eyes. That trade-off (speed vs
   depth) is intentional and should be re-litigated as models improve.
4. **No document filter on answer requests** is a known retrieval
   limitation (documented in `docs/EVALUATION.md`): questions that need a
   *named* contract can retrieve across several agreements and are then
   correctly refused as ambiguous. Acceptable for the golden set; worth a
   `documentId` filter in API v2.
5. **The health button was removed from the UI** (product decision) and the
   now-dead handler reference in the client was cleaned out.
6. **`GroqProvider` is registered but not wired** into the active fallback
   chain (Gemini ↔ Ollama only). It is dead-ish code — harmless, but it
   should either be wired as a second fallback or removed.

---

## 4. Decisions needing attention before launch (watch-list)

| # | Watch item | Why |
|---|---|---|
| W-1 | Spend-quota rate limiting (G-01) | top operational risk; protects the P&L before anything else |
| W-2 | PITR/Daily backup (G-09) | legal memos are the product's output; losing them is a existential failure |
| W-3 | Refusal-rate alerting (G-07) | the early-warning signal for grounding/injection regressions |
| W-4 | Broker for ingestion (G-03) | removes long jobs from the request path before multi-tenant |
| W-5 | `RiskAssessorTimeoutSeconds` | re-tune per model family; document per-provider quality deltas from the evaluation harness |

---

## 5. Traceability

Each BR requirement maps to shipped/partial components and to the evidence
that proves it (see `docs/BRD.md` for the requirement IDs after the BRD
expansion; current IDs are listed as tracked):

| Requirement | Status | Evidence |
|---|---|---|
| BR-01 Clause extraction ≥90 % section coverage | Implemented (to be confirmed by BRD expansion + evaluation trace) | `ClauseAwareChunker`, `ClauseExtractorAgent`, ingest chunk counts, `E-01…E-08` evaluation cases |
| BR-02 Flag deviations with cited source clause | Implemented | `RiskAssessorAgent` + memos with `ReviewMemoEvidence`; approval desk UI |
| BR-03 No memo finalized without explicit Counsel approval | **Implemented and tested** | `Counsel` authorization policy on approve/reject/edit; `MemoApprovalService` state machine (`ReviewMemo.cs` guards draft-only decisions); 403 for `Lawyer` verified in smoke test |

> The BRD is being expanded (measurable objectives, full requirement set,
> business rules, risks, and the BR→status→evidence traceability matrix) —
> see `docs/BRD.md`.

---

## 6. Appendix — current pricing inputs

`LlmTelemetry__Gemini*CostPerMillionTokensUsd` (`.env`) feed
`EstimatedCostUsd` per LLM call. Current indicative prices (verify against
the provider at review time):

| Provider/Model | Input $/1M | Output $/1M | Notes |
|---|---:|---:|---|
| Gemini 2.5 Flash (indicative) | ~0.30 | ~2.50 | generous free tier; default provider |
| Gemini 2.5 Flash-Lite (indicative) | ~0.10 | ~0.40 | candidate for extraction path at scale |
| Ollama `llama3.1:8b` (local) | $0 | $0 | fallback; needs CPU/GPU host |

Embeddings: `nomic-embed-text` (local, $0) or Gemini embedding API
(indicative ~$0.30–1.25 /1M tokens) — batch-indexed once per document.

---

*Part B's reasoning is intentionally longer than Part A's grand design: the
value of this document is that every deferred component names its risk, its
temporary mitigation, and the exact effort to close it.*