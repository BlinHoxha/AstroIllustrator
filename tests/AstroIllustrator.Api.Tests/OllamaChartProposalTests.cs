using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AIFramework.Core.Abstractions;
using AIFramework.Core.Metering;
using AIFramework.Ollama;
using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class OllamaChartProposalTests
{
    private static Dictionary<string, string?> Settings(string endpoint = "http://127.0.0.1:11434/") => new()
    {
        ["ChartAI:Enabled"] = "true", ["ChartAI:Provider"] = "Ollama",
        ["ChartAI:Ollama:Endpoint"] = endpoint, ["ChartAI:Ollama:Model"] = "qwen3:1.7b",
        ["ChartAI:Budget:MonthlyAmount"] = "10", ["ChartAI:Budget:Currency"] = "USD",
        ["ChartAI:MaxOutputTokens"] = "512"
    };

    [Fact]
    public void Startup_rejects_invalid_Ollama_configuration_and_keeps_Azure_default()
    {
        static void Configure(Dictionary<string, string?> settings, string environment = "Development")
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            new ServiceCollection().AddChartProposals(configuration, "Data Source=:memory:", environment);
        }
        Configure(Settings(), "development");
        Assert.Throws<InvalidOperationException>(() => Configure(Settings(), "Production"));
        var missing = Settings(); missing.Remove("ChartAI:Ollama:Model");
        Assert.Throws<InvalidOperationException>(() => Configure(missing));
        var remote = Settings("http://example.com:11434/");
        Assert.Throws<InvalidOperationException>(() => Configure(remote));
        var noBudget = Settings(); noBudget.Remove("ChartAI:Budget:MonthlyAmount");
        Assert.Throws<InvalidOperationException>(() => Configure(noBudget));
        var unknown = Settings(); unknown["ChartAI:Provider"] = "Unknown";
        Assert.Throws<InvalidOperationException>(() => Configure(unknown));
        var tooShort = Settings(); tooShort["ChartAI:Ollama:TimeoutSeconds"] = "25";
        Assert.Throws<InvalidOperationException>(() => Configure(tooShort));
        tooShort["ChartAI:Ollama:TimeoutSeconds"] = "24";
        Assert.Throws<InvalidOperationException>(() => Configure(tooShort));
        var context = Settings(); context["ChartAI:MaxOutputTokens"] = "1024";
        Assert.Throws<InvalidOperationException>(() => Configure(context));
        Configure(Settings());
        var services = new ServiceCollection();
        services.AddChartProposals(new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build(),
            "Data Source=:memory:", "Development");
        using var provider = services.BuildServiceProvider();
        Assert.IsType<OllamaGenerationModel>(provider.GetRequiredService<IStructuredGenerationModel>());
        Assert.Equal("ollama", provider.GetRequiredService<ChartAiSettings>().Provider);
        Assert.Null(provider.GetService<IChatModel>());
    }

    [Fact]
    public void Context_bound_matches_the_interpreter_prompt_and_three_byte_description()
    {
        string instructions = (string)typeof(ChartProposalInterpreter)
            .GetField("Instructions", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        Assert.Equal(AiCostCalculator.EstimateInputTokensUpperBound(instructions, new string('€', 2000)),
            ChartProposalInterpreter.MaxInputTokenBound(2000));
        Assert.True(ChartProposalInterpreter.MaxInputTokenBound(2000) + 512 <= 8192);
    }

    [Fact]
    public async Task Long_three_byte_description_reaches_the_loopback_server()
    {
        await using var server = await FakeOllama.StartAsync(_ => (200, Native(ValidOutput(false))));
        using var factory = new RenderApiTests.Factory();
        using var host = Host(factory, Settings(server.Endpoint));
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest(new string('€', 2000), "en"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/api/chat", Assert.Single(server.Paths));
    }

    [Fact]
    public async Task Success_reports_provider_and_closes_zero_price_ledger_entry()
    {
        await using var server = await FakeOllama.StartAsync(_ => (200, Native(ValidOutput(true))));
        using var factory = new RenderApiTests.Factory();
        using var host = Host(factory, Settings(server.Endpoint));
        using var client = host.CreateClient();
        var status = await client.GetFromJsonAsync<ChartAiStatus>("/api/chart-proposals/status");
        Assert.True(status!.Enabled);
        Assert.Equal("ollama", status.Provider);
        var response = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest("Show the Summer Triangle", "en"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proposal = await response.Content.ReadFromJsonAsync<ChartProposalResponse>();
        Assert.Equal("ollama", proposal!.Provider);
        Assert.Equal("Ready", proposal.Status);
        Assert.Equal("/api/chat", Assert.Single(server.Paths));
        var entry = Entry(factory.Root);
        Assert.Equal("Committed", entry.State);
        Assert.Equal("Completed", entry.Outcome);
        Assert.Equal(0m, entry.Amount);
        Assert.Equal(12, entry.InputTokens);
        Assert.Equal(7, entry.OutputTokens);
    }

    [Theory]
    [InlineData("invalid-json", 503, "ai_provider_unavailable")]
    [InlineData("schema-mismatch", 503, "ai_provider_unavailable")]
    [InlineData("done-false", 503, "ai_provider_unavailable")]
    [InlineData("oversize", 503, "ai_provider_unavailable")]
    [InlineData("length", 503, "ai_incomplete")]
    [InlineData("length-no-content", 503, "ai_provider_unavailable")]
    [InlineData("http-500", 503, "ai_provider_unavailable")]
    [InlineData("http-404", 503, "ai_provider_unavailable")]
    public async Task Provider_failures_preserve_http_semantics_and_close_ledger(
        string mode, int expectedStatus, string expectedCode)
    {
        await using var server = await FakeOllama.StartAsync(_ => mode switch
        {
            "invalid-json" => (200, "not json"),
            "schema-mismatch" => (200, Native("{}")),
            "done-false" => (200, Native(ValidOutput(true), done: false)),
            "oversize" => (200, new string('x', 131_073)),
            "length" => (200, Native(ValidOutput(true), reason: "length")),
            "length-no-content" => (200, """{"done":true,"done_reason":"length","message":{}}"""),
            "http-500" => (500, "failed"),
            _ => (404, "missing")
        });
        using var factory = new RenderApiTests.Factory();
        using var host = Host(factory, Settings(server.Endpoint));
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest("Summer Triangle", "en"));
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, body.RootElement.GetProperty("code").GetString());
        Assert.Equal("/api/chat", Assert.Single(server.Paths));
        Assert.Equal("Committed", Entry(factory.Root).State);
    }

    [Fact]
    public async Task Unavailable_loopback_port_maps_to_provider_unavailable()
    {
        await using var server = await FakeOllama.StartAsync(_ => (200, Native(ValidOutput(true))));
        string endpoint = server.Endpoint;
        await server.DisposeAsync();
        using var factory = new RenderApiTests.Factory();
        using var host = Host(factory, Settings(endpoint));
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest("Summer Triangle", "en"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ai_provider_unavailable", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Committed", Entry(factory.Root).State);
    }

    [Fact]
    public async Task Application_deadline_maps_to_timeout()
    {
        await using var server = await FakeOllama.StartAsync(async context =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), context.RequestAborted);
            return (200, Native(ValidOutput(true)));
        });
        var settings = Settings(server.Endpoint);
        settings["ChartAI:TimeoutSeconds"] = "1";
        settings["ChartAI:Ollama:TimeoutSeconds"] = "3";
        using var factory = new RenderApiTests.Factory();
        using var host = Host(factory, settings);
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest("Summer Triangle", "en"));
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ai_timeout", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Committed", Entry(factory.Root).State);
    }

    private static WebApplicationFactory<Program> Host(RenderApiTests.Factory factory, Dictionary<string, string?> settings)
        => factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
        });

    private static (string State, string Outcome, decimal Amount, int? InputTokens, int? OutputTokens) Entry(string root)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(root, "data", "astroillustrator.db") }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"State\", \"Outcome\", \"Amount\", \"InputTokens\", \"OutputTokens\" FROM \"AiUsageEntries\"";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        var entry = (reader.GetString(0), reader.GetString(1), decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3), reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4));
        Assert.False(reader.Read());
        return entry;
    }

    internal static string ValidOutput(bool supported) => JsonSerializer.Serialize(new
    {
        supported,
        parameters = new
        {
            scene = supported ? "Asterism" : null, constellationCodes = (string[]?)null,
            diagramType = (string?)null, mode = (string?)null, language = (string?)null,
            preset = (string?)null, magnitudeLimit = (double?)null, altitudeDegrees = (double?)null,
            azimuthDegrees = (double?)null, starHipIds = (int[]?)null
        }
    });

    private static string Native(string content, string reason = "stop", bool done = true)
        => JsonSerializer.Serialize(new { done, done_reason = reason, message = new { content },
            model = "qwen3:1.7b", prompt_eval_count = 12, eval_count = 7 });

    private sealed class FakeOllama(WebApplication app) : IAsyncDisposable
    {
        public ConcurrentBag<string> Paths { get; } = [];
        public string Endpoint => app.Urls.Single() + "/";

        public static async Task<FakeOllama> StartAsync(Func<HttpContext, Task<(int Status, string Body)>> respond)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            var server = new FakeOllama(app);
            app.MapPost("/api/chat", async context =>
            {
                server.Paths.Add(context.Request.Path);
                var result = await respond(context);
                context.Response.StatusCode = result.Status;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(result.Body);
            });
            await app.StartAsync();
            return server;
        }

        public static Task<FakeOllama> StartAsync(Func<HttpContext, (int Status, string Body)> respond)
            => StartAsync(context => Task.FromResult(respond(context)));

        public async ValueTask DisposeAsync() => await app.DisposeAsync();
    }
}
