# AstroIllustrator repository instructions

- Before significant changes, read `docs/context/README.md`, `SESSION_HANDOFF.md` and the
  relevant architecture/science/status documents in that directory. Check current source
  and schema examples; context documents are checkpoints, not substitutes for verification.
- Preserve existing scientific and architectural decisions. Backend/Python owns astronomical
  calculations and authoritative geometry; clients consume typed inputs and artifacts.
- Consider the sibling AstroIllustrator.Frontend contract before changing endpoints, DTOs,
  errors, job semantics or artifacts. Do not silently break schema 2.1 compatibility.
- For API changes, read `docs/api-contract.md`. Export/review `docs/openapi/v1.json`, sync
  and regenerate frontend contracts, and run snapshot/drift checks. Preserve JSON-string job
  fields and distinguish liveness, dependency readiness and per-request scientific support.
- Check Framework and AI-Framework for suitable existing abstractions before duplicating
  reusable functionality. Neither is currently consumed by Astro; avoid incidental adoption,
  unnecessary abstractions or unrelated changes to those repositories.
- Keep implemented, historically tested, source-inspected and proposed capabilities distinct.
  Record actual checks and limitations; do not claim unexecuted tests or native/cloud readiness.
- After meaningful changes, update `docs/context/06_IMPLEMENTATION_STATUS.md`; record new or
  changed decisions in `07_ARCHITECTURAL_DECISIONS.md`. Refresh `SESSION_HANDOFF.md` before
  ending major development sessions and update the concise frontend handoff when relevant.
- Keep canonical cross-repository context here; preserve existing documentation and secrets
  boundaries. Follow the current user's scope rather than treating a roadmap as authorization.
