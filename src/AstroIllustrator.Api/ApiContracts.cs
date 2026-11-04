using System.Text.Json;
using AstroIllustrator.Core;
using Microsoft.EntityFrameworkCore;

public sealed record HealthResponse(string Status);
public sealed record ReadinessResponse(string Status, bool RendererReady, bool DatabaseReady, string? Code);
public sealed record ResolvedTimeResponse(DateTimeOffset InstantUtc);
public sealed record ConstellationResponse(string Code, string Name, IReadOnlyList<int[]> Edges);
public sealed record AsterismResponse(string Code, string Name, IReadOnlyList<string> ConstellationCodes,
    IReadOnlyList<int> StarHipIds, IReadOnlyList<int[]> Edges);
public sealed record CatalogResponse(IReadOnlyDictionary<string, JsonElement> Metadata, int StarCount, string Sha256);
public sealed record OutputResponse(string Format, string FileName, string Sha256, string DownloadUrl);
public sealed class ApiProblemResponse
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }
    public string? TraceId { get; init; }
    public string? Code { get; init; }
    public Guid? JobId { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
// Describes the Python-produced JSON attachment; it is never reserialized by the API.
public sealed record RenderManifestResponse(string SchemaVersion, string RendererVersion, IllustrationRequest Request,
    string GeometryHash, JsonElement Geometry, IReadOnlyDictionary<string, JsonElement> ScientificResults,
    IReadOnlyList<string> ScientificAssumptions, IReadOnlyDictionary<string, JsonElement> Catalog,
    string CatalogSha256, JsonElement LineSet, string LineSetSha256, IReadOnlyDictionary<string, string> Libraries,
    IReadOnlyList<RenderOutput> Outputs);

public static class ReadinessEndpoints
{
    public static void MapRendererCapabilities(this WebApplication app)
    {
        app.MapGet("/api/capabilities", async (IRendererDiagnostics diagnostics, CancellationToken token) =>
        {
            var probe = await diagnostics.CheckAsync(token);
            return probe.Ready ? Results.Ok(probe.Capabilities) : Results.Problem(statusCode: 503,
                title: "Renderer capabilities unavailable", detail: "Restore renderer dependencies and catalogs, then retry.",
                extensions: new Dictionary<string, object?> { ["code"] = probe.Code });
        }).WithName("GetCapabilities").Produces<RendererCapabilities>().ProducesProblem(503)
          .WithDescription("Actual installed renderer capabilities and conservative offline IERS observation coverage. Cached for up to 30 seconds; not a guarantee of every scientific request.");

        app.MapGet("/health/ready", async (IRendererDiagnostics diagnostics,
            AstroIllustrator.Infrastructure.AstroDbContext db, CancellationToken token) =>
        {
            var probe = await diagnostics.CheckAsync(token);
            bool databaseReady;
            try { databaseReady = await db.Database.CanConnectAsync(token); }
            catch (Exception) when (!token.IsCancellationRequested) { databaseReady = false; }
            bool ready = databaseReady && probe.Ready;
            return Results.Json(new ReadinessResponse(ready ? "Ready" : "NotReady", probe.Ready, databaseReady,
                !databaseReady ? "database_unavailable" : probe.Code), statusCode: ready ? 200 : 503);
        }).WithName("GetReadiness").Produces<ReadinessResponse>().Produces<ReadinessResponse>(503)
          .WithDescription("Checks database connectivity, Python imports, catalogs, bundled IERS and writable artifact/cache storage. Does not create a render or guarantee future requests; renderer checks cached up to 30 seconds.");
    }
}
