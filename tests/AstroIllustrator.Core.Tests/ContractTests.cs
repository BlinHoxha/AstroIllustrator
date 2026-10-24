using AstroIllustrator.Core;
using Xunit;

public sealed class ContractTests
{
    [Fact]
    public void Core_has_no_infrastructure_or_shared_framework_dependencies()
    {
        var names = typeof(IllustrationRequest).Assembly.GetReferencedAssemblies().Select(x => x.Name!);
        Assert.All(names, name => Assert.True(name.StartsWith("System.") || name == "System", name));
    }

    [Fact]
    public void Valid_orion_request_is_accepted() =>
        Assert.Empty(IllustrationValidator.Validate(new() { SkyChart = new() }));

    [Theory]
    [InlineData("UMA")]
    [InlineData("UNKNOWN")]
    [InlineData("ori")]
    public void Unsupported_catalogs_are_rejected(string code) =>
        Assert.NotEmpty(IllustrationValidator.Validate(new() { SkyChart = new() { ConstellationCode = code } }));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(7)]
    [InlineData(-1)]
    public void Invalid_magnitude_is_rejected(double value) =>
        Assert.NotEmpty(IllustrationValidator.Validate(new() { SkyChart = new() { MagnitudeLimit = value } }));

    [Fact]
    public void Missing_payload_and_unknown_discriminator_are_rejected()
    {
        Assert.NotEmpty(IllustrationValidator.Validate(new()));
        Assert.NotEmpty(IllustrationValidator.Validate(new() { IllustrationType = "ScientificDiagram", SkyChart = new() }));
    }
}
