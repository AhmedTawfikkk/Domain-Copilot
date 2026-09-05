# Business Requirements Document — Domain Copilot (Legal, D1T6)

## Context
An organisation holds a large body of legal contracts. Lawyers spend hours
manually reviewing contracts against internal playbook standards, searching
for risky clauses, and drafting review memos. This system automates that
workflow while keeping a human (Counsel) in control of the final decision.

## Personas
- **Lawyer**: uploads contracts, reviews AI-drafted assessments
- **Counsel**: reviews and approves/rejects the final memo before it is issued

## Objectives (draft — will expand)
- BR-01: System must extract clauses from an uploaded contract with >=90% section coverage
- BR-02: System must flag deviations from the legal playbook with a cited source clause
- BR-03: No memo may be finalized without explicit Counsel approval

## Out of scope (draft)
- Real-time contract negotiation
- Multi-party contract merging

## Assumptions
- Corpus is synthetic/public contracts only, no real client data
