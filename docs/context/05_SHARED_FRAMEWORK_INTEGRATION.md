# Framework and AI-Framework integration boundaries

Checkpoint: 26 October 2026. All four repositories were accessible and inspected.
**AstroIllustrator currently references neither Framework nor AI-Framework.** No existing
capability in those libraries should be described as an AstroIllustrator integration.

## Framework: actual library capabilities

Evidence: sibling [README](../../../Framework/README.md),
`docs/DEPENDENCY_CLEANUP.md`, and project/source files under `Framework/src/`.

| Module                                         | Inspected responsibility                             | AstroIllustrator use today             |
| ---------------------------------------------- | ---------------------------------------------------- | -------------------------------------- |
| Domain                                         | Entity bases/domain exceptions                       | None                                   |
| Contracts                                      | Common result, pagination and health contracts       | None; Astro uses its own HTTP shapes   |
| Application.Abstractions                       | Provider-neutral application interfaces              | None                                   |
| Persistence                                    | Thin provider-neutral EF DbContext base              | None; AstroDbContext is product-owned  |
| Persistence.PostgreSql / Persistence.SqlServer | Optional provider DI adapters                        | None; current product uses SQLite      |
| Mapping.AutoMapper                             | Optional mapping/generic CRUD support                | None                                   |
| Application                                    | Compatibility/type-forwarding surface                | None                                   |
| Infrastructure                                 | Heavier legacy mapping/SQL provider dependency graph | None                                   |
| Web                                            | Generic base CRUD controllers                        | None; Astro uses explicit minimal APIs |
| Api                                            | Reference host                                       | Not a mandatory shared service         |

These modules target .NET 9 at this checkpoint; Astro targets .NET 10/EF Core 10. A simple
net9 library may be consumable by net10, but provider/EF/package compatibility must be
reviewed deliberately. Do not bulk-reference legacy Infrastructure to obtain one helper,
or infer that all versions/providers are already aligned. Existing framework test counts
or security notes in its own documents are not current Astro verification.

### Sensible candidate reuse

- Common result/pagination/health interfaces if they solve a demonstrated problem, with
  an adapter preserving the existing Astro wire contract.
- Provider-neutral application interfaces where an actual shared workflow exists.
- Optional persistence/provider modules only if the product chooses those providers and
  the dependency versions/migration ownership are made explicit.
- Existing hosting/infrastructure patterns as references, not evidence of deployed Astro
  infrastructure or automatically inherited security.

The existing three-layer backend, small job-store port and SQLite model do not need a
generic repository/UoW or CRUD controller hierarchy merely to resemble the framework.
Sharing code is useful when it removes real duplication with compatible semantics.

## AI-Framework: actual library capabilities

Evidence: sibling [README](../../../AI-Framework/README.md), architecture/getting-started
documents, `src/` Core/Application/Local/Azure/Aws implementations and DI registration.
Its libraries target .NET 9/version 0.1.0 and are not dependencies of Astro today.

- **Core contracts**: `ITextChunker`, `IKnowledgeIndex`, `IKnowledgeRetriever`, `IChatModel`,
  `ITextEmbeddingModel`, `IDocumentProfileCatalog`, `IDocumentTextExtractor` plus knowledge,
  profile, tenant/scope and grounded-generation models. The extractor interface alone does
  not implement OCR or arbitrary document ingestion.
- **Application**: `DocumentIngestionService` validates text/profile/tenant and chunks/indexes;
  `KnowledgeAnswerService` validates questions, retrieves evidence, rechecks access/profile
  filters and generates grounded answers/citations. Empty evidence avoids model generation.
- **Local**: in-memory indexing/retrieval and `LocalGroundedChatModel`, which extracts/combines
  evidence text with review messaging. It is not a learned local LLM or durable vector store.
- **Azure**: real adapter code for Azure OpenAI chat/embeddings and Azure Search index/retrieval,
  explicit DI registrations and Azure credential integration. Source/mock-test evidence is
  not a live-cloud verification or proof any Astro credentials/deployment exist.
- **Aws**: Bedrock chat adapter and registration. Do not infer AWS retrieval/embedding features
  from generation support.
- **Sample host**: an example composition, not a service Astro must call or copy wholesale.

The application enforces useful retrieval filters, but the consuming host still owns
authentication/authorization, human approval, budgets, privacy, durable storage and operational
policy. “Grounded” indicates evidence was supplied; it is not a formal correctness proof or
guarantee that every generated assertion is cited. Profile review metadata is not a UI approval
flow unless the host implements one.

## Proposed product use

Grounded educational captions are a plausible first reuse of AI-Framework retrieval/generation
with curated astronomy material, product-specific profiles, citations and review. Choose and
verify a provider deliberately; neither a cloud provider nor model is selected for Astro.

Natural-language-to-render-request is a separate structured interpretation problem. The current
grounded-answer API does not already provide a typed astronomy command parser. Design a narrow
proposal contract and evaluation; extract a reusable structured-generation abstraction only if
multiple products need it. Do not force every prompt through document Q&A or introduce RAG when
the task merely selects an already-known scene and parameters.

Scientific geometry remains in the product's Python pipeline. No shared AI contract should
accept invented coordinates as authoritative. The web/native client consumes reviewed typed
requests and backend artifacts; it does not call providers with embedded keys.

## Ownership and anti-overengineering decisions

Keep these project-specific: astronomy catalogs/guide definitions, coordinate transformations,
ephemerides/event semantics, scientific assumptions/accuracy, render schema, artifact generation,
manifest/hash rules, render lifecycle and product permission decisions. Keep generic transport,
tokens and neutral helpers in frontend shared packages. Consider shared framework additions only
after checking existing contracts and proving reuse, without changing another product's semantics.

Do not add a shared platform service, message bus, durable queue, generic CRUD hierarchy,
multi-agent orchestrator or cross-product database merely because it might be useful later.
Do not create duplicate AI/provider plumbing if an existing suitable adapter can be reused.

The frontend repository's existing instructions prohibit modifying Framework/AI-Framework
within its current scope. The present documentation task inspected them read-only. Framework's
pre-existing `docs/documentation.code-workspace` change belongs to the user and must remain intact.

## Decisions still required before adoption

1. Demonstrated shared requirement and smallest compatible module.
2. .NET/EF/provider version strategy and test boundary.
3. Product wire-contract compatibility, ownership and migration plan.
4. For AI: typed proposal/review UX, evidence collection/licensing, provider, privacy/cost limits,
   failure behavior and multilingual/scientific evaluation.
5. Which generic abstractions deserve extraction after a second concrete consumer appears.
