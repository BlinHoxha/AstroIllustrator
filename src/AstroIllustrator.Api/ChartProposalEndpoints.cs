using AIFramework.Azure;
using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;

public static class ChartProposalEndpoints
{
    public static void AddChartProposals(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new ChartAiSettings(configuration.GetValue("ChartAI:Enabled", false),
            configuration.GetValue("ChartAI:TimeoutSeconds", 25), configuration.GetValue("ChartAI:MaxRequestsPerDay", 100),
            configuration.GetValue("ChartAI:MaxOutputTokens", 1024));
        if (settings.TimeoutSeconds is < 1 or > 60 || settings.MaxRequestsPerDay is < 1 or > 1000 || settings.MaxOutputTokens is < 256 or > 4096)
            throw new InvalidOperationException("ChartAI limits are outside supported bounds.");
        services.AddSingleton(settings);
        if (!settings.Enabled) return;
        if ((configuration["ChartAI:Provider"] ?? "AzureOpenAI") != "AzureOpenAI")
            throw new InvalidOperationException("Only AzureOpenAI structured chart interpretation is implemented. Bedrock is a later adapter.");
        if (!Uri.TryCreate(configuration["ChartAI:Azure:Endpoint"], UriKind.Absolute, out var endpoint) ||
            string.IsNullOrWhiteSpace(configuration["ChartAI:Azure:Deployment"]))
            throw new InvalidOperationException("Enabling ChartAI requires an explicit Azure endpoint and deployment.");
        services.AddAzureStructuredGeneration(new(endpoint, configuration["ChartAI:Azure:Deployment"]!));
        services.AddSingleton<IChartProposalInterpreter, ChartProposalInterpreter>();
    }

    public static void MapChartProposals(this WebApplication app)
    {
        app.MapGet("/api/chart-proposals/status", (ChartAiSettings settings) => Results.Ok(
            new ChartAiStatus(settings.Enabled, settings.Enabled ? "azure-openai" : null, 2000)))
            .WithName("GetChartAiStatus").Produces<ChartAiStatus>();
        app.MapPost("/api/chart-proposals", async (ChartDescriptionRequest request, ChartAiSettings settings,
            IServiceProvider services, CancellationToken token) =>
        {
            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 2000 || request.Language is not ("en" or "sq"))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["description"] = ["Enter 1–2000 characters and choose en or sq interface language."] });
            if (!settings.Enabled) return Failure(503, "ai_disabled");
            try { return Results.Ok(await services.GetRequiredService<IChartProposalInterpreter>().InterpretAsync(request.Description, request.Language, token)); }
            catch (ChartProposalException e)
            { return Failure(e.Code is "ai_busy" or "ai_daily_limit" ? 429 : e.Code == "ai_timeout" ? 504 : 503, e.Code); }
        }).RequireRateLimiting("chart-ai").WithName("InterpretChartDescription")
          .Produces<ChartProposalResponse>().ProducesValidationProblem().ProducesProblem(429).ProducesProblem(503).ProducesProblem(504)
          .WithDescription("Optional AI proposal only; never creates a render job. User reviews and applies fields, then submits through normal scientific validation. No automatic retries; coordinates and observation time must be entered explicitly.");
    }

    private static IResult Failure(int status, string code) => Results.Problem(statusCode: status,
        title: "Chart interpretation unavailable", detail: "Your draft is retained. You can continue using the chart form or retry deliberately later.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
