# One-Pager — The mistakes trainees make (and the fixes)

> Print this. It is the 8 mistakes observed repeatedly in live sessions, each
> with the 30-second fix.

---

**M1 — Opening `localhost:7082` and wondering why nothing works.**
`7082` is the HTTPS dev profile. The Docker quick start is
**http://localhost:8080**. *Fix: use 8080, or launch the dev profile with
`dotnet run` and accept the dev cert.*

**M2 — Registering once (as Lawyer) and getting 403 on everything in the
Approval desk.**
That is the product working. The approval gate is *supposed* to block
Lawyers. *Fix: register the second, Counsel, account (README step 6) — or read
the 403 as the evidence.*

**M3 — Asking an out-of-corpus question and reporting it as "the AI refused =
broken".**
Refusal (E-09…E-15) is a designed behaviour. *Fix: ask a question that the
**indexed** contracts can answer, and verify the answer carries citations.*

**M4 — "Why is my answer missing the loan interest rate?" after seeding with
`-MaxFiles 8`.**
The corpus is partial — you intentionally did not ingest the loan document.
Nothing is blended from invisible contracts. *Fix: seed more files, or ingest
the specific contract first, or accept that Refused is the correct answer.*

**M5 — Ignoring the Docker health gate / not waiting for migrations.**
"Registering" against a half-started API fails weirdly. *Fix: wait for
`{"status":"Healthy"}` from `/health/ready` and for migrations-on-boot to
finish before the UI demo.*

**M6 — Re-running ingest ten times and calling duplicates a bug.**
409 duplicates are the **idempotent-corpus** rule (BR-05). *Fix: read the
status column of the ingest response — `DUPLICATE` is a feature.*

**M7 — Judging "AI quality" from one chat message.**
One answer, one provider, one mood. *Fix: run the evaluation harness
(`POST /api/Evaluation/run`) and compare **metrics** across providers —
Challenges ③ and the 25-case report.*

**M8 — "The LLM hallucinated!" without checking retrieval.**
In this system answers are citation-validated; a hallucinated *answer* should
be near-impossible, but a **wrongly retrieved chunk** is possible. *Fix: click
the citation → read the source chunk → distinguish retrieval error from
generation error. Those are different bugs with different fixes.*

---

*Trainer: hand this out before the lab; review each mistake at the debrief by
asking "which mistake did you almost make today?"*