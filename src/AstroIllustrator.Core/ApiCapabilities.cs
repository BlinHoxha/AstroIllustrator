using System.Text.Json;

namespace AstroIllustrator.Core;

public sealed record RenderSceneMetadata(string IllustrationType, string SceneType, string? DiagramType,
    string? Mode, IReadOnlyList<string> ConstellationCodes, string? AsterismCode, string Language, string Preset)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RenderSceneMetadata? FromRequestJson(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<IllustrationRequest>(json, JsonOptions);
            if (request is null || request.Language is not ("en" or "sq") || request.Preset is not ("DarkAtlas" or "PrintMonochrome")) return null;
            if (request.SkyChart is { } chart && request.IllustrationType == "SkyChart" &&
                chart.SceneType is "SingleConstellation" or "MultipleConstellations" or "Asterism" or "HorizonView")
            {
                var codes = chart.ConstellationCodes ?? (chart.ConstellationCode is { } code ? [code]
                    : chart.SceneType == "SingleConstellation" ? new[] { "ORI" } : []);
                if (codes.Any(code => !IllustrationValidator.ConstellationCodes.Contains(code))) return null;
                return new("SkyChart", chart.SceneType, null, null, codes,
                    chart.AsterismCode, request.Language, request.Preset);
            }
            if (request.ScientificDiagram is { } diagram && request.IllustrationType == "ScientificDiagram" &&
                IllustrationValidator.DiagramTypes.Contains(diagram.DiagramType) && diagram.Mode is "Schematic" or "Computed")
                return new("ScientificDiagram", "ScientificDiagram", diagram.DiagramType, diagram.Mode,
                    [], null, request.Language, request.Preset);
        }
        catch (JsonException) { }
        return null; // Historical/corrupt input must not prevent listing other jobs.
    }
}

public sealed record ObservationCoverage(int NominalStartYear, int NominalEndYear,
    DateTimeOffset StartUtcInclusive, DateTimeOffset EndUtcExclusive, string IersVersion,
    string IersSha256, string TimeZoneDataVersion, bool PredictionsIncluded, string Note);
public sealed record DiagramCapability(string Code, IReadOnlyList<string> Modes);
public sealed record RendererCapabilities(string SchemaVersion, string RendererVersion,
    IReadOnlyList<string> SceneTypes, IReadOnlyList<string> ConstellationCodes,
    IReadOnlyList<string> AsterismCodes, IReadOnlyList<DiagramCapability> DiagramTemplates,
    IReadOnlyList<string> Languages, IReadOnlyList<string> Presets, IReadOnlyList<string> OutputFormats,
    ObservationCoverage ObservationCoverage);
public sealed record RendererProbe(bool Ready, string? Code, RendererCapabilities? Capabilities);
public interface IRendererDiagnostics
{
    Task<RendererProbe> CheckAsync(CancellationToken cancellationToken);
}
