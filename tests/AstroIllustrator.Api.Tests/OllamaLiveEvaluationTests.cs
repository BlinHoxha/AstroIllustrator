using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIFramework.Core.Abstractions;
using AIFramework.Core.Models;
using AIFramework.Ollama;
using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

public sealed class OllamaLiveEvaluationTests(ITestOutputHelper output)
{
    public sealed class LiveOllamaFactAttribute : FactAttribute
    {
        public LiveOllamaFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASTRO_CHARTAI_OLLAMA_LIVE_MODEL")))
                Skip = "Set ASTRO_CHARTAI_OLLAMA_LIVE_MODEL to run the local evaluation explicitly.";
        }
    }

    private sealed class Recorder(IStructuredGenerationModel inner) : IStructuredGenerationModel
    {
        public string? Raw { get; private set; }
        public async Task<StructuredGenerationResult> GenerateStructuredAsync(StructuredGenerationRequest request, CancellationToken token)
        {
            var result = await inner.GenerateStructuredAsync(request, token);
            Raw = result.OutputJson;
            return result;
        }
    }

    [LiveOllamaFact]
    public async Task Evaluate_installed_model_through_chart_api()
    {
        string model = Environment.GetEnvironmentVariable("ASTRO_CHARTAI_OLLAMA_LIVE_MODEL")!;
        string repo = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        using var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "docs", "qa", "chart-ai-evaluation-cases.json")));
        var evaluations = cases.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => (Description: c.GetProperty("description").GetString()!, Expected: c.GetProperty("expected").GetString()!)).ToArray();
        Assert.Equal(12, evaluations.Length);
        string albanian = string.Concat(Enumerable.Repeat("Krijo një hartë të yjeve me etiketa shqip. ", 50))[..2000];
        foreach (var (description, expected) in evaluations.Append((albanian, "Bound probe only")))
        {
            using var factory = new RenderApiTests.Factory();
            Recorder? recorder = null;
            using var host = Host(factory, model, services =>
            {
                services.RemoveAll<IStructuredGenerationModel>();
                services.AddSingleton<IStructuredGenerationModel>(provider =>
                    recorder = new Recorder(provider.GetRequiredService<OllamaGenerationModel>()));
            });
            using var client = host.CreateClient();
            var timer = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest(description, "en"));
            string body = await response.Content.ReadAsStringAsync();
            timer.Stop();
            Assert.NotEqual(500, (int)response.StatusCode);
            using var json = JsonDocument.Parse(body);
            string? code = null;
            if (response.IsSuccessStatusCode)
            {
                var proposal = await response.Content.ReadFromJsonAsync<ChartProposalResponse>();
                Assert.NotNull(proposal);
                Assert.Equal("ollama", proposal.Provider);
                Assert.Contains(proposal.Status, new[] { "Ready", "NeedsInput", "Unsupported", "Invalid" });
                if (proposal.Parameters is { } p)
                {
                    var numbers = Regex.Matches(description, @"(?<![\p{L}\d])[-+]?\d+(?:\.\d+)?(?![\p{L}\d])")
                        .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToHashSet();
                    Assert.True(p.MagnitudeLimit is null || numbers.Contains(p.MagnitudeLimit.Value));
                    Assert.True(p.AltitudeDegrees is null || numbers.Contains(p.AltitudeDegrees.Value));
                    Assert.True(p.AzimuthDegrees is null || numbers.Contains(p.AzimuthDegrees.Value));
                    Assert.True(p.StarHipIds is null || p.StarHipIds.All(id => numbers.Contains(id)));
                }
            }
            else
            {
                code = json.RootElement.GetProperty("code").GetString();
                Assert.Contains(code, new[] { "ai_busy", "ai_daily_limit", "ai_budget_exhausted", "ai_metering_unavailable",
                    "ai_timeout", "ai_provider_unavailable", "ai_refused", "ai_incomplete", "ai_invalid_response" });
            }
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(factory.Root, "data", "astroillustrator.db") }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"InputTokens\", \"OutputTokens\" FROM \"AiUsageEntries\" ORDER BY rowid DESC LIMIT 1";
            using var reader = command.ExecuteReader();
            int? input = null, generated = null;
            if (reader.Read())
            {
                input = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                generated = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            }
            output.WriteLine(JsonSerializer.Serialize(new { description, expected, http = (int)response.StatusCode,
                code, raw = recorder?.Raw, final = body, elapsedSeconds = timer.Elapsed.TotalSeconds,
                inputTokens = input, outputTokens = generated,
                maxInputTokenBound = description.Length == 2000 ? ChartProposalInterpreter.MaxInputTokenBound(2000) : (long?)null }));
        }
    }

    private static WebApplicationFactory<Program> Host(RenderApiTests.Factory factory, string model, Action<IServiceCollection> decorate)
        => factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            foreach (var (key, value) in new Dictionary<string, string>
            {
                ["ChartAI:Enabled"] = "true", ["ChartAI:Provider"] = "Ollama", ["ChartAI:Ollama:Model"] = model,
                ["ChartAI:Ollama:Endpoint"] = "http://127.0.0.1:11434/", ["ChartAI:TimeoutSeconds"] = "60",
                ["ChartAI:MaxOutputTokens"] = "512", ["ChartAI:Budget:MonthlyAmount"] = "10",
                ["ChartAI:Budget:Currency"] = "USD"
            }) builder.UseSetting(key, value);
            builder.ConfigureTestServices(decorate);
        });
}
