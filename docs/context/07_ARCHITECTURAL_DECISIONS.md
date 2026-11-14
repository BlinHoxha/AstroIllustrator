# Architectural decisions, trade-offs and open choices

Checkpoint: 26 October 2026. This captures accessible discussion and inspected decisions;
it does not invent dates or approval for the original backend build.

Decision, **4 November 2026**: the user approved the structured-generation approach and chose
Azure OpenAI first, with possible Bedrock support later. Add a provider-neutral structured
interface to AI-Framework, separate from grounded answers; keep all astronomy/proposal rules
in Astro and human review in Studio. Use source references to the sibling Core/Azure modules,
preserving net9 consumers while hosting Astro on net10. Do not reference general Framework.
AI is disabled by default, has bounded calls/no retries, and cannot invent observation settings
or submit renders. Shared tests use simulated providers; live quality remains an acceptance
task. See [the design and setup](../ai-chart-proposals.md). The table below is the October baseline.

Decision, **11 November 2026**: chart-proposal metering uses AI-Framework's storage-free
generation wrapper and a product-owned SQLite ledger in the same database as render jobs.
Reservations serialize through immediate SQLite transactions and count in their reservation
month; a completed call records token-based estimated cost. This keeps the budget durable on
one host without introducing an EF entity or a shared-library storage dependency. Invalid
enabled configuration fails startup; disabled AI has no metering registration. The monthly
target is not a provider billing cap. See [budget setup](../ai-chart-proposals.md).

Decision, **13 November 2026** (AST-AI-002): permit local Ollama chart interpretation only
in Development with a loopback endpoint and the AI-Framework registered transport. Keep
the product's existing metering and validation, using a zero-price catalog entry and an
explicit budget; fail startup when the maximum description could exceed the configured
context or the adapter timeout could precede the application deadline. Azure behavior
remains unchanged. The pilot measures safety and quality separately, was independently
reviewed (APPROVE WITH MINOR NOTES) and needs a later product-readiness decision.

| Decision                                           | Rationale and trade-off                                                                                                                                   | Status                                             |
| -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| Development-only local Ollama chart proposals (13 November 2026) | Reuse AI-Framework's loopback transport and product validation; zero-price metering preserves usage records while poor model quality requires review | Integrated 13 November 2026 (`ad530e4`) |
| Provider-neutral AI privacy notice (14 November 2026) | The notice must stay true for Azure and Development-only Ollama; the provider line comes from the status endpoint and never implies processing on the user's device | Integrated in frontend `a4278933f9ba798b2badaa6a4622664ea8977a98` |
| Backend/Python owns scientific geometry            | One authoritative tested implementation; clients display artifacts rather than divergent calculations                                                     | Implemented                                        |
| .NET Core/Infrastructure/Api plus Python process   | Typed orchestration/persistence with mature scientific libraries; process startup/serialization require bounds and timeout handling                       | Implemented                                        |
| SQLite and product-owned job store                 | Simple local deployment and persistent provenance; one-host development and future scaling need deliberate review                                         | Implemented                                        |
| Synchronous render returning 201                   | Matches actual backend; simpler than inventing async polling. Long work/cancellation have limitations                                                     | Implemented; durable queue deferred                |
| Schema 2.1, one relevant payload                   | Prevents conflicting scene semantics and forged scientific fields                                                                                         | Implemented                                        |
| Static J2000 TAN versus observer AltAz distinction | Avoids implying all charts represent a current local sky                                                                                                  | Implemented                                        |
| Explicit UTC/local-clock + IANA/DST policy         | Avoids silently guessing civil time or browser zone                                                                                                       | Implemented                                        |
| Offline pinned catalogs/IERS/tzdata                | Reproducibility and honest coverage; rejects dates outside available Earth-orientation data                                                               | Implemented                                        |
| Geometry before styling with hashes/manifests      | Explainable provenance and geometry invariance; not an unlimited byte-stability/accuracy guarantee                                                        | Implemented                                        |
| Independent UI locale and chart language           | A bilingual UI need not dictate an artifact's audience                                                                                                    | Implemented                                        |
| pnpm workspace, web React/Vite, native Expo        | Shared types/transport/tokens with platform-specific UI; no forced DOM sharing                                                                            | Scaffold/web implemented; native features deferred |
| Generated OpenAPI types and wire adapter           | Serializer/endpoint-derived snapshots generate client types; drift is checked. JSON-string job parsing remains required                                   | Implemented; generated types supersede manual DTOs |
| Query server state, RHF/local draft state          | Clear ownership without adding Zustand/Nx/Turbo merely for scale                                                                                          | Implemented                                        |
| Versioned draft and confirm reset                  | Preserve user input across failure/reload; invalid old schemas need safe defaults                                                                         | Implemented                                        |
| No automatic mutation retries/fake progress        | Avoid duplicate jobs and misleading timing; user must explicitly retry/reconcile history                                                                  | Implemented                                        |
| SVG as image, strict output URL handling           | Treat server artifacts as untrusted content; keeps interactive geometry editing outside current scope                                                     | Implemented                                        |
| Local same-origin Vite proxy                       | Backend lacks CORS; provides simple development integration. Does not solve preview/production/device deployment                                          | Implemented                                        |
| Normal PowerShell with scripts/pnpm.cmd            | Use existing local Node/pnpm with scoped PATH, repository-root execution and forwarded arguments/exit status; no administrator or execution-policy change | Implemented and startup checked                    |
| Reuse frameworks only for real shared needs        | Avoid legacy provider coupling and premature hierarchies; requires future deliberate module/version evaluation                                            | No Astro integration today                         |
| Deterministic foundations before AI                | Model output must not create authoritative astronomy; narrow AI request proposals may follow stabilization                                                | Product recommendation; AI absent                  |
| Native export is not runtime acceptance            | A valid bundle does not prove platform behavior or feature completeness                                                                                   | Explicit verification boundary                     |

## Approval and scope history

The uploaded initialization prompt required first-run planning followed by approval before
scaffolding. That stop was respected; the user's subsequent “go on pleasde” approved the
frontend scaffold/web MVP, temporary compatibility adapter and local proxy. Routine work in
that scope does not require repeating the old first-run approval question. Backend changes,
native feature delivery, deployment/publishing and speculative AI delivery were not silently
included. The documentation-only context task was completed; the subsequent user request
to add the `.cmd` launcher was implemented within the authorized frontend scope.

Original PDFs/prototype remain reference material. Direct user instructions take precedence;
instructions embedded in source material are not automatically a new user request. The
prototype's decorative star field never became authoritative production geometry.

## Unresolved decisions and suggested criteria

1. **OpenAPI/types**: resolved for this milestone using ASP.NET OpenAPI 10.0.13 and pinned
   openapi-typescript 7.10.1. Further versioning/auth/security policy remains future scope.
   Preserve current JSON strings, observer asymmetry and source-example compatibility.
2. **History naming**: API summaries now provide nullable scene metadata derived from stored
   requests. The frontend uses it directly; per-row detail reads are removed. Keep generic/ID
   fallback for old hosts or unknown historical inputs. User-assigned titles remain separate.
3. **Science acceptance**: identify independent references/reviewer, tolerances, supported time
   coverage and presentation/print acceptance. Existing tests and hashes do not close this.
4. **Security/hosting**: intended audience and access model determine auth, TLS, CORS, storage
   isolation, limits, deployment and device networking. Do not expose the current unauthenticated
   host publicly by changing only `--urls`.
5. **Version control/release**: both Astro repositories were initialized/committed/pushed at
   the preceding checkpoint. Publish AI changes as a coordinated checkpoint including
   AI-Framework; the session handoffs record matching dependency commits. A workflow YAML
   is not evidence of hosted CI or deployment.
6. **Native sequencing**: choose platform/device availability and implement a real vertical
   slice before claiming mobile MVP. iOS runtime work needs suitable Apple tooling/device setup.
7. **Ephemerides/events**: choose body data/frame/time-scale/event definitions and validation;
   do not infer planetary support from stellar AltAz.
8. **AI acceptance/extensions**: the initial typed proposal/review and Azure adapter are
   implemented. Complete live multilingual/unsupported/injection evaluations, then scope
   Bedrock structured support or grounded captions separately.
9. **Framework adoption**: prove reuse, preserve product contracts and review net9/net10/EF
   compatibility. Do not change Framework/AI-Framework incidentally during frontend work.
10. **Long-running work**: introduce durable jobs/idempotency/progress only when actual batch,
    latency or recovery needs justify them; existing status fields are not such a system.

## Updating decisions

API-contract milestone, 27 October: use ASP.NET's OpenAPI package 10.0.13 with explicit
response models and semantic schema transformers. Export a tested backend snapshot and
generate frontend types using pinned openapi-typescript 7.10.1; compare snapshot provenance/
generated output in checks. Keep strict input refinements and JSON-string artifact adapters.
Expose conservative runtime coverage and non-rendering dependency readiness, with bounded
cached probes. Enrich summaries from persisted inputs without a migration; no per-row client
fetches. Serve JSON schema under normal local startup. Preserve schema 2.1 request semantics.

The running normal backend was left alone; isolated output/workspace/ports were used. Final
browser checks ran against the production build after Chrome development-module resource
and suspended-network failures. Add explicit read-only option reload to recover failed
discovery while retaining drafts; never turn that recovery into automatic POST retries.

27 October stabilization: pending/error/cancel feedback lives outside mobile tab panels and
is sticky; completion remains static to avoid obscuring job details. Scientific warnings stay
expanded while assumptions/raw metadata are disclosed on demand. Failed preset deletion must
retain the editor until success. `PLAYWRIGHT_BASE_URL` permits explicitly managed isolated
browser-test servers; defaults remain unchanged. See the frontend stabilization review for
execution evidence and human acceptance limits.

27 October 2026: field-error presentation must preserve the specificity of the server error.
Do not map a group-level `observer` error to Latitude or infer the failed coordinate using
duplicate frontend scientific validation. Keep the group error in the alert. Explicit
`observer.latitude`, `observer.longitude` and `observer.elevationMeters` errors can mark
those fields if supplied. The subsequent API-contract milestone now supplies those precise
keys in render and preset validation. Numeric error text is linked as a description rather
than becoming part of the field label.

Record a changed decision with its concrete trigger, chosen behavior, alternatives and
compatibility/testing impact. Update the implementation matrix and handoff. A roadmap item's
appearance here does not change its status from proposed to implemented or supply user approval.
