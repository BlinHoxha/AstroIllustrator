using System.Text.Json;
using System.Text.Json.Serialization;
using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.CreateSchemaReferenceId = info => info.Type.IsEnum || info.Kind == System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object
        ? info.Type.Name : null;
    options.AddSchemaTransformer<ContractSchemaTransformer>();
    options.AddOperationTransformer(async (operation, context, token) =>
    {
        operation.Responses ??= new();
        operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Unexpected server error (ProblemDetails with traceId)." });
        var problem = await context.GetOrCreateSchemaAsync(typeof(ApiProblemResponse), null, token);
        foreach (var response in operation.Responses)
            if (int.TryParse(response.Key, out int status) && status >= 400 && !(operation.OperationId == "GetReadiness" && status == 503) && response.Value is OpenApiResponse errorResponse)
                errorResponse.Content = new Dictionary<string, OpenApiMediaType>
                { ["application/problem+json"] = new() { Schema = problem } };
        if (operation.OperationId == "DownloadRenderOutput" && operation.Responses.TryGetValue("200", out var downloadResponse) && downloadResponse is OpenApiResponse download)
        {
            download.Content ??= new Dictionary<string, OpenApiMediaType>();
            foreach (string contentType in new[] { "image/svg+xml", "image/png" })
                download.Content[contentType] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } };
        }
        if (operation.OperationId is "RenderIllustration" or "CreateObserver" && operation.Responses.TryGetValue("201", out var createdResponse) && createdResponse is OpenApiResponse created)
        {
            created.Headers ??= new Dictionary<string, IOpenApiHeader>();
            created.Headers["Location"] = new OpenApiHeader { Description = "Relative URL of the created resource", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        }
    });
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "AstroIllustrator API";
        document.Info.Version = "1";
        document.Servers = [new OpenApiServer { Url = "/" }];
        document.Info.Description = "Schema 2.1 deterministic astronomy API. Rendering is synchronous; job requestJson/resultJson remain serialized JSON strings. Local-first host; authentication is not implemented.";
        return Task.CompletedTask;
    });
});
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64_000);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddConcurrencyLimiter("render", limiter =>
    {
        limiter.PermitLimit = 2;
        limiter.QueueLimit = 8;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});
string root = Path.GetFullPath(builder.Configuration["WorkspaceRoot"] ?? "../..", builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.Combine(root, "data"));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
    c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    o.SerializerOptions.RespectRequiredConstructorParameters = true;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.Converters.Add(new StrictInstantConverter());
});
builder.Services.AddOptions<RendererOptions>().Configure(o =>
{
    o.WorkspaceRoot = root;
    o.PythonExecutable = builder.Configuration["Renderer:PythonExecutable"] ?? Path.Combine(root, ".venv",
        OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python");
    o.TimeoutSeconds = builder.Configuration.GetValue("Renderer:TimeoutSeconds", 60);
}).Validate(o => File.Exists(o.PythonExecutable) && o.TimeoutSeconds is >= 1 and <= 300,
    "Configure an existing Python executable and a timeout between 1 and 300 seconds.").ValidateOnStart();
builder.Services.AddSingleton<IRendererAdapter, PythonRendererClient>();
builder.Services.AddSingleton<IRendererDiagnostics, RendererDiagnostics>();
builder.Services.AddDbContext<AstroDbContext>(o => o.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    { DataSource = Path.Combine(root, "data", "astroillustrator.db") }.ToString()));
builder.Services.AddScoped<IRenderJobStore>(p => p.GetRequiredService<AstroDbContext>());
builder.Services.AddScoped<RenderJobService>();
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async status => await Results.Problem(statusCode: status.HttpContext.Response.StatusCode,
    title: Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status.HttpContext.Response.StatusCode))
    .ExecuteAsync(status.HttpContext));
app.UseRateLimiter();
app.MapOpenApi();
app.MapRendererCapabilities();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AstroDbContext>();
    await db.Database.MigrateAsync();
    // A process interruption cannot leave a job running after restart.
    foreach (var stale in await db.RenderJobs.Where(x => x.Status == RenderStatus.Running || x.Status == RenderStatus.Queued).ToListAsync())
    {
        stale.Status = RenderStatus.Failed;
        stale.ErrorCode = "host_interrupted";
        stale.CompletedAtUtc = DateTimeOffset.UtcNow;
    }
    await db.SaveChangesAsync();
}

app.MapGet("/health/live", () => Results.Ok(new HealthResponse("Healthy")))
    .WithName("GetLiveness").Produces<HealthResponse>();
using var definitions = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "renderer", "catalogs", "scene-definitions-v1.json")));
var constellations = definitions.RootElement.GetProperty("constellations").Deserialize<ConstellationResponse[]>(PythonRendererClient.JsonOptions)!;
var asterisms = definitions.RootElement.GetProperty("asterisms").Deserialize<AsterismResponse[]>(PythonRendererClient.JsonOptions)!;
app.MapGet("/api/constellations", () => Results.Ok(constellations)).WithName("GetConstellations").Produces<ConstellationResponse[]>();
app.MapGet("/api/asterisms", () => Results.Ok(asterisms)).WithName("GetAsterisms").Produces<AsterismResponse[]>();
app.MapGet("/api/diagram-templates", () => Results.Ok(new[]
{
    new DiagramCapability("StarAltitude", ["Schematic", "Computed"]),
    new DiagramCapability("PolarisLatitude", ["Schematic", "Computed"]),
    new DiagramCapability("AltitudeAzimuth", ["Schematic"]),
    new DiagramCapability("AngularSeparation", ["Computed"])
})).WithName("GetDiagramTemplates").Produces<DiagramCapability[]>();
app.MapGet("/api/catalogs", () =>
{
    string path = Path.Combine(root, "renderer", "catalogs", "bright-hyg-v41.json");
    using var catalog = JsonDocument.Parse(File.ReadAllText(path));
    var metadata = catalog.RootElement.EnumerateObject().Where(p => p.Name != "stars").ToDictionary(p => p.Name, p => p.Value.Clone());
    return Results.Ok(new CatalogResponse(metadata, catalog.RootElement.GetProperty("stars").GetArrayLength(), Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))));
}).WithName("GetCatalog").Produces<CatalogResponse>();
app.MapGet("/api/presets", () => Results.Ok(new[] { "DarkAtlas", "PrintMonochrome" }))
    .WithName("GetPresets").Produces<string[]>();
app.MapPost("/api/illustrations/render", async (IllustrationRequest request, RenderJobService service,
    ILogger<Program> logger, HttpContext context) =>
{
    var issues = IllustrationValidator.Validate(request);
    if (issues.Count > 0) return Results.ValidationProblem(issues.GroupBy(x => x.Field)
        .ToDictionary(x => x.Key, x => x.Select(i => i.Message).ToArray()));
    string json = JsonSerializer.Serialize(request, PythonRendererClient.JsonOptions);
    var job = await service.RenderAsync(request, json, context.RequestAborted);
    logger.LogInformation("Render {RenderJobId} finished with {RenderStatus} ({ErrorCode})", job.Id, job.Status, job.ErrorCode);
    return job.Status == RenderStatus.Completed ? Results.Created($"/api/render-jobs/{job.Id}", job)
        : Results.Problem(statusCode: job.ErrorCode switch { "renderer_timeout" => 504, "scientific_unsupported" => 422, _ => 503 },
            title: "Render did not complete", detail: job.ErrorMessage, extensions: new Dictionary<string, object?> { ["jobId"] = job.Id, ["code"] = job.ErrorCode });
}).RequireRateLimiting("render").WithName("RenderIllustration").Produces<RenderJob>(201)
  .ProducesValidationProblem().ProducesProblem(422).ProducesProblem(429).ProducesProblem(503).ProducesProblem(504)
  .WithDescription("Synchronous render, returning a completed job with 201 and Location. Validation failures create no job. Scientific/renderer failures can include jobId/code. Disconnecting does not guarantee no persisted job; no automatic POST retries.");
app.MapGet("/api/render-jobs", async (int? page, int? pageSize, string? status, IRenderJobStore store, CancellationToken token) =>
{
    int number = page ?? 1, size = pageSize ?? 20;
    RenderStatus? filter = null;
    if (number is < 1 or > 10000 || size is < 1 or > 100) return Results.Problem(statusCode: 400, title: "Page must be 1–10000 and pageSize 1–100.");
    if (status is not null)
    {
        if (!Enum.TryParse<RenderStatus>(status, out var parsed) || !Enum.IsDefined(parsed) || status != parsed.ToString())
            return Results.Problem(statusCode: 400, title: "Unknown job status.");
        filter = parsed;
    }
    return Results.Ok(await store.ListAsync(number, size, filter, token));
}).WithName("GetRenderJobs").Produces<RenderJobPage>().ProducesProblem(400);
app.MapPost("/api/astronomy/resolve-time", (TimeReference request) =>
{
    try { return Results.Ok(new ResolvedTimeResponse(ObserverTime.Resolve(null, request))); }
    catch (ArgumentException e) { return Results.Problem(statusCode: 400, title: "Invalid observation time", detail: e.Message); }
}).WithName("ResolveTime").Produces<ResolvedTimeResponse>().ProducesProblem(400)
  .WithDescription("Resolves civil time and nominal year bounds only. Consult capabilities and render validation for actual IERS support.");
app.MapObserverPresets();
app.MapGet("/api/render-jobs/{id:guid}", async (Guid id, IRenderJobStore store, CancellationToken token) =>
    await store.FindAsync(id, token) is { } job ? Results.Ok(job) : Results.NotFound())
    .WithName("GetRenderJob").Produces<RenderJob>().ProducesProblem(404);
app.MapGet("/api/render-jobs/{id:guid}/outputs", async (Guid id, IRenderJobStore store, CancellationToken token) =>
{
    var job = await store.FindAsync(id, token);
    if (job?.Status != RenderStatus.Completed || job.ResultJson is null) return Results.NotFound();
    var result = JsonSerializer.Deserialize<RendererResult>(job.ResultJson, PythonRendererClient.JsonOptions)!;
    return Results.Ok(result.Outputs.Select(output => new OutputResponse(output.Format, output.FileName, output.Sha256,
        $"/api/render-jobs/{id}/outputs/{output.Format}")));
}).WithName("GetRenderOutputs").Produces<OutputResponse[]>().ProducesProblem(404);
app.MapGet("/api/render-jobs/{id:guid}/outputs/{format}", async (Guid id, string format, IRenderJobStore store, CancellationToken token) =>
{
    if (format is not ("svg" or "png" or "manifest")) return Results.NotFound();
    var job = await store.FindAsync(id, token);
    if (job?.Status != RenderStatus.Completed) return Results.NotFound();
    string name = format == "manifest" ? "manifest.json" : $"chart.{format}";
    string path = Path.Combine(root, "artifacts", id.ToString("N"), name);
    return File.Exists(path) ? Results.File(path, format switch { "svg" => "image/svg+xml", "png" => "image/png", _ => "application/json" }, name)
        : Results.NotFound();
}).WithName("DownloadRenderOutput").Produces(200, contentType: "image/svg+xml")
  .Produces(200, contentType: "image/png").Produces<RenderManifestResponse>(200, "application/json").ProducesProblem(404)
  .WithDescription("Completed-job SVG, PNG or manifest attachment. File SHA-256 is in output metadata. Manifest has independent scientific provenance and is not recursively self-hashed.");
app.Run();

public partial class Program;
