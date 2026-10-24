# Backend coverage — renderer 0.2.0

The three-production-project modular monolith is preserved. Core has no project or NuGet
references. Python owns scientific calculations. Neither shared framework is modified.
No frontend, extra service, broker, generic repository or Unit of Work has been added.

## Discovery and management API

| Method and path | Behavior |
| --- | --- |
| `GET /health/live` | Host liveness |
| `GET /api/constellations` | ORI, UMI, LYR, CYG, AQL and their line conventions |
| `GET /api/asterisms` | SummerTriangle, its three HIP identities and edges |
| `GET /api/diagram-templates` | Supported diagram codes and modes |
| `GET /api/catalogs` | Catalog provenance, license, star count and snapshot checksum |
| `GET /api/presets` | DarkAtlas and PrintMonochrome |
| `GET /api/observer-presets` | Saved observers, ordered by name |
| `GET /api/observer-presets/{id}` | One saved observer |
| `POST /api/observer-presets` | Save validated name and WGS84 observer; duplicate name returns 409 |
| `PUT /api/observer-presets/{id}` | Update saved observer |
| `DELETE /api/observer-presets/{id}` | Delete preset; existing job requests are unchanged |
| `POST /api/astronomy/resolve-time` | Resolve AbsoluteUtc or LocalClock to a concrete UTC instant |
| `POST /api/illustrations/render` | Validate, render synchronously, persist job, return 201 |
| `GET /api/render-jobs?page=1&pageSize=20&status=Completed` | Newest first; optional named status filter; page size 1–100 |
| `GET /api/render-jobs/{id}` | Request, result, status and failure details |
| `GET /api/render-jobs/{id}/outputs` | Output hashes, formats and download URLs |
| `GET /api/render-jobs/{id}/outputs/{svg\|png\|manifest}` | Download completed artifacts |

History pages contain summaries rather than full request/result JSON. Detail rows retain the
original request. Rendered manifests retain computed geometry, resolved UTC and scientific
assumptions. There is no durable background queue: POST waits for rendering; caller cancellation
cancels its job. Run one host against a metadata database. This is a local single-user backend.

## Request families

Keep `schemaVersion="2.1"`, `language="en"|"sq"` and `preset="DarkAtlas"|"PrintMonochrome"`.
Supply exactly one payload matching `illustrationType`. Unknown fields are rejected.

| Family / scene | Inputs | Runnable example |
| --- | --- | --- |
| SkyChart / SingleConstellation | One `constellationCode`; omitted selector preserves Orion default | `examples/orion.json`, `examples/ursa-minor.json` |
| SkyChart / MultipleConstellations | Two to five distinct `constellationCodes`; optional SummerTriangle overlay | `examples/multiple-constellations.json` |
| SkyChart / Asterism | `asterismCode=SummerTriangle`; omit constellation selectors | `examples/summer-triangle.json` |
| SkyChart / HorizonView | Observer, time, AltitudeAzimuth projection, azimuth center and field of view | `examples/southern-horizon.json` |
| ScientificDiagram / StarAltitude / Schematic | `parameters.altitudeDegrees` | `examples/altitude-45.json` |
| ScientificDiagram / AltitudeAzimuth / Schematic | Altitude and clockwise-from-north azimuth | `examples/altitude-azimuth.json` |
| ScientificDiagram / PolarisLatitude / Schematic | Observer latitude; ray represents the ideal NCP, not exact Polaris | `examples/polaris-latitude.json` |
| ScientificDiagram / StarAltitude or PolarisLatitude / Computed | `starId=Polaris`, observer and time; supplied angles rejected | `examples/polaris-computed.json` |
| ScientificDiagram / AngularSeparation / Computed | Two distinct bundled `starHipIds`; static J2000 separation | `examples/angular-separation.json` |

Static charts require `projection=Gnomonic`, have automatic spherical centering/framing and
cannot accept observer/time fields. Very distant constellation combinations that cannot fit a
bounded TAN projection return 422. Duplicate star IDs are never introduced by scene merging.

Horizon scenes without selectors use all bright catalog stars. Optional constellation selectors
restrict the catalog subset. Magnitude limit is 2–6.5, field of view 10–180°, center azimuth [0,360).
The rectilinear azimuth/altitude view is explicitly not an equal-angle projection of the sphere.
Stars below the geometric horizon are omitted; counts and warnings are stored in the manifest.

## Observer and time

Observers have latitude [-90,90], longitude [-180,180], elevation [-500,10000] metres and an
optional IANA `timeZoneId`. Preset bodies use `{ "name": "Home", "observer": { ... } }`.
Render requests embed coordinates so later preset changes cannot alter an existing request.

Supply exactly one of:

```json
{ "timeUtc": "2026-07-15T21:00:00Z" }
```

```json
{ "timeReference": { "type": "AbsoluteUtc", "instantUtc": "2026-07-15T21:00:00Z" } }
```

```json
{ "timeReference": { "type": "LocalClock", "localDateTime": "2026-07-15T23:00:00",
  "timeZoneId": "Europe/Belgrade", "ambiguityPolicy": "Reject" } }
```

Absolute instants require Z or an explicit offset. LocalClock must omit an offset. DST gaps
are rejected. Overlaps require `Earlier` or `Later`, otherwise they are rejected. The
resolve-time endpoint accepts the inner TimeReference object. .NET validates civil-time
input; Python independently resolves it using the locked tzdata package, whose version and
resolved UTC are stored in observer manifests. UTC input avoids OS/tzdb rule differences.

Dates must fall within 2000–2030 and within the bundled Earth-orientation table. The latter
coverage is narrower and is enforced in Python; there is no silent extrapolation or network
refresh. Predicted IERS entries are labeled. Solar-relative times are not implemented yet.

## Scientific assumptions and compatibility

HYG v4.1 is interpreted as ICRS J2000. Static charts retain that epoch. Observer views apply
catalog angular proper motion and use Astropy/ERFA AltAz with WGS84 and pressure=0 hPa.
Stellar parallax, perspective acceleration, horizon dip, terrain, atmospheric refraction and
visibility brightness models are not included. Elevation enters the observer position.
No planetary or lunar positions are supplied by this release.

Schematic diagrams are visibly labeled. Their measured arcs, labels and rays share the same
numeric value. Polaris teaching mode uses ideal NCP altitude=latitude; computed mode derives
Polaris altitude/azimuth at the requested instant and warns when below the horizon. Angular
separation diagrams illustrate the catalog-derived angle on two rays, rather than claiming
to be sky projections. Language/style changes preserve scientific geometry hashes.

The new migration adds observer presets and error messages while preserving existing jobs.
The previously shipped initial migration identifier is retained; targeted tooling should use
EF's resolved migration names. A test applies the initial schema, inserts a historical job,
upgrades, verifies preservation/model consistency and repeats migration safely.
Renderer 0.2.0 uses generalized framing, so its static chart geometry hashes differ from 0.1.0.
Keep existing immutable artifacts for exact reproduction of earlier renderer output.

HTTP 400 indicates invalid contracts; 422 indicates scientific/catalog/coverage limitations;
503 indicates renderer failure/unavailability; 504 indicates process timeout. Scientific failures
remain persisted failed jobs with an error code/message and a trace/job ID. Incomplete output
files are never exposed as completed outputs. Global resource limits remain process timeout,
bounded request/output size and concurrency; no OS memory quota is implemented.

## Remaining backend milestones before broad product delivery

1. Broader independent astronomy/planetarium and print/editorial validation.
2. JPL ephemerides, Sun/Moon/planets, solar-relative times and twilight conventions.
3. Fixed-date astronomical event scenes, then bounded event discovery and visibility ranking.
4. Atlas figure/chapter management, configurable export profiles, vector PDF and batch publishing.
5. Optional schema-constrained AI interpretation, then scenery/caption retrieval.

The current backend can support an initial manual-authoring client, but no frontend work was
performed in this increment. See the v2.1 PDFs for the complete eventual product scope.
