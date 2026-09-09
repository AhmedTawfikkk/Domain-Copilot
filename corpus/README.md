# Corpus Sources

## `dataset/`
Real commercial legal contracts sourced from the **Contract Understanding
Atticus Dataset (CUAD) v1**, a public, expert-annotated dataset of 510+
commercial contracts filed with the U.S. Securities and Exchange Commission
(SEC EDGAR).

- **Source:** https://www.atticusprojectai.org/cuad
- **Direct download:** https://zenodo.org/records/4595826/files/CUAD_v1.zip
- **License:** Creative Commons Attribution 4.0 (CC BY 4.0)
- **Citation:** Hendrycks, D., Burns, C., Chen, A., & Ball, S. (2021).
  "CUAD: An Expert-Annotated NLP Dataset for Legal Contract Review."
  NeurIPS 2021 Datasets and Benchmarks Track.
- A curated random sample of the full dataset (~31 contracts) is included
  under this folder for the project corpus.

## `synthetic/`
15 long-form synthetic legal contracts (NDA, Employment, Lease, Loan,
Franchise, Distribution, etc.) generated via the Groq API to supplement
the real corpus and reach the required document/page count. All parties,
names, and figures are entirely fictitious. Generation script:
`scripts/generate_legal_corpus.py`.

## No real personal data
Neither source contains real personal data. `dataset/` consists of
publicly filed commercial agreements between corporate entities;
`synthetic/` is entirely LLM-generated with fictitious parties.