using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AstroIllustrator.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public sealed class ApiContractTests
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    [Fact]
    public async Task OpenApi_snapshot_matches_live_contract()
    {
        using var factory = new RenderApiTests.Factory();
        using var client = factory.CreateClient();
        var live = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        Assert.StartsWith("3.0.", live["openapi"]!.GetValue<string>());
        var schemas = live["components"]!["schemas"]!;
        Assert.Equal("string", schemas["RenderJob"]!["properties"]!["requestJson"]!["type"]!.GetValue<string>());
        Assert.Equal("string", schemas["RenderJob"]!["properties"]!["resultJson"]!["type"]!.GetValue<string>());
        Assert.Equal("string", schemas["RenderJob"]!["properties"]!["createdAtUtc"]!["type"]!.GetValue<string>());
        Assert.True(schemas["RenderJob"]!["properties"]!["completedAtUtc"]!["nullable"]!.GetValue<bool>());
        Assert.Equal("integer", schemas["RenderJobPage"]!["properties"]!["page"]!["type"]!.GetValue<string>());
        Assert.Equal(2, schemas["IllustrationRequest"]!["oneOf"]!.AsArray().Count);
        Assert.NotNull(live["paths"]!["/api/illustrations/render"]!["post"]!["responses"]!["201"]!["content"]!["application/json"]);
        Assert.NotNull(live["paths"]!["/api/observer-presets"]!["post"]!["responses"]!["409"]);
        Assert.NotNull(live["paths"]!["/health/ready"]!["get"]!["responses"]!["503"]);
        Assert.NotNull(schemas["RenderJobSummary"]!["properties"]!["scene"]);
        var media = live["paths"]!["/api/render-jobs/{id}/outputs/{format}"]!["get"]!["responses"]!["200"]!["content"]!;
        Assert.NotNull(media["image/svg+xml"]);
        Assert.NotNull(media["image/png"]);
        Assert.NotNull(media["application/json"]);
        string repo = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string snapshot = Path.Combine(repo, "docs", "openapi", "v1.json");
        if (Environment.GetEnvironmentVariable("ASTRO_UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            await File.WriteAllTextAsync(snapshot, live.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        Assert.True(File.Exists(snapshot), "Export the contract with ASTRO_UPDATE_OPENAPI=1.");
        Assert.True(JsonNode.DeepEquals(live, JsonNode.Parse(await File.ReadAllTextAsync(snapshot))),
            "Live OpenAPI drifted. Review the API change, export the backend snapshot and regenerate frontend types.");
    }

    [Fact]
    public async Task Invalid_longitude_only_marks_longitude_and_creates_no_job()
    {
        using var factory = new RenderApiTests.Factory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/illustrations/render", new IllustrationRequest
        {
            SkyChart = new() { SceneType = "HorizonView", Projection = "AltitudeAzimuth",
                Observer = new(47, 181), TimeUtc = DateTimeOffset.Parse("2026-07-15T21:00:00Z") }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotNull(problem["errors"]!["observer.longitude"]);
        Assert.Null(problem["errors"]!["observer.latitude"]);
        Assert.Null(problem["errors"]!["observer"]);
        Assert.NotNull(problem["traceId"]);
        var history = JsonNode.Parse(await client.GetStringAsync("/api/render-jobs"))!;
        Assert.Equal(0, history["totalCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task Readiness_and_capabilities_probe_real_runtime_without_rendering()
    {
        using var factory = new RenderApiTests.Factory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        string content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, content);
        var ready = JsonNode.Parse(content)!;
        Assert.True(ready["rendererReady"]!.GetValue<bool>());
        Assert.True(ready["databaseReady"]!.GetValue<bool>());
        var capabilities = await client.GetFromJsonAsync<RendererCapabilities>("/api/capabilities");
        Assert.NotNull(capabilities);
        Assert.Equal("2.1", capabilities.SchemaVersion);
        Assert.Equal(5, capabilities.ConstellationCodes.Count);
        Assert.Equal(4, capabilities.DiagramTemplates.Count);
        Assert.Equal(64, capabilities.ObservationCoverage.IersSha256.Length);
        Assert.True(capabilities.ObservationCoverage.StartUtcInclusive < capabilities.ObservationCoverage.EndUtcExclusive);
        Assert.True(capabilities.ObservationCoverage.EndUtcExclusive < DateTimeOffset.Parse("2031-01-01T00:00:00Z"));
        Assert.Empty(Directory.GetFiles(Path.Combine(factory.Root, "artifacts")));
        Assert.Empty((await client.GetFromJsonAsync<RenderJobPage>("/api/render-jobs"))!.Items);
    }

    private sealed class UnavailableRenderer : IRendererDiagnostics
    {
        public Task<RendererProbe> CheckAsync(CancellationToken token) => Task.FromResult(new RendererProbe(false, "renderer_dependencies_unavailable", null));
    }

    [Fact]
    public async Task Unready_renderer_does_not_change_liveness_or_publish_fake_coverage()
    {
        using var factory = new RenderApiTests.Factory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRendererDiagnostics>();
            services.AddSingleton<IRendererDiagnostics, UnavailableRenderer>();
        }));
        using var client = configured.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.False(JsonNode.Parse(await ready.Content.ReadAsStringAsync())!["rendererReady"]!.GetValue<bool>());
        var response = await client.GetAsync("/api/capabilities");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotNull(body["traceId"]);
        Assert.Null(body["observationCoverage"]);
    }

    [Fact]
    public async Task History_scene_metadata_preserves_detail_strings_and_observer_wire_shape()
    {
        using var factory = new RenderApiTests.Factory();
        using var client = factory.CreateClient();
        var render = await client.PostAsJsonAsync("/api/illustrations/render", new IllustrationRequest { SkyChart = new() { ConstellationCode = "ORI" } });
        Assert.Equal(HttpStatusCode.Created, render.StatusCode);
        var job = await render.Content.ReadFromJsonAsync<RenderJob>(WireOptions);
        var history = (await client.GetFromJsonAsync<RenderJobPage>("/api/render-jobs", WireOptions))!;
        Assert.Equal("SingleConstellation", history.Items[0].Scene!.SceneType);
        Assert.Equal("ORI", history.Items[0].Scene!.ConstellationCodes[0]);
        Assert.Equal(job!.Id, history.Items[0].Id);
        Assert.IsType<string>(job.RequestJson);
        var observer = await client.PostAsJsonAsync("/api/observer-presets", new ObserverPresetRequest("Home", new(47, 19)));
        Assert.Equal(HttpStatusCode.Created, observer.StatusCode);
        var saved = JsonNode.Parse(await observer.Content.ReadAsStringAsync())!;
        Assert.NotNull(saved["latitude"]);
        Assert.Null(saved["observer"]);
        var missing = await client.GetAsync("/api/render-jobs/00000000-0000-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.NotNull(JsonNode.Parse(await missing.Content.ReadAsStringAsync())!["traceId"]);
    }
}
