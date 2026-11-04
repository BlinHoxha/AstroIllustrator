using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIFramework.Core.Abstractions;
using AIFramework.Core.Models;
using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class ChartProposalTests
{
    [Fact]
    public void Enabled_configuration_requires_an_explicit_supported_provider_and_deployment()
    {
        IConfiguration Config(Dictionary<string, string?> entries) => new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddChartProposals(Config(new() { ["ChartAI:Enabled"] = "true" })));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddChartProposals(Config(new() { ["ChartAI:Enabled"] = "true", ["ChartAI:Provider"] = "Bedrock" })));
        var services = new ServiceCollection();
        services.AddChartProposals(Config(new() { ["ChartAI:Enabled"] = "true", ["ChartAI:Azure:Endpoint"] = "https://test.openai.azure.com/", ["ChartAI:Azure:Deployment"] = "explicit-deployment" }));
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

    private static ChartProposalInterpreter Interpreter(Model model, ChartAiSettings? settings = null) => new(model, settings ?? new(true), NullLogger<ChartProposalInterpreter>.Instance);
    private static void Register(IServiceCollection services, Model model)
    {
        services.RemoveAll<ChartAiSettings>();
        services.AddSingleton(new ChartAiSettings(true));
        services.AddSingleton<IStructuredGenerationModel>(model);
        services.AddSingleton<IChartProposalInterpreter, ChartProposalInterpreter>();
    }
    private sealed class Model(ChartProposalParameters parameters, bool supported = true) : IStructuredGenerationModel
    {
        public int Calls { get; private set; }
        public bool Block { get; init; }
        public StructuredGenerationStatus Status { get; init; } = StructuredGenerationStatus.Completed;
        public StructuredGenerationRequest? LastRequest { get; private set; }
        public async Task<StructuredGenerationResult> GenerateStructuredAsync(StructuredGenerationRequest request, CancellationToken token)
        {
            Calls++; LastRequest = request;
            if (Block) await Task.Delay(Timeout.Infinite, token);
            return new(Status, JsonSerializer.Serialize(new { supported, parameters }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "test-provider", new(10, 20));
        }
    }
}
