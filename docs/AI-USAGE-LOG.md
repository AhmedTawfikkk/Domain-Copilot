# AI Usage Log



## Day 4 — LLM Provider Abstraction
- AI mistake caught: suggested a Groq model name that had since been
  deprecated. Found via a runtime 404, fixed after verifying the
  current model list with a live search instead of relying on
  outdated training data.
- AI mistake caught: initial environment-variable loading order was
  wrong, causing config values to silently read as empty. Corrected.
- Independent decision: kept Groq over switching to Gemini, documented
  as a reversible choice in ADR-0001.
