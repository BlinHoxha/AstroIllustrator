# Atlas chart evaluation — 17 November 2026

This record transcribes the completed 17 November evaluation. No evaluation or render was rerun for this record. Single CPU samples are not throughput or quality claims; rendered outputs were not published.

## Materials, method, results and findings

- B2 Materials: the atlas drafts available were `Atlasi_Astronomik_Draft_01.pdf` (dated
  14 October 2026; book structure of 7 parts and 37 chapters, about 550 pages, chart and
  illustration standard, sources, review plan) and `Atlasi_Astronomik_Kapitulli_01.pdf`
  (Chapter 1 complete, 8 pages). Both are owner-held and outside version control. Missing and
  therefore not assessed: chapters 2-37 text, per-chapter figure lists or specifications, the
  terminology list beyond Chapter 1, print format and illustration sizes, map date ranges.
  Also used: `docs/specification-v2.1` (Product Requirements, AI Integration Roadmap).
- B3 Method: as A1 and A2 for qwen3:4b, with `docs/qa/chart-ai-evaluation-cases.json` replaced
  in the scratch copy only by the B1 cases. Application support was verified by rendering the
  supportable cases directly with the renderer (`python -m astro_renderer --output-dir ...`,
  renderer 0.2.0 from the scratch copy, repository `.venv`), outputs in a scratch directory
  (Appendix 2).
- B4 Per-case results (application support / qwen3:4b final result / classification / failure
  attributed to / latency):
  - A1 Supported (rendered, correct geometry) / Invalid: correct altitude 35, azimuth 125 and
    sq, wrong scene HorizonView plus `diagramType` / mismatch / AI / 44.4 s (includes model load).
  - A2 Supported with editorial gaps (title "ORI", English star names) / Invalid: all fields
    correct except extra `diagramType`/`mode` / mismatch / AI, plus editorial gap / 21.0 s.
  - A3 Partial: Ursa Minor and Polaris supported; Ursa Major rejected ("Invalid constellation
    codes") / Invalid: raw output substituted `["UMI","UMI"]` for Ursa Major, validation
    blocked it / partial (safe final outcome) / application gap plus AI substitution / 30.0 s.
  - A4 Supported for dates up to 2027-10-04 (January 2028 rejected) / Unsupported (wrongly
    refused) / mismatch / AI; the AI schema also cannot carry the stated place or time / 21.4 s.
  - A5 Partial: magnitude filter and magnitude-scaled glyphs work; no magnitude legend or
    values / Invalid: magnitude 4 extracted, wrong scene / mismatch / AI, plus missing legend /
    19.8 s.
  - A6 Absent (no RA/Dec grid; EquatorialCoordinates not implemented) / Unsupported / match /
    application / 23.6 s.
  - A7 Absent (EarthSeasons not implemented) / Unsupported / match / application / 23.0 s.
  - A8 Absent (no Moon) / Unsupported / match / application / 32.0 s.
  - A9 Absent / Unsupported / match / application / 20.7 s.
  - A10 Absent (no ephemerides, no sunset-relative time) / Unsupported, no star-chart
    substitute / match / application / 21.3 s.
  - A11 Absent / Unsupported / match / application / 28.6 s.
  - A12 Absent as a scene; workaround four manual HorizonView renders / Unsupported / match /
    application / 26.6 s.
- B5 Totals: 7 match, 1 partial, 4 mismatch; 8 Unsupported, 4 Invalid, 0 Ready, 0 NeedsInput;
  12 HTTP 200; probe 504 `ai_timeout` at 60.3 s. No invented value reached a response. Latency
  19.8-44.4 s, mean 26.0 s (24.4 s excluding the first); input 343-376, output 108-199 tokens.
  Of 8 Unsupported results, only A4 was wrong.
- B6 Accuracy checks of the renders (hand checks, not an independent engine): Prishtina
  (42.6629 N, 21.1655 E, 600 m), 2027-01-15 21:00 Europe/Belgrade (20:00 UTC): Orion's belt
  near the meridian at about 45° altitude; Sirius at about 26° (maximum 30.6°); computed
  Polaris altitude 43.201°, azimuth 359.588° (expected 42.66° ± 0.74°); Capella near the
  zenith and Deneb low in the north-west. The 125° azimuth ray falls in the south-east
  quadrant as in Chapter 1 Figure 2.
- B7 Chapter 1 science check: sidereal day, the culmination formula h = 90° − |φ − δ|, the
  angular-distance formula, pole altitude equal to latitude and the answers 45,12° and 64° are
  correct. One wording correction for the author in §1.4: "15,041° për orë yjore të matur në orë
  diellore" should read approximately "≈15,041° për orë diellore mesatare (15° për orë yjore)".
- B8 Capability summary against Draft 01: Part I (orientation) mostly supported (StarAltitude,
  AltitudeAzimuth, PolarisLatitude schematic and computed, HorizonView); Chapter 1 Figures 1
  and 2 and §1.9 are producible, Figure 3 (pointer stars to Polaris) is not (needs Ursa
  Major). Parts II-V (coordinates, ecliptic, time, seasons, orbits, Kepler, Sun, Moon, eclipses,
  planets) absent. Part VI partial: 8,870 stars to magnitude 6.5 with all 88 constellation
  memberships, line figures for 5 constellations only, no IAU boundaries, one asterism, no
  magnitude legend, no deep-sky objects. Part VII partial (global observer, presets, time zones;
  no seasonal series). Production: SVG/PNG and reproducible manifest present; no vector PDF,
  figure or chapter ids, print profiles or batch runs.
- B9 Scientific and rendering gaps: (1) bundled IERS-A data ends 2027-10-04 and requests stop at
  2030; (2) no ephemerides; (3) five original line sets, no IAU boundaries; (4) the Draft 01
  chart standard is not met: title shows the code ("ORI"), only UTC is printed, north/east
  only as a footnote on static charts, no angular scale, ecliptic, boundaries, magnitude legend
  or on-figure calculation method; (5) English star names, 13 hard-coded labels (Sirius and
  Capella unlabelled), decimal points with three decimals ("43.201°") where the atlas writes
  decimal commas ("42,7°"); (6) HYG is CC BY-SA 4.0; compatibility with a commercial atlas is
  unresolved pending licensing assessment (owner decision 2026-11-17); (7) already documented
  and labelled: geometric horizon without refraction or terrain, non-equal-angle horizon
  projection.
- B10 AI interpretation and validation gaps: (1) all schema fields are required and the model
  fills scene-incompatible fields; (2) validation runs before grounding, so an invented
  out-of-range value invalidates the whole proposal; (3) the grounding regex accepts only dot
  decimals, so Albanian decimal commas ("23,4", "0,5") are never grounded; (4) the schema has no
  observer or time fields; (5) the prompt conflict in A8(3); (6) Unsupported results carry no
  reason; (7) the evaluation set is small and manually scored (the specification asks for 20
  representative prompts); (8) CPU latency of 20-30 s and long-input timeouts. Owner decision
  2026-11-17: scene-incompatible fields are not discarded automatically until a separate safety
  and validation assessment is completed.

## Per-case captured API results

### Case A1

- Expected: A1 Horizon orientation: ScientificDiagram, AltitudeAzimuth, Schematic, azimuth 125, altitude 35, sq -> Ready
- Classification: mismatch
- Captured outcome, latency, tokens and raw proposal: A1. "Krijo diagramin e azimutit për një objekt me azimut 125° dhe lartësi 35°, me etiketa shqip." -> 200 Invalid issues=diagramType; 44.4 s; tokens 373/108; raw `{"supported":true,"parameters":{"scene":"HorizonView","constellationCodes":[],"diagramType":"StarAltitude","mode":"Schematic","language":"sq","preset":"DarkAtlas","magnitudeLimit":null,"altitudeDegrees":35,"azimuthDegrees":125,"starHipIds":[]}}`

### Case A2

- Expected: A2 Orion sq: SingleConstellation, ORI, sq, DarkAtlas -> Ready
- Classification: mismatch
- Captured outcome, latency, tokens and raw proposal: A2. "Krijo hartën e yjësisë Orioni me etiketa shqip për atlasin, me sfond të errët." -> 200 Invalid issues=diagramType; 21.0 s; tokens 363/117; raw `{"supported":true,"parameters":{"scene":"SingleConstellation","constellationCodes":["ORI"],"diagramType":"StarAltitude","mode":"Schematic","language":"sq","preset":"DarkAtlas","magnitudeLimit":null,"altitudeDegrees":null,"azimuthDegrees":null,"starHipIds":null}}`

### Case A3

- Expected: A3 UMA not supported: Unsupported or Invalid; never substitute UMI alone
- Classification: partial
- Captured outcome, latency, tokens and raw proposal: A3. "Show Ursa Major and Ursa Minor together so the pointer stars Dubhe and Merak lead to Polaris." -> 200 Invalid issues=constellationCodes,diagramType; 30.0 s; tokens 354/156; raw `{"supported":true,"parameters":{"scene":"MultipleConstellations","constellationCodes":["UMI","UMI"],"diagramType":"StarAltitude","mode":"Schematic","language":"en","preset":"PrintMonochrome","magnitudeLimit":3.0,"altitudeDegrees":45.0,"azimuthDegrees":90.0,"starHipIds":[200000,200001]}}`

### Case A4

- Expected: A4 Winter sky: HorizonView, sq; observer and timeReference required -> NeedsInput; no geocoding or invented time
- Classification: mismatch
- Captured outcome, latency, tokens and raw proposal: A4. "Shfaq qiellin e dimrit drejt jugut nga Prishtina më 15 janar 2027 në orën 21:00 sipas orës lokale." -> 200 Unsupported; 21.4 s; tokens 376/121; raw `{"supported":false,"parameters":{"scene":"ScientificDiagram","constellationCodes":["ORI"],"diagramType":"StarAltitude","mode":"Schematic","language":"sq","preset":"DarkAtlas","magnitudeLimit":100,"altitudeDegrees":0,"azimuthDegrees":0,"starHipIds":null}}`

### Case A5

- Expected: A5 Magnitudes: SingleConstellation, ORI, magnitudeLimit 4 -> Ready
- Classification: mismatch
- Captured outcome, latency, tokens and raw proposal: A5. "Show the stars of Orion down to magnitude 4 to explain apparent brightness." -> 200 Invalid issues=constellationCodes,magnitudeLimit; 19.8 s; tokens 346/112; raw `{"supported":true,"parameters":{"scene":"ScientificDiagram","constellationCodes":["ORI"],"diagramType":"StarAltitude","mode":"Schematic","language":"en","preset":"DarkAtlas","magnitudeLimit":4,"altitudeDegrees":null,"azimuthDegrees":null,"starHipIds":null}}`

### Case A6

- Expected: A6 RA/Dec: Unsupported (EquatorialCoordinates not implemented)
- Classification: match
- Captured outcome, latency, tokens and raw proposal: A6. "Create an equatorial coordinate diagram showing the right ascension and declination of Betelgeuse." -> 200 Unsupported; 23.6 s; tokens 351/130; raw `{"supported":false,"parameters":{"scene":"Asterism","constellationCodes":["ORI"],"diagramType":"StarAltitude","mode":"Schematic","language":"en","preset":"DarkAtlas","magnitudeLimit":100,"altitudeDegrees":0,"azimuthDegrees":0,"starHipIds":[4994]}}`

### Case A7

- Expected: A7 Axial tilt: Unsupported (EarthSeasons not implemented); no invented angle
- Classification: match
- Captured outcome, latency, tokens and raw proposal: A7. "Vizato pjerrësinë e boshtit të Tokës prej 23,4° dhe mënyrën si krijohen stinët." -> 200 Unsupported; 23.0 s; tokens 369/122; raw `{"supported":false,"parameters":{"scene":"ScientificDiagram","constellationCodes":["ORI"],"diagramType":"StarAltitude","mode":"Schematic","language":"sq","preset":"PrintMonochrome","magnitudeLimit":100,"altitudeDegrees":23.4,"azimuthDegrees":null,"starHipIds":null}}`

### Case A8

- Expected: A8 Lunar phases: Unsupported
- Classification: match
- Captured outcome, latency, tokens and raw proposal: A8. "Show the eight phases of the Moon with Albanian labels." -> 200 Unsupported; 32.0 s; tokens 343/199; raw `{"supported":false,"parameters":{"scene":"MultipleConstellations","constellationCodes":["CYG","AQL","LYR","ORI","UMI"],"diagramType":"StarAltitude","mode":"Schematic","language":"sq","preset":"DarkAtlas","magnitudeLimit":10000,"altitudeDegrees":0,"azimuthDegrees":0,"starHipIds":[1,2,1000,1001,1002,1003,1004,1005]}}`

### Case A9

- Expected: A9 Eclipse: Unsupported
- Classification: match
- Captured outcome, latency, tokens and raw proposal: A9. "Draw the geometry of a total solar eclipse with the Sun, Moon, Earth, umbra and penumbra." -> 200 Unsupported; 20.7 s; tokens 354/111; raw `{"supported":false,"parameters":{"scene":"Asterism","constellationCodes":[],"diagramType":"StarAltitude","mode":"Schematic","language":"en","preset":"DarkAtlas","magnitudeLimit":null,"altitudeDegrees":null,"azimuthDegrees":null,"starHipIds":null}}`

### Case A10

- Expected: A10 Planets: Unsupported; no HorizonView substitute without planets
- Classification: match
- Captured outcome, latency, tokens and raw proposal: A10. "Which planets are visible from Pristina tonight? Show them above the western horizon after sunset." -> 200 Unsupported; 21.3 s; tokens 350/111; raw `{"supported":false,"parameters":{"scene":"Asterism","constellationCodes":null,"diagramType":"StarAltitude","mode":"Schematic","language":"en","preset":"DarkAtlas","magnitudeLimit":null,"altitudeDegrees":null,"azimuthDegrees":null,"starHipIds":null}}`

### Case A11

- Expected: A11 Kepler: Unsupported
- Classification: match
- Captured outcome, latency, tokens and raw proposal: A11. "Ilustro ligjin e parë të Keplerit: orbita eliptike me Diellin në njërin fokus, me ekscentricitet 0,5." -> 200 Unsupported; 28.6 s; tokens 369/132; raw `{"supported":false,"parameters":{"scene":"Asterism","constellationCodes":["CYG"],"diagramType":"StarAltitude","mode":"Schematic","language":"sq","preset":"DarkAtlas","magnitudeLimit":100,"altitudeDegrees":0,"azimuthDegrees":0,"starHipIds":[12345]}}`

### Case A12

- Expected: A12 Seasonal charts: Unsupported (SeasonalSky not implemented); a single HorizonView is a substitution
- Classification: match
- Captured outcome, latency, tokens and raw proposal: A12. "Create four seasonal sky charts for Kosovo: spring, summer, autumn and winter." -> 200 Unsupported; 26.6 s; tokens 347/143; raw `{"supported":false,"parameters":{"scene":"Asterism","constellationCodes":["ORI","UMI","LYR","CYG","AQL"],"diagramType":"StarAltitude","mode":"Schematic","language":"sq","preset":"DarkAtlas","magnitudeLimit":100,"altitudeDegrees":0,"azimuthDegrees":0,"starHipIds":null}}`

### 2,000-character Albanian probe

- probe. "2,000-character Albanian probe" -> 504 ai_timeout; 60.3 s; tokens None/None; raw `none`

## Direct renderer checks

Exit 0 means artifacts and a manifest were produced; exit 2 is a scientific rejection.
IERS-A table of the bundled `astropy-iers-data` 0.2026.10.19.0.52.1: last row 2027-10-04.

- A1-altaz-sq: exit 0, geometryHash 3548e80bc742071bf550e8f3c4b8489131f83b636d7051a15a693b403c51815c; title=Altitude / Lartësia këndore — schematic, altitudeDeg=35; request `{"schemaVersion":"2.1","language":"sq","preset":"DarkAtlas","illustrationType":"ScientificDiagram","scientificDiagram":{"diagramType":"AltitudeAzimuth","mode":"Schematic","parameters":{"altitudeDegrees":35,"azimuthDegrees":125}}}`
- A2-orion-sq: exit 0, geometryHash 6b78044381636cfc1e781367e8f84e4d856aae916524009e2edfd25a2b4c4e65; title=ORI, plottedStarCount=128, warnings=Static J2000 catalog positions; not an observer-specific current sky.; request `{"schemaVersion":"2.1","illustrationType":"SkyChart","language":"sq","preset":"DarkAtlas","skyChart":{"sceneType":"SingleConstellation","constellationCode":"ORI","projection":"Gnomonic","magnitudeLimit":6}}`
- A3-uma: exit 2, ScientificError "Invalid constellation codes"; request `{"schemaVersion":"2.1","illustrationType":"SkyChart","language":"sq","preset":"DarkAtlas","skyChart":{"sceneType":"MultipleConstellations","constellationCodes":["UMA","UMI"],"projection":"Gnomonic","magnitudeLimit":5}}`
- A3b-umi-sq: exit 0, geometryHash fb1d6545d33f790244f3a5a818b90b00115a4a67e7012d16b94d88a04d45c06e; title=UMI, plottedStarCount=11, warnings=Static J2000 catalog positions; not an observer-specific current sky.; request `{"schemaVersion":"2.1","illustrationType":"SkyChart","language":"sq","preset":"DarkAtlas","skyChart":{"sceneType":"SingleConstellation","constellationCode":"UMI","projection":"Gnomonic","magnitudeLimit":5}}`
- A3c-north-horizon-pristina: exit 0, geometryHash 958178e2839434367b8562c234d34030d2e723ee5fd6058e24ca43536eb8cadc; title=Horizon / Horizonti, plottedStarCount=68, observationUtc=2027-01-15T20:00:00+00:00, warnings=Bundled IERS Earth-orientation values include predictions.; request `{"schemaVersion":"2.1","language":"sq","preset":"DarkAtlas","illustrationType":"SkyChart","skyChart":{"sceneType":"HorizonView","projection":"AltitudeAzimuth","magnitudeLimit":4,"observer":{"latitude":42.6629,"longitude":21.1655,"elevationMeters":600,"timeZoneId":"Europe/Belgrade"},"timeReference":{"type":"LocalClock","localDateTime":"2027-01-15T21:00:00","timeZoneId":"Europe/Belgrade","ambiguityPolicy":"Reject"},"azimuthCenterDegrees":0,"fieldOfViewDegrees":120}}`
- A3d-polaris-computed-pristina: exit 0, geometryHash be30eac9f63c26d37dd6d8f87eb34af1cfde73476554dd296211dc3d0644d03a; title=Polaris — computed / e llogaritur, observationUtc=2027-01-15T20:00:00+00:00, altitudeDeg=43.201054338258125, azimuthDeg=359.5875126314631; request `{"schemaVersion":"2.1","language":"sq","preset":"DarkAtlas","illustrationType":"ScientificDiagram","scientificDiagram":{"diagramType":"PolarisLatitude","mode":"Computed","observer":{"latitude":42.6629,"longitude":21.1655,"elevationMeters":600},"timeUtc":"2027-01-15T20:00:00Z","parameters":{"starId":"Polaris"}}}`
- A4-winter-south-pristina: exit 0, geometryHash 6b04cd4225cb67873649724b35a8cea7a6290824b8e5ce88906e00255e7f04f2; title=Horizon / Horizonti, plottedStarCount=314, observationUtc=2027-01-15T20:00:00+00:00, warnings=Bundled IERS Earth-orientation values include predictions.; request `{"schemaVersion":"2.1","language":"sq","preset":"DarkAtlas","illustrationType":"SkyChart","skyChart":{"sceneType":"HorizonView","projection":"AltitudeAzimuth","magnitudeLimit":5,"observer":{"latitude":42.6629,"longitude":21.1655,"elevationMeters":600,"timeZoneId":"Europe/Belgrade"},"timeReference":{"type":"LocalClock","localDateTime":"2027-01-15T21:00:00","timeZoneId":"Europe/Belgrade","ambiguityPolicy":"Reject"},"azimuthCenterDegrees":180,"fieldOfViewDegrees":120}}`
- A4b-winter-2028: exit 2, ScientificError "Requested time is outside the bundled IERS Earth-orientation coverage"; request `{"schemaVersion":"2.1","language":"sq","preset":"DarkAtlas","illustrationType":"SkyChart","skyChart":{"sceneType":"HorizonView","projection":"AltitudeAzimuth","magnitudeLimit":5,"observer":{"latitude":42.6629,"longitude":21.1655,"elevationMeters":600},"timeUtc":"2028-01-15T20:00:00Z","azimuthCenterDegrees":180,"fieldOfViewDegrees":120}}`
- A5-orion-mag4: exit 0, geometryHash 87223cb15ea441ab85466428be316209d0414ed42744beb725bac19fe927fbfa; title=ORI, plottedStarCount=15, warnings=Static J2000 catalog positions; not an observer-specific current sky.; request `{"schemaVersion":"2.1","illustrationType":"SkyChart","language":"en","preset":"PrintMonochrome","skyChart":{"sceneType":"SingleConstellation","constellationCode":"ORI","projection":"Gnomonic","magnitudeLimit":4}}`
