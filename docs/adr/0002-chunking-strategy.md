# ADR-0002: Chunking Strategy

## Status
Accepted

## Context
FR-2 requires chunking strategy to be a deliberate, documented decision
justified against the document structure (legal contracts).

## Decision
Clause-aware chunking: text is split on numbered legal headings
(supports nested numbering like "4.1", "4.1.2", and "ARTICLE III"
style headings) rather than fixed-size token windows. Within each
detected clause, content is further split into sub-chunks if it
exceeds a maximum character threshold (2000 chars), to keep individual
chunks small enough for effective retrieval while preserving the
clause as the primary semantic boundary.

Pages with no recognisable heading pattern fall back to a single
whole-page chunk, so no contract text is silently dropped even if the
document uses an unrecognised numbering style.

Extraction is format-specific (PdfPig for PDF, OpenXml for DOCX, plain
read for TXT), composed behind a single ITextExtractor abstraction
selected by file extension (CompositeTextExtractor).

Text cleaning (LegalTextCleaner) runs between extraction and chunking,
normalising line endings and collapsing excess whitespace — this is a
distinct pipeline stage per FR-1 (extract -> clean -> chunk -> embed
-> index).

## Alternatives Considered
- Fixed-size token windows (e.g. 512 tokens per chunk): rejected
  because it routinely splits a single legal clause across chunk
  boundaries, which would damage citation accuracy (FR-2 requires
  citations traceable to the exact chunk) and risk cutting risk-
  relevant clause language in half.
- Using page.Text directly from PdfPig: rejected in favour of
  ContentOrderTextExtractor, per PdfPig's own documentation warning
  that raw page.Text does not reliably preserve reading order.

## Consequences
- Chunking quality depends on the contract using conventional numbered
  headings. Contracts with unconventional formatting fall back to
  whole-page chunks, which are coarser and may reduce retrieval
  precision for those specific documents — to be observed once the
  evaluation harness (Day 11) provides real hit-rate numbers.
- Idempotent re-ingestion is enforced via SHA-256 file hash at the
  Document level, not per-chunk.

## Note on Null `ClauseOrSection` Values

Not all `ClauseOrSection = null` chunks represent chunking failures. Manual
inspection of the corpus (post-ingestion review via direct database query)
revealed three distinct categories:

1. **Genuine numbered clauses missed** due to the heading and its content
   appearing on the same line (e.g. `1.2 Interpretation. Section 10.5...`)
   rather than on separate lines as the original regex assumed — addressed
   by refining `ClauseHeadingPattern` to no longer require the heading to
   end the line.
2. **Document headers and exhibit titles** (e.g. `Exhibit 10.2 Execution
   Version INTELLECTUAL PROPERTY AGREEMENT...`), which correctly have no
   clause number — this is expected, not a defect.
3. **Tabular data** (schedules, trademark/domain name lists) flattened to
   plain text during PDF extraction, which has no natural clause structure
   and is out of scope for regex-based chunking in this MVP.

Categories 2 and 3 are accepted limitations, not chunking bugs. Category 1
was fixed; residual null rates for categories 2–3 will be quantified against
the golden set in `docs/EVALUATION.md` (Day 11).