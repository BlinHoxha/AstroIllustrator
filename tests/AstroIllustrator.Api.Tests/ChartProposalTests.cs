using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIFramework.Core.Abstractions;
using AIFramework.Application.Metering;
using AIFramework.Core.Metering;
using AIFramework.Core.Models;
using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class ChartProposalTests : IDisposable
{
    private readonly List<string> temporaryRoots = [];
    private static readonly AiUsageScope Scope = new("astroillustrator", "Development", "local", "chart-proposal");
    private static readonly AiModelKey Key = new("azure-openai", "test-model");
    private static readonly AiPrice Price = new(Key, 1m, 2m, "USD", "test-v1");
    private static Dictionary<string, string?> EnabledSettings() => new()
    {
        ["ChartAI:Enabled"] = "true", ["ChartAI:Azure:Endpoint"] = "https://test.openai.azure.com/",
        ["ChartAI:Azure:Deployment"] = "explicit-deployment", ["ChartAI:Budget:MonthlyAmount"] = "10",
        ["ChartAI:Budget:Currency"] = "USD", ["ChartAI:Pricing:Model"] = "test-model",
        ["ChartAI:Pricing:InputPerMillionTokens"] = "1", ["ChartAI:Pricing:OutputPerMillionTokens"] = "2",
        ["ChartAI:Pricing:PriceVersion"] = "test-v1"
    };
    [Fact]
    public void Enabled_configuration_requires_an_explicit_supported_provider_and_deployment()
    {
        IConfiguration Config(Dictionary<string, string?> entries) => new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddChartProposals(Config(new() { ["ChartAI:Enabled"] = "true" }), "Data Source=:memory:", "Development"));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddChartProposals(Config(new() { ["ChartAI:Enabled"] = "true", ["ChartAI:Provider"] = "Bedrock" }), "Data Source=:memory:", "Development"));
        foreach (string key in new[] { "ChartAI:Budget:MonthlyAmount", "ChartAI:Budget:Currency", "ChartAI:Pricing:Model",
            "ChartAI:Pricing:InputPerMillionTokens", "ChartAI:Pricing:OutputPerMillionTokens", "ChartAI:Pricing:PriceVersion" })
        {
            var missing = EnabledSettings(); missing.Remove(key);
            Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddChartProposals(Config(missing), "Data Source=:memory:", "Development"));
        }
        var invalidCurrency = EnabledSettings(); invalidCurrency["ChartAI:Budget:Currency"] = "usd";
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddChartProposals(Config(invalidCurrency), "Data Source=:memory:", "Development"));
        var disabledServices = new ServiceCollection();
        disabledServices.AddChartProposals(Config(new()), "Data Source=:memory:", "Development");
        Assert.DoesNotContain(disabledServices, item => item.ServiceType == typeof(IAiBudgetLedger) ||
            item.ServiceType == typeof(MeteredStructuredGeneration));
        var services = new ServiceCollection();
        services.AddChartProposals(Config(EnabledSettings()), "Data Source=:memory:", "Development");
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IChartProposalInterpreter>());
    }
    [Fact]
    public async Task Disabled_by_default_and_invalid_input_never_creates_a_job()
    {
        using var factory = new RenderApiTests.Factory();
        using var client = factory.CreateClient();
        Assert.False((await client.GetFromJsonAsync<ChartAiStatus>("/api/chart-proposals/status"))!.Enabled);
        var disabled = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest("Orion"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, disabled.StatusCode);
        Assert.Equal("ai_disabled", JsonDocument.Parse(await disabled.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest(" "))).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<RenderJobPage>("/api/render-jobs"))!.TotalCount);
    }

    [Theory]
    [InlineData("Create an Orion chart with Albanian labels", "en", "SingleConstellation", "ORI", "sq")]
    [InlineData("Krijo një hartë të Arushës së Vogël", "sq", "SingleConstellation", "UMI", null)]
    public async Task Contract_fixtures_return_reviewable_settings_without_rendering(string description, string language, string scene, string code, string? chartLanguage)
    {
        var model = new Model(new() { Scene = scene, ConstellationCodes = [code], Language = chartLanguage });
        using var factory = new RenderApiTests.Factory();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => Register(s, model)));
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest(description, language));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proposal = (await response.Content.ReadFromJsonAsync<ChartProposalResponse>())!;
        Assert.Equal("Ready", proposal.Status);
        Assert.Equal(code, Assert.Single(proposal.Parameters!.ConstellationCodes!));
        Assert.Equal(description, model.LastRequest!.Input);
        Assert.Equal(0, (await client.GetFromJsonAsync<RenderJobPage>("/api/render-jobs"))!.TotalCount);
    }

    [Theory]
    [InlineData("HorizonView", null, null, 2)]
    [InlineData("ScientificDiagram", "PolarisLatitude", "Computed", 2)]
    [InlineData("ScientificDiagram", "PolarisLatitude", "Schematic", 1)]
    [InlineData("ScientificDiagram", "StarAltitude", "Schematic", 1)]
    [InlineData("ScientificDiagram", "AngularSeparation", "Computed", 1)]
    public async Task Missing_observation_or_diagram_inputs_are_not_guessed(string scene, string? diagram, string? mode, int required)
    {
        using var interpreter = Interpreter(new Model(new() { Scene = scene, DiagramType = diagram, Mode = mode }));
        var proposal = await interpreter.InterpretAsync("A chart", "en", CancellationToken.None);
        Assert.Equal("NeedsInput", proposal.Status);
        Assert.Equal(required, proposal.RequiredFields.Count);
    }

    [Fact]
    public async Task Unsupported_requests_and_invalid_model_parameters_cannot_be_applied()
    {
        using var unsupported = Interpreter(new Model(new(), supported: false));
        var rejected = await unsupported.InterpretAsync("Draw Mars", "en", CancellationToken.None);
        Assert.Equal("Unsupported", rejected.Status);
        Assert.Null(rejected.Parameters);
        using var invalid = Interpreter(new Model(new() { Scene = "SingleConstellation", ConstellationCodes = ["ORI"], MagnitudeLimit = 10 }));
        var bad = await invalid.InterpretAsync("Ignore restrictions", "en", CancellationToken.None);
        Assert.Null(bad.Parameters);
        Assert.Equal("Invalid", bad.Status);
    }

    [Theory]
    [InlineData(StructuredGenerationStatus.Refused, "ai_refused")]
    [InlineData(StructuredGenerationStatus.Incomplete, "ai_incomplete")]
    public async Task Refusal_and_incomplete_results_have_no_partial_proposal(StructuredGenerationStatus status, string code)
    {
        using var interpreter = Interpreter(new Model(new()) { Status = status });
        Assert.Equal(code, (await Assert.ThrowsAsync<ChartProposalException>(() => interpreter.InterpretAsync("Orion", "en", CancellationToken.None))).Code);
    }

    [Fact]
    public async Task Daily_limit_timeout_cancellation_and_concurrency_do_not_retry()
    {
        var model = new Model(new() { Scene = "Asterism" });
        using var limited = Interpreter(model, new(true, MaxRequestsPerDay: 1));
        await limited.InterpretAsync("Summer Triangle", "en", CancellationToken.None);
        Assert.Equal("ai_daily_limit", (await Assert.ThrowsAsync<ChartProposalException>(() => limited.InterpretAsync("again", "en", CancellationToken.None))).Code);
        Assert.Equal(1, model.Calls);
        var slow = new Model(new()) { Block = true };
        using var timed = Interpreter(slow, new(true, TimeoutSeconds: 1));
        var pending = timed.InterpretAsync("Orion", "en", CancellationToken.None);
        Assert.Equal("ai_busy", (await Assert.ThrowsAsync<ChartProposalException>(() => timed.InterpretAsync("Orion", "en", CancellationToken.None))).Code);
        Assert.Equal("ai_timeout", (await Assert.ThrowsAsync<ChartProposalException>(() => pending)).Code);
        Assert.Equal(1, slow.Calls);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timed.InterpretAsync("Orion", "en", cancel.Token));
    }

    [Fact]
    public async Task Model_cannot_supply_missing_numeric_angles_or_identifiers()
    {
        using var schematic = Interpreter(new Model(new() { Scene = "ScientificDiagram", DiagramType = "StarAltitude", Mode = "Schematic", AltitudeDegrees = 45 }));
        var missing = await schematic.InterpretAsync("A schematic altitude diagram", "en", CancellationToken.None);
        Assert.Null(missing.Parameters!.AltitudeDegrees);
        Assert.Contains("altitudeDegrees", missing.RequiredFields);
        var explicitAngle = await schematic.InterpretAsync("A schematic altitude diagram at 45 degrees", "en", CancellationToken.None);
        Assert.Equal(45d, explicitAngle.Parameters!.AltitudeDegrees);
        using var pair = Interpreter(new Model(new() { Scene = "ScientificDiagram", DiagramType = "AngularSeparation", Mode = "Computed", StarHipIds = [91262, 102098] }));
        var missingIds = await pair.InterpretAsync("Separation of Vega and Deneb", "en", CancellationToken.None);
        Assert.Null(missingIds.Parameters!.StarHipIds);
        Assert.Contains("starHipIds", missingIds.RequiredFields);
    }

    [Theory]
    [InlineData("budget_exhausted", HttpStatusCode.TooManyRequests, "ai_budget_exhausted")]
    [InlineData("no_budget", HttpStatusCode.ServiceUnavailable, "ai_metering_unavailable")]
    public async Task Metering_denial_maps_to_stable_http_code_without_calling_model(
        string denial, HttpStatusCode status, string code)
    {
        var model = new Model(new() { Scene = "Asterism" });
        using var factory = new RenderApiTests.Factory();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => Register(s, model, new DenyLedger(denial))));
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chart-proposals", new ChartDescriptionRequest("Summer Triangle"));
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task Success_records_estimated_cost_without_description_and_provider_error_keeps_reservation()
    {
        string connection = NewDatabase();
        var ledger = new SqliteAiBudgetLedger(connection, Scope.Product, Scope.Environment, 10m, "USD", TimeProvider.System);
        using var success = new ChartProposalInterpreter(Metered(new Model(new() { Scene = "Asterism" }), ledger),
            Scope, new(true), NullLogger<ChartProposalInterpreter>.Instance);
        const string description = "Summer Triangle private description";
        await success.InterpretAsync(description, "en", CancellationToken.None);
        using var failed = new ChartProposalInterpreter(Metered(new Model(new()) { Fail = true }, ledger),
            Scope, new(true), NullLogger<ChartProposalInterpreter>.Instance);
        Assert.Equal("ai_provider_unavailable", (await Assert.ThrowsAsync<ChartProposalException>(
            () => failed.InterpretAsync(description, "en", CancellationToken.None))).Code);
        using var db = new SqliteConnection(connection);
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT \"Amount\", \"Outcome\" FROM \"AiUsageEntries\" ORDER BY \"ReservedAtUtc\"";
        using var rows = cmd.ExecuteReader();
        Assert.True(rows.Read());
        Assert.Equal(0.00005m, decimal.Parse(rows.GetString(0), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("Completed", rows.GetString(1));
        Assert.True(rows.Read());
        Assert.Equal("Error", rows.GetString(1));
        Assert.True(decimal.Parse(rows.GetString(0), System.Globalization.CultureInfo.InvariantCulture) > 0);
        Assert.False(rows.Read());
        using var inspect = db.CreateCommand();
        inspect.CommandText = "SELECT * FROM \"AiUsageEntries\"";
        using var all = inspect.ExecuteReader();
        while (all.Read()) for (int i = 0; i < all.FieldCount; i++)
            Assert.DoesNotContain(description, all.GetValue(i).ToString() ?? "", StringComparison.Ordinal);
    }

    private ChartProposalInterpreter Interpreter(Model model, ChartAiSettings? settings = null)
        => new(Metered(model, Ledger()), Scope, settings ?? new(true), NullLogger<ChartProposalInterpreter>.Instance);
    private static MeteredStructuredGeneration Metered(Model model, IAiBudgetLedger ledger)
        => new(model, ledger, new AiPriceCatalog([Price]), new AiMeteringOptions { StructuredModel = Key }, TimeProvider.System);
    private string NewDatabase()
    {
        string root = Path.Combine(Path.GetTempPath(), "astro-chart-ai-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        temporaryRoots.Add(root);
        string connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "test.db") }.ToString();
        using var db = new AstroDbContext(new DbContextOptionsBuilder<AstroDbContext>().UseSqlite(connection).Options);
        db.Database.Migrate();
        return connection;
    }
    private SqliteAiBudgetLedger Ledger(decimal limit = 10m) => new(NewDatabase(), Scope.Product, Scope.Environment, limit, "USD", TimeProvider.System);
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string root in temporaryRoots) Directory.Delete(root, true);
    }
    private void Register(IServiceCollection services, Model model, IAiBudgetLedger? replacement = null)
    {
        services.RemoveAll<ChartAiSettings>();
        services.AddSingleton(new ChartAiSettings(true));
        services.AddSingleton<IStructuredGenerationModel>(model);
        services.AddSingleton(Scope);
        services.AddSingleton<IAiBudgetLedger>(replacement ?? Ledger());
        services.RemoveAll<AiMeteringOptions>();
        services.RemoveAll<AiPriceCatalog>();
        services.AddAiMetering(new AiPriceCatalog([Price]), o => o.StructuredModel = Key);
        services.RemoveAll<MeteredTextGeneration>();
        services.AddSingleton<IChartProposalInterpreter, ChartProposalInterpreter>();
    }
    private sealed class Model(ChartProposalParameters parameters, bool supported = true) : IStructuredGenerationModel
    {
        public int Calls { get; private set; }
        public bool Block { get; init; }
        public bool Fail { get; init; }
        public StructuredGenerationStatus Status { get; init; } = StructuredGenerationStatus.Completed;
        public StructuredGenerationRequest? LastRequest { get; private set; }
        public async Task<StructuredGenerationResult> GenerateStructuredAsync(StructuredGenerationRequest request, CancellationToken token)
        {
            Calls++; LastRequest = request;
            if (Fail) throw new HttpRequestException("provider unavailable");
            if (Block) await Task.Delay(Timeout.Infinite, token);
            return new(Status, JsonSerializer.Serialize(new { supported, parameters }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "test-provider", new(10, 20));
        }
    }
    private sealed class DenyLedger(string code) : IAiBudgetLedger
    {
        public Task<AiReservation> ReserveAsync(AiReservationRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new AiReservation("", false, code));
        public Task CommitAsync(AiUsageRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReleaseAsync(string reservationId, AiOutcome reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
