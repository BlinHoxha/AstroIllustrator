# Deterministic astronomy, scientific accuracy and proposed AI

Checkpoint: 26 October 2026. Source evidence:
[contracts](../../renderer/astro_renderer/contracts.py),
[astronomy](../../renderer/astro_renderer/astronomy.py),
[charts](../../renderer/astro_renderer/charts.py),
[diagrams](../../renderer/astro_renderer/diagrams.py),
[renderer entry point](../../renderer/astro_renderer/__main__.py),
[scientific validation notes](../scientific-validation.md) and
[third-party notices](../../THIRD_PARTY_NOTICES.md).

## Scientific ownership and current AI status

All current chart geometry is deterministic Python/backend work. Astropy transforms and
Matplotlib drawing are scientific software and visualization, not learned AI inference.
AstroIllustrator currently consumes no LLM, embedding model, image generation service,
RAG pipeline, provider credentials or AI-Framework library. No natural-language prompt
or chat endpoint is implemented. Palette/language changes do not make an output AI-generated.

The frontend selects inputs and displays artifacts. It must not invent positions, infer
unsupported bodies, copy decorative prototype stars or compute alternative astronomy.

## Catalog and constellation definitions

- `renderer/catalogs/bright-hyg-v41.json` bundles 8,870 HYG v4.1 stars with HIP identifiers
  and magnitude <=6.5, with source commit/hash, coordinate and selection provenance.
- Epoch/equinox is treated as ICRS/J2000.0 according to the pinned upstream snapshot.
  The catalog retains proper motions, distance and radial velocity; runtime uses angular
  proper motion, not full parallax/perspective acceleration.
- `scene-definitions-v1.json` supplies original minimal guides for ORI, UMI, LYR, CYG, AQL.
  These line drawings are not IAU constellation boundaries or a canonical universal mythology.
- Summer Triangle joins Vega HIP 91262, Deneb HIP 102098, Altair HIP 97649. HIP identities,
  deduplication and edges must survive filtering/scene merging.
- `tools/prepare_catalog.py` is a source preparation utility, not a startup requirement.
  Do not regenerate/change the science snapshot merely to run the app.

HYG-derived data is CC BY-SA 4.0 with attribution/share-alike obligations, pinned upstream
commit `3bf37f4b2d5460e1278286320d1d62fab9b493c1`. Preserve bundled notices and identify changes.
Distribution/print packaging requires checking applicable catalog/font terms; a manifest
is provenance, not an automatic licensing clearance.

## Static sky charts

Single/multiple constellation and asterism charts use catalog coordinates with a FITS TAN
projection via Astropy WCS. A spherical 3D mean determines center; east is left and north up
(`cdelt` signs encode this). Deduplicate HIP stars, keep only edges whose endpoints remain,
and use finite projection/extent guards and automatic padding. A near-cancelling center or
extreme projection cannot be silently drawn as valid. Multiple-constellation selection is
restricted to 2–5 supported codes.

These charts represent a catalog sky view, not an observer's current horizon. Observer/time
must not be implied when the scene does not use them. J2000/catalog orientation is distinct
from an AltAz observation.

## Observer-dependent horizon geometry

1. Validate WGS84 latitude [-90,90], longitude [-180,180] and elevation [-500,10000] metres.
2. Resolve an explicit UTC instant, including local-clock ambiguity handling when requested.
3. Create Astropy EarthLocation and catalog SkyCoord with J2000 epoch and proper motions
   (`pm_ra_cosdec`, `pm_dec` in mas/year); propagate angular motion to observation time.
4. Transform into AltAz with pressure zero: **geometric**, unrefracted altitude/azimuth.
5. Keep altitude >=0 and within the selected azimuth field. Use signed wrap offsets in
   [-180,180); record below-horizon/out-of-field counts and meaningful warnings.

Azimuth is clockwise from true north: north 0°, east 90°, south 180°, west 270°.
Horizon output is a rectangular azimuth-offset versus altitude teaching view, not a globally
equal-angle spherical projection or a calibrated camera photograph. Terrain, buildings,
observer horizon dip and atmospheric refraction are excluded. No visible stars is a possible
scientific result with a warning, not a reason to fabricate stars.

## Time and supported accuracy

Inputs distinguish absolute UTC from local civil time plus IANA zone. Python explicitly uses
the locked `tzdata` package on every OS via `reset_tzpath`, rather than relying on an unversioned
system zone database. A local DST gap is invalid; an overlap requires `Earlier` or `Later`
unless the default Reject policy can resolve uniquely. The frontend preserves this distinction.

Both validators constrain observer time to 2000–2030, **subject to actual bundled IERS
coverage**. This nominal range does not promise every date is supported. IERS auto-download
is disabled; unsupported UT1 coverage/status or time warnings are rejected rather than
silently extrapolated. Predicted IERS values are identified in provenance/warnings. There
is now a runtime `/api/capabilities` endpoint that reports conservative start-inclusive/
end-exclusive bounds and data identity from that same table. At the 27 October checkpoint
the interval is 2000-01-01 inclusive to 2027-10-04 exclusive (UTC). It remains subject to
per-request checks and predictions; `/health/ready` is dependency readiness, not a claim of
scientific support for every input. See [API contract](../api-contract.md).

Manifest assumptions record time scales, resolved instant, observer, proper-motion behavior,
IERS identity/hash/status and relevant tzdata version. Do not equate locked inputs with
unlimited astrophysical accuracy: parallax/perspective/refraction/terrain and independent
external acceptance remain bounded or pending.

## Diagram semantics

| Template/mode                 | Meaning                                                                    |
| ----------------------------- | -------------------------------------------------------------------------- |
| `StarAltitude`, schematic     | Caller-selected teaching altitude, not a measured observation              |
| `StarAltitude`, computed      | Backend-derived altitude of Polaris (HIP 11767), observer/time required    |
| `PolarisLatitude`, schematic  | NCP/latitude teaching relation; not the exact observed altitude of Polaris |
| `PolarisLatitude`, computed   | Actual catalog Polaris AltAz with observer/time; differs from ideal NCP    |
| `AltitudeAzimuth`, schematic  | Teaching altitude and clockwise-from-north azimuth conventions             |
| `AngularSeparation`, computed | Exactly two distinct valid HIP stars; spherical SkyCoord separation        |

Computed angular separation draws two illustrative rays/arc carrying the derived angle;
the diagram is not a literal all-sky projection of the two bodies. Arc, ray and text share
one geometric value. Below-horizon computed Polaris can produce warnings. Computed-mode
manual angle injection is rejected. The allowed-mode discovery endpoint is authoritative.

## Artifacts, reproducibility and provenance

Renderer `0.2.0` builds geometry/scientific results first, then renders DarkAtlas or
PrintMonochrome with Matplotlib Agg. Geometry SHA-256 uses sorted canonical JSON with
non-finite numbers forbidden. Language and style are presentation changes, not changes
to underlying science. SVG uses a stable hash salt and omits variable date metadata;
PNG is saved at 200 dpi. DejaVu Sans is referenced through Matplotlib, not a guarantee that
every downstream print workflow embeds fonts correctly.

Each completed result has SVG, PNG and manifest. The manifest includes original normalized
request, geometry/hash, scientific results/assumptions, catalog and guide-set provenance,
library versions and SVG/PNG hashes. The manifest's own hash is returned in output metadata
after the manifest is written, avoiding a recursive self-hash requirement. A request hash,
geometry hash and file hashes identify different things; do not interchange them.

Reproducibility is tied to the locked environment and scientific snapshots. Do not promise
byte-identical output across arbitrary dependency/font/platform changes.

## Validation evidence and remaining scientific work

Source tests cover TAN orientation, HIP references, determinism/finite coordinates, all
example scenes, edge identity, wrap/clipping, global/polar observers, an Astropy published
reference, IERS rejection, DST, independent spherical separation, angle consistency,
language/style geometry invariance and rejection of caller positions/forged computed angles.
These tests were inspected, not rerun in the present task. Historical real API/browser tests
verified rendering/artifact hashes, not a complete independent planetarium acceptance study.

Pending: independent scientific review, broader accuracy tolerances/reference comparisons,
print legibility/font checks, editorial/Albanian review and honest capability/date reporting.
Sun/Moon/planets, JPL ephemerides, conjunction/event searches and solar-time calculations
are absent. A future conjunction needs defined bodies, observer/frame, time scale, event
criterion, search interval/refinement and reference tolerances; “near each other” text is
not an implemented event algorithm.

## Proposed AI boundary

1. Natural language may propose a typed request limited to real supported scenes.
2. Show the interpretation for user review, preserve ambiguity and reject unsupported asks.
3. Run existing backend validation and scientific computation; the model never supplies
   authoritative star coordinates, angles, event times or ephemerides.
4. Optional educational captions can use curated evidence with citations, uncertainty and
   review. Evidence retrieval alone does not certify every generated assertion.
5. Optional scenery can be a separate artistic layer, labeled and isolated from the scientific
   overlay. No image-generation integration or provider is currently selected.

Provider choice, structured-output contract, language support, privacy, cost limits, provenance
and evaluation are unresolved. See [framework integration](05_SHARED_FRAMEWORK_INTEGRATION.md).
