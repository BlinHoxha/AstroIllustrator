# First vertical slice — verification record

Historical renderer 0.1.0 record. See `backend-validation.md` for current coverage.

Validated on Windows with local .NET SDK 10.0.402 / runtime 10.0.13 and Python 3.13.11.

- 13 Core tests: supported/unsupported contracts, finite numeric bounds, assembly
  dependencies and successful/failed/cancelled job lifecycle persistence.
- 5 API/process tests: real Python + SQLite + output downloads, invalid/unknown inputs,
  process exit failure, malformed JSON and a forced subprocess timeout.
- 6 Python tests: HIP identity, two SIMBAD reference positions, independent analytic
  TAN calculation, center/orientation, repeated output hashes/style-invariant geometry,
  and rejected nonfinite/unsupported inputs.
- Orion preview generated as SVG, PNG and JSON manifest under `artifacts/orion-demo`.
  Visually inspected; belt labels adjusted with leader lines.

Commands:

```powershell
$env:DOTNET_ROOT = "$PWD\.tools\dotnet"
.\.tools\dotnet\dotnet.exe test AstroIllustrator.sln --no-restore
Push-Location renderer
..\.venv\Scripts\python.exe -m unittest discover -s tests -v
Pop-Location
```

Scientific geometry SHA256 for `examples/orion.json`:
`3934207c796bdf71e9355b764761d90f743e4a58bab47b2a3703bf34734ff465`.
File hashes and dependency versions are recorded in the example manifest. Presets can
change file bytes while preserving this geometry hash.

Remaining gates: full independent reference-chart comparison, remaining catalog/line
review, commercial publication/license review, editorial Albanian text and print-size
legibility. Observer-specific astronomy and AI are outside this milestone.
