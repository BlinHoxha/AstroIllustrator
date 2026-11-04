# Backend architecture and integration contract

Checkpoint: 27 October 2026. Primary evidence:
[API registration](../../src/AstroIllustrator.Api/Program.cs),
[models](../../src/AstroIllustrator.Core/Rendering.cs),
[validation](../../src/AstroIllustrator.Core/IllustrationValidator.cs),
[job service](../../src/AstroIllustrator.Core/RenderJobService.cs),
[persistence](../../src/AstroIllustrator.Infrastructure/Persistence.cs), and
[Python bridge](../../src/AstroIllustrator.Infrastructure/PythonRendererClient.cs).

## Solution and dependency direction

`AstroIllustrator.sln` contains Core, Infrastructure and Api plus test projects.
`Directory.Build.props` targets .NET 10, enables nullable/implicit usings and treats warnings
as errors. `global.json` pins SDK `10.0.402` with latest-patch roll-forward.

- **Core**: DTOs, enum/model definitions, validation, observer/time handling, render lifecycle
  and ports. It does not depend on shared Framework or AI-Framework.
- **Infrastructure**: EF Core SQLite persistence and a bounded Python subprocess adapter;
  references Core. EF Core SQLite and Microsoft.Extensions packages are pinned to `10.0.13`.
- **Api**: ASP.NET minimal API composition, endpoint handling, problem responses, strict instant
  conversion, request/rate limits and startup migration/recovery. References Core/Infrastructure
  and Microsoft.AspNetCore.OpenApi 10.0.13. Explicit response models/schema transformers export
  the live contract; see [the API contract](../api-contract.md).
- **renderer/**: Python module with contracts, Astropy calculations, scene construction and
  Matplotlib artifact generation. It is a process boundary, not a frontend dependency.

These are minimal API endpoints, not MVC controller classes. The application does not use
generic Framework CRUD controllers, an AI orchestrator, an LLM SDK or a separate job worker.

## Rendering lifecycle

1. Deserialize strict camelCase JSON. Unknown members and missing required constructor
   values are rejected; enums use their named string values. The instant converter requires
   a UTC/offset designator, rather than silently interpreting a browser-local timestamp.
2. Validate schema, scene, finite numeric ranges, observer/time and mutually exclusive payloads.
   Invalid input returns 400 before a render job is created.
3. Serialize the typed request, compute SHA-256 of that serialization and persist a `Queued`
   job, then `Running`. This is normalized request preservation, not an exact HTTP-body archive.
4. Invoke Python with JSON through stdin and arguments through a process argument list.
   The subprocess owns scientific validation and computation. A semaphore serializes renderer
   access; stdout/stderr are bounded at 64 KiB each, timeout defaults to 60 seconds with allowed
   configuration 1–300 seconds, and cancellation kills the process tree.
5. Validate returned output descriptors, expected names, size limits (32 MiB per artifact)
   and SHA-256 hashes before accepting success.
6. Persist terminal state/result/error and return a synchronous 201 completed job on success.
   Terminal persistence uses a short cancellation-independent save window. Unexpected faults
   can still reach generic server-error handling; do not promise every error has a job ID.

The HTTP concurrency limiter allows two active requests and eight queued requests. This
in-memory admission queue is **not** a durable background job queue. The database lifecycle
states do not imply a submit/poll protocol. No server cancel endpoint or progress stream exists.
On host restart, startup marks interrupted Queued/Running records failed (`host_interrupted`).

## API surface

New contract endpoints: `GET /openapi/v1.json`, `GET /health/ready`, and
`GET /api/capabilities`. The bounded cached diagnostics probe checks installed Python imports,
catalogs/IERS, writable artifact/cache storage and database connectivity without rendering.
It distinguishes process liveness, dependency readiness and per-request scientific validity.
The Python module `astro_renderer.capabilities` does not initialize plotting/font caches.
See the API contract for response DTOs, failure codes, caching and actual coverage semantics.

| Method/path                                              | Current behavior                                                           |
| -------------------------------------------------------- | -------------------------------------------------------------------------- |
| `GET /health/live`                                       | `{ "status": "Healthy" }`; process liveness, not Python readiness          |
| `GET /api/constellations`                                | Supported guide codes/names/HIP edges                                      |
| `GET /api/asterisms`                                     | Asterism definitions, constituent codes, HIP stars and edges               |
| `GET /api/diagram-templates`                             | Template identifiers and allowed modes                                     |
| `GET /api/catalogs`                                      | Catalog metadata, star count and SHA-256; not the entire star array        |
| `GET /api/presets`                                       | Palette identifiers `DarkAtlas`, `PrintMonochrome`                         |
| `GET /api/observer-presets`                              | Saved observer list                                                        |
| `GET /api/observer-presets/{id}`                         | One saved observer, or 404                                                 |
| `POST /api/observer-presets`                             | Nested write body, 201 and Location                                        |
| `PUT /api/observer-presets/{id}`                         | Update, 200; 404 missing, 409 duplicate name                               |
| `DELETE /api/observer-presets/{id}`                      | 204, or 404                                                                |
| `POST /api/astronomy/resolve-time`                       | Inner time-reference body; returns `instantUtc`, 400 on invalid civil time |
| `POST /api/illustrations/render`                         | Synchronous render; 201 completed job and Location                         |
| `GET /api/render-jobs`                                   | Paged history, optional named status filter                                |
| `GET /api/render-jobs/{id}`                              | Full persisted job with JSON strings                                       |
| `GET /api/render-jobs/{id}/outputs`                      | Completed-job descriptors, otherwise 404                                   |
| `GET /api/render-jobs/{id}/outputs/{svg\|png\|manifest}` | Download artifact with MIME type and filename                              |

There is no `/` homepage, Swagger UI, login, job deletion/rename or job-cancel endpoint.
OpenAPI JSON is published at `/openapi/v1.json`. Opening the root still returns 404.

Render errors include 400 validation, 422 scientific rejection, 429 admission limit,
503 renderer unavailable/failure and 504 timeout. ProblemDetails may contain `traceId`,
`code` and `jobId`; clients must also handle empty/non-JSON errors (including 429), network
failure and generic 500 responses. A Retry-After header is not guaranteed.

## Wire details clients must preserve

- Top-level schema `2.1`, `illustrationType` `SkyChart` or `ScientificDiagram`, `language`
  `en`/`sq`, preset string, and exactly one matching `skyChart`/`scientificDiagram` payload.
- Case-sensitive scene/mode identifiers and relevant-field-only serialization. Ten existing
  request examples in `examples/` are runnable compatibility fixtures; they do not enumerate
  every valid user combination.
- Time supports explicit absolute UTC or local-clock/IANA reference with ambiguity policy.
  Client sends local civil time unchanged; backend resolves DST. Legacy `timeUtc` is supported
  on applicable server payloads, but current frontend builds `timeReference`.
- Observer writes are `{ name, observer: { latitude, longitude, elevationMeters, timeZoneId } }`.
  Read DTOs flatten observer fields beside `id`/`name`; never assume symmetric shapes.
- Job detail `requestJson` and nullable `resultJson` are **strings**, parsed at the client
  adapter boundary while preserving raw values for provenance.
- History is `{ items, page, pageSize, totalCount }`, defaults page 1/size 20; bounds page
  1–10000 and size 1–100. Status uses `Queued`, `Running`, `Completed`, `Failed`, `Cancelled`.
  Summary rows now add nullable `scene` metadata derived from stored request JSON, without
  returning the raw request or adding database columns. This is scene metadata, not a user title.
- Output metadata is an array with `format`, `fileName`, `sha256`, relative `downloadUrl`.
  Manifest metadata is a file download, not an extra server-generated explanation endpoint.

## Persistence and local resources

`AstroDbContext` lives in **Infrastructure/Persistence.cs**, not a separate AstroDbContext.cs.
It implements the job store and maps jobs and observer presets. SQLite is an embedded file:
default `data/astroillustrator.db`, with application-owned startup migrations. Initial render-job
migration and the observer-presets migration are retained with a model snapshot. No SQLite
server, credentials or manual table creation is required for normal local use.

Jobs preserve input JSON/hash, created/completed timestamps, status, result JSON and errors.
UTC timestamp history ordering uses parameterized SQL over stored UTC text. Observer names
are trimmed, limited to 100 characters and unique; past jobs keep their embedded observer,
so editing a preset does not rewrite history.

Artifacts are in `artifacts/{jobId:N}/chart.svg`, `chart.png`, `manifest.json`. API identifiers
can use normal hyphenated GUID strings; do not derive local paths from display formatting.
Use one host per database during local testing; provide writable data, artifacts and renderer
cache directories. Workspace root is resolved from API content root unless overridden.

## Security, configuration and dependencies

The current host has no authentication, tenant authorization, CORS policy or production
deployment. Explicit local commands bind loopback; source does not enforce loopback under
all possible host configuration. Resource limits help bound a request but are not OS memory
isolation or production hardening. Python runs offline with bundled catalogs/IERS data and
a `.tools/matplotlib` cache; runtime network downloads are disabled for scientific data.

Use the repository SDK and virtual environment. Startup/environment examples and the unsigned
PowerShell workaround are in [the handoff](SESSION_HANDOFF.md). `--no-launch-profile` can label
the host environment Production; this does not mean the app was deployed publicly. IDE launch
profile ports differ from the explicit 5188 development command.

## Tests and remaining work

Existing Core/API/process-bridge and Python tests cover validation, lifecycle, real render
artifacts, persistence/migration, time/DST and scientific references. Fresh API-contract work
also checks live OpenAPI snapshot drift, precise coordinate errors, real/unavailable readiness,
coverage boundaries and history metadata. The latest run passed 29 Core and 15 API/process
tests; Python passed 25. See [status](06_IMPLEMENTATION_STATUS.md) for the final verification.

OpenAPI, generated frontend types, capability/coverage reporting and field-specific errors
are implemented. Independent scientific/editorial/accessibility review and secured test
hosting remain next priorities. No database migration, authentication or deployment was added.
