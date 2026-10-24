using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class ProcessBridgeTests
{
    [Theory]
    [InlineData("import sys; sys.stdin.read(); sys.exit(2)", "renderer_failed")]
    [InlineData("import sys; sys.stdin.read(); print('not JSON')", "renderer_protocol")]
    [InlineData("import sys, time; sys.stdin.read(); time.sleep(30)", "renderer_timeout")]
    public async Task Subprocess_failures_are_bounded_and_classified(string script, string expected)
    {
        string repo = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string root = Path.Combine(Path.GetTempPath(), "astro-process-tests-" + Guid.NewGuid().ToString("N"));
        string package = Path.Combine(root, "renderer", "astro_renderer");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "__init__.py"), "");
        File.WriteAllText(Path.Combine(package, "__main__.py"), script);
        try
        {
            using var client = new PythonRendererClient(Options.Create(new RendererOptions
            {
                WorkspaceRoot = root, TimeoutSeconds = expected == "renderer_timeout" ? 2 : 10,
                PythonExecutable = Path.Combine(repo, ".venv", OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python")
            }), NullLogger<PythonRendererClient>.Instance);
            var error = await Assert.ThrowsAsync<RendererException>(() => client.RenderAsync(Guid.NewGuid(), new() { SkyChart = new() }, default));
            Assert.Equal(expected, error.Code);
        }
        finally { Directory.Delete(root, true); }
    }
}
