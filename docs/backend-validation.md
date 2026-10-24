# Backend expansion verification — renderer 0.2.0

Environment: Windows, local .NET SDK 10.0.402 / runtime 10.0.13, Python 3.13.11,
and the existing pinned scientific dependencies. No new runtime packages were required.

Coverage:

- Core: request family exclusivity, supported catalog/scene/template combinations, numeric
  bounds, global observers, UTC/local time, DST gaps/overlaps, rejected injected angles and
  all ten documented JSON examples. Core assembly dependency boundary remains enforced.
- API: all ten examples rendered through real Python and SQLite; output downloads and
  metadata, paginated/filtered history, discovery, preset CRUD/conflicts/validation and
  strict timestamp parsing. Scientific catalog misses and out-of-IERS dates return 422,
  persist failed jobs and do not expose completed outputs.
- Persistence: upgrade initial schema with a historical job, preserve the row, add observer
  presets/error details, check no model drift and safely repeat migrations.
- Process bridge: forced exit, malformed response and timeout, alongside real successful renders.
- Python: catalog identities, published coordinate/observer benchmarks, analytic TAN and
  angular separation, polar projection, Summer Triangle topology, observer/time dependence,
  cardinal azimuths, horizon clipping, exact numeric ray/arc geometry, stable geometry across
  styles/languages, repeatability and invalid/out-of-coverage rejection.

Generated examples reside in `artifacts/backend-examples/{example-name}/` with SVG, PNG
and manifest. Summer Triangle, Ursa Minor, southern horizon and computed Polaris previews
were visually inspected. Editorial/print review and broader independent references remain
open; this is not a claim of full v2.1 product completion.

Commands from the repository root:

```powershell
$env:DOTNET_ROOT = "$PWD\.tools\dotnet"
.\.tools\dotnet\dotnet.exe test AstroIllustrator.sln --no-restore
Push-Location renderer
..\.venv\Scripts\python.exe -m unittest discover -s tests -v
Pop-Location
```

Passed suite totals: 27 Core tests, 10 API/process/persistence tests and 23 Python tests.
