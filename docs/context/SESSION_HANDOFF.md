# AstroIllustrator session handoff

## Ollama chart-proposal pilot — 13 November 2026

AST-AI-002 is implemented on `task/AST-AI-002-ollama-pilot` as uncommitted Mode B work,
awaiting independent R2 review. The Development-only Ollama path uses AI-Framework's
registered loopback adapter, zero-price metering and existing proposal validation. The
live Qwen3 1.7B evaluation and complete verification results are in the QA record and
EngineeringCoordination task handoff. Azure behavior and the OpenAPI contract remain
unchanged. Frontend privacy wording needs AST-AI-003 before user-facing release.

## AI budget pilot — 11 November 2026

AST-AI-001 was committed as `f8f5f88`, fast-forwarded into `master` and pushed on
11 November 2026. The
Infrastructure project now references AI-Framework Application. Chart proposals use its
metered structured-generation wrapper and a new product-owned SQLite budget ledger. A
hand-written migration creates `AiUsageEntries`; the EF entity model and OpenAPI snapshot
are unchanged. AI remains disabled by default. Enabled AI fails startup without a positive
monthly budget and explicit model prices. Budget exhaustion returns 429; other metering
denials return 503. The daily request limit, timeout, rate limiter and no-retry policy remain.

Builds use `UseArtifactsOutput=true` and an `ArtifactsPath` under Astro's ignored `.tools`.
Offline restore, both Release test-project builds (zero warnings/errors), focused ledger,
chart and migration tests (20/20), Python renderer tests (25/25), and a full .NET run
(29 Core and 34 API) passed in the executor sandbox. The final post-edit .NET rerun and
repeated concurrency checks are recorded in the coordination task handoff. The owner must
apply the migration to the normal workspace database; the API was never started against it.
No live Azure call, real price or provider-billing reconciliation was performed.

## Current state — 9 November 2026

Backend `master` is at `3a1a0ce238e12735171e104cc6a0e74cfb9c04e6` (`docs: reconcile
AstroIllustrator implementation status`). That docs-only change to
`06_IMPLEMENTATION_STATUS.md` was committed locally on **8 November 2026** and merged locally
on **9 November 2026**. It has not been pushed: local `origin/master` remains at
`1b11142a85a05cdab940f5fe960caec5f186b24e`. Frontend `main` and local `origin/main`
are both at `4640c561566e1527af3d242a4ee8cdfb05615120`; AI-Framework `master` is at
`1918f0e49d360869e72e3714f4acace07c21f25b`. Application code is unchanged since
**4 November 2026**.

Verification run by Claude on **9 November 2026** for EngineeringCoordination task
`AST-PILOT-002`: backend `dotnet test` passed 58/58 (29 Core, 29 API/process, including the
OpenAPI snapshot); Python `unittest` from `renderer` passed 25/25. Frontend
`contracts:check`, lint, `format:check`, typecheck, 42/42 unit/component tests and web build
passed. Browser checks using installed Google Chrome through
`PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` passed 50 mock-backed checks; eight real-API checks
were skipped (`ASTRO_REAL_API` unset). See the frontend handoff for local browser setup.
AI-Framework tests, real-API browser checks, native exports and live Azure were not run on
**9 November 2026**.

Cross-repository tasks are now planned, approved and reviewed through
`C:\Users\blinh\source\repos\EngineeringCoordination` (`tasks/AstroIllustrator/`). This
repository's AGENTS.md and documents remain authoritative for repository work. Older
sections below are historical checkpoints.

First recorded **26 October 2026**; last refreshed **9 November 2026**, Europe/Budapest.
Canonical cross-repository entry point:
[README](README.md). This file is intended for a new session without conversation history.

## Current objective and stopping point

Publication checkpoint, **4 November 2026**: the user explicitly requested Conventional
Commits and origin pushes for AI-Framework, backend and frontend, in dependency order.
The matching AI-Framework revision is `1918f0e49d360869e72e3714f4acace07c21f25b`
(`feat(generation): support strict Azure structured outputs`). Restore that revision or
a compatible descendant alongside this backend. Frontend records its matching backend
revision in its handoff. This is a source checkpoint, not package/deployment publication.

Latest update, **4 November 2026 — AI proposals**: after the repository checkpoint, the user
approved implementing the proposed structured-generation approach. They selected Azure
OpenAI now and asked to allow Bedrock later. AI-Framework now has a separate structured model
interface, schema subset validation, bounded Azure adapter and provider tests. Astro source-
references Core/Azure and owns proposal settings, validation, quotas/timeouts and additive
status/interpretation endpoints. Studio reviews/confirms proposals before applying to the form;
it never auto-renders. Coordinates/time are deliberately entered manually; applying an
observation proposal clears inherited examples. Empty numeric drafts survive reload.

See [AI setup/design](../ai-chart-proposals.md), its evaluation cases and the updated shared
integration/scientific-boundary documents. AI stays disabled by default; no live Azure call
or semantic model evaluation was performed. Current generated OpenAPI/types include the
proposal routes. General Framework, native delivery, Python geometry and SQLite migrations
are unchanged. A sibling updated AI-Framework checkout is now required for backend builds.

Final verification: **58 Astro .NET tests** (29 Core, 29 API/process), **36 AI-Framework tests**
(22 unit/provider, two contract, six architecture, six integration), **42 frontend tests**,
and **58 browser checks** (50 mock-backed, including ten new AI checks, plus eight real API
checks across desktop/narrow). Contract generation/drift, full workspace typecheck, lint,
formatting and web build passed. The final schema-only follow-up verified nullable proposal
enums and description bounds before exporting/regenerating contracts. Shared net9 tests ran
with the local SDK and `DOTNET_ROLL_FORWARD=Major`; Astro hosts the libraries on net10.
Native typechecking passed; native exports/device tests and standalone Python tests were not
rerun for this feature. Scientific Python source is unchanged. Browser report is retained in
frontend `playwright-report/`; mocked review screenshots are under `docs/qa/2026-11-04`.
Isolated verification servers were stopped after testing. This feature spans all three
repositories; the publication checkpoint above records the matching dependency revision.
No new package/deployment was published. Live Azure remains unconfigured.

Initial NuGet restore
was denied in the sandbox and succeeded on approved external retry. One initial existing
missing-star real-Python test returned 503; its focused and subsequent full runs passed.
Its assertion now includes the actual error body to aid future diagnostics; no speculative
renderer/scientific change was made. Frontend strict typing and formatting findings were
corrected before the final checks. Do not claim live/provider/device acceptance from mocks.

Update, **4 November 2026**: the user explicitly requested checking, committing and pushing
both backend and frontend to origin. Frontend Git was initialized on `main` and connected
to the existing empty `https://github.com/BlinHoxha/AstroIllustrator.Frontend.git`; backend
retains `master` and `https://github.com/BlinHoxha/AstroIllustrator.git`. Remote checks
confirmed backend master was still `35f35a9` before the new checkpoint. Local identity is
the existing configured Git author; credentials were not changed.

Fresh pre-commit checks passed: **44 .NET tests** (29 Core, 15 API/process), **25 Python
tests**, **37 frontend unit/component tests**, Python dependency consistency, generated
contract drift, full workspace typecheck, lint, formatting, frozen-lockfile validation
and the web production build. All **48 browser checks** passed (40 mock-backed and eight
real-API, desktop and narrow, 5.5 minutes); Android and iOS scaffold bundle exports passed.
These exports are not native device/runtime acceptance. Isolated runtime probes returned
readiness `Ready`, schema `2.1` capabilities and OpenAPI `3.0.4`.
The initial Python invocation used the wrong working directory and was corrected to
`renderer`; the first sandbox API run had one unexpected 503. Its focused rerun and the
complete suite passed outside the sandbox. Frontend esbuild also required the approved
sandbox-external run. These failed attempts are not passing evidence.

Publication review found no credential patterns, local databases or oversized files in
the publishable set. Ignore rules now cover environment overrides and backend SQLite/test
outputs. Explicit line-ending attributes preserve contract bytes across platforms. Local
toolchains, dependencies, databases, generated artifacts and test reports remain ignored.
The exported OpenAPI snapshot was normalized to LF and frontend contracts/provenance were
regenerated, avoiding a changed schema hash on Linux checkout. Imported reference documents
and checksummed historical fixtures retain their original bytes. Twelve current context/API
documentation files passed local-link checks. The final browser report remains in frontend
`playwright-report/` (ignored).
The test workspace is frontend `.tools/prepush-20261104`, with API 5299 and built SPA 5291;
normal application data is not used. Scientific/human acceptance and production readiness
remain open; passing these checks does not certify them. At that publication checkpoint, no
AI feature had been added; the later 4 November AI proposal update above supersedes this.

Newest checkpoint, **27 October 2026**: the user approved "Strengthen the API contract" and
later asked about checks/next steps while work continued. The five priorities are implemented:
OpenAPI at `/openapi/v1.json`, generated client types/drift checks, field-specific coordinate
errors, `/health/ready` plus `/api/capabilities`, and nullable scene metadata in history.
Read [docs/api-contract.md](../api-contract.md) for exact behavior and regeneration commands.
Requests stay schema 2.1, render remains synchronous 201, job JSON fields remain strings,
observer envelopes/artifacts remain compatible, and no database migration was needed.

Fresh final results: **44 .NET tests** (29 Core, 15 API/process), **25 Python**, **37 frontend
unit/component**, **48 browser checks** (40 mock, eight real API across desktop/narrow), plus
generation drift/typecheck/lint/production build. Added field-label regression: new precise
errors had exposed error text becoming part of the accessible name; labels/errors are now
separate and associated. Real Studio and preset longitude errors mark Longitude correctly.
History labels no longer issue per-row detail requests. Source-example compatibility remains.

Readiness uses a separate `astro_renderer.capabilities` subprocess rather than initializing
plotting/font caches; the initial full-entry-point probe had timed out under test load.
Successful diagnostics cache 30 seconds, failures five, with a 15-second process deadline.
Runtime IERS interval currently reports 2000-01-01 inclusive to 2027-10-04 exclusive UTC;
nominal 2000–2030 does not guarantee that coverage. Each render still validates science.

The user's running API locked normal binaries, so builds/tests used backend
`.tools/contract-build` and frontend `.tools/contract-integration-20261027` for data/catalogs.
The temporary API used port 5299. A dev check server on 5290 hit a Chrome ERR_NO_BUFFER_SPACE
module failure; a subsequent run encountered explicit ERR_NETWORK_IO_SUSPENDED and a long
host pause. Those runs are not passing evidence. Final 48 checks passed against built assets
on local proxy 5291. The suspend incident exposed missing discovery recovery: a new explicit
"Reload supported options" action refetches reads without losing drafts/resubmitting renders.
No normal backend data, Framework/AI-Framework, native features or deployment was changed.
The passing report is retained in frontend `.tools/api-contract-report-20261027/`; task-owned
5290/5291/5299 servers were stopped and confirmed no longer responding at completion.

Dependencies added: API Microsoft.AspNetCore.OpenApi 10.0.13 and frontend development-only
openapi-typescript 7.10.1 with lockfile. Native typecheck passed; device/export checks were
not rerun. Restart the user's normal backend/frontend to load the new code. Next priority:
independent science/print/accessibility/editorial acceptance, then secured shared testing.
Older updates below preserve chronology and do not supersede this checkpoint.

Latest update, **27 October 2026**: the user requested all six web stabilization areas be
checked and findings fixed. This work is complete for local functional verification; read
[the full review](../../../AstroIllustrator.Frontend/docs/06-web-stabilization-review.md).
Fresh results: 36 unit/component tests, 42 full browser checks (34 mock, eight real API),
four focused keyboard/cancel rechecks, typecheck, lint, formatting and production build.
All supported scene/mode renders, invalid input/time/DST, observer workflows and real exports
were checked on desktop/narrow layouts. Further real pagination, en/sq geometry identity and
SVG/PNG preview checks passed. Six screenshots are retained in frontend docs/qa/2026-10-27.

Improvements include cached scene labels beside job IDs, distinct cancelled styling, larger
preview/controls, collapsible assumptions with visible scientific warnings, sticky pending/
error/cancel feedback outside mobile panels, and static completion feedback. Failed-job text
without a code is now shown; failed preset deletion retains edits; edit mode clears stale
save errors; numeric field errors are associated accessibly. Backend source/science and shared
frameworks are unchanged; no dependencies installed. Visible history rows make cached detail
GETs because summaries still lack scene metadata. PLAYWRIGHT_BASE_URL allows isolated tests
on 5190 without touching the user's existing 5173 server.
Task-owned servers on 5190/5199 were stopped and confirmed no longer responding after checks.

Human screen-reader/Albanian editorial/usability acceptance and independent science/print
review remain open. Next candidate is API contract/capability readiness with those reviews
alongside; backend/native/deployment/AI work must still be explicitly scoped. The older
updates below preserve chronology and are not the latest incomplete-task list.

Update, **27 October 2026**: user reported Longitude 181 incorrectly marking Latitude red.
Root cause was Studio's `observer -> observer.latitude` error mapping. Removed that mapping
and added explicit coordinate-field mappings only. Generic backend observer errors remain
visible in the global alert without falsely blaming any one field. Backend validation and
scientific rules are unchanged; precise per-coordinate backend errors are future work.
The new mock-backed regression passed desktop/narrow checks, including correction and retry.
Workspace typecheck, lint and web build passed. Browser checks used installed Chrome; build
passed on an approved sandbox-external retry after esbuild access denial. No dependencies
were installed. The full historical test suites were not rerun.
Continue manual error-recovery testing; do not treat this as a full real-API suite rerun.

The preceding user request asked how to add the `.cmd` launcher. Frontend **`scripts/pnpm.cmd`**
is now implemented alongside `scripts/pnpm.ps1`. It checks both local Node and pnpm, selects
the repository root, forwards arguments/exit status and confines PATH changes to the batch
process. README/development/context documents were updated. No dependencies were installed.

Fresh checks verified pnpm 10.28.2, Node 24.22.1, invocation from another working directory,
a quoted argument, exit code 7, and unchanged caller PATH/location. The initial Vite startup
check hit sandbox access restrictions; the approved retry outside the sandbox started Vite
on temporary port 5190 and served HTTP 200. The task-owned test server was then stopped.
Historical 33/28 test counts below were not rerun for this launcher change.

The preceding request was documentation/context preservation only, supplied in `Pasted text.txt`.
That earlier task created the canonical docs and instructions without application changes.
The working baseline remains functional web plus deterministic backend, native scaffold and
no AI integration. Continue the remaining priority-1 stabilization work; backend/native/AI
and deployment remain separate scope.

### Documentation-task verification

- Created nine canonical Markdown files here, a new backend AGENTS.md and the concise
  frontend handoff; appended context guidance to the existing frontend AGENTS.md.
- Checked all 40 relative Markdown links across the 12 created/updated files: no missing targets.
- Compared SHA-256 snapshots of 69 existing backend files and 192 existing frontend files
  (excluding ignored tools/build/runtime directories and the intentionally updated AGENTS.md):
  no changes. This includes existing source/configuration/reference documentation in the scan.
- Backend Git status shows only the new AGENTS.md and docs/context directory. Framework's
  pre-existing workspace-file modification remains; AI-Framework remains clean.
- Formatted the new Markdown using the already-installed formatter. No dependencies were
  installed and no application test/build/render was run for this task.

## Repositories and authority

| Repository                                              | Checkpoint and observations                                                                                      |
| ------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `C:\Users\blinh\source\repos\AstroIllustrator`          | Backend master `35f35a97e4849f0e6c93294f723761b8422b4e14`; clean before this task                                |
| `C:\Users\blinh\source\repos\AstroIllustrator.Frontend` | Working frontend files, no `.git`/commit/remote at checkpoint                                                    |
| `C:\Users\blinh\source\repos\Framework`                 | Master `c146f854981305aad52d2df638834e3bfa39b2a1`; pre-existing modification `docs/documentation.code-workspace` |
| `C:\Users\blinh\source\repos\AI-Framework`              | Master `d8a3fb6e0a0798664ebff8b63474a962f83cd65a`; clean at inspection                                           |

All repositories were accessible. No backend AGENTS.md existed before this task; one was
added. Existing frontend AGENTS.md was preserved and extended. No applicable ancestor
instructions were found during inspection. Source/examples determine current contracts;
old planning documents do not establish implementation. Backend README's statement that
frontend work has not begun is stale; originals were preserved and this discrepancy recorded.

## Accessible conversation chronology

### 1. Uploaded specification and first-run scope

The user had created `AstroIllustrator.Frontend` and supplied:

- `C:\Users\blinh\Downloads\09_Codex_Frontend_Initialization_Prompt.md`.
- `C:\Users\blinh\Downloads\AstroIllustrator_Frontend_v1_With_Codex_Prompt.zip`.

The frontend directory initially had no application or Git repository. The earlier session
reviewed the eight two-page PDF documents in order, the prototype HTML, backend source and
all ten runnable request examples. The PDFs cover product scope, UX, design, web, mobile,
architecture, API integration and plan/acceptance. Initialization markdown/PDF and prototype
are preserved under frontend `docs/reference/`, together with eight extracted PDF text files.
Archive originals were compared byte-for-byte; the standalone/archive initialization markdown
matched. Text extraction used stdlib ASCII85/Flate handling and is a reading aid, not a perfect
visual/glyph reproduction.

The initialization prompt's first-run step 9 required stopping for approval before scaffolding.
The assistant produced the audit/capability/API/implementation proposal first. The user asked
whether it was okay to proceed and then said **“go on pleasde”**. This approved scaffold/web
MVP with a temporary source-derived adapter and local proxy. Do not repeat an already satisfied
first-run approval gate for routine work in that scope. Separate later scope remains backend
contract changes, native delivery and deployment. The decorative prototype star positions were
explicitly excluded from scientific production geometry.

### 2. Frontend delivery

Implemented a pnpm workspace with React/Vite web, Expo native scaffold and shared contracts,
API client, tokens and neutral helpers. Web pages include Dashboard, Studio, Catalog, Library
with observer management, and persisted job detail. The web consumes actual supported scene
discovery/render/history/artifact APIs. No fake backend star chart or AI output was substituted.

State uses TanStack Query for server data and React Hook Form/local state for versioned editor
drafts and transforms. Relevant-field request building preserves schema 2.1. English/Albanian
UI and separate chart language, safe image previews, zoom/pan, warnings/provenance, actual
downloads and responsive/keyboard behavior were implemented. Native has tabs and tokens only.

Source-derived contracts were necessary because the backend publishes no OpenAPI. Job JSON
strings and observer write/read asymmetry were handled explicitly. No backend application code
was changed to fit UI assumptions. Framework and AI-Framework were not modified or adopted.

Frontend existing records retained: `docs/00-source-audit.md`, `01-capability-matrix.md`,
`02-api-compatibility.md`, `03-implementation-plan.md`, `04-implementation-status.md`,
`05-development.md`, `docs/adr/0001-frontend-architecture.md`, README and AGENTS.md.

### 3. Tooling and defects encountered

| Problem                                                            | Resolution / remaining boundary                                                                                                                                       |
| ------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| System Node 20.14 insufficient; pnpm unavailable                   | Provisioned ignored repo-local Node 24.22.1/pnpm 10.28.2; global Node left intact. Network/native esbuild restrictions required explicit tool escalation at the time. |
| Abort errors cross JS realms                                       | Structural abort-name detection, rather than relying on `instanceof Error`/DOMException identity.                                                                     |
| Windows fixture resolution under jsdom produced wrong root         | Resolve fixtures from process.cwd and use node environment directives for relevant tests.                                                                             |
| Asynchronously loaded select choices disagreed with restored draft | Controlled constellation/diagram/mode/preset values.                                                                                                                  |
| Mobile tabs hid render errors                                      | Error feedback moved above tabs.                                                                                                                                      |
| Invalid hidden numbers from a previous scene blocked a valid scene | Validate finite numbers on the actual built request; keep strict restoration validation for saved drafts.                                                             |
| Ambiguous browser selectors and duplicated warning text            | Use role-based combobox/exact warning selectors appropriate to actual markup.                                                                                         |
| Chromium datetime-local rejected redundant zero seconds in tests   | Use `...02:30`, not redundant `...02:30:00`; no change to time science.                                                                                               |
| Large initial web chunk                                            | Lazy route splitting; largest recorded chunk 363.09 kB minified/116.94 kB gzip. No timing SLA implied.                                                                |
| Accessibility details                                              | Decorative icons hidden from AT; proper roving tab focus/arrows/Home/End and panels; preview keyboard pan/Home and reduced motion.                                    |
| Test process cleanup error                                         | Verified task-owned PID and corrected process termination; does not authorize stopping arbitrary current servers.                                                     |
| Unsigned PowerShell wrapper blocked on user's machine              | Initially used toolchain `pnpm.cmd` after process-local PATH setup; now `scripts/pnpm.cmd` handles this directly without admin or policy changes.                     |

### 4. Historical verification and screenshots

Earlier local execution recorded:

- Frozen/offline install, formatting, lint with zero warnings, all package/app/tool/E2E type
  checks and web production build passed.
- **33 unit/component tests** passed across five files.
- **28 Playwright checks** passed across desktop 1280×900/narrow 390×844. Mock-backed error,
  draft/language/accessibility tests and real-API tests were distinct.
- Real API journeys rendered **11 supported scene/mode scenarios**, checked actual SVG/PNG/
  manifest SHA-256/MIME/downloads, reopened persisted jobs after reload, exercised observer
  CRUD and local-time DST overlap rejection/later selection.
- Android/iOS Expo bundle exports passed; no native device/simulator runtime test occurred.

The integration run used frontend `.tools/integration-workspace`, copied backend renderer/
catalog inputs, the existing API assembly/Python environment and port 5199. Normal backend
database/artifacts were not used. Task-owned test processes were stopped after that run.
Screenshots under `.tools/qa-dashboard-desktop.png`, `qa-studio-desktop.png`,
`qa-dashboard-narrow.png`, `qa-studio-narrow.png` showed the implemented UI with a real Orion
render. These are ignored local artifacts, not published/source-controlled acceptance images.
A focused “skip to content” in a narrow capture was a screenshot state, not an established bug.

This documentation session inspected backend .NET/Python tests and current frontend sources;
it did not rerun the above checks. No new backend full-suite pass count, hosted CI result,
cloud verification, native acceptance or production readiness claim is justified.

### 5. User explanations and local startup

The user asked what was implemented, how the UI should look, for images, what to configure
before startup, what restore/pip-check do, whether to use administrator PowerShell, whether
SQLite needs setup, how to interpret startup logs, what the frontend script does, what to
test, which AI concepts exist and how to prioritize next work.

Key explanations preserved:

- `dotnet restore` restores solution NuGet dependencies; `python -m pip check` checks installed
  environment dependency consistency. It does not install packages or validate astronomy.
- Use **normal, non-admin PowerShell** for ordinary local operation.
- SQLite is embedded; normal startup creates/migrates its file. No database service/account
  or manual schema setup is necessary. Ensure writable data/artifacts/cache and one host per DB.
- API root `/` returns expected 404; `/health/live` is the correct liveness URL. User screenshot
  showed `{ "status": "Healthy" }` on 5188. This proves liveness, not renderer readiness.
- `--no-launch-profile` may display Production environment while still bound only to loopback.
  IDE profile ports are not the explicit command's 5188.
- `scripts/pnpm.ps1 dev` selects local tooling and invokes the workspace dev script. It starts
  the web dev server, not the backend, and the terminal must stay running.
- The user's unsigned-script error was a local PowerShell execution-policy issue. The existing
  toolchain `.cmd` workaround avoids changing that policy. The user then said “nice”; this is
  not evidence that every acceptance workflow was manually completed.
- At this October checkpoint there was no AI integration: supported astronomy was deterministic.
  The later 4 November chart-proposal workflow is described above; scientific geometry remains
  deterministic in the backend.

### 6. Roadmap and launcher location

The recommendation was web stabilization first, API readiness second, independent scientific/
export/accessibility acceptance alongside, then secured test hosting/native work and later
ephemerides/events/limited AI. Priority 1 was broken into startup, real-scene smoke tests,
errors/drafts, Studio readability, meaningful history labels, observer/download checks,
responsive/localized/accessibility review and regression/docs refresh.

The user asked “where to add .cmd launcher?”. The answer was frontend `scripts/pnpm.cmd`
beside `pnpm.ps1`; no launcher was created. That is the concrete next proposed web task,
but it was intentionally not implemented during the documentation-only request.

## Local startup reference

These commands describe the established local setup; verify paths/tool availability before
using them in another environment. They are not commands executed by this documentation task.

### Backend: normal PowerShell, terminal 1

```powershell
Set-Location C:\Users\blinh\source\repos\AstroIllustrator
$env:WorkspaceRoot = $PWD.Path
$env:Renderer__PythonExecutable = Join-Path $PWD.Path '.venv\Scripts\python.exe'
$env:Renderer__TimeoutSeconds = '60'
.\.tools\dotnet\dotnet.exe run --project .\src\AstroIllustrator.Api --no-launch-profile --urls http://127.0.0.1:5188
```

Leave it running. Check `http://127.0.0.1:5188/health/live`. Use a real supported render to
check the Python path/catalog/runtime as well; liveness does not do that. Historical setup
inspection found .NET 10.0.402, Python 3.13.11 in `.venv`, and no broken pip requirements;
those version checks were not repeated during this documentation task.

### Frontend: normal PowerShell, terminal 2

```powershell
Set-Location C:\Users\blinh\source\repos\AstroIllustrator.Frontend
$env:API_PROXY_TARGET = 'http://127.0.0.1:5188'
$env:VITE_API_BASE_URL = ''
.\scripts\pnpm.cmd dev
```

Open `http://127.0.0.1:5173`. Settings above affect the current terminal process, not the whole
machine. Local proxy avoids absent backend CORS. `.env.local` is not required for these commands;
an `.env.example` exists. Do not hardcode a direct backend URL and assume CORS/CSP is configured.
The launcher is now present. Vite preview/production hosting does not
automatically inherit the dev proxy. Credentials are not needed for current local rendering.

## Architecture facts a new session must keep

- Minimal ASP.NET API -> Core validation/lifecycle -> Infrastructure SQLite/Python bridge.
- Schema 2.1, exact enum strings, one payload, synchronous 201; no durable queue/progress API.
- History detail contains request/result JSON strings; observer writes nest fields but reads flatten.
- Requests are normalized before hashing/persistence; do not claim exact original HTTP bytes.
- Outputs are SVG/PNG/manifest with distinct file/request/geometry hashes and scientific provenance.
- Catalog is 8,870 HYG bright HIP stars, five guide constellations and one Summer Triangle.
- Static TAN catalog charts differ from time/observer AltAz; pressure-zero horizon is unrefracted.
- Polaris computed altitude differs from ideal NCP latitude teaching geometry.
- Locked tzdata/IERS, no science auto-download, nominal 2000–2030 still limited by IERS coverage.
- Web displays backend artifacts; native has no working render/data flows.
- At this October checkpoint neither shared framework was referenced by Astro. The later
  chart-proposal feature source-references AI-Framework Core/Azure; general Framework remains
  unreferenced.
- At this October checkpoint there was no auth, deployment, OpenAPI, planets/events, AI,
  PDF/batch/projects or functional mobile MVP. The later API contract and chart-proposal
  milestones above added OpenAPI and a narrow, disabled-by-default AI workflow.

## Immediate recommended continuation

1. Read the 9 November current-state section first, both AGENTS.md files, this handoff and
   [status](06_IMPLEMENTATION_STATUS.md); recheck current files, servers and Git state.
2. Work only on tasks approved through EngineeringCoordination. The `scripts/pnpm.cmd` wrapper
   and API-contract milestone are implemented; consult their dated reviews before repeating
   checks.
3. The narrow AI chart-proposal workflow exists but is disabled by default. Framework or
   AI-Framework changes require their own approved task.
4. Independent science/print/accessibility/editorial acceptance and secured shared testing
   remain documented candidates; select work only after its task is approved.
5. Record exactly which checks ran; update the handoff and other documents as the approved
   task requires.

## Unrecoverable or uncertain information

The available history supports the chronology above but is not a complete archive of every
earlier project conversation. The original backend build/design conversation and any other
Codex sessions are not recoverable from current messages. Repository source/old docs provide
evidence of resulting backend behavior, not a complete record of every original rationale.
No unseen business plan, accepted scientific tolerance, provider choice, customer research,
budget, release date, credentials or production hosting decision is assumed.

The fresh documentation inspection did not re-extract/review every PDF visually or rerun
historical tests. Earlier extraction/review and verification are recorded as historical evidence.
The frontend has been a Git repository on `main` with `origin` since **4 November 2026**;
its current checkpoint can be recovered from Git. There is no demonstrated
hosted CI run, native hardware acceptance, independent full scientific sign-off or complete
user manual acceptance. All four repositories were accessible; missing access is not an issue.

## Suggested next-session prompt

```text
Resume AstroIllustrator. Read both repositories' AGENTS.md and the backend
docs/context/README.md, SESSION_HANDOFF.md and 06_IMPLEMENTATION_STATUS.md.
Read the 9 November current-state section first and verify it against Git and source.
The frontend scripts/pnpm.cmd launcher and API-contract strengthening are complete;
read docs/api-contract.md and frontend docs/07-api-contract.md for the contract.
Work only on a task approved through EngineeringCoordination. Independent
science/print/accessibility/editorial acceptance and secured shared testing remain
candidates, not approved work. Use normal non-admin startup.
Keep scientific calculations in the backend and preserve schema 2.1 contracts.
The narrow AI chart-proposal workflow exists but is disabled by default;
Framework/AI-Framework changes need their own approved task. Report actual checks
and refresh status/handoff as the approved task requires.
```

The prompt is provided for the user to start the next implementation session; it does not
authorize unrelated backend/native/AI/deployment changes.
