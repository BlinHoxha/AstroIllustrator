# Frontend architecture, UX and conventions

Checkpoint: 26 October 2026. Source is in the sibling `AstroIllustrator.Frontend` directory,
which has no Git metadata at this checkpoint. Existing detailed records remain in its
`docs/00-source-audit.md` through `05-development.md` and `docs/adr/0001-frontend-architecture.md`.

## Workspace and stack

The pnpm workspace has `apps/web`, `apps/mobile` and four shared packages:
`contracts`, `api-client`, `design-tokens`, `shared-utils`. Dependency versions are pinned;
`pnpm-lock.yaml` is the installation authority. Root engines require Node 24, pnpm `10.28.2`.
Local Node `24.22.1` and pnpm were provisioned under ignored `.tools/toolchain`; the system
Node `20.14` was insufficient and was not replaced globally.

Web uses React/React DOM `19.3.0`, Vite `7.2.2`, React Router `7.9.6`, TanStack Query `5.90.10`,
React Hook Form `7.66.1`, Zod `3.25.76`, Tailwind `4.1.17`, i18next/react-i18next and Lucide.
UI primitives use Radix Slot/CVA patterns; do not describe this as a complete imported
component library. Tooling includes TypeScript `5.9.3`, ESLint `9.39.1`, Vitest `3.2.4`,
Playwright `1.56.1` and axe. Read current package manifests before changing versions.

Native uses Expo `58.0.19`, Expo Router `58.0.26`, React Native `0.88.0` and React `19.3.0`,
chosen together from Expo compatibility information. It shares tokens/types/transport,
not DOM components. Native feature implementation is deferred.

## Web navigation and implemented pages

Evidence: [route composition](../../../AstroIllustrator.Frontend/apps/web/src/app.tsx)
and `apps/web/src/features/`.

| Route          | Implemented behavior and limits                                                                                                           |
| -------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `/`            | Dashboard with recent real jobs, discovery counts, quick links for Orion/UMI/Summer Triangle/Horizon; truthful empty states               |
| `/studio`      | Scene/mode controls, relevant observer/time fields, palettes/chart language, versioned draft, render/cancel/reset, returned artifact view |
| `/catalog`     | Search loaded supported names/codes, discovery metadata/provenance, create links; not a complete all-sky catalog browser                  |
| `/library`     | Paged history/status filter and observer-preset management, accessible tabs                                                               |
| `/library/:id` | Full persisted job, completed artifacts, warnings, assumptions and raw provenance                                                         |

Routes are lazy loaded behind Suspense. The responsive dark shell has desktop navigation
and narrow-screen layouts. Health polling every 30 seconds drives “API connected”; this
means `/health/live` succeeds, not that the renderer or every workflow is ready.

Studio uses dedicated scene/observation/numeric-field controls, React Hook Form and a
TanStack mutation with AbortController. Submission disables duplicate input actions while
pending. No fake progress or automatic mutation retries are shown. Reset requires
confirmation. A client abort preserves the draft and cannot guarantee no persisted job;
history is the recovery mechanism.

## State, validation and request construction

- TanStack Query owns remote discovery/jobs/observers/manifest state; retry is disabled for
  queries and mutations. Local form/view state owns draft fields, tabs and image transforms.
- Editor storage key is `astro.studio-draft.v1`; strict versioned restoration rejects invalid,
  malformed or unsupported drafts and uses defaults. UI language uses `astro.ui-language`.
- UI locale and chart request `language` are separate choices. Both English and Albanian exist.
- The request builder serializes only scene-relevant fields and one payload, not every hidden
  form field. Outgoing finite-number validation uses the actual built payload. Hidden values
  from another scene must not invalidate an otherwise valid request.
- Defaults are examples: ORI, a LYR/CYG/AQL group, magnitude 6, DarkAtlas, latitude 42.6629,
  longitude 21.1655, elevation 600; explicit example dates rather than live geolocation/“now”.
  Check current draft defaults in `packages/api-client/src/` before relying on them.
- Local civil times remain plain civil-time strings plus IANA zone/policy. JavaScript Date
  must not silently convert them before backend resolution.
- The Horizon UI currently uses the broad bright-star view; it does not expose every selector
  combination the backend can accept. Computed diagrams do not accept forged manual angles.

No Zustand store was added: current Query/form/local state already serves the workflow.
Named projects/accounts/collections are not inferred from a local draft or render history.

## Shared packages and integration

`packages/contracts/src/generated.ts` now derives from the backend's actual exported OpenAPI
snapshot. Public aliases/refinements in `index.ts` require explicit client choices but derive
their fields/enums from generated types. `contracts:sync/generate/check` and provenance hashes
guard drift; the frontend CI workflow includes the check. Historical example fixtures remain
unchanged compatibility evidence. See [API contract](../api-contract.md) and the frontend
[workflow](../../../AstroIllustrator.Frontend/docs/07-api-contract.md).

`packages/api-client` owns fetch transport, typed endpoints, request construction and the
temporary compatibility adapter. It handles 204, empty/non-JSON errors, ProblemDetails,
network failures and cross-realm aborts. JSON strings in job detail are parsed while raw
strings remain available. Most response typing is TypeScript casting, not comprehensive
runtime schema validation of every server response. Output descriptors/URLs have extra
safety checks; do not generalize that into complete wire validation.

`packages/design-tokens` shares colors/spacing/typographic values across web/native. Web
projects them into CSS variables; the dark palette includes background `#0B1220`, panels
`#17243A` and accent `#4F83FF`. `shared-utils` contains domain-neutral JSON/timestamp helpers,
not astronomy calculations. Scientific ownership stays backend-side.

Vite development uses port **5173**, strict port selection and loopback binding. With
`VITE_API_BASE_URL` empty, relative `/api` and `/health` requests proxy to
`API_PROXY_TARGET` (default `http://127.0.0.1:5188`). The preview server on 4173 does not carry
this development proxy; production hosting must deliberately provide routing/proxy/CORS.
Changing the direct API URL alone does not configure backend CORS or browser CSP.

## Artifact preview, UX and safety

`features/job-view.tsx` renders server-generated SVG/PNG as images, not injected SVG markup.
The UI supports zoom 0.5–4, pointer pan, arrow-key pan, Home/Fit, copy ID and direct downloads.
Output URLs must match the configured origin and expected safe path/format rules; credentials,
query and fragment tricks are rejected. Manifest text is escaped. The page CSP restricts
scripts/connect/image sources; additional remote deployments need deliberate review.

Completed-job warnings stay visible; scientific assumptions are collapsible. Manifest fetch failure is
separate from render success, so a completed job is not mislabeled failed because provenance
retrieval failed. Raw manifest/geometry hash/catalog information remains accessible. There
is no browser astronomy engine, editable star geometry or AI image viewer.

Keyboard focus, reduced motion, decorative icon labeling, responsive layout and library tab
keyboard navigation were addressed. Historical axe checks passed for tested flows; manual
screen-reader review, editorial/Albanian review and broader usability acceptance remain open.
History uses `features/job-description.tsx` to display scene/mode/constellation labels directly
from the new nullable summary metadata. Per-row detail reads are removed; older/unknown rows
fall back to the generic label and ID. Opening a job still fetches its detail.
Cancelled states have a distinct style. Studio uses a larger viewport and controls,
an independently scrollable desktop inspector, grouped exports/disclosures, and sticky
pending/error feedback outside the mobile panels. Completion feedback remains static.

Studio also checks dependency readiness separately from liveness, and observation forms show
the conservative installed UTC coverage. Diagnostic failures hide stale positive data;
missing endpoints on older hosts do not advertise false readiness or block existing renders.
Coordinate-specific backend errors attach to the correct Studio/preset input. Numeric labels
remain stable, with errors outside labels and connected using aria-describedby.

The [27 October stabilization review](../../../AstroIllustrator.Frontend/docs/06-web-stabilization-review.md)
records fresh 36 unit/component and 42 browser checks, including real API scenarios, plus
focused keyboard/cancellation rechecks and retained screenshots. Earlier counts below are
historical. Independent human screen-reader/editorial/scientific acceptance remains open.

## Native status

`apps/mobile/app/` has Expo Router tabs Explore/Create/Library/Settings, a shared native
screen wrapper and tokens. Screens explicitly describe their scaffold state. No working
render forms, API data flows, persistent editor, localization settings, sharing or location
permission flow is implemented there. Android/iOS bundle export succeeded historically;
no simulator/device runtime acceptance, APK delivery or native MVP completion is claimed.

## Testing and delivery limits

Historical local checks passed: frozen offline install, formatting, zero-warning lint,
workspace/tool/E2E type checking, **33 unit/component tests**, web production build,
**28 Playwright checks** across desktop/narrow layouts, and Android/iOS Expo exports.
The browser set included mocked failures and real API journeys; these are different evidence.
Largest lazy chunk was recorded at 363.09 kB minified/116.94 kB gzip, not a total-load or
performance SLA. No checks were rerun during this documentation-only task.

Real-API Playwright execution is opt-in (`ASTRO_REAL_API`); default tests/CI do not automatically
prove backend integration. The CI workflow is present, but no hosted run is established and
there is no frontend Git remote/commit. Ignored QA screenshots under `.tools/` are local
evidence, not durable source-controlled acceptance artifacts.
