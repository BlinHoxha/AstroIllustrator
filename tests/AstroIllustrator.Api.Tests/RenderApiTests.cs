using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AstroIllustrator.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using AstroIllustrator.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

public sealed class RenderApiTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "astro-tests-" + Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            string repo = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
            Directory.CreateDirectory(Path.Combine(Root, "renderer"));
            foreach (string source in Directory.EnumerateFiles(Path.Combine(repo, "renderer"), "*", SearchOption.AllDirectories)
                .Where(x => x.EndsWith(".py") || x.EndsWith(".json")))
            {
                string target = Path.Combine(Root, "renderer", Path.GetRelativePath(Path.Combine(repo, "renderer"), source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }
            builder.UseSetting("WorkspaceRoot", Root);
            builder.UseSetting("Renderer:PythonExecutable", Path.Combine(repo, ".venv", OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python"));
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    [Fact]
    public async Task Real_python_render_persists_job_and_downloadable_hashed_outputs()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/illustrations/render", new IllustrationRequest { SkyChart = new() });
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var json = JsonDocument.Parse(body);
        string id = json.RootElement.GetProperty("id").GetString()!;
        Assert.Equal("Completed", json.RootElement.GetProperty("status").GetString());
        var saved = await client.GetAsync($"/api/render-jobs/{id}");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        foreach (string format in new[] { "svg", "png", "manifest" })
        {
            var output = await client.GetAsync($"/api/render-jobs/{id}/outputs/{format}");
            Assert.Equal(HttpStatusCode.OK, output.StatusCode);
            Assert.True((await output.Content.ReadAsByteArrayAsync()).Length > 100);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/render-jobs/{id}/outputs/exe")).StatusCode);
        Assert.True(File.Exists(Path.Combine(factory.Root, "data", "astroillustrator.db")));
        using var outputMetadata = JsonDocument.Parse(await client.GetStringAsync($"/api/render-jobs/{id}/outputs"));
        Assert.Equal(3, outputMetadata.RootElement.GetArrayLength());
        Assert.True(outputMetadata.RootElement[0].TryGetProperty("downloadUrl", out _));
    }

    [Fact]
    public async Task Unsupported_input_and_unknown_properties_do_not_create_jobs()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/illustrations/render", new IllustrationRequest { SkyChart = new() { ConstellationCode = "UNKNOWN" } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var unknown = await client.PostAsJsonAsync("/api/illustrations/render", new { schemaVersion = "2.1", geometry = new[] { 1, 2 } });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(factory.Root, "artifacts")));
    }

    [Fact]
    public async Task All_documented_examples_render_through_api_and_are_listed_in_history()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        string repo = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        int count = 0;
        foreach (string path in Directory.GetFiles(Path.Combine(repo, "examples"), "*.json"))
        {
            var response = await client.PostAsync("/api/illustrations/render", new StringContent(File.ReadAllText(path), System.Text.Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Created, path + ": " + body);
            using var job = JsonDocument.Parse(body);
            string id = job.RootElement.GetProperty("id").GetString()!;
            using var manifest = JsonDocument.Parse(await client.GetStringAsync($"/api/render-jobs/{id}/outputs/manifest"));
            Assert.True(manifest.RootElement.TryGetProperty("scientificResults", out _));
            count++;
        }
        using var history = JsonDocument.Parse(await client.GetStringAsync("/api/render-jobs?page=1&pageSize=3&status=Completed"));
        Assert.Equal(count, history.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, history.RootElement.GetProperty("items").GetArrayLength());
        using var failed = JsonDocument.Parse(await client.GetStringAsync("/api/render-jobs?status=Failed"));
        Assert.Equal(0, failed.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/render-jobs?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/render-jobs?status=123")).StatusCode);
    }

    [Fact]
    public async Task Observer_presets_support_validated_create_update_delete_and_unique_names()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        var request = new ObserverPresetRequest("Prishtina", new(42.6629,21.1655,600,"Europe/Belgrade"));
        var created = await client.PostAsJsonAsync("/api/observer-presets", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        string id = json.RootElement.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/observer-presets/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/observer-presets", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/observer-presets", request with { Observer = new(91,0) })).StatusCode);
        var missingCoordinates = await client.PostAsync("/api/observer-presets", new StringContent("""{"name":"Missing coordinates","observer":{}}""", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, missingCoordinates.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/observer-presets/{id}", request with { Name = "Home" })).StatusCode);
        using var rows = JsonDocument.Parse(await client.GetStringAsync("/api/observer-presets"));
        Assert.Equal("Home", rows.RootElement[0].GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/observer-presets/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/observer-presets/{id}")).StatusCode);
    }

    [Fact]
    public async Task Missing_catalog_star_is_an_actionable_422_and_failed_job()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        var request = new IllustrationRequest { IllustrationType = "ScientificDiagram", ScientificDiagram = new()
        {
            DiagramType = "AngularSeparation", Mode = "Computed", Parameters = new() { StarHipIds = [91262, int.MaxValue] }
        } };
        var response = await client.PostAsJsonAsync("/api/illustrations/render", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("HIP pair", problem.RootElement.GetProperty("detail").GetString());
        string id = problem.RootElement.GetProperty("jobId").GetString()!;
        using var job = JsonDocument.Parse(await client.GetStringAsync($"/api/render-jobs/{id}"));
        Assert.Equal("Failed", job.RootElement.GetProperty("status").GetString());
        Assert.Equal("scientific_unsupported", job.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/render-jobs/{id}/outputs/svg")).StatusCode);
        var future = new IllustrationRequest { IllustrationType = "ScientificDiagram", ScientificDiagram = new()
        {
            DiagramType = "PolarisLatitude", Mode = "Computed", Observer = new(42,21),
            TimeUtc = new DateTimeOffset(2030,7,15,21,0,0,TimeSpan.Zero), Parameters = new() { StarId = "Polaris" }
        } };
        var unsupported = await client.PostAsJsonAsync("/api/illustrations/render", future);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
        using var coverage = JsonDocument.Parse(await unsupported.Content.ReadAsStringAsync());
        Assert.Contains("IERS", coverage.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Discovery_and_time_resolution_endpoints_expose_supported_backend_capabilities()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        using var constellations = JsonDocument.Parse(await client.GetStringAsync("/api/constellations"));
        Assert.Equal(5, constellations.RootElement.GetArrayLength());
        using var diagrams = JsonDocument.Parse(await client.GetStringAsync("/api/diagram-templates"));
        Assert.Equal(4, diagrams.RootElement.GetArrayLength());
        using var catalog = JsonDocument.Parse(await client.GetStringAsync("/api/catalogs"));
        Assert.Equal(8870, catalog.RootElement.GetProperty("starCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/asterisms")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/astronomy/resolve-time", new TimeReference { Type = "LocalClock", LocalDateTime = new(2026,7,15,23,0,0), TimeZoneId = "Europe/Belgrade" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var instant = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new DateTimeOffset(2026,7,15,21,0,0,TimeSpan.Zero), instant.RootElement.GetProperty("instantUtc").GetDateTimeOffset());
        var invalid = await client.PostAsync("/api/astronomy/resolve-time", new StringContent("""{"type":"AbsoluteUtc","instantUtc":"2026-07-15T21:00:00"}""", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Migration_upgrades_existing_job_database_without_losing_history()
    {
        string root = Path.Combine(Path.GetTempPath(), "astro-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var db = new AstroDbContext(new DbContextOptionsBuilder<AstroDbContext>()
                .UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(root,"test.db") }.ToString()).Options);
            // Preserve the already shipped initial identifier; resolve its tooling name
            // through EF rather than changing an identifier present in existing databases.
            string baseline = db.GetService<IMigrationsIdGenerator>().GetName(db.Database.GetMigrations().First());
            await db.GetService<IMigrator>().MigrateAsync(baseline);
            Guid id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "RenderJobs" ("Id","Status","CreatedAtUtc","RequestJson","RequestHash")
                VALUES ({id},{"Completed"},{DateTimeOffset.UtcNow},{"{}"},{"old-hash"})
                """);
            await db.Database.MigrateAsync();
            var old = await db.RenderJobs.SingleAsync();
            Assert.Equal(id, old.Id);
            Assert.Equal("old-hash", old.RequestHash);
            Assert.Null(old.ErrorMessage);
            Assert.Empty(await db.ObserverPresets.ToListAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            await db.Database.MigrateAsync(); // Repeat startup must remain safe.
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root,true);
        }
    }
}
