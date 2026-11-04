# Project vision, requirements and scope

Checkpoint: 26 October 2026. For evidence levels, see [the index](README.md).

## Purpose and users

AstroIllustrator produces scientifically grounded astronomy charts and educational
diagrams, with inspectable inputs, downloadable artifacts and reproducible provenance.
The client helps a person select a supported scene, supply relevant observation settings,
render it, understand its scientific caveats and recover the result later.

The product vision serves astronomy learners, teachers, enthusiasts and creators preparing
educational or print material. These are intended audiences inferred from the supplied
product/UX specification and discussed workflows, not validated customer research or
confirmed commercial commitments. No organization, subscription, tenancy or account model
has been implemented.

## Current useful workflows

1. Discover supported constellations, asterisms, diagrams, catalog metadata and styles.
2. Render a single constellation, several supported constellations together, the Summer
   Triangle, or an observer-dependent horizon view.
3. Render supported schematic teaching diagrams or computed stellar diagrams.
4. Enter WGS84 latitude, longitude and elevation; choose an explicit UTC instant or local
   civil time with an IANA time zone and DST ambiguity policy where observation is needed.
5. Save, edit, select and delete named observer presets without altering old render inputs.
6. Choose English (`en`) or Albanian (`sq`) chart labels and independently choose UI language.
7. Inspect SVG/PNG previews, warnings, assumptions, catalog references and manifest data;
   zoom/pan the image and download SVG, PNG and manifest files.
8. Browse persistent render history, reopen a result after reload and retain a local editor draft.

The actual current clients are a functional web MVP and a native navigation scaffold.
The native scaffold is not a mobile implementation of these workflows.

## Product and engineering requirements

- Scientific positions and angles must come from deterministic backend/Python calculations.
  Decorative prototype positions and AI-generated star locations must never become data.
- Separate schematic explanation from observer/time-derived geometry. Never present a
  supplied teaching angle as a measured astronomical observation.
- Keep supported scene options discoverable; unsupported capabilities must not appear to work.
- Use schema version `2.1`, exact wire enum values and only the relevant scientific payload.
- Preserve render input semantics, geometry identity, catalog/line-set hashes and assumptions.
  Stored request JSON is the backend's normalized serialization, not necessarily the original
  HTTP byte sequence.
- Show genuine loading, errors and empty states. Synchronous rendering has no percentage
  progress stream or durable submission queue.
- Keep drafts on failures and cancellation; a cancelled client request may still have a job.
- Expose scientific limitations in the UI, rather than hiding them in implementation notes.
- Provide responsive, keyboard-accessible UI, visible focus, reduced motion and English/Albanian
  resources. Native touch targets and permission flows matter when native delivery begins.
- Keep local setup understandable to a beginner using normal PowerShell. No SQLite server,
  administrator shell or permanent execution-policy relaxation is required for ordinary startup.
- Keep credentials, Python execution and database access outside clients. Public or device
  access requires deliberate networking, access control and deployment work.
- Share contracts, transport, tokens and domain-neutral helpers across clients; do not force
  web DOM components into React Native or introduce orchestration frameworks without need.

## Supported scene boundary

The bundled catalog contains 8,870 bright HIP-identified HYG stars. The current constellation
guide definitions cover **ORI, UMI, LYR, CYG and AQL**, not all 88 constellations. Summer Triangle
is the only asterism definition. Four diagram templates exist with restricted schematic and/or
computed modes; see [the scientific model](04_AI_AND_SCIENTIFIC_MODEL.md).

Current output is deterministic scientific SVG/PNG plus JSON manifest. A palette change is
a style choice, not an AI-generated image. The preview displays backend artifacts rather than
recomputing stars in the browser.

## Future capabilities: discussed, not delivered

- Sun/Moon/planet ephemerides, solar-time calculations, conjunctions and other event discovery.
- Natural-language interpretation into a typed, supported request for user review.
- Grounded educational captions, with evidence and review controls.
- Optional artistic scenery kept separate from authoritative scientific overlays.
- A usable mobile client, explicit location/share permissions and real-device verification.
- Atlas/projects/collections, PDF/print packaging, batch processing and durable jobs when needed.
- Broader catalog coverage and a secured shared test environment. OpenAPI-generated client
  types and drift checks were subsequently delivered on 27 October 2026.

AI/AR/planets/events/PDF/batch are not hidden completed features. Their mention in requirements
does not authorize implementing them during the present documentation task. Earlier approval
covered frontend scaffold/web MVP, the temporary adapter and local proxy; backend changes,
native feature delivery and deployment are later scope.

## Recommended priorities

Stabilize the working web workflow and local startup first. Then improve the API contract and
capability reporting, alongside independent scientific/export/accessibility review. Establish
secured networking before broader testing; build native workflows on that stable contract.
Ephemerides/events and limited AI interpretation follow explicit product decisions. A useful
AI slice can be brought forward after web/API/scientific foundations; mobile and planets are
not technical prerequisites for interpreting already-supported scenes.

The user requested priorities and substeps, not immediate AI implementation. The immediate
discussed startup improvement, frontend `scripts/pnpm.cmd`, is now implemented. The six-area
local web stabilization review passed on 27 October; see the implementation status for
current evidence and remaining independent human/scientific acceptance requirements.
