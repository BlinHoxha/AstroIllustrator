using AIFramework.Azure;
using AIFramework.Application.Metering;
using AIFramework.Core.Metering;
using AIFramework.Ollama;
using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ChartProposalEndpoints
{
    public static void AddChartProposals(this IServiceCollection services, IConfiguration configuration,
        string connectionString, string environment)
    {
        string selectedProvider = configuration["ChartAI:Provider"] ?? "AzureOpenAI";
        var settings = new ChartAiSettings(configuration.GetValue("ChartAI:Enabled", false),
            configuration.GetValue("ChartAI:TimeoutSeconds", 25), configuration.GetValue("ChartAI:MaxRequestsPerDay", 100),
            configuration.GetValue("ChartAI:MaxOutputTokens", 1024),
            selectedProvider == "Ollama" ? "ollama" : "azure-openai");
        if (settings.TimeoutSeconds is < 1 or > 60 || settings.MaxRequestsPerDay is < 1 or > 1000 || settings.MaxOutputTokens is < 256 or > 4096)
            throw new InvalidOperationException("ChartAI limits are outside supported bounds.");
        services.AddSingleton(settings);
        if (!settings.Enabled) return;
        if (selectedProvider == "Ollama")
        {
            AddOllamaChartProposals(services, configuration, connectionString, environment, settings);
            return;
        }
        if (selectedProvider != "AzureOpenAI")
            throw new InvalidOperationException("Only AzureOpenAI and Development-only Ollama structured chart interpretation are implemented. Bedrock is a later adapter.");
        if (!Uri.TryCreate(configuration["ChartAI:Azure:Endpoint"], UriKind.Absolute, out var endpoint) ||
            string.IsNullOrWhiteSpace(configuration["ChartAI:Azure:Deployment"]))
            throw new InvalidOperationException("Enabling ChartAI requires an explicit Azure endpoint and deployment.");
        string? amountText = configuration["ChartAI:Budget:MonthlyAmount"];
        string? currency = configuration["ChartAI:Budget:Currency"];
        string? model = configuration["ChartAI:Pricing:Model"];
        string? inputText = configuration["ChartAI:Pricing:InputPerMillionTokens"];
        string? outputText = configuration["ChartAI:Pricing:OutputPerMillionTokens"];
        string? version = configuration["ChartAI:Pricing:PriceVersion"];
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0 ||
            !decimal.TryParse(inputText, NumberStyles.Number, CultureInfo.InvariantCulture, out var inputPrice) || inputPrice < 0 ||
            !decimal.TryParse(outputText, NumberStyles.Number, CultureInfo.InvariantCulture, out var outputPrice) || outputPrice < 0 ||
            string.IsNullOrWhiteSpace(currency) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException("Enabling ChartAI requires a positive budget and explicit model prices, currency and price version.");
        string tenant = configuration["ChartAI:Budget:TenantKey"] ?? "local";
        var scope = new AiUsageScope("astroillustrator", environment, tenant, "chart-proposal");
        var key = new AiModelKey("azure-openai", model);
        AiPriceCatalog catalog;
        try
        {
            scope.Validate();
            catalog = new([new AiPrice(key, inputPrice, outputPrice, currency, version)]);
            _ = new SqliteAiBudgetLedger(connectionString, scope.Product, scope.Environment, amount, currency, TimeProvider.System);
        }
        catch (ArgumentException e) { throw new InvalidOperationException("Invalid ChartAI budget, scope or price configuration.", e); }
        services.AddSingleton(scope);
        services.AddAiMetering(catalog, o => o.StructuredModel = key);
        services.RemoveAll<MeteredTextGeneration>(); // This host has no text-generation adapter.
        services.AddSingleton<IAiBudgetLedger>(provider => new SqliteAiBudgetLedger(connectionString,
            scope.Product, scope.Environment, amount, currency, provider.GetRequiredService<TimeProvider>()));
        services.AddAzureStructuredGeneration(new(endpoint, configuration["ChartAI:Azure:Deployment"]!));
        services.AddSingleton<IChartProposalInterpreter, ChartProposalInterpreter>();
    }

    private static void AddOllamaChartProposals(IServiceCollection services, IConfiguration configuration,
        string connectionString, string environment, ChartAiSettings settings)
    {
        if (!string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ollama chart interpretation is Development-only.");
        if (!Uri.TryCreate(configuration["ChartAI:Ollama:Endpoint"] ?? "http://127.0.0.1:11434/",
            UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("Ollama requires a loopback endpoint root.");
        var options = new OllamaOptions(endpoint, configuration["ChartAI:Ollama:Model"]!,
            configuration.GetValue("ChartAI:Ollama:ContextTokens", 8192),
            configuration.GetValue("ChartAI:Ollama:TimeoutSeconds", 120));
        try { options.Validate(); }
        catch (ArgumentException e) { throw new InvalidOperationException("Invalid Ollama configuration.", e); }
        if (options.TimeoutSeconds <= settings.TimeoutSeconds)
            throw new InvalidOperationException("ChartAI:Ollama:TimeoutSeconds must exceed ChartAI:TimeoutSeconds.");
        if (ChartProposalInterpreter.MaxInputTokenBound(2000) + settings.MaxOutputTokens > options.ContextTokens)
            throw new InvalidOperationException("Chart description may exceed Ollama context; lower ChartAI:MaxOutputTokens or raise ChartAI:Ollama:ContextTokens.");

        string? amountText = configuration["ChartAI:Budget:MonthlyAmount"];
        string? currency = configuration["ChartAI:Budget:Currency"];
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0 || string.IsNullOrWhiteSpace(currency))
            throw new InvalidOperationException("Enabling ChartAI requires a positive budget and currency.");
        string tenant = configuration["ChartAI:Budget:TenantKey"] ?? "local";
        var scope = new AiUsageScope("astroillustrator", environment, tenant, "chart-proposal");
        var key = new AiModelKey("ollama", options.Model);
        AiPriceCatalog catalog;
        try
        {
            scope.Validate();
            catalog = new([new AiPrice(key, 0, 0, currency, "local-zero")]);
            _ = new SqliteAiBudgetLedger(connectionString, scope.Product, scope.Environment, amount, currency, TimeProvider.System);
        }
        catch (ArgumentException e) { throw new InvalidOperationException("Invalid ChartAI budget, scope or Ollama model configuration.", e); }
        services.AddSingleton(scope);
        services.AddAiMetering(catalog, o => o.StructuredModel = key);
        services.RemoveAll<MeteredTextGeneration>();
        services.AddSingleton<IAiBudgetLedger>(provider => new SqliteAiBudgetLedger(connectionString,
            scope.Product, scope.Environment, amount, currency, provider.GetRequiredService<TimeProvider>()));
        services.AddOllamaModels(options);
        services.AddSingleton<IChartProposalInterpreter, ChartProposalInterpreter>();
    }

    public static void MapChartProposals(this WebApplication app)
    {
        app.MapGet("/api/chart-proposals/status", (ChartAiSettings settings) => Results.Ok(
            new ChartAiStatus(settings.Enabled, settings.Enabled ? settings.Provider : null, 2000)))
            .WithName("GetChartAiStatus").Produces<ChartAiStatus>();
        app.MapPost("/api/chart-proposals", async (ChartDescriptionRequest request, ChartAiSettings settings,
            IServiceProvider services, CancellationToken token) =>
        {
            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 2000 || request.Language is not ("en" or "sq"))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["description"] = ["Enter 1–2000 characters and choose en or sq interface language."] });
            if (!settings.Enabled) return Failure(503, "ai_disabled");
            try { return Results.Ok(await services.GetRequiredService<IChartProposalInterpreter>().InterpretAsync(request.Description, request.Language, token)); }
            catch (ChartProposalException e)
            { return Failure(e.Code is "ai_busy" or "ai_daily_limit" or "ai_budget_exhausted" ? 429 : e.Code == "ai_timeout" ? 504 : 503, e.Code); }
        }).RequireRateLimiting("chart-ai").WithName("InterpretChartDescription")
          .Produces<ChartProposalResponse>().ProducesValidationProblem().ProducesProblem(429).ProducesProblem(503).ProducesProblem(504)
          .WithDescription("Optional AI proposal only; never creates a render job. User reviews and applies fields, then submits through normal scientific validation. No automatic retries; coordinates and observation time must be entered explicitly.");
    }

    private static IResult Failure(int status, string code) => Results.Problem(statusCode: status,
        title: "Chart interpretation unavailable", detail: "Your draft is retained. You can continue using the chart form or retry deliberately later.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
