using System.Text.Json;
using System.Text.Json.Serialization;
using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddDbContext<AstroDbContext>(o => o.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    { DataSource = Path.Combine(root, "data", "astroillustrator.db") }.ToString()));
builder.Services.AddScoped<IRenderJobStore>(p => p.GetRequiredService<AstroDbContext>());
builder.Services.AddScoped<RenderJobService>();
var app = builder.Build();
app.UseExceptionHandler();
app.UseRateLimiter();
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

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
using var definitions = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "renderer", "catalogs", "scene-definitions-v1.json")));
app.MapGet("/api/constellations", () => Results.Ok(definitions.RootElement.GetProperty("constellations")));
app.MapGet("/api/asterisms", () => Results.Ok(definitions.RootElement.GetProperty("asterisms")));
app.MapGet("/api/diagram-templates", () => Results.Ok(new[]
{
    new { code = "StarAltitude", modes = new[] { "Schematic", "Computed" } },
    new { code = "PolarisLatitude", modes = new[] { "Schematic", "Computed" } },
    new { code = "AltitudeAzimuth", modes = new[] { "Schematic" } },
    new { code = "AngularSeparation", modes = new[] { "Computed" } }
}));
app.MapGet("/api/catalogs", () =>
{
    string path = Path.Combine(root, "renderer", "catalogs", "bright-hyg-v41.json");
    using var catalog = JsonDocument.Parse(File.ReadAllText(path));
    var metadata = catalog.RootElement.EnumerateObject().Where(p => p.Name != "stars").ToDictionary(p => p.Name, p => p.Value.Clone());
    return Results.Ok(new { metadata, starCount = catalog.RootElement.GetProperty("stars").GetArrayLength(), sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))) });
});
app.MapGet("/api/presets", () => Results.Ok(new[] { "DarkAtlas", "PrintMonochrome" }));
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
}).RequireRateLimiting("render");
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
});
app.MapPost("/api/astronomy/resolve-time", (TimeReference request) =>
{
    try { return Results.Ok(new { instantUtc = ObserverTime.Resolve(null, request) }); }
    catch (ArgumentException e) { return Results.Problem(statusCode: 400, title: "Invalid observation time", detail: e.Message); }
});
app.MapObserverPresets();
app.MapGet("/api/render-jobs/{id:guid}", async (Guid id, IRenderJobStore store, CancellationToken token) =>
    await store.FindAsync(id, token) is { } job ? Results.Ok(job) : Results.NotFound());
app.MapGet("/api/render-jobs/{id:guid}/outputs", async (Guid id, IRenderJobStore store, CancellationToken token) =>
{
    var job = await store.FindAsync(id, token);
    if (job?.Status != RenderStatus.Completed || job.ResultJson is null) return Results.NotFound();
    var result = JsonSerializer.Deserialize<RendererResult>(job.ResultJson, PythonRendererClient.JsonOptions)!;
    return Results.Ok(result.Outputs.Select(output => new { output.Format, output.FileName, output.Sha256,
        downloadUrl = $"/api/render-jobs/{id}/outputs/{output.Format}" }));
});
app.MapGet("/api/render-jobs/{id:guid}/outputs/{format}", async (Guid id, string format, IRenderJobStore store, CancellationToken token) =>
{
    if (format is not ("svg" or "png" or "manifest")) return Results.NotFound();
    var job = await store.FindAsync(id, token);
    if (job?.Status != RenderStatus.Completed) return Results.NotFound();
    string name = format == "manifest" ? "manifest.json" : $"chart.{format}";
    string path = Path.Combine(root, "artifacts", id.ToString("N"), name);
    return File.Exists(path) ? Results.File(path, format switch { "svg" => "image/svg+xml", "png" => "image/png", _ => "application/json" }, name)
        : Results.NotFound();
});
app.Run();

public partial class Program;
