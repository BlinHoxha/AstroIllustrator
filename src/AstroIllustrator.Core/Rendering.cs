namespace AstroIllustrator.Core;

public sealed record IllustrationRequest
{
    public string SchemaVersion { get; init; } = "2.1";
    public string IllustrationType { get; init; } = "SkyChart";
    public string Language { get; init; } = "en";
    public string Preset { get; init; } = "DarkAtlas";
    public SkyChartRequest? SkyChart { get; init; }
    public ScientificDiagramRequest? ScientificDiagram { get; init; }
}

public sealed record SkyChartRequest
{
    public string SceneType { get; init; } = "SingleConstellation";
    // Backwards compatible singular selector. Omit it when using constellationCodes.
    public string? ConstellationCode { get; init; }
    public IReadOnlyList<string>? ConstellationCodes { get; init; }
    public string? AsterismCode { get; init; }
    public string Projection { get; init; } = "Gnomonic";
    public double MagnitudeLimit { get; init; } = 6;
    public ObserverLocation? Observer { get; init; }
    public DateTimeOffset? TimeUtc { get; init; }
    public TimeReference? TimeReference { get; init; }
    public double AzimuthCenterDegrees { get; init; } = 180;
    public double FieldOfViewDegrees { get; init; } = 100;
}

public sealed record ObserverLocation(double Latitude, double Longitude, double ElevationMeters = 0, string? TimeZoneId = null);
public sealed record TimeReference
{
    public string Type { get; init; } = "AbsoluteUtc";
    public DateTimeOffset? InstantUtc { get; init; }
    public DateTime? LocalDateTime { get; init; }
    public string? TimeZoneId { get; init; }
    public string AmbiguityPolicy { get; init; } = "Reject";
}

public sealed record ScientificDiagramRequest
{
    public string DiagramType { get; init; } = "StarAltitude";
    public string Mode { get; init; } = "Schematic";
    public ObserverLocation? Observer { get; init; }
    public DateTimeOffset? TimeUtc { get; init; }
    public TimeReference? TimeReference { get; init; }
    public DiagramParameters Parameters { get; init; } = new();
}

public sealed record DiagramParameters
{
    public double? AltitudeDegrees { get; init; }
    public double? AzimuthDegrees { get; init; }
    public string? StarId { get; init; }
    public IReadOnlyList<int>? StarHipIds { get; init; }
}

public sealed record ValidationIssue(string Field, string Message);
public sealed record RenderJobSummary(Guid Id, RenderStatus Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc, string? ErrorCode, string? ErrorMessage = null, RenderSceneMetadata? Scene = null);
public sealed record RenderJobPage(IReadOnlyList<RenderJobSummary> Items, int Page, int PageSize, int TotalCount);
public sealed class ObserverPreset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ElevationMeters { get; set; }
    public string? TimeZoneId { get; set; }
}
public sealed record ObserverPresetRequest(string Name, ObserverLocation Observer);

public enum RenderStatus { Queued, Running, Completed, Failed, Cancelled }
public sealed class RenderJob
{
    public Guid Id { get; set; }
    public RenderStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string RequestJson { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed record RenderOutput(string Format, string FileName, string Sha256);
public sealed record RendererResult(string SchemaVersion, string RendererVersion, string GeometryHash,
    IReadOnlyList<RenderOutput> Outputs);

public interface IRendererAdapter
{
    Task<RendererResult> RenderAsync(Guid jobId, IllustrationRequest request, CancellationToken cancellationToken);
}

public interface IRenderJobStore
{
    Task AddAsync(RenderJob job, CancellationToken cancellationToken);
    Task<RenderJob?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
    Task<RenderJobPage> ListAsync(int page, int pageSize, RenderStatus? status, CancellationToken cancellationToken);
}

public sealed class RendererException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
