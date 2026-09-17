# ADR-0006: Human Approval Gate for Review Memos

- Status: Accepted
- Date: 2026-09-13

## Context

Domain Copilot generates a draft risk memo from extracted contract clauses and
playbook findings. A generated memo must not be treated as final legal output
or used for any side-effecting action without explicit counsel approval.

## Decision

A review memo is persisted initially with `Draft` status.

Counsel must explicitly choose one of the following actions:

1. Approve
2. Reject
3. Edit and approve

Only a memo with `Approved` status may be retrieved through
`GetApprovedForFinalizationAsync`. Future export, final-save, send, or other
side-effecting services must call this approval guard before execution.

## State Transitions

```text
Draft -> Approved
Draft -> Rejected
Draft -> Edited and Approved
```

## Consequences

- The Memo Drafter returns only a proposed memo and source chunk IDs; it cannot
  approve, export, send, or otherwise perform a side effect.
- The orchestrator persists the generated result as `Draft` and preserves the
  generated original when counsel edits and approves revised content.
- A review can complete without creating an irreversible action, while counsel
  retains control over future finalization or delivery.
