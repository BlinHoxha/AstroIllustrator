# AstroIllustrator project context

Canonical cross-repository context, recorded on **26 October 2026**. Start here when
resuming work without the earlier conversation. These documents preserve inspected
implementation and accessible conversation context; they are not a new feature authorization.

Latest, **4 November 2026**: the explicitly approved first AI proposal workflow is implemented
locally across AI-Framework/Astro/web, disabled until Azure configuration. See
[AI chart proposals](../ai-chart-proposals.md) and the newest handoff for current verification.
Update, **13 November 2026**: chart proposals can use a Development-only local Ollama provider (AST-AI-002); Azure OpenAI remains the only provider for other environments. See [AI chart proposals](../ai-chart-proposals.md).
The October entries below preserve earlier milestones.

Latest update, **27 October 2026**: web stabilization and the subsequent API-contract milestone
are implemented and verified. Read [the API contract](../api-contract.md), current status/
handoff and frontend review for fresh checks, screenshots and remaining human/scientific gates.

| Document                                                | Contents                                                              |
| ------------------------------------------------------- | --------------------------------------------------------------------- |
| [Project vision](01_PROJECT_VISION.md)                  | Users, requirements, supported scope and future capabilities          |
| [Backend architecture](02_BACKEND_ARCHITECTURE.md)      | Layers, contracts, endpoints, persistence and process bridge          |
| [Frontend architecture](03_FRONTEND_ARCHITECTURE.md)    | Web pages, native scaffold, packages, state and UX                    |
| [Science and AI](04_AI_AND_SCIENTIFIC_MODEL.md)         | Coordinates, time, accuracy, provenance and proposed AI boundaries    |
| [Shared frameworks](05_SHARED_FRAMEWORK_INTEGRATION.md) | Actual and candidate reuse, dependency boundaries                     |
| [Implementation status](06_IMPLEMENTATION_STATUS.md)    | Evidence, verification limits, missing features and priorities        |
| [Decisions](07_ARCHITECTURAL_DECISIONS.md)              | Rationale, trade-offs and unresolved choices                          |
| [Session handoff](SESSION_HANDOFF.md)                   | Conversation chronology, fixes, startup and continuation instructions |

## Evidence and authority

Update, **4 November 2026**: the user explicitly requested review, commits and pushes of
both repositories. Frontend Git is now initialized on `main`, with `origin` set to the
existing `BlinHoxha/AstroIllustrator.Frontend` repository. Backend remains on `master`
with its existing `BlinHoxha/AstroIllustrator` origin. The original observations below
describe the October baseline; current verification is recorded in the session handoff.

- Backend inspected at `35f35a97e4849f0e6c93294f723761b8422b4e14` on `master`.
- Frontend directory is accessible but has no Git repository/commit at this checkpoint.
- Framework inspected at `c146f854981305aad52d2df638834e3bfa39b2a1` on `master`.
- AI-Framework inspected at `d8a3fb6e0a0798664ebff8b63474a962f83cd65a` on `master`.
- All four repositories were readable. Framework already had an unrelated change to
  `docs/documentation.code-workspace`; preserve it.

Source code, schema 2.1 examples and actual execution evidence determine implementation
status. Earlier planning documents describe intent and may lag implementation. In
particular, the backend root README statement that frontend work has not begun is stale;
some milestones in `docs/integration-decisions.md` also predate the present frontend.
Existing documents are preserved rather than silently rewritten.

“Historical execution” means checks recorded earlier in the accessible conversation,
not rerun during this documentation task. “Source inspected” means code exists and was
read; it does not certify runtime behavior on every machine. User screenshots establish
backend liveness, not full scientific or native acceptance. See the status document for
the exact distinctions and limits.

## Repository map

- Backend: `C:\Users\blinh\source\repos\AstroIllustrator`.
- Frontend: sibling [AstroIllustrator.Frontend](../../../AstroIllustrator.Frontend/README.md).
- Shared libraries: siblings [Framework](../../../Framework/README.md) and
  [AI-Framework](../../../AI-Framework/README.md).
- Frontend-specific entry point:
  [frontend handoff](../../../AstroIllustrator.Frontend/docs/context/SESSION_HANDOFF.md).

Read the repository's `AGENTS.md`, this index, the handoff, and the relevant detailed
documents before significant work. Verify claims against current source before editing.
After meaningful implementation, update status and decisions; refresh the handoff at the
end of major sessions. Keep this directory canonical and the frontend handoff concise.
