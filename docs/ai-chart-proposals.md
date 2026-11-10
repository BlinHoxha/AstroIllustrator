# Describe your chart — first AI milestone

Implemented locally on 4 November 2026. Azure OpenAI is the first provider; live Azure
credentials, deployment compatibility and model-quality evaluation are separate checks.

## Ownership and boundaries

AI-Framework supplies `IStructuredGenerationModel`, strict structured output, bounded Azure
transport, refusal/incomplete handling and token-usage metadata. Existing document Q&A,
retrieval and Bedrock chat behavior are preserved. General Framework is not referenced.

Astro owns the proposal schema, supported astronomy options, product validation, request
limits, the SQLite budget ledger and the review workflow. AI never renders, resolves a location/time or computes
scientific geometry. Applying settings changes the editor only; Generate uses the existing
schema 2.1 renderer and its scientific validation. The budget ledger has its own SQL migration
and is outside the EF entity model.

Current supported proposals cover existing sky scenes and diagram templates. Unsupported
scenes, planets/events/art requests, refusals, incomplete output and invalid settings cannot
be applied. Missing scene/selectors/mode require a refined description; missing numerical
diagram inputs can be completed in the form. Numeric angles and HIP identifiers must occur
as digits in the description to be retained; spelled-out numbers can be entered manually.
Chart language changes only when explicitly proposed; description language is independent.

Coordinates and observation times are intentionally entered or selected in the form in this
version. Applying an observation-dependent proposal clears inherited coordinates and times,
rather than silently reusing example values. Unfinished numeric fields survive draft reload.
No geocoding, relative-date interpretation, location permission or automatic tool execution
is implemented. Existing form language/style/magnitude remain when unspecified.

## Dependencies and provider setup

Checkout the updated `AI-Framework` repository as a sibling of `AstroIllustrator`. Infrastructure
currently references its Azure project by source; that transitively supplies Core. A backend
checkout/CI therefore needs both repositories, even when AI is disabled. No shared package
has been published. The net9.0 libraries compile into the net10.0 Astro host; no general
Framework/EF provider stack is imported.

AI is **disabled by default**. Normal forms, rendering, startup and renderer readiness do not
require Azure credentials or call Azure. `GET /api/chart-proposals/status` reports configuration,
not a successful cloud probe. Restart the backend after changing configuration.

To enable it, first provision a deployment that supports strict JSON-schema output and
`max_completion_tokens`. Authenticate the backend process through Azure's existing
`DefaultAzureCredential` chain, for example an authorized Azure CLI/Visual Studio session
locally or workload identity when deployed. Grant the appropriate Azure OpenAI access;
never put credentials in the web/native clients or commit local configuration.

In the backend PowerShell window:

```powershell
$env:ChartAI__Enabled = 'true'
$env:ChartAI__Provider = 'AzureOpenAI'
$env:ChartAI__Azure__Endpoint = 'https://YOUR-RESOURCE.openai.azure.com/'
$env:ChartAI__Azure__Deployment = 'YOUR-STRUCTURED-OUTPUT-DEPLOYMENT'
$env:ChartAI__TimeoutSeconds = '25'
$env:ChartAI__MaxRequestsPerDay = '20'
$env:ChartAI__MaxOutputTokens = '1024'
$env:ChartAI__Budget__MonthlyAmount = '<positive monthly operating target>'
$env:ChartAI__Budget__Currency = '<three-letter uppercase currency>'
$env:ChartAI__Budget__TenantKey = 'local'
$env:ChartAI__Pricing__Model = '<deployed model identifier>'
$env:ChartAI__Pricing__InputPerMillionTokens = '<configured input price>'
$env:ChartAI__Pricing__OutputPerMillionTokens = '<configured output price>'
$env:ChartAI__Pricing__PriceVersion = '<operator price version>'

.\.tools\dotnet\dotnet.exe run --project .\src\AstroIllustrator.Api --no-launch-profile --urls "http://127.0.0.1:5188"
```

The endpoint is an HTTPS resource root, without credentials/query/path. No model is silently
selected. The adapter uses the existing Azure token transport and stable `2024-10-21` API;
an incompatible deployment fails explicitly, without falling back to unconstrained text.
When AI is enabled, all budget and pricing fields shown above except `TenantKey` are required.
The monthly amount must be positive; prices must be nonnegative. Invalid or missing values
fail startup. The price currency is the budget currency, and the tenant key defaults to `local`.
When AI is disabled, none of these values is required or registered.
See [Microsoft's structured-output documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/structured-outputs).

Setting `ChartAI__Enabled` back to `false` and restarting disables interpretation. The
frontend can recheck configuration using Check AI availability again, or reload.

## HTTP and review workflow

- `GET /api/chart-proposals/status`: enabled flag, provider label and maximum description length.
- `POST /api/chart-proposals`: `{ "description": "Create an Orion chart with Albanian labels", "language": "en" }`.
- Response: `status` (`Ready`, `NeedsInput`, `Unsupported`, `Invalid`), nullable `parameters`,
  required form fields, validation issues and provider label. This is not a render job.
- Description length is 1–2000 characters. Interface language is en/sq; model output has a
  closed schema and is independently validated. OpenAPI and generated clients include these routes.
- Errors preserve ProblemDetails trace IDs and codes: `ai_disabled`, `ai_busy`, `ai_daily_limit`,
  `ai_budget_exhausted` (429), `ai_metering_unavailable` (503),
  `ai_timeout`, `ai_provider_unavailable`, `ai_refused`, `ai_incomplete`, `ai_invalid_response`.
  HTTP 429 also covers the fixed-window limiter. No automatic interpretation/render POST retry.

In Studio, type a description and select Suggest chart settings. The privacy notice explains
that the description leaves the machine for Azure. Inspect the proposed fields and missing
inputs, confirm Apply settings to form, complete observation/diagram inputs, then Generate.
Unsupported/invalid responses have no applicable parameters. Cancellation and failures retain
both description and chart draft. Model text is never inserted as HTML or executed.

## Limits and operational scope

The local host allows five interpretation submissions per minute, one active provider call,
25 seconds by default and 100 attempts per UTC day by default (configurable). Attempts,
including provider failures, count against the daily cap. The daily counter is in memory;
restart resets it and multiple host instances have independent counters. It is not a durable
account budget. A separate durable SQLite ledger enforces the configured monthly
product/environment budget for chart proposals. It reserves an upper bound before calling
the provider and records an estimated cost from reported token usage and configured prices
afterward. A provider failure keeps the reservation as the estimated cost. The budget is an
operating target, not a provider billing cap; pricing or usage reports can differ from invoices.
The SQLite ledger is intended for a single host using this database, not distributed hosts.
Output is capped at 4096 tokens (default 1024) and provider JSON at 128 KiB.
Logs include result/provider and token counts, not descriptions or generated payloads.
Token counts are not monetary billing; resource-side budgets/monitoring remain an operator task.

The host remains a loopback single-user application. Shared/public access still requires the
separate authentication, authorization and deployment milestone.

## Verification and evaluations

Provider tests use fake HTTP/credentials and cover strict schema transport, malformed/extra/
duplicate fields, refusal, truncation, response bounds and cancellation. Product tests cover
validation, explicit missing inputs/numbers, no job creation, timeout/concurrency/quota and
disabled mode. Browser tests mock AI output to verify review/apply, accessibility, failure,
cancellation and draft recovery, alongside real scientific rendering tests.

These tests do **not** establish live model accuracy. After configuring Azure, record outcomes
for [the evaluation cases](qa/chart-ai-evaluation-cases.json), including the deployment/version,
date, expected/actual proposal, unwanted guesses and token usage. Do not mark unexecuted cases
as passed. Prompt restrictions plus schema validation cannot prove semantic understanding;
user review and authoritative rendering remain required.

## Later Bedrock support

Add a Bedrock adapter implementing the same structured-generation interface, with equivalent
schema/refusal/incomplete/usage guarantees and contract tests. Extend the host's provider
configuration selector. Both adapters can coexist, with one selected for each operation.
The existing Bedrock document-answer adapter is not yet a structured chart interpreter;
there is no silent cross-provider fallback or automatic second paid call.
