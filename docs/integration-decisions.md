# Integration decisions — 22 October 2026

Based on the eight v2.1 specification PDFs and a source inspection of sibling Framework
and AI-Framework repositories. Both shared repositories remain independently maintained.

1. Core has no project/package references. Api composes Core and Infrastructure.
2. SQLite is application metadata, not an astronomy catalog. Scientific catalogs are files.
3. Do not reference Framework.Infrastructure/Application compatibility facades: they pull
   mapping and database providers that do not serve this local SQLite workflow.
4. Framework.Persistence currently adds only a thin DbContext base. Use EF directly here;
   reconsider if shared behavior develops. Framework.Contracts pagination and the PostgreSQL
   adapter are optional later reuse points. No source copying from shared frameworks.
5. AI-Framework's actual implemented workflow is evidence-grounded document answering.
   Reuse it for optional educational captions. It does not implement schema-constrained
   prompt interpretation or background generation. Propose any shared enhancement before
   modifying that repository; do not force illustration requests through a RAG answer API.
6. Workspace project references are appropriate when a real integration appears. Use pinned
   private NuGet packages for independently released products, with no duplicate assembly
   consumption through both mechanisms. No shared reference is required for the first slice.
7. Use .NET 10 for the new product. The shared frameworks currently target .NET 9; introducing
   EF-related references later requires explicit dependency alignment and compatibility tests.
8. Keep scientific calculations in Python. Never accept AI-supplied celestial geometry as
   authoritative. Styles alter presentation, not projected coordinates.

Next milestones: complete Orion reference review and Ursa Minor; generalized scenes and
asterisms; global observer/time/horizon; scientific diagrams; ephemerides/solar-relative
events; bounded event search; typed AI interpretation; optional scenery/captions/batch output.
