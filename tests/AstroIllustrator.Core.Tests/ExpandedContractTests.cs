using System.Text.Json;
using AstroIllustrator.Core;
using Xunit;

public sealed class ExpandedContractTests
{
    [Theory]
    [InlineData("ursa-minor")]
    [InlineData("summer-triangle")]
    [InlineData("multiple-constellations")]
    [InlineData("southern-horizon")]
    [InlineData("altitude-45")]
    [InlineData("altitude-azimuth")]
    [InlineData("polaris-latitude")]
    [InlineData("polaris-computed")]
    [InlineData("angular-separation")]
    public void Documented_examples_pass_contract_validation(string name)
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        var request = JsonSerializer.Deserialize<IllustrationRequest>(File.ReadAllText(Path.Combine(root, "examples", name + ".json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Empty(IllustrationValidator.Validate(request));
    }

    [Fact]
    public void Local_clock_resolves_dst_gap_and_overlap_explicitly()
    {
        TimeReference Local(DateTime time, string policy = "Reject") => new() { Type = "LocalClock", LocalDateTime = time, TimeZoneId = "Europe/Budapest", AmbiguityPolicy = policy };
        Assert.Throws<ArgumentException>(() => ObserverTime.Resolve(null, Local(new(2026, 3, 29, 2, 30, 0))));
        var overlap = new DateTime(2026, 10, 25, 2, 30, 0);
        Assert.Throws<ArgumentException>(() => ObserverTime.Resolve(null, Local(overlap)));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), ObserverTime.Resolve(null, Local(overlap, "Earlier")));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), ObserverTime.Resolve(null, Local(overlap, "Later")));
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 21, 0, 0, TimeSpan.Zero), ObserverTime.Resolve(null, Local(new(2026, 7, 15, 23, 0, 0))));
    }

    [Theory]
    [InlineData(91,0)]
    [InlineData(0,181)]
    [InlineData(double.NaN,0)]
    public void Invalid_global_coordinates_are_rejected(double lat, double lon) =>
        Assert.NotEmpty(IllustrationValidator.ValidateObserver(new(lat, lon)));

    [Fact]
    public void Coordinate_errors_identify_every_failed_field_without_blaming_valid_fields()
    {
        Assert.Equal("observer.longitude", Assert.Single(IllustrationValidator.ValidateObserver(new(47, 181))).Field);
        Assert.Equal("observer.latitude", Assert.Single(IllustrationValidator.ValidateObserver(new(-91, 19))).Field);
        Assert.Equal("observer.elevationMeters", Assert.Single(IllustrationValidator.ValidateObserver(new(47, 19, 10001))).Field);
        Assert.Equal(3, IllustrationValidator.ValidateObserver(new(double.NaN, double.PositiveInfinity, -501)).Count);
        Assert.Equal("observer", Assert.Single(IllustrationValidator.ValidateObserver(null)).Field);
    }

    [Fact]
    public void Historical_or_invalid_request_metadata_does_not_break_history()
    {
        Assert.Null(RenderSceneMetadata.FromRequestJson("{}"));
        Assert.Null(RenderSceneMetadata.FromRequestJson("not JSON"));
        Assert.Null(RenderSceneMetadata.FromRequestJson("null"));
        Assert.Equal("ORI", RenderSceneMetadata.FromRequestJson("""{"skyChart":{}}""")!.ConstellationCodes[0]);
        Assert.Empty(RenderSceneMetadata.FromRequestJson("""{"skyChart":{"sceneType":"HorizonView"}}""")!.ConstellationCodes);
    }

    [Fact]
    public void Diagram_and_sky_payloads_cannot_be_combined_or_forge_computed_angles()
    {
        Assert.NotEmpty(IllustrationValidator.Validate(new() { SkyChart = new(), ScientificDiagram = new() }));
        Assert.NotEmpty(IllustrationValidator.Validate(new() { IllustrationType = "ScientificDiagram", ScientificDiagram = new()
        {
            Mode = "Computed", Observer = new(42,21), TimeUtc = DateTimeOffset.Parse("2026-07-15T21:00:00Z"),
            Parameters = new() { StarId = "Polaris", AltitudeDegrees = 42 }
        } }));
        Assert.NotEmpty(IllustrationValidator.Validate(new() { SkyChart = new() { TimeUtc = DateTimeOffset.UtcNow } }));
    }
}
