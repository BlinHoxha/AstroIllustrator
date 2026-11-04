# API contract — schema 2.1, OpenAPI v1

Updated 27 October 2026. The user explicitly approved strengthening the backend API and
updating its frontend integration. These changes are additive; no database migration is
needed. Scientific geometry and synchronous rendering behavior are preserved.

## Published schema and generated clients

`GET /openapi/v1.json` publishes OpenAPI 3.0.4 from ASP.NET endpoint metadata, explicit
response DTOs and schema/operation transformers. It is available with the normal local
startup, including `--no-launch-profile`. There is no separate Swagger UI installation.
OpenAPI document version `v1` and render request `schemaVersion: "2.1"` are different concepts.

- Backend snapshot: [openapi/v1.json](openapi/v1.json).
- Backend regression: `ApiContractTests.OpenApi_snapshot_matches_live_contract` compares the
  actual hosted schema structurally with the committed snapshot.
- Frontend copy: sibling `AstroIllustrator.Frontend/packages/contracts/openapi.json`.
- Generator: pinned `openapi-typescript@7.10.1`, with deterministic Prettier formatting.
- Output: `packages/contracts/src/generated.ts`; public aliases/refinements remain in `index.ts`.
- Provenance: frontend `openapi-provenance.json` records the exact snapshot SHA-256 and generator.
- Drift check: `pnpm contracts:check` verifies generated output/provenance and, when the backend
  checkout exists, equality with its exported snapshot. Frontend CI runs this check without
  needing a live API or backend checkout. A workflow definition is not evidence of a hosted run.

The public aliases require explicit client choices for defaults and refine the UTC/local-clock
and illustration payload unions. Field types and enum values come from generated schemas.
Manifest metadata is also described; arbitrary scientific/catalog JSON remains `unknown` data,
not invented typed astronomy. Generation is compile-time safety, not complete runtime response
validation. Keep output URL/hash validation and raw JSON provenance handling.

## Compatibility kept

- Existing endpoint paths, schema 2.1 examples, enum spellings and discovery response shapes.
- Synchronous render: 201 completed job with Location; no new durable queue or automatic retries.
- `requestJson` and nullable `resultJson` remain strings. Hashes refer to normalized backend
  serialization, scientific geometry and artifact bytes respectively.
- Nested observer writes and flat observer reads; names/uniqueness behavior is unchanged.
- UTC offsets, local-clock/IANA/DST policy and nominal 2000–2030 observer validation.
- Artifact filenames, MIME types, checksums and download routes.
- Existing migrations/data. Summary scene metadata is derived from persisted requests on read.

The schema documents canonical JSON numbers and timestamps. Historical .NET acceptance of
quoted numeric input is retained, but generated clients send numbers. Server business rules
still validate scene/mode combinations, time zones, catalog identities, finite ranges and
scientific coverage; OpenAPI is not a replacement for that validation.

## New and enhanced endpoints

| Endpoint                | Behavior                                                                                                                           |
| ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `GET /openapi/v1.json`  | Published schema, success/error responses, media types and Location headers                                                        |
| `GET /health/live`      | Unchanged process liveness: 200 `{ "status": "Healthy" }`                                                                          |
| `GET /health/ready`     | 200 Ready or 503 NotReady with `rendererReady`, `databaseReady`, nullable `code`                                                   |
| `GET /api/capabilities` | 200 installed scene/mode/catalog identifiers, languages/styles/formats and observation coverage; 503 ProblemDetails if unavailable |
| `GET /api/render-jobs`  | Existing pagination fields plus nullable `scene` on each summary                                                                   |

### Readiness and coverage

The renderer diagnostic launches `python -m astro_renderer.capabilities`, independently of
plotting. It checks Python imports, catalog definitions, the same offline IERS table/tzdata
used by observer calculations, and artifact/cache writability. It creates no render job or
scientific output. Database connectivity is checked separately on each readiness request.
This is dependency readiness, not a guarantee that any requested scene/date will render.

The subprocess has a 15-second deadline, 64 KiB output/error bounds, process-tree cancellation
and a single probe gate. Success is cached for up to 30 seconds; failure for up to five seconds.
Raw paths/process error text are not returned to clients. Scientific data is not downloaded.

`observationCoverage` includes:

- `nominalStartYear`, `nominalEndYear`: 2000–2030.
- `startUtcInclusive`, `endUtcExclusive`: conservative half-open UTC bounds from the installed
  IERS table, intersected with nominal bounds. The final table row itself is excluded.
- `iersVersion`, `iersSha256`, `timeZoneDataVersion`, `predictionsIncluded`, explanatory `note`.

At this checkpoint the installed IERS version `0.2026.10.19.0.52.1` reports
**2000-01-01T00:00:00Z inclusive to 2027-10-04T00:00:00Z exclusive**. This is runtime-derived,
not a hardcoded UI guarantee. Static J2000 charts and time-free diagrams do not require this
observer interval. `resolve-time` still checks civil-time rules/nominal years only; each render
remains authoritative for scientific support. Predictions may be used and remain identified
in provenance.

### Field-specific errors

Invalid coordinates now return independent keys shared by render and observer-preset validation:

```json
{
  "status": 400,
  "errors": {
    "observer.longitude": [
      "Longitude must be a finite number between -180 and 180 degrees."
    ]
  },
  "traceId": "example-correlation-id"
}
```

Latitude/elevation failures use `observer.latitude`/`observer.elevationMeters`. Several invalid
values produce several issues. A missing observer still uses group key `observer`; do not
map that group key to Latitude. Malformed/missing required JSON can return a general 400
ProblemDetails without field keys. Inputs remain available for correction.

Validation, business errors and empty HTTP failures now use ProblemDetails consistently,
including trace IDs for 400/404/429 and optional `code`/`jobId` on render failures. Clients
must still handle network failures and absent fields. Readiness 503 intentionally uses its
documented health DTO. Retry-After/progress/server cancellation are not newly promised.

### Scene metadata in history

Each summary adds nullable `scene`, with `illustrationType`, `sceneType`, nullable `diagramType`
and `mode`, `constellationCodes`, nullable `asterismCode`, `language`, `preset`. This identifies
the scene, not a user-assigned project/title. It does not expose observer coordinates in the
summary. Unknown/unreadable historical input returns null without breaking history.

The frontend uses this metadata directly and no longer performs a detail GET for every row.
Missing metadata from older hosts falls back to the generic label and ID. Detail is still
fetched when opening a job. Missing diagnostic endpoints (404) are treated as an older host,
not as fake readiness; actual diagnostic failures remain visible. Restart the backend to use
the new endpoints and metadata after updating source.

## Updating/exporting the contract

Use normal PowerShell. The isolated build path avoids locked binaries if the normal API is
running. Stop any test host using that isolated output before rebuilding it.

```powershell
Set-Location C:\Users\blinh\source\repos\AstroIllustrator
.\.tools\dotnet\dotnet.exe restore AstroIllustrator.sln --artifacts-path .tools/contract-build
$env:ASTRO_UPDATE_OPENAPI = '1'
try {
    .\.tools\dotnet\dotnet.exe test tests/AstroIllustrator.Api.Tests --artifacts-path .tools/contract-build --no-restore --filter FullyQualifiedName~ApiContractTests.OpenApi_snapshot
    if ($LASTEXITCODE -ne 0) { throw 'OpenAPI export failed.' }
} finally {
    Remove-Item Env:\ASTRO_UPDATE_OPENAPI -ErrorAction SilentlyContinue
}

Set-Location ..\AstroIllustrator.Frontend
.\scripts\pnpm.cmd contracts:sync
.\scripts\pnpm.cmd contracts:check
.\scripts\pnpm.cmd typecheck
```

Review both snapshot and generated diffs. Run backend tests normally **without**
`ASTRO_UPDATE_OPENAPI`, so drift fails instead of silently rewriting the baseline. When only
regenerating frontend output from its committed snapshot, use `contracts:generate`. Original
byte-preserved request fixtures/provenance are historical compatibility evidence and are not
silently replaced by this process.

## Scope and remaining work

Final executed checks on 27 October: **29 Core + 15 API/process .NET tests**, **25 Python
tests**, **37 frontend unit/component tests**, **48 browser checks** (40 mocked, eight
real-API across desktop/narrow), generated-code/provenance drift, typecheck, zero-warning
lint and production build. The real browser tests include all 11 scene/mode scenarios per
layout, artifacts/hashes, observer workflows, precise coordinate highlighting and DST.

The normal API output was locked by the running host; isolated build/workspace paths were
used. Earlier dev-browser runs encountered ERR_NO_BUFFER_SPACE while loading an application
module, then ERR_NETWORK_IO_SUSPENDED during a long host pause. Those failed runs are not
passing evidence. The final 48-check run passed against the production build with a task-owned
loopback SPA/API proxy on 5291 to isolated API 5299. The temporary helper is under ignored
frontend `.tools/serve-contract-build.mjs`; it is verification infrastructure, not a deployed
production server. Recovery for failed discovery was added explicitly: reload supported
options retains drafts and refetches only reads, never a render submission. All normal
development/test defaults remain unchanged. No device/cloud/hosted-CI checks are claimed.
The passing report is retained locally under frontend `.tools/api-contract-report-20261027/`.
Task-owned servers on 5290/5291/5299 were stopped and confirmed no longer responding at
completion. The user's normal running hosts were left alone.

No auth/CORS/TLS, deployment, native feature delivery, Framework/AI-Framework integration,
AI or astronomy expansion is included. Human/scientific/print acceptance remains open.
The current host remains local-first; document publication is not a production deployment.

Implementation references: API `Program.cs`, `ApiContracts.cs`, `ContractSchemaTransformer.cs`;
Core `ApiCapabilities.cs`/validator; Infrastructure `RendererDiagnostics.cs`/persistence;
Python `capabilities.py`; frontend generated contracts and contract scripts.

Framework/provider dependencies were not added: the new probe and metadata describe this
product's Python renderer/catalogs. API OpenAPI package is `Microsoft.AspNetCore.OpenApi 10.0.13`.

Primary implementation documentation: [ASP.NET Core OpenAPI transformers](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0)
and [OpenAPI TypeScript generation](https://openapi-ts.dev/cli).
