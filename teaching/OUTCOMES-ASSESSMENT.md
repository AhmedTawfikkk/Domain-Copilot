# Learning Outcomes & Assessment Map

> Maps every lesson outcome to an assessment task and a 3-level rubric
> (Pass / Good / Excellent). Formative checks happen inside `LAB.md`; the
> summative check is the rubric section below (for the trainer).

---

## Outcomes

| ID | Outcome — the trainee can… | Assessment in | Rubric level |
|---|---|---:|---|
| LO-1 | Start the stack with Docker (health-gated, migrations on boot) and create roles correctly | `LAB.md` Part 0 | Core |
| LO-2 | Explain why answers must be grounded in retrieved chunks | Deck slides 4, 10; quiz Q2 | Core |
| LO-3 | Ingest documents via UI and via `seed-corpus.ps1`; interpret `409` duplicates and chunk counts | `LAB.md` Part 1 | Core |
| LO-4 | Distinguish a *grounded* answer (+citations) from a *refused* answer; know both are correct | `LAB.md` Part 2; Challenge ① | Core |
| LO-5 | Run a multi-agent review and read SSE progress events | `LAB.md` Part 3 | Core |
| LO-6 | Demonstrate the approval gate: Lawyer 403 vs Counsel approve, incl. DOCX export | `LAB.md` Part 3; Challenge ⑤ | Core |
| LO-7 | Explain prompt injection, direct vs indirect, and the code-level defence | Deck slide 12; Challenge ① | Good |
| LO-8 | Locate low-confidence OCR chunks and explain the flagging policy | Challenge ② | Good |
| LO-9 | Switch `LLM_PROVIDER`, re-run evaluation, and compare **metrics** across providers | Challenge ③ | Good |
| LO-10 | Trigger and explain the 429 rate limiter; state its request-bound limitation (G-01) | Challenge ④ | Excellent |
| LO-11 | Replay a run trace and read LLM telemetry incl. `EstimatedCostUsd` | Deck slide 17; Part 3 audit step | Good |
| LO-12 | Run the evaluation harness (Counsel) and interpret the 25-case report | Deck slide 18; Challenge ① step 4 | Core |

---

## Quiz (5 questions, open book)

| # | Question | Expected answer |
|---|---|---|
| Q1 | What is the ONE condition under which a memo can be issued? | An approval (or edit-and-approve) decision by a `Counsel` — a machine-drafted memo is always `Draft` until then (BR-08). |
| Q2 | Where is citation validity enforced? | In code — `GroundedAnswerService` / `StreamingGroundedAnswerService` reject citations that did not come from retrieval *before* the UI receives them. |
| Q3 | A child session asks about a contract that was never uploaded. What should the system do? | Refuse (or reply only from what *is* in the retrieval set). Never invent. |
| Q4 | List two defences against prompt injection. | (1) retrieved contract text is treated as **data**, not instructions; (2) citation validation + injection golden cases (E-16…E-21) prove refusal in the harness. Any two consistent with the deck are fine. |
| Q5 | Why is a 10-requests/min limiter NOT a spend limiter? | It counts requests, not tokens — one huge prompt counts once. Managed **spend-quota** limiting is gap G-01. |

---

## Summative rubric

| Band | Requirement |
|---|---|
| **Pass** | LO-1…LO-6 + LO-12 observed; lab rubric ≥ 8 ticks; any failure reproducible and explained |
| **Good** | Pass + at least Challenge ①/② done and correctly interpreted |
| **Excellent** | Pass + Good + Challenges ③–⑤ completed; Q5 answered in own words; provider comparison reported with numbers |

**Trainer tooling:** every claim is checkable — evaluation run output,
`GET /api/Telemetry`, `GET /api/ReviewRuns/{id}`, and the DOCX export are all
evidence the trainee can bring back to their seat.