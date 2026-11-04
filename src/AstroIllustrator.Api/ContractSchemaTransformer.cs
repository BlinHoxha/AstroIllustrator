using System.Text.Json.Nodes;
using AstroIllustrator.Core;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>Add semantic constraints to the serializer-derived schemas, without changing the wire DTOs.</summary>
public sealed class ContractSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken token)
    {
        var type = context.JsonTypeInfo.Type;
        var primitive = Nullable.GetUnderlyingType(type) ?? type;
        bool nullable = Nullable.GetUnderlyingType(context.JsonPropertyInfo?.PropertyType ?? type) is not null ||
            schema.Type?.HasFlag(JsonSchemaType.Null) == true;
        if (primitive == typeof(DateTimeOffset) || primitive == typeof(DateTime))
            schema.Type = JsonSchemaType.String | (nullable ? JsonSchemaType.Null : 0);
        // Canonical JSON output is numeric. Historical quoted-number input coercion remains
        // accepted by the host, but generated clients send canonical JSON numbers.
        if (primitive == typeof(int) || primitive == typeof(long))
        {
            schema.Type = JsonSchemaType.Integer | (nullable ? JsonSchemaType.Null : 0);
            schema.AnyOf = null;
            schema.Pattern = null;
        }
        if (primitive == typeof(double) || primitive == typeof(float) || primitive == typeof(decimal))
        {
            schema.Type = JsonSchemaType.Number | (nullable ? JsonSchemaType.Null : 0);
            schema.AnyOf = null;
            schema.Pattern = null;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) &&
            type.GetGenericArguments()[1] == typeof(System.Text.Json.JsonElement))
        {
            schema.AdditionalPropertiesAllowed = true;
            schema.AdditionalProperties = new OpenApiSchema();
        }
        if (type == typeof(IllustrationRequest))
        {
            Values(schema, "schemaVersion", "2.1");
            Values(schema, "illustrationType", "SkyChart", "ScientificDiagram");
            Values(schema, "language", "en", "sq");
            Values(schema, "preset", "DarkAtlas", "PrintMonochrome");
            schema.OneOf = new List<IOpenApiSchema>();
            foreach (var (kind, payload, other, model, otherModel) in new[]
            {
                ("SkyChart", "skyChart", "scientificDiagram", "SkyChartRequest", "ScientificDiagramRequest"),
                ("ScientificDiagram", "scientificDiagram", "skyChart", "ScientificDiagramRequest", "SkyChartRequest")
            })
                schema.OneOf.Add(new OpenApiSchema
                {
                    Type = JsonSchemaType.Object, Required = new HashSet<string>(kind == "ScientificDiagram" ? new[] { payload, "illustrationType" } : new[] { payload }),
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["illustrationType"] = StringEnum(kind),
                        [payload] = new OpenApiSchemaReference(model, context.Document)
                    },
                    Not = new OpenApiSchema
                    {
                        Required = new HashSet<string> { other },
                        Properties = new Dictionary<string, IOpenApiSchema> { [other] = new OpenApiSchemaReference(otherModel, context.Document) }
                    }
                });
            schema.Description = "Exactly one non-null matching scientific payload. Omitted scalar properties retain schema 2.1 defaults; unknown members are rejected. Further scene-dependent rules are enforced by the backend validator.";
        }
        if (type == typeof(SkyChartRequest))
        {
            Values(schema, "sceneType", "SingleConstellation", "MultipleConstellations", "Asterism", "HorizonView");
            Values(schema, "projection", "Gnomonic", "AltitudeAzimuth");
            Range(schema, "magnitudeLimit", "2", "6.5");
            Range(schema, "azimuthCenterDegrees", "0", "360", exclusiveMaximum: true);
            Range(schema, "fieldOfViewDegrees", "10", "180");
        }
        if (type == typeof(ObserverLocation))
        {
            Range(schema, "latitude", "-90", "90");
            Range(schema, "longitude", "-180", "180");
            Range(schema, "elevationMeters", "-500", "10000");
        }
        if (type == typeof(TimeReference))
        {
            Values(schema, "type", "AbsoluteUtc", "LocalClock");
            Values(schema, "ambiguityPolicy", "Reject", "Earlier", "Later");
            if (schema.Properties?["localDateTime"] is OpenApiSchema local)
            {
                local.Format = null;
                local.Description = "Local civil timestamp without UTC offset. Requires IANA timeZoneId; Reject/Earlier/Later resolves DST overlaps. Gaps are rejected.";
            }
        }
        if (type == typeof(ScientificDiagramRequest))
        {
            Values(schema, "diagramType", "StarAltitude", "PolarisLatitude", "AltitudeAzimuth", "AngularSeparation");
            Values(schema, "mode", "Schematic", "Computed");
        }
        if (type == typeof(DiagramParameters)) Values(schema, "starId", "Polaris");
        if (type == typeof(DiagramCapability))
        {
            Values(schema, "code", "StarAltitude", "PolarisLatitude", "AltitudeAzimuth", "AngularSeparation");
            ArrayValues(schema, "modes", "Schematic", "Computed");
        }
        if (type == typeof(OutputResponse) || type == typeof(RenderOutput)) Values(schema, "format", "svg", "png", "manifest");
        if (type == typeof(ConstellationResponse) || type == typeof(AsterismResponse))
            if (schema.Properties?["edges"] is OpenApiSchema edges && edges.Items is OpenApiSchema pair)
            { pair.MinItems = 2; pair.MaxItems = 2; }
        if (type == typeof(HealthResponse)) Values(schema, "status", "Healthy");
        if (type == typeof(ReadinessResponse)) Values(schema, "status", "Ready", "NotReady");
        if (type == typeof(RendererCapabilities))
        {
            Values(schema, "schemaVersion", "2.1");
            ArrayValues(schema, "sceneTypes", "SingleConstellation", "MultipleConstellations", "Asterism", "HorizonView");
            ArrayValues(schema, "languages", "en", "sq");
            ArrayValues(schema, "presets", "DarkAtlas", "PrintMonochrome");
            ArrayValues(schema, "outputFormats", "svg", "png", "manifest");
        }
        if (type == typeof(ChartDescriptionRequest))
        {
            Values(schema, "language", "en", "sq");
            if (schema.Properties?["description"] is OpenApiSchema description)
            { description.MinLength = 1; description.MaxLength = 2000; }
        }
        if (type == typeof(ChartProposalResponse)) Values(schema, "status", "Ready", "NeedsInput", "Unsupported", "Invalid");
        if (type == typeof(ChartProposalParameters))
        {
            Values(schema, "scene", "SingleConstellation", "MultipleConstellations", "Asterism", "HorizonView", "ScientificDiagram");
            Values(schema, "diagramType", "StarAltitude", "PolarisLatitude", "AltitudeAzimuth", "AngularSeparation");
            Values(schema, "mode", "Schematic", "Computed");
            Values(schema, "language", "en", "sq");
            Values(schema, "preset", "DarkAtlas", "PrintMonochrome");
        }
        if (type == typeof(RenderJob) || type == typeof(RenderJobSummary) || type == typeof(ObserverPreset) ||
            type == typeof(ChartProposalResponse) || type == typeof(ChartAiStatus))
            schema.Required = new HashSet<string>(schema.Properties?.Keys ?? []);
        return Task.CompletedTask;
    }

    private static OpenApiSchema StringEnum(params string[] values) => new()
    {
        Type = JsonSchemaType.String,
        Enum = values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList()
    };
    private static void Values(OpenApiSchema schema, string property, params string[] values)
    {
        if (schema.Properties?.TryGetValue(property, out var value) == true && value is OpenApiSchema field)
        {
            field.Enum = StringEnum(values).Enum;
            if (field.Type?.HasFlag(JsonSchemaType.Null) == true) field.Enum!.Add(null!);
        }
    }
    private static void ArrayValues(OpenApiSchema schema, string property, params string[] values)
    {
        if (schema.Properties?.TryGetValue(property, out var value) == true && value is OpenApiSchema field)
            field.Items = StringEnum(values);
    }
    private static void Range(OpenApiSchema schema, string property, string min, string max, bool exclusiveMaximum = false)
    {
        if (schema.Properties?.TryGetValue(property, out var value) == true && value is OpenApiSchema field)
        {
            field.Minimum = min;
            field.Maximum = exclusiveMaximum ? null : max;
            field.ExclusiveMaximum = exclusiveMaximum ? max : null;
        }
    }
}
