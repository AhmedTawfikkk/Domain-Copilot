# Architecture — Domain Copilot

> **Status:** current for the shipped MVP (September 2026). Companion to
> `docs/SYSTEM-DESIGN.md` (target architecture + gap analysis). All diagrams
> below are committed **as Mermaid sources** (rendered by GitHub) so the
> diagrams are versioned with the code, per the deliverable requirement
> "diagram source committed, not only images".
>
> ADR register (extended reasons): `docs/adr/0001–0008`.

---

## 1. System context (C4 — Level 1)

```mermaid
flowchart LR
    uLawyer[Lawyer — asks questions<br/>runs legal reviews]
    uCounsel[Counsel — approves memos]
    uAdmin[Operator / Evaluator]

    subgraph DC[Domain Copilot system]
        api[Domain Copilot API<br/>ingest · grounded answers · multi-agent review · approval gate]
    end

    gemini[(Google Gemini API)]
    ollama[(Ollama local<br/>llama3.1:8b + nomic-embed-text)]
    gh[GitHub Actions CI]

    uLawyer -->|HTTPS/SSE| api
    uCounsel -->|HTTPS/SSE| api
    uAdmin -->|container/pipeline| gh
    api -->|provider completions/embeddings| gemini
    api -->|provider completions/embeddings| ollama
    gh -.->|build, test, vuln, secret scan| api
```

**Actors:** `Lawyer` (creates documents, asks, runs reviews, exports),
`Counsel` (approval gate only), `Operator/Evaluator` (deploys, runs the
25-case golden harness). Providers: Gemini (primary), Ollama (local
fallback). CI: GitHub Actions.

---

## 2. Containers (C4 — Level 2)

```mermaid
flowchart TB
    subgraph Browser
        ui[Static SPA<br/>wwwroot: index.html · site.css · app.js]
    end

    subgraph Container[Docker Compose stack]
        api[DomainCopilot.Api — ASP.NET Core 9 / Kestrel<br/>controllers · SSE streaming · static files<br/>port 8080]
        ocr[Tesseract + Poppler<br/>in-image binaries]
        pg[(PostgreSQL 16 + pgvector 0.x<br/>HNSW + GIN · 9 EF migrations)]
    end

    subgraph Providers[LLM providers]
        gemini[Google Gemini API]
        ollama[Ollama server<br/>localhost:11434]
    end

    ui -->|same-origin /api| api
    api --> ocr
    api -->|Npgsql| pg
    api -->|FallbackLlmProvider: gemini-primary<br/>ollama-fallback (or reversed)| gemini
    api -->|FallbackLlmProvider| ollama
```

**Key container decisions** (ADR-referenced): a single API container serves
both the static UI and the JSON/SSE API (`UseDefaultFiles` + controllers);
Postgres runs in its own container with a health gate; OCR binaries live in
the API image (no service boundary needed yet — see G-03 broker gap in
`docs/SYSTEM-DESIGN.md`); the DB is the only stateful component.

---

## 3. Components (C4 — Level 3, inside the API)

```mermaid
flowchart TB
    subgraph Controllers[API layer]
        authC[AuthController]
        ingestC[IngestController]
        embC[EmbeddingsController]
        ansC[AnswersController stream]
        revC[LegalReviewsController stream]
        runsC[ReviewRunsController]
        memosC[ReviewMemosController]
        telC[TelemetryController]
        evalC[EvaluationController]
    end

    subgraph App[Application layer]
        ingestS[DocumentIngestionService<br/>extract → clean → clause chunk]
        embS[EmbeddingIndexingService]
        retrS[ChunkRetrievalService<br/>hybrid vector+full-text]
        gans[StreamingGroundedAnswerService]
        orch[LegalReviewOrchestrator]
        cx[ClauseExtractorAgent]
        ra[RiskAssessorAgent]
        md[MemoDrafterAgent]
        appr[MemoApprovalService]
        evalS[EvaluationHarness · 25 golden cases]
    end

    subgraph Infra[Infrastructure layer]
        ocrS[TesseractPdfOcrService · CompositeTextExtractor]
        prov[ILlmProvider — GeminiProvider / OllamaProvider]
        repo[(Repositories + DbContext)]
        tel[LlmRequestTelemetryRecorder]
        xport[OpenXmlReviewMemoDocxRenderer]
        rl[RateLimiter middleware<br/>10/20/60 per min]
        authz[Identity + role policies<br/>Lawyer / Counsel]
    end

    authC --> authz
    ingestC --> ingestS --> ocrS
    ingestS --> repo
    embC --> embS --> prov
    embS --> repo
    ansC --> gans --> retrS --> repo
    gans --> prov
    revC --> orch
    orch --> cx --> prov
    orch --> ra --> prov
    orch --> md --> prov
    cx --> repo
    md --> repo
    memosC --> appr --> repo
    memosC --> xport
    runsC --> repo
    telC --> tel --> repo
    evalS --> gans
    evalS --> repo
    rl -.-> Controllers
    prov -.-> tel
```

---

## 4. Sequence diagram — full agentic workflow (ingest → answer → review → approval → export)

The approval gate and streaming are first-class; SSE event names below are
the actual wire events (`GroundedAnswerStreamEventType`,
`LegalReviewProgressEventType`).

```mermaid
sequenceDiagram
    autonumber
    actor L as Lawyer (browser)
    participant API as DomainCopilot.Api
    participant DB as Postgres + pgvector
    participant LLM as LLM provider (Gemini/Ollama)

    Note over L,LLM: Ingest
    L->>API: POST /api/Ingest (multipart pdf/docx/txt)
    API->>API: Tesseract/Poppler OCR if scanned
    API->>API: clause chunking
    API->>DB: store Document + Chunks
    API->>LLM: embeddings
    API->>DB: store DocumentChunkEmbeddings (HNSW)
    API-->>L: 200 {status, chunkCount}

    Note over L,LLM: Grounded answer (SSE)
    L->>API: POST /api/Answers/stream
    API-->>L: event: Started
    API->>DB: hybrid retrieval (top-k chunks)
    API->>LLM: system + user prompt (chunks + question)
    loop token stream
        LLM-->>API: tokens
        API-->>L: event: Delta
    end
    API->>API: GroundedAnswerService validates citations
    alt citations valid
        API-->>L: event: Completed {answer, citations}
    else no support (out-of-corpus / ambiguous / injection)
        API-->>L: event: Refused
    end

    Note over L,LLM: Multi-agent review (SSE + cancel)
    L->>API: POST /api/LegalReviews/stream
    API-->>L: event: Started
    API->>DB: load chunks (≤100) + playbook
    API-->>L: event: AgentStarted {ClauseExtractor}
    API->>LLM: extract clauses (batched, retried ≤3)
    API->>DB: persist ReviewAgentStep
    API-->>L: event: AgentCompleted
    API-->>L: event: AgentStarted {RiskAssessor}
    API->>LLM: assess vs playbook (≤5 s)
    API->>DB: persist findings → ReviewMemo
    API-->>L: event: AgentCompleted
    API-->>L: event: AgentStarted {MemoDrafter}
    API->>LLM: draft memo (evidence-linked)
    API->>DB: persist memo (Draft)
    API-->>L: event: AgentCompleted
    API-->>L: event: Completed {memo}
    opt user cancels
        L->>API: POST /api/ReviewRuns/{id}/cancel
        API->>API: CTS.Cancel in cancellation registry
        API-->>L: event: Cancelled
    end

    Note over L,LLM: Approval gate (Counsel) — machine output is not a memo
    actor C as Counsel (browser)
    C->>API: GET /api/ReviewMemos/{id}
    C->>API: POST approve / reject / edit-and-approve
    API->>API: authorize policy Counsel (Lawyer → 403)
    alt Approved / EditAndApproved
        API->>DB: memo Status = Approved
        C->>API: GET /api/ReviewMemos/{id}/export/docx
        API-->>C: OpenXML DOCX download
    else Rejected
        API->>DB: memo Status = Rejected (never final)
    end

    Note over L,LLM: Evaluation (Counsel only, read-only)
    C->>API: POST /api/Evaluation/run
    API->>DB: golden chunks retrieval
    API->>LLM: 25 cases E-01…E-25
    API-->>C: metrics (outcome, hit, groundedness, refusal, injection)
```

---

## 5. Data-flow diagram with trust boundaries

```mermaid
flowchart LR
    subgraph T1[Trust boundary A — Browser/User]
        session[Cookie session<br/>HttpOnly · SameSite=Strict]
        upload[Uploaded contract]
        q[Question text]
        export[DOCX export]
    end

    subgraph T2[Trust boundary B — API host]
        api[Web API + auth + rate limit]
        db[(Postgres<br/>documents, chunks, memos, telemetry)]
        ocr[Tesseract/Poppler]
        prompts[Prompt assembly<br/>system + user prompt]
        cite[Citation validation<br/>GroundedAnswerService]
    end

    subgraph T3[Trust boundary C — LLM provider]
        llm[Gemini / Ollama]
    end

    upload -->|multipart| api
    q -->|JSON| api
    api --> db
    api --> ocr
    db -->|retrieved chunks| api
    api -->|docs, chunks, questions| prompts
    prompts -->|WHAT LEAVES: chunk text + question + system prompt| llm
    llm -->|tokens| cite
    cite -->|validated answer/memo| api
    api -->|memo| export
    session --> api

    classDef leak fill:#ffe0e0,stroke:#c00
    class prompts leak

    Note1["Trust note:
    The only data that cross into the provider boundary are
    retrieved contract chunks and the user question.
    NEVER sent: credentials, secrets, auth cookies, other tenants' docs.
    Chunk text is untrusted (prompt injection) → prompt-injection cases
    E-16…E-21 in the evaluation harness verify refusal behaviour."]
    Note1 -.-> T3
```

**What the LLM provider sees** (explicitly): the system prompt
(`PromptTemplate.Version`), the user's question, and the *retrieved* chunk
text of the document being processed for that request — nothing else.
Injection defense is layered: (1) chunk text is data, not instructions, in
prompt assembly; (2) `GroundedAnswerService` **enforces** that citations come
from retrieval results — the model cannot cite what was not retrieved;
(3) the golden harness verifies refusal under direct/indirect injection.

---

## 6. ER diagram (core domain, key fields)

```mermaid
erDiagram
    DOCUMENT ||--o{ DOCUMENT_CHUNK : contains
    DOCUMENT_CHUNK ||--o{ DOCUMENT_CHUNK_EMBEDDING : embedded_as
    DOCUMENT ||--o{ REVIEW_RUN : reviewed_by
    REVIEW_RUN ||--o{ REVIEW_AGENT_STEP : traces
    REVIEW_RUN ||--o| REVIEW_MEMO : produces
    REVIEW_MEMO ||--o{ REVIEW_MEMO_CITATION : cites
    REVIEW_MEMO ||--o{ REVIEW_MEMO_RISK_FINDING : flags
    APPLICATION_USER ||--o{ REVIEW_MEMO : decides

    APPLICATION_USER {
        guid id PK
        string email UK
        string display_name
        datetime created_at_utc
    }
    DOCUMENT {
        guid id PK
        string file_name
        string source_label
        string status
        decimal extraction_confidence "per-page OCR low-confidence share"
        guid uploaded_by_user_id FK
        datetime created_at_utc
    }
    DOCUMENT_CHUNK {
        guid id PK
        guid document_id FK
        int chunk_index
        text body "tsvector full-text index"
        decimal extraction_confidence
    }
    DOCUMENT_CHUNK_EMBEDDING {
        guid id PK
        guid chunk_id FK
        vector embedding "HNSW index"
        string model
    }
    REVIEW_RUN {
        guid id PK
        guid document_id FK
        string status
        string termination_reason
        datetime started_at_utc
    }
    REVIEW_AGENT_STEP {
        guid id PK
        guid review_run_id FK
        string agent_name
        string status
        int input_item_count
        int output_item_count
        bigint duration_ms
    }
    REVIEW_MEMO {
        guid id PK
        guid review_run_id FK
        string status "Draft/Approved/Rejected"
        guid approved_by_user_id FK
        datetime decided_at_utc
    }
    REVIEW_MEMO_CITATION {
        guid id PK
        guid memo_id FK
        guid chunk_id FK
        string quote
    }
    REVIEW_MEMO_RISK_FINDING {
        guid id PK
        guid memo_id FK
        string clause_type
        string severity
        string finding
    }
    LLM_REQUEST_TELEMETRY {
        bigint id PK
        string provider
        string model
        string operation "embedding/chat"
        int input_tokens
        int output_tokens
        decimal estimated_cost_usd
        bigint latency_ms
    }
```

**Migrated columns are authoritative** (9 EF migrations in
`src/DomainCopilot.Infrastructure/Persistence/Migrations`); the diagram shows
the stable core, with Identity tables (`ApplicationUser`, roles) from the
`AddIdentityAuthentication` migration.

---

## 7. Layer-dependency diagram

```mermaid
flowchart TB
    api[DomainCopilot.Api<br/>controllers · middleware · DI composition]
    inf[DomainCopilot.Infrastructure<br/>Npgsql/pgvector · OCR · providers · exports · Identity]
    app[DomainCopilot.Application<br/>use-case orchestration · agents · prompt templates · policies]
    dom[DomainCopilot.Domain<br/>entities · domain guards · playbook]

    api -->|ProjectReference| inf
    api -->|ProjectReference| app
    inf -->|ProjectReference| app
    app -->|ProjectReference| dom
```

- **Domain**: entities + domain rules (e.g., `ReviewMemo` refuses decisions on
  non-draft memos — the approval gate invariant lives in the domain, not the
  controller).
- **Application**: use cases, the three agents, the grounded-answer service,
  evaluation harness, interfaces (ports).
- **Infrastructure**: adapters — EF/Npgsql, Tesseract/Poppler, LLM providers,
  OpenXML export, Identity storage, telemetry recorder.
- **Api**: composition root, controllers, middleware, streaming endpoints.
- Dependencies point **inward only**; Application never references
  Infrastructure (testability + the reason the Application test project needs
  no database).

---

## 8. ADR register (architecture decision records)

All ADRs: `docs/adr/`.

| # | Decision | One-line rationale |
|---|---|---|
| 0001 | Provider abstraction (`ILlmProvider` + fallback chain) | Gemini outage/quota must degrade to Ollama, not fail the product |
| 0002 | Clause-aware chunking strategy | legal structure (recitals/definitions/obligations) beats fixed-size pieces for both retrieval and review |
| 0003 | Embedding + hybrid retrieval (vector + GIN full-text) | exact-entity queries (clause names/§ numbers) need lexical match; semantic similarity needs vectors |
| 0004 | Grounded answers with enforced citations | untrusted document text must not steer answers; citation validity is enforced in `GroundedAnswerService` |
| 0005 | Pipeline agent orchestration (extractor → assessor → drafter) | one model call cannot both inventory clauses and write a defensible memo; separate roles → separate telemetry and retries |
| 0006 | Human approval gate (`Counsel` policy) | machine-written memos are advice candidates, not legal deliverables; `MemoApprovalService` + role policy enforce it |
| 0007 | OCR export and API security | scanned PDFs need real OCR (not "no text" failures) and the API constrains uploads/export surface |
| 0008 | Identity cookie authentication (HttpOnly, SameSite=Strict, Secure-on-HTTPS) | cookie session + roles gives policy-grade gates with the smallest XSS/storage surface for a same-origin SPA |

---

## 9. Observability and trace flows

```mermaid
flowchart LR
    req[HTTP request] --> corr[CorrelationIdMiddleware]
    corr --> ep[Controller]
    ep --> repo[(Postgres)]
    ep --> prov[LLM provider]
    prov -.->|per call| rec[LlmRequestTelemetryRecorder]
    rec --> tel[(LlmRequestTelemetry<br/>tokens + estimated cost)]
    ep --> hc[Health endpoints<br/>health/live · health/ready]
    corr --> log[Structured console logs<br/>scopes + timestamps]
```

- **Per-run trace**: `ReviewRuns` + `ReviewAgentStep` rows → replayable via
  `GET /api/ReviewRuns/{id}` (see `docs/RUN-TRACE.md`).
- **Per-LLM-call telemetry**: provider, model, operation, tokens, latency,
  `EstimatedCostUsd` when pricing configured → `docs/OBSERVABILITY.md`.
- **Health**: `/health/live` (always ok) and `/health/ready` (DB ping) drive
  the compose health gate and can drive orchestrator probes later.

---

*Diagrams are Mermaid sources in this committed file — regenerate/render
locally with `mmdc` or on GitHub. For the target architecture and the honest
gap analysis, see `docs/SYSTEM-DESIGN.md`.*