# Domain Copilot

**Grounded AI contract review** — upload a contract, ask questions that are answered only from retrieved clauses (with citations), and run a multi-agent legal review whose memos cannot leave the workspace until Counsel approves them.

Domain Copilot is an end-to-end RAG + multi-agent pipeline with an **approval gate** at the point where machine output becomes a legal deliverable. Every LLM call is traced, every review run is replayable, and a 25-case golden evaluation harness verifies that the system refuses what it cannot support — including prompt-injection attacks.

> 🎬 **شرح الفيديوهات / Tutorial videos** — an unlisted Google Drive folder with the product demo and teaching walkthroughs: [Domain Copilot — explanation videos](https://drive.google.com/drive/folders/1gPAw2qaiU8z7ewf7bqRo0jHVChJ3Aa5s?usp=drive_link)

| Stack | |
|---|---|
| **Runtime** | .NET 9 (ASP.NET Core) |
| **Database** | PostgreSQL 16 + `pgvector` (HNSW indexes, full-text + vector hybrid retrieval) |
| **LLM providers** | Google Gemini (default) · Ollama local models · fallback chain, provider-agnostic |
| **OCR** | Tesseract + Poppler for scanned PDFs, per-page confidence tracking |
| **Auth** | ASP.NET Identity, cookie-based, role-based (Lawyer / Counsel) |
| **UX** | Server-rendered static front end with SSE streaming, run traces, export to DOCX |

> **Architecture & design:** see `docs/ARCHITECTURE.md` (C4, sequence diagrams, ER, ADRs) and `docs/SYSTEM-DESIGN.md` (target architecture vs. shipped MVP, with a candid gap table and cost model).

---

## What it does

1. **Ingest** — PDF, DOCX, or TXT. Text PDFs are extracted directly; scanned PDFs fall back to Tesseract OCR with per-page confidence. Documents are cleaned and split into clauses.
2. **Index** — clauses are embedded and stored in `pgvector` (HNSW) alongside a full-text search vector for hybrid retrieval.
3. **Ask (chat)** — questions are answered **only from retrieved clauses**; every answer carries source citations, and anything unsupported is refused explicitly. The chat streams token-by-token over SSE.
4. **Legal review (multi-agent)** — a clause extractor identifies obligations, a risk assessor scores deviations against a legal playbook, and a memo drafter writes a review memo with evidence links. Runs stream live progress and can be cancelled.
5. **Approval gate** — only a Counsel-role user can approve, edit-and-approve, or reject a memo. Approved memos export to DOCX.
6. **Traceability** — every run is persisted with a full trace (agents, statuses, duration, tokens), and every LLM call is recorded with provider, model, operation, token counts, and **estimated cost** when pricing is configured.

**The twist**: the product is designed around *controlled refusal* and the **human approval gate** — it measures when *not* to answer, and it treats "Counsel signs off" as a first-class architectural boundary, not a UI nicety.

---

## Quick start (15 minutes, Docker only)

You need **Docker Desktop** (Compose included). No .NET SDK required.

### 1. Clone and configure

```bash
git clone https://github.com/AhmedTawfikkk/Domain-Copilot.git
cd Domain-Copilot
cp .env.example .env
```

Edit `.env`:

```dotenv
POSTGRES_PASSWORD=change_me          # pick a strong value
LLM_API_KEY=your_gemini_api_key      # free, see "Getting an API key" below
```

### 2. Start the stack

```bash
docker compose up --build -d
```

First build takes a few minutes (image includes Tesseract + Poppler). Watch readiness:

```bash
docker compose ps
# wait until both containers show "healthy" / "running (healthy)"
curl http://localhost:8080/health/ready   # → {"status":"Healthy"}
```

### 3. Open the app

Browse to **http://localhost:8080** and create an account:

- Click **Create account**, choose role **Lawyer**.
- Password must be **12+ characters** with upper case, lower case, a digit, and a symbol (example: `Lawyer#2k26`). Use any email; it need not be real.
- You are signed in automatically.

### 4. Do the 5-minute demo path below

> **Local, non-Docker alternative:** with the .NET 9 SDK installed you can run the API locally against the same PostgreSQL container:
> ```bash
> docker compose up -d postgres
> dotnet run --project src/DomainCopilot.Api --launch-profile https
> # open https://localhost:7082
> ```
> (Local runs need `dotnet user-secrets` or the `.env` values above, plus a trusted HTTPS dev certificate. The Docker path below is the supported quick start.)

---

## Getting an API key (free)

- **Gemini (default provider)** — go to [Google AI Studio](https://aistudio.google.com/) → *Get API key* → create a key (free tier included), copy it into `LLM_API_KEY`. If the key is invalid or the quota is exhausted, the app automatically falls back to a local Ollama model if one is reachable (auto-configured in Docker via `host.docker.internal`; `localhost` when running `dotnet run`).
- **No key at all? Run fully local with Ollama** — install [Ollama](https://ollama.com) and pull the models:
  ```bash
  ollama pull llama3.1:8b      # chat / generation
  ollama pull nomic-embed-text # embeddings
  ```
  Then set `LLM_PROVIDER=ollama` in `.env`. In the Docker stack the API automatically reaches an Ollama installed on your machine (via `OLLAMA_BASE_URL=http://host.docker.internal:11434`); when running `dotnet run` locally, `localhost:11434` is used by default.

---

## 5-Minute Demo Path

Follow this numbered script with the app open at `http://localhost:8080`.

1. **Register** a **Lawyer** account (one-time, ~15 s).
2. **Upload** — in *Workspace*, choose `corpus/raw/synthetic/01_Master_Services_Agreement.pdf`, label it *Demo*, and upload. You'll see the extraction and indexing status (chunk count included).
3. **Grounded answer** — open **Ask about your document** and ask: *"What are the client's payment obligations?"* → you get a streaming answer with **[source clauses]** you can click.
4. **Controlled refusal** — ask: *"What is the airspeed velocity of an unladen swallow?"* → the system **refuses** instead of inventing an answer.
5. **Multi-agent review** — open **Legal review** and press *Run legal review*. Watch live progress: clause extractor → risk assessor → memo drafter. A findings memo appears with evidence links.
6. **Approval gate** — **Sign out**, register a second account with role **Counsel**, sign in, open **Approval desk**, open the memo, and **Approve** it. (A Lawyer cannot approve — HTTP 403.)
7. **Export** — from the memo, *Download DOCX*.
8. **Trace & cost** — open **Run history**: inspect the full run trace and the LLM telemetry (calls, tokens, and estimated cost once pricing is set in `.env` via `LlmTelemetry__Gemini*CostPerMillionTokensUsd`).
9. *(Expert)* **Evaluation harness** — as Counsel, `POST /api/Evaluation/run` (see below).

---

## Environment variables

All values are optional except where noted. **There are two `.env` files:**
the repository-root `.env` (loaded via `env_file` in Docker Compose — the
supported quick start), and `src/DomainCopilot.Api/.env` (loaded by the
`dotnet run` dev profile via `DotNetEnv`). Keep the LLM settings
(`LLM_PROVIDER` / `LLM_API_KEY`) in sync in both; a stale root `.env` makes
the Docker stack fall back to a dead provider while the dev profile still
works.

| Variable | Default | Purpose |
|---|---|---|
| `POSTGRES_PASSWORD` | *(required)* | Database password; Docker Compose fails fast if unset |
| `POSTGRES_USER` | `copilot` | Database user |
| `POSTGRES_DB` | `domain_copilot` | Database name |
| `POSTGRES_CONNECTION_STRING` | `Host=localhost;Port=5432;Database=domain_copilot;Username=copilot;Password=change_me` | Used by the API (Override fully if not using the defaults) |
| `LLM_API_KEY` | *(empty)* | Provider API key (Gemini). Ollama needs no key |
| `LLM_PROVIDER` | `gemini` | Primary provider: `gemini` or `ollama`. The other provider is kept as automatic fallback |
| `OLLAMA_BASE_URL` | `http://host.docker.internal:11434` (Docker) / `localhost:11434` (local dev default) | Where the Ollama fallback lives. Docker auto-reaches an Ollama installed on the host; override if your Ollama runs elsewhere |
| `PdfOcr__TesseractExecutablePath` | *(empty)* | Path to `tesseract` (`.exe` on Windows). Preinstalled inside the Docker image |
| `PdfOcr__PdfToPpmExecutablePath` | *(empty)* | Path to `pdftoppm` (Poppler). Preinstalled inside the Docker image |
| `LlmTelemetry__GeminiInputCostPerMillionTokensUsd` | *(empty)* | Input price per 1M tokens → enables `EstimatedCostUsd` in telemetry |
| `LlmTelemetry__GeminiOutputCostPerMillionTokensUsd` | *(empty)* | Output price per 1M tokens → enables `EstimatedCostUsd` in telemetry |
| `Database__ApplyMigrationsOnStartup` | `false` | `true` in Docker Compose so the container migrates on first boot |

Standard ASP.NET variables (`ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`) apply as usual.

---

## Running tests and the evaluation harness

### Unit / application tests

```bash
dotnet test
```

Two projects: `DomainCopilot.Domain.Tests` and `DomainCopilot.Application.Tests`. CI (`GitHub Actions`) builds, checks formatting drift, runs the tests, scans for vulnerable packages, and runs a Gitleaks secret scan on every push/PR to `main`.

### Evaluation harness (prompt-injection-resistant RAG)

A fixed, versioned golden set of **25 cases** (`E-01`…`E-25`) covering baseline grounded answers, out-of-corpus refusals, ambiguous questions, **direct and indirect prompt injection**, and conflicting sources.

1. Ingest the evaluation corpus (see `docs/EVALUATION.md` for the required set, including `31_Indirect_Prompt_Injection_Test.txt`).
2. Sign in as a **Counsel** account.
3. Run the harness:
   ```bash
   curl -X POST http://localhost:8080/api/Evaluation/run -b cookies.txt
   ```
   Or via **Swagger** (`/swagger` in development) → `Evaluation → POST /api/Evaluation/run`.

The response includes per-case outcomes and aggregate metrics — outcome match rate, answer hit rate, groundedness rate, refusal correctness rate, and prompt-injection resistance rate. The most recent recorded run: **25/25, 100% across all five metrics** — details and methodology in `docs/EVALUATION.md`.

### Seeding the corpus

`scripts/seed-corpus.ps1` bulk-uploads a folder of contracts through the same authenticated endpoint the UI uses:

```bash
powershell -File scripts/seed-corpus.ps1 -Email you@example.com -Password 'Lawyer#2k26' -MaxFiles 5
```

Add `-SkipCertificateValidation` for the local HTTPS dev certificate, and `-FolderPath path\to\contracts` to change the source folder (default: `corpus/raw`). Authenticated sessions are managed by a cookie jar, mirroring the browser.

> **Demo accounts:** the app intentionally has **no pre-seeded users** — you register here. Registration is self-service and idempotent per email; the role choice (Lawyer/Counsel) is made at sign-up and drives the approval gate.

---

## API surface

| Endpoint | Auth | Purpose |
|---|---|---|
| `POST /api/Auth/register` `/login` `/logout`, `GET /api/Auth/me` | — | Cookie-session authentication, Identity role assignment |
| `POST /api/Ingest` | Lawyer | Multipart upload (`file`, `source`) → extract → chunk → index. 409 on duplicates |
| `POST /api/Embeddings/index` | Lawyer | Index pending chunks (manual trigger) |
| `POST /api/Answers/stream` | Lawyer | SSE streaming grounded answer with citations |
| `POST /api/LegalReviews/stream` | Lawyer | SSE multi-agent review run (live progress) |
| `POST /api/ReviewRuns/{runId}/cancel` | Lawyer | Cancel a running review |
| `GET /api/ReviewRuns`, `GET /api/ReviewRuns/{runId}` | Lawyer | Run list and full trace |
| `GET /api/ReviewMemos/{memoId}` | Lawyer | Memo details with evidence |
| `POST /api/ReviewMemos/{memoId}/approve` `/reject` `/edit-and-approve` | **Counsel** | Approval gate (Lawyer → 403) |
| `GET /api/ReviewMemos/{memoId}/export/docx` | Lawyer | OpenXML DOCX export |
| `GET /api/Telemetry` | Lawyer | LLM call traces + estimated cost |
| `POST /api/Evaluation/run` | **Counsel** | Run the 25-case golden harness |
| `GET /health/live`, `GET /health/ready` | — | Liveness / database readiness |

Rate limiting is enforced in-process (fixed window): `expensive-operations` 10 req/min, `state-changing` 20 req/min, `retrieval` 60 req/min — see `docs/SECURITY.md` and the gap analysis in `docs/SYSTEM-DESIGN.md` for the trade-offs.

---

## Repository layout

```
corpus/          Contracts for demos and evaluation (real filings + synthetic)
docs/            BRD, system design, architecture (C4/ADRs/ER), security,
                 evaluation, observability, run traces, AI usage log
scripts/         seed-corpus.ps1 + smoke-docker.ps1 (end-to-end verification)
src/             DomainCopilot.{Domain,Application,Infrastructure,Api}
teaching/        Training deck, hands-on lab + answer key, outcomes/assessment, mistakes guide
tests/           DomainCopilot.{Domain,Application}.Tests
.github/         CI: build + format + tests + vuln scan + Gitleaks
docker-compose.yml · .env.example · Dockerfile
```

Documentation index:

| Document | Contents |
|---|---|
| `docs/BRD.md` | Business requirements, personas, objectives, requirement IDs with acceptance criteria, traceability |
| `docs/SYSTEM-DESIGN.md` | **Part A** target architecture (gateway, managed rate limits, secrets manager, broker, autoscaling, caching, managed vector DB, observability, CI/CD, DR, cost model) · **Part B** shipped MVP with a component gap table and design decisions |
| `docs/ARCHITECTURE.md` | C4 Level 1–3, sequence diagram of the full agentic workflow (approval gate + streaming), data-flow with trust boundaries, ER diagram, layer dependencies, ADRs |
| `docs/SECURITY.md` | Controls, OWASP (Web + LLM) mapping, known limitations |
| `docs/EVALUATION.md` | Golden set, metrics, recorded run, failure analysis |
| `docs/AGENTIC-WORKFLOW.md` | How the agents collaborate (extractor → assessor → drafter) |
| `docs/OBSERVABILITY.md` · `docs/RUN-TRACE.md` | Telemetry and run-trace schema |
| `docs/adr/*` | Eight architecture decision records |
| `docs/AI-USAGE-LOG.md` | How AI tooling was used to build the product |
| `teaching/DECK.md` | 22-slide training deck (design principles → pipeline → approval gate) |
| `teaching/LAB.md` | Hands-on lab with 5 stretch challenges + trainer answer key |
| `teaching/OUTCOMES-ASSESSMENT.md` · `teaching/TRAINEE-MISTAKES.md` | Learning outcomes/quiz rubric and the one-page trainee-mistakes guide |

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `POSTGRES_PASSWORD must be set in .env` | Copy `.env.example` to `.env` and set `POSTGRES_PASSWORD` before `docker compose up` |
| Container is `(unhealthy)` | PostgreSQL is still starting; wait, then `docker compose ps`. Check `docker compose logs postgres` |
| `connection refused` on port 5432 | Postgres container not up — `docker compose up -d postgres` |
| Auth cookie not persisted | The session cookie follows the request scheme (`SameAsRequest`): it is `Secure` over HTTPS (local dev, TLS-terminated deploys) and plain over HTTP — the Docker path at `http://localhost:8080` is tested end-to-end. If you terminate TLS in front of the Docker port, cookies become `Secure` automatically (no config change) |
| Ollama calls time out | Start Ollama (`ollama serve`) and `ollama pull llama3.1:8b nomic-embed-text`. The Docker stack reaches a host-installed Ollama via `OLLAMA_BASE_URL=http://host.docker.internal:11434` (the compose file also maps `host.docker.internal` for Linux hosts). If your Ollama runs elsewhere, point `OLLAMA_BASE_URL` there |
| LLM chain silently dead in Docker while dev works | A stale repository-root `.env` can keep `LLM_PROVIDER`/`LLM_API_KEY` of a different provider than `src/DomainCopilot.Api/.env`. Point both files at the same provider/API key, then recreate the API container (`docker compose up -d --force-recreate api`) |
| `Unable to find ... tesseract` on Windows | Install Tesseract + Poppler bin and set `PdfOcr__*` paths in `.env`, or use the Docker image (both preinstalled) |
| Registration fails | Password must be ≥12 chars with upper, lower, digit, and symbol |
| API returns 429 | You hit a rate-limit window (10/min for LLM-backed ops); wait a minute |
| `docker compose up --build` after schema changes | Migrations run on container start (`Database__ApplyMigrationsOnStartup=true`); on local `dotnet run`, execute `dotnet ef database update` if the schema lags the code |
| HTTPS certificate warning in local dev | `dotnet dev-certs https --trust` once |

---

## Docs, demos, and evaluation

- Live deployment … *see the deployment section of `docs/SYSTEM-DESIGN.md` for the free-tier target and the CI/CD notes.*

**License:** not yet licensed — see repository for notices.