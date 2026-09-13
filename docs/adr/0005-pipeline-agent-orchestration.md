# ADR-0005: Pipeline Orchestration for Contract Review

- Status: Accepted
- Date: 2026-09-11

## Context

Domain Copilot requires multiple explicit agents for legal contract review.
The first implemented workflow requires a Clause Extractor and a Risk Assessor.
The system must use typed agent contracts, restricted tool sets, iteration
breakers, and per-step timeouts.

## Decision

Use a sequential pipeline orchestrated by `LegalReviewOrchestrator`:

```text
Document chunks
  -> Clause Extractor
  -> typed ExtractedClause records
  -> Risk Assessor
  -> typed RiskFinding records