# Implementation status and prioritized remaining work

Original checkpoint: **27 October 2026**, with later updates from 4 November 2026 in the
sections immediately below. This is an evidence inventory, not a release certificate.

## AI budget ledger — 11 November 2026

AST-AI-001 adds a product-owned SQLite reservation ledger and routes chart proposals through
AI-Framework metering. Enabled AI now requires a positive monthly budget and configured model
prices; disabled AI requires none. Budget denial is 429 and other metering denial is 503.
Costs are estimates from reported tokens and operator prices, not a provider billing cap.
The work is uncommitted on its task branch pending independent R2 review. Verification and
remaining limits are recorded in the dated session handoff and coordination task record.

## AI chart proposals — 4 November 2026

Implemented the approved first AI feature across AI-Framework, backend and web. The shared
interface/Azure adapter are separate from retrieval; Astro owns proposal validation and
limits; Studio provides explicit review/apply without automatic rendering. Coordinates/time
remain explicit form inputs. AI is disabled by default; enabling requires a compatible Azure
deployment and credentials. Bedrock structured support, captions, RAG and image generation
remain future work. General Framework and scientific Python geometry are unchanged.
Read [setup, limits and evaluation scope](../ai-chart-proposals.md).

Verification passed: 58 Astro .NET tests, 36 AI-Framework tests, 42 frontend tests and 58
browser checks (50 mocked/eight real API), plus contract/static/format/build checks. Provider
responses are simulated; actual science rendering is exercised separately. Review screenshots
are explicitly fixture-backed. Details are recorded in the session handoff. Live Azure/model quality,
independent scientific/human acceptance and deployment remain separate outstanding checks.

## Repository checkpoint preparation — 4 November 2026

The user requested review, commit and push of both repositories. Fresh checks passed:
44 .NET tests, 25 Python tests, 37 frontend tests, Python dependency consistency,
generated-contract drift, workspace typecheck, lint, formatting, frozen-lockfile validation
and the web production build. Isolated readiness/capabilities/OpenAPI probes also passed.
All 48 browser checks passed (40 mock-backed and eight real-API across both layouts),
and Android/iOS scaffold exports passed; native device/runtime acceptance remains open.
Frontend Git is initialized on `main` against the existing frontend origin; backend keeps
`master`. Ignore rules and explicit contract line endings support safe, reproducible
checkpoints. The October sections below retain their original historical evidence.

## API-contract milestone — 27 October 2026 (latest at the original checkpoint)

The user approved and the implementation now delivers all five API priorities: published
OpenAPI, generated frontend types, coordinate-specific errors, dependency readiness/runtime
capabilities and scene metadata in history. Details and regeneration instructions are in
[the API contract](../api-contract.md) and frontend
[workflow](../../../AstroIllustrator.Frontend/docs/07-api-contract.md).

Fresh results: **29 Core + 15 API/process .NET tests**, **25 Python tests**, **37 frontend
unit/component tests**, and **48 browser checks** (40 mock-backed, eight real-API, both
desktop/narrow). Generated-output/provenance drift, workspace typecheck, zero-warning lint
and production build passed. The final browser run used the production build with an isolated
local proxy; earlier dev-server runs hit Chrome resource/suspended-network errors and are not
passing evidence. Explicit discovery reload was added after the suspend incident exposed a
recovery gap; it retains drafts and never resubmits renders. Numeric field errors keep stable
labels through aria-describedby. The old 42/36 counts below belong to the previous checkpoint.

No database migration, scientific geometry change, shared-framework adoption, native delivery
or deployment was introduced. Backend output was isolated because the user's normal API
binaries were locked. Restart the normal backend/frontend to load the new code. Independent
science/print, manual screen-reader/Albanian editorial acceptance and secured hosting are next.

## Previous: six-area web stabilization review

The user requested verification and fixes across forms/recovery, Studio layout, history,
observers/exports, accessibility/layout/language and milestone closure. Current evidence is in
the [review and screenshot record](../../../AstroIllustrator.Frontend/docs/06-web-stabilization-review.md).
The local functional checks passed: **36 unit/component tests**, **42 browser checks** with
real API enabled (34 mocked, eight real-API), four focused keyboard/cancel rechecks, workspace
typecheck, zero-warning lint, formatting and production build. Additional real pagination,
Albanian geometry-invariance and SVG/PNG preview checks passed. Six screenshots are retained
in frontend `docs/qa/2026-10-27/`. Native/cloud/backend standalone suites were not rerun.

Fixed: cached scene labels beside history IDs; distinct cancelled styling; readable controls
and larger preview; collapsible assumptions with visible warnings; mobile-independent pending/
error/cancel feedback; non-obstructing completion feedback; failure text without error codes;
retained observer edits after failed delete; stale preset save errors; associated field error
descriptions. Backend source and scientific calculations are unchanged. One detail GET per
visible history row is the documented temporary trade-off for absent summary metadata.

The local web stabilization review is complete with explicit remaining limits: human
screen-reader/editorial/usability acceptance, backend coordinate-specific errors, scientific
print/accuracy review, OpenAPI/capability reporting and secured/native delivery. Earlier
verification tables and counts below preserve baseline history; use this section and the
linked review for the current checkpoint.
The original context-preservation task was read-only. A subsequent launcher change added
frontend `scripts/pnpm.cmd` without dependency installation or application behavior changes.
Fresh launcher checks are recorded below; historical app suites were not rerun.

## Launcher update: 26 October 2026

Implemented `scripts/pnpm.cmd` beside the unchanged PowerShell wrapper. It checks for local
Node/pnpm, runs from the repository root and forwards arguments/exit codes with scoped PATH.
Verified from another working directory: pnpm 10.28.2, Node 24.22.1, a quoted argument, exit
code 7 and unchanged caller PATH/location. Vite started on temporary port 5190 and served
HTTP 200; the task-owned server was stopped. Startup required a sandbox-external retry after
esbuild access was denied inside the sandbox. No machine execution policy was changed.

## Evidence legend

Latest fix (27 October): removed the Studio mapping from generic `observer` validation errors
to Latitude. Group errors remain in the error banner; only explicit coordinate field keys
can mark individual inputs. The backend still returns a combined WGS84 error, so the current
response cannot pinpoint Longitude. No client-side astronomy rules or backend changes were
introduced. A new mock-backed browser regression passed on desktop and narrow layouts,
including longitude 181 rejection and successful retry after correction. This does not
constitute a new real-backend validation run or rerun of the historical full browser suite.
Fresh workspace typecheck, zero-warning lint and web build passed. The build needed an
approved sandbox-external retry after esbuild access denial. Browser checks used installed
Google Chrome because the default Playwright headless executable was absent; nothing was
downloaded.

- **S**: actual source inspected in this task.
- **H**: actual execution recorded earlier in the accessible conversation; not rerun now.
- **U**: direct user screenshot/statement, with its limited scope.
- **P**: proposed/discussed only.

“Implemented and verified” below identifies the verification kind. A broader claim such as
production-ready, independently certified scientific accuracy, live cloud operation or native
device acceptance requires evidence that does not currently exist.

## Implemented and verified

| Capability                                                                        | State/evidence                        | Relevant source or record                                                                                      |
| --------------------------------------------------------------------------------- | ------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| Layered .NET/Python backend                                                       | S; real API used historically H       | `src/{AstroIllustrator.Core,AstroIllustrator.Infrastructure,AstroIllustrator.Api}`, `renderer/astro_renderer/` |
| Backend liveness on 5188                                                          | S/H/U; user displayed Healthy         | `Api/Program.cs`, `/health/live` screenshot                                                                    |
| Supported discovery endpoints                                                     | S/H                                   | `Api/Program.cs`; frontend dashboard/catalog                                                                   |
| Schema 2.1 relevant-payload validation                                            | S/H                                   | `Core/IllustrationValidator.cs`, `renderer/astro_renderer/contracts.py`, request examples/client tests         |
| Single ORI/UMI, multiple constellations, Summer Triangle and Horizon rendering    | S/H                                   | `charts.py`; real-API browser journey scenarios                                                                |
| Six supported template/mode diagram combinations                                  | S/H                                   | `diagrams.py`, discovery definitions, real-API scenarios                                                       |
| UTC/local time and DST rejection/selection                                        | S/H                                   | `ObserverTime`, `contracts.py`; DST browser journey                                                            |
| Synchronous lifecycle/error persistence                                           | S/H for exercised cases               | `RenderJobService.cs`, `Persistence.cs`; full lifecycle test source also exists                                |
| SQLite startup migrations/history                                                 | S/H for normal API persistence/reopen | `Persistence.cs`; migration upgrade test source inspected, not freshly executed                                |
| Observer CRUD/duplicate-name errors                                               | S/H                                   | `ObserverPresetEndpoints.cs`, `features/observers.tsx`, browser scenarios                                      |
| SVG/PNG/manifest delivery and SHA-256 verification                                | S/H                                   | `PythonRendererClient.cs`, renderer entry point, real-API artifact checks                                      |
| Web Dashboard/Studio/Catalog/Library/job detail                                   | S/H                                   | `apps/web/src/app.tsx`, `features/`                                                                            |
| Shared client packages and temporary source-derived adapter                       | S/H                                   | `packages/{contracts,api-client,design-tokens,shared-utils}`                                                   |
| Versioned editor draft/reset, locale separation, actual-payload finite validation | S/H                                   | draft schema/request builder, web component/browser checks                                                     |
| Preview zoom/pan/keyboard controls, safe artifact URLs and downloads              | S/H                                   | `features/job-view.tsx`, api-client; tested browser flows                                                      |
| English/Albanian resources and responsive layouts                                 | S/H for tested views                  | `apps/web/src/locales/i18n.ts`, resources/CSS; editorial review pending                                        |
| Client failure/cancel feedback and draft preservation                             | S/H                                   | Studio/API client; mocked error and cancellation tests                                                         |
| Workspace checks and web build                                                    | H                                     | Prior formatting/lint/typecheck/unit/build runs; not executed by this task                                     |
| Android/iOS JavaScript bundle exports                                             | H only for export                     | `apps/mobile`; does not establish runtime/mobile feature completion                                            |

### Exact historical frontend verification record

- **33 unit/component tests**, across five files (client/request builder, draft/feedback,
  domain-neutral helpers).
- **28 Playwright checks** across desktop 1280×900 and narrow 390×844 Chromium projects.
  The set mixed mock-backed UX/failure tests and opt-in real-API tests.
- Real API journeys included **11 supported scene/mode scenarios**, artifact MIME/hash
  checks and downloads, reopen/reload, observer CRUD, and an explicit DST overlap case.
- Frozen offline dependency install, formatting, zero-warning lint, workspace/tool/E2E
  type checking and web build passed earlier.
- Android and iOS Expo exports passed earlier. No emulator/simulator/device execution.
- Ignored QA screenshots exist under frontend `.tools/`; they are local historical evidence.

The integrated tests used an isolated frontend `.tools/integration-workspace`, copied
renderer/catalog inputs, existing backend assembly and existing Python environment on port 5199. They did not use the user's normal database/artifact workspace. Historical task-owned
servers were stopped then; this does not describe which user servers are running now.

Backend .NET/Python test **source** was inspected (Core contracts/lifecycle, API/migrations,
process bridge, Python renderer/extended scenes). The full backend suites were not freshly
executed during this documentation task; no new backend pass count is claimed. Existing
backend validation documents remain records of their own earlier checks.

## Implemented but requiring improvements

| Area                       | Improvement / verification still needed                                                                                                                                 |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Local startup              | `scripts/pnpm.cmd` now avoids unsigned PowerShell execution. Versions, arguments, exit status, caller environment and HTTP startup were verified; README/setup updated. |
| Studio usability           | Preview, controls, feedback and disclosures were improved and locally checked. Fine chart typography and independent usability/print acceptance remain open.            |
| Library/history            | Summary scene metadata now provides labels without per-row reads. Unknown history keeps generic labels/IDs; existing fields remain compatible.                          |
| Manual DTOs                | Generated OpenAPI types and drift checks are now implemented; runtime response validation remains a separate risk-based improvement.                                    |
| Error/capability reporting | ProblemDetails, precise coordinate keys, runtime bounds and readiness are implemented. Readiness is not per-request scientific validity.                                |
| Response validation        | Client has typed casts and output safety checks, not comprehensive runtime response validation. Decide risk-based coverage.                                             |
| Scientific/export quality  | Independent reference/tolerance review, print legibility/fonts, licensing/attribution packaging and Albanian editorial review.                                          |
| Accessibility              | Automated checks and keyboard fixes exist; manual screen-reader/contrast/usability coverage remains.                                                                    |
| Hosting                    | Development proxy works; preview/production routing, TLS, CORS and access control require explicit setup.                                                               |
| Existing documentation     | Older backend README/integration milestones lag frontend reality; this context directory records that discrepancy.                                                      |

## Partially implemented

| Area                  | Existing part                                                            | Missing part                                                                          |
| --------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------- |
| Native app            | Expo tabs/shared wrapper/tokens, compatible packages, bundle exports     | Forms/API/history/drafts/localization/share/location permission and device acceptance |
| Scientific acceptance | Deterministic pipeline, reference-oriented tests, assumptions and hashes | Broad independent external review and declared end-to-end accuracy acceptance         |
| Release automation    | Local checks, frontend CI workflow file; 4 Nov push recorded (see note)  | Backend CI workflow, evidence of hosted CI runs, release/deployment                   |
| Reproducibility       | Pinned snapshots, request/geometry/output hashes, library provenance     | Cross-environment guarantees and accepted publication/print workflow                  |

Note: the 4 November push of both repositories is a historical record from the session
handoff. On 7 November 2026 the local remote-tracking refs `origin/master` (`1b11142`) and
`origin/main` (`4640c56`) pointed at those checkpoint commits as of their last fetch; the
remote itself was not freshly queried.

## Planned but not implemented

- Authentication/authorization, public/test hosting controls, deliberate TLS/CORS/device access.
- Functional mobile MVP, permissions, native share and platform runtime testing.
- Sun/Moon/planet ephemerides, solar time, conjunctions and event discovery.
- Broader AI astronomical interpretation, grounded explanations/captions, scenery generation
  and Bedrock structured support. Only the narrower chart-description proposal/review
  capability, with an Azure OpenAI provider, is implemented locally (4 November, disabled by
  default, not live-verified; see above).
- General Framework references or runtime integration. AI-Framework Core/Azure is
  source-referenced by the chart-proposal feature since 4 November.
- Projects/collections/accounts, atlas/PDF/batch, durable workers/progress/server cancellation.
- AR and other speculative capabilities from the broader vision.

## Blocked or requiring clarification

There is **no access blocker** to this documentation task: all four repositories were available.
These are unresolved choices, not excuses to stop ordinary authorized web work:

- Scene metadata now comes from summaries; per-row reads are removed. User-defined naming/
  projects remain a future product decision.
- Which scientific tolerances/independent reviewer and print acceptance define “ready”?
- Which test hosting/authentication model and intended audience precede external sharing?
- Frontend versioning/publication policy. Its Git repository exists on `main` with `origin`
  (4 November); no package or deployment has been published.
- Native delivery priority and available Android/iOS device/tooling ownership.
- Further AI use cases, the live Azure deployment/model, operator budgets/monitoring and
  recorded live evaluations. The first use case (chart proposals) and first provider (Azure
  OpenAI) were decided on 4 November.
- Ephemeris source and event definitions before planet/conjunction implementation.

Do not label any of these as approved implementation merely because a roadmap was discussed.

## Recommended implementation order

### Priority 1: stabilize the working web MVP

1. **Complete:** frontend `scripts/pnpm.cmd` selects existing local tooling and preserves
   arguments/exit status; startup and setup docs were checked. No execution-policy changes.
2. Manually smoke-test supported scenes/modes with the real API; confirm the distinct UTC,
   local time, DST gap/overlap, empty-sky and scientific rejection behavior.
3. Exercise validation/network/server/cancel states and draft restoration/reset across scenes.
4. Improve Studio preview readability and presentation of lengthy provenance/details.
5. Improve scene identification in history without fabricating absent summary metadata.
6. Confirm observer create/edit/delete/conflict and actual SVG/PNG/manifest downloads.
7. Review desktop/narrow layouts, keyboard/focus/reduced motion and en/sq wording manually.
8. Run relevant regression checks/build, record precise evidence and refresh docs/handoff.

These substeps were recommended in conversation. Step 1 is now implemented and checked.
Steps 2–8 have now received local functional verification and frontend improvements in the
six-area review above. Human screen-reader/editorial/usability and independent science/print
acceptance remain open; do not equate local checks with a production release gate.

### Later priorities

2. **Implemented and verified:** OpenAPI, generated client types/drift checks, precise
   coordinate errors, capabilities/readiness and history metadata. Broader versioning/auth/
   deployment policies remain separate decisions.
3. Independent science, export/print/editorial and accessibility acceptance; run alongside
   contract work rather than postponing scientific correctness until after AI.
4. Secured reachable test environment with deliberate access/TLS/CORS/device networking.
5. Functional native MVP and real-device verification on the stable contract.
6. Backend ephemerides/solar-time foundation, then rigorously defined event discovery.
7. **Implemented locally (4 November), not live-verified:** narrow chart-description
   proposal/review. Grounded explanations, with evaluations, remain.
8. Optional artistic layers and larger atlas/project/PDF/batch/durable-job features as needed.

This is a product recommendation, not a rigid technical dependency graph. AI for supported
scenes can be moved forward after foundations 1–3 by explicit prioritization; mobile and
planetary calculations are not prerequisites for that narrow use case.
