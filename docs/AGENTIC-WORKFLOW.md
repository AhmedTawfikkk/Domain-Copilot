# Agentic Workflow

## Approach: supervised, step-by-step execution
Work follows a pre-agreed day-by-day plan. Within each day, code is
generated in small units, run immediately, and verified before moving
to the next step, rather than generating a large batch of code
upfront. This caught several issues early (see AI-USAGE-LOG.md) that
would likely have surfaced much later otherwise.

## Architectural boundaries enforced on every request
Every code-generation request specifies which Clean Architecture layer
the code belongs in (Domain / Application / Infrastructure / Api), and
the constraint that Domain/Application must not reference any LLM or
vector-store SDK directly.

## Where it fell short
Documentation (ADRs, this file, the usage log) initially fell behind
the code during Day 4 and had to be caught up after the fact.
Corrected from Day 4 onward by writing docs as part of closing out
each day, not as a separate pass afterward.
