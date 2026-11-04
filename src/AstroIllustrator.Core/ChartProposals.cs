namespace AstroIllustrator.Core;

public sealed record ChartDescriptionRequest(string Description, string Language = "en");
public sealed record ChartAiStatus(bool Enabled, string? Provider, int MaxDescriptionLength);

// Proposal fields are deliberately separate from authoritative render requests.
// Observation coordinates/time are entered explicitly by the person in the existing form.
public sealed record ChartProposalParameters
{
    public string? Scene { get; init; }
    public IReadOnlyList<string>? ConstellationCodes { get; init; }
    public string? DiagramType { get; init; }
    public string? Mode { get; init; }
    public string? Language { get; init; }
    public string? Preset { get; init; }
    public double? MagnitudeLimit { get; init; }
    public double? AltitudeDegrees { get; init; }
    public double? AzimuthDegrees { get; init; }
    public IReadOnlyList<int>? StarHipIds { get; init; }
}
public sealed record ChartProposalResponse(string Status, ChartProposalParameters? Parameters,
    IReadOnlyList<string> RequiredFields, IReadOnlyList<ValidationIssue> Issues, string Provider);
public interface IChartProposalInterpreter
{
    Task<ChartProposalResponse> InterpretAsync(string description, string language, CancellationToken token);
}
public sealed class ChartProposalException(string code) : Exception("Chart interpretation did not complete.")
{
    public string Code { get; } = code;
}

public static class ChartProposalValidation
{
    public static ChartProposalResponse Check(ChartProposalParameters p, string provider)
    {
        List<string> required = [];
        List<ValidationIssue> issues = [];
        void Invalid(string field) => issues.Add(new(field, "The proposed value is unsupported. Edit the description or use the form."));
        if (p.Scene is null) required.Add("scene");
        else if (p.Scene is not ("SingleConstellation" or "MultipleConstellations" or "Asterism" or "HorizonView" or "ScientificDiagram")) Invalid("scene");
        if (p.Language is not (null or "en" or "sq")) Invalid("language");
        if (p.Preset is not (null or "DarkAtlas" or "PrintMonochrome")) Invalid("preset");
        if (p.MagnitudeLimit is { } magnitude && (!double.IsFinite(magnitude) || magnitude is < 2 or > 6.5)) Invalid("magnitudeLimit");
        if (p.ConstellationCodes is { } codes && (codes.Count > 5 || codes.Any(c => !IllustrationValidator.ConstellationCodes.Contains(c)) || codes.Distinct().Count() != codes.Count)) Invalid("constellationCodes");
        if (p.Scene is "SingleConstellation" or "MultipleConstellations")
        {
            if (p.ConstellationCodes is not { Count: > 0 }) required.Add("constellationCodes");
            else if (p.Scene == "SingleConstellation" && p.ConstellationCodes.Count != 1 || p.Scene == "MultipleConstellations" && p.ConstellationCodes.Count < 2) Invalid("constellationCodes");
        }
        else if (p.ConstellationCodes is { Count: > 0 }) Invalid("constellationCodes");
        if (p.Scene == "ScientificDiagram")
        {
            if (p.DiagramType is null) required.Add("diagramType");
            else if (!IllustrationValidator.DiagramTypes.Contains(p.DiagramType)) Invalid("diagramType");
            if (p.Mode is null) required.Add("mode");
            else if (p.Mode is not ("Schematic" or "Computed") || p.DiagramType == "AltitudeAzimuth" && p.Mode != "Schematic" || p.DiagramType == "AngularSeparation" && p.Mode != "Computed") Invalid("mode");
            if (p.Mode == "Schematic" && p.DiagramType is "StarAltitude" or "AltitudeAzimuth")
            {
                if (p.AltitudeDegrees is null) required.Add("altitudeDegrees");
                else if (!double.IsFinite(p.AltitudeDegrees.Value) || p.AltitudeDegrees is < -90 or > 90) Invalid("altitudeDegrees");
                if (p.DiagramType == "AltitudeAzimuth")
                {
                    if (p.AzimuthDegrees is null) required.Add("azimuthDegrees");
                    else if (!double.IsFinite(p.AzimuthDegrees.Value) || p.AzimuthDegrees is < 0 or >= 360) Invalid("azimuthDegrees");
                }
                else if (p.AzimuthDegrees is not null) Invalid("azimuthDegrees");
            }
            else if (p.AltitudeDegrees is not null || p.AzimuthDegrees is not null) Invalid("altitudeDegrees");
            if (p.DiagramType == "AngularSeparation")
            {
                if (p.StarHipIds is null || p.StarHipIds.Count == 0) required.Add("starHipIds");
                else if (p.StarHipIds.Count != 2 || p.StarHipIds.Any(id => id < 1) || p.StarHipIds[0] == p.StarHipIds[1]) Invalid("starHipIds");
            }
            else if (p.StarHipIds is { Count: > 0 }) Invalid("starHipIds");
            if (p.MagnitudeLimit is not null) Invalid("magnitudeLimit");
        }
        else if (p.DiagramType is not null || p.Mode is not null || p.AltitudeDegrees is not null || p.AzimuthDegrees is not null || p.StarHipIds is { Count: > 0 }) Invalid("diagramType");
        if (p.Scene == "HorizonView" || p.Scene == "ScientificDiagram" && p.DiagramType == "PolarisLatitude" ||
            p.Scene == "ScientificDiagram" && p.Mode == "Computed" && p.DiagramType != "AngularSeparation") required.Add("observer");
        if (p.Scene == "HorizonView" || p.Scene == "ScientificDiagram" && p.Mode == "Computed" && p.DiagramType != "AngularSeparation") required.Add("timeReference");
        return issues.Count > 0 ? new("Invalid", null, [], issues, provider)
            : new(required.Count > 0 ? "NeedsInput" : "Ready", p, required, [], provider);
    }
}
