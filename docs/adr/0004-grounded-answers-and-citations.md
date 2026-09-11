# ADR-0004: Grounded Answers, Citations, and Refusal

## Status
Accepted

## Context
Legal answers must be traceable to contract evidence. A response without
verifiable source chunks is unsafe and cannot be used by later review agents.

## Decision
- Retrieve evidence before every LLM answer.
- Store the answer prompt as versioned embedded artifact
  `GroundedAnswer.v1.md`, rather than a string inside service code.
- Require the LLM to return strict JSON containing a decision, answer, and
  retrieved chunk IDs.
- Validate every citation ID against the retrieved evidence.
- Return a controlled refusal when there are no chunks, malformed LLM output,
  no citations, or an unknown citation ID.
- Treat all document content as untrusted data to reduce prompt-injection risk.

## Consequences
- Every successful answer has citation-ready chunk provenance.
- Unsupported claims fail closed as refusals.
- Future prompt changes are versioned and reviewable in Git.