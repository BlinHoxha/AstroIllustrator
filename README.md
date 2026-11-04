# AstroIllustrator

Local-first astronomy illustration backend using .NET 10, Python/Astropy and SQLite.
The backend renders constellation/asterism charts, observer-dependent horizon views and
scientific diagrams as SVG/PNG with reproducibility manifests. The sibling
AstroIllustrator.Frontend repository contains the functional web client and native scaffold.

Optional **Describe your chart** interpretation uses the sibling AI-Framework structured
generation interface and Azure adapter. It is disabled by default and requires explicit
provider setup; see [AI chart proposals](docs/ai-chart-proposals.md). Keep the updated
AI-Framework checkout beside this repository for backend restore/build.

Current coverage:

- Orion, Ursa Minor, Lyra, Cygnus and Aquila; multiple constellations and Summer Triangle.
- Global WGS84 observer coordinates, UTC or IANA local time with DST ambiguity policies.
- Horizon views with azimuth framing, proper-motion propagation and below-horizon filtering.
- Star altitude, Polaris/latitude, altitude/azimuth and catalog angular-separation diagrams.
- Saved observer presets, paginated job history, output metadata/downloads and catalog discovery.
- English/Albanian labels and Dark Atlas / Print Monochrome styles.

See [backend capabilities and requests](docs/backend-capabilities.md) for endpoints,
examples, scientific assumptions and remaining milestones.

See [the API contract](docs/api-contract.md) for `/openapi/v1.json`, generated client types,
field-specific errors, readiness, actual observation coverage and history scene metadata.

## Architecture

- `src/AstroIllustrator.Core`: infrastructure-free requests, validation, job model and external ports.
- `src/AstroIllustrator.Infrastructure`: Python process adapter and EF Core SQLite metadata/migrations.
- `src/AstroIllustrator.Api`: local HTTP host, composition and ProblemDetails.
- `renderer/astro_renderer`: scientific projection and rendering; no runtime network requests.
- `tests`: Core contracts and API/SQLite/real-Python integration tests.
- `docs/specification-v2.1`: original eight PDFs and documentation README, dated 22 October 2026.

Shared Framework and AI-Framework repositories are unchanged. No reference to their
generic CRUD stack is needed for this slice. Later caption retrieval can reuse AI-Framework.
See [integration decisions](docs/integration-decisions.md).

## Windows setup

Use the .NET SDK specified in `global.json` and Python 3.13. The local workspace SDK
is available at `.tools/dotnet/dotnet.exe`; installing it does not change the system SDK.
For other machines install a compatible .NET 10 SDK, then replace the local executable
path below with `dotnet`.

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r renderer/requirements-lock.txt
# Only needed to regenerate the checked-in, attributed scientific data snapshot:
python tools/prepare_catalog.py
.\.tools\dotnet\dotnet.exe restore AstroIllustrator.sln
.\.tools\dotnet\dotnet.exe test AstroIllustrator.sln
.\.tools\dotnet\dotnet.exe run --project src/AstroIllustrator.Api --urls http://127.0.0.1:5188
```

The host expects the repository root two directories above its content root. Override
`WorkspaceRoot` when deploying; set `Renderer__PythonExecutable` for a different virtual
environment and `Renderer__TimeoutSeconds` for the process deadline (1â€“300 seconds).
It creates `data/astroillustrator.db`, applies migrations and writes `artifacts/{jobId}/`.
Bind to loopback: this milestone is a single-user local tool, with no account/authorization layer.

```powershell
$request = Get-Content examples/orion.json -Raw
$job = Invoke-RestMethod http://127.0.0.1:5188/api/illustrations/render -Method Post -ContentType application/json -Body $request
Invoke-WebRequest "http://127.0.0.1:5188/api/render-jobs/$($job.id)/outputs/svg" -OutFile orion.svg
```

`POST /api/illustrations/render` completes synchronously and returns 201 with the stored
job. `GET /api/render-jobs/{id}` returns job metadata. Downloads use
`/api/render-jobs/{id}/outputs/{svg|png|manifest}`. Invalid requests return 400; renderer
scientifically unsupported requests return 422; renderer failures return 503 and timeouts 504 with a job ID and trace ID. Incomplete jobs are marked
failed on application restart. Start only one host against a workspace database.

Python tests and a standalone rendering command, from `renderer/`:

```powershell
..\.venv\Scripts\python.exe -m unittest discover -s tests -v
Get-Content ../examples/orion.json -Raw | ..\.venv\Scripts\python.exe -m astro_renderer --output-dir ../artifacts/orion-demo
```

## Scientific and release limits

Static constellation charts use J2000 coordinates with TAN projection, north up and east
left. Horizon views transform catalog stars using angular proper motion, offline IERS data,
observer and UTC instant. They assume a geometric horizon and no atmospheric refraction;
stellar parallax, perspective acceleration, terrain and obstructions are not modeled.
Observer computations are bounded to 2000?2030 and reject dates outside bundled IERS coverage.

Scientific diagrams distinguish schematic numeric geometry, ideal NCP/latitude teaching
mode and computed Polaris altitude. Computed star angles cannot be supplied by the caller.
The 8,870-star snapshot is limited to HYG HIP entries with magnitude <=6.5.
Original minimal line conventions are guides, not IAU constellation boundaries.

Automated checks include published reference positions, an Astropy observer benchmark,
projection/angle formula checks and end-to-end API renders. Full independent planetarium
comparison and editorial/print-size approval remain release gates. SVG text references
DejaVu Sans; font embedding, PDF/print production, planetary ephemerides, solar events,
event search, batch publishing and AI remain later work.

The process bridge uses argument lists, cancellation, a timeout, bounded output capture and
one renderer process at a time. It does not enforce an operating-system memory quota. Failed
jobs may leave partial files, which are not served as completed outputs. Determinism is checked
within the pinned environment; scientific geometry hashes are the cross-preset invariant.

See [third-party notices](THIRD_PARTY_NOTICES.md) before redistributing catalog derivatives
or publishing atlas assets. The Python dependency lock is in `renderer/requirements-lock.txt`.
