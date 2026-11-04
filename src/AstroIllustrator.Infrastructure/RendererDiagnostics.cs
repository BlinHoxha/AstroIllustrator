using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using AstroIllustrator.Core;
using Microsoft.Extensions.Options;

namespace AstroIllustrator.Infrastructure;

/// <summary>Bounded, cached dependency/catalog probe. Never creates a render job.</summary>
public sealed class RendererDiagnostics(IOptions<RendererOptions> options) : IRendererDiagnostics, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private RendererProbe? cached;
    private DateTimeOffset expires;

    public async Task<RendererProbe> CheckAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cached is not null && expires > DateTimeOffset.UtcNow) return cached;
            cached = await ProbeAsync(cancellationToken);
            expires = DateTimeOffset.UtcNow.AddSeconds(cached.Ready ? 30 : 5);
            return cached;
        }
        finally { gate.Release(); }
    }

    private async Task<RendererProbe> ProbeAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process();
        try
        {
            var settings = options.Value;
            foreach (string folder in new[] { "artifacts", ".tools/matplotlib" })
            {
                string directory = Path.Combine(settings.WorkspaceRoot, folder);
                Directory.CreateDirectory(directory);
                string probeFile = Path.Combine(directory, ".ready-" + Guid.NewGuid().ToString("N"));
                await using (var stream = new FileStream(probeFile, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 1, FileOptions.DeleteOnClose))
                    await stream.WriteAsync(new byte[] { 0 }, deadline.Token);
            }
            process.StartInfo = new(settings.PythonExecutable)
            {
                WorkingDirectory = Path.Combine(settings.WorkspaceRoot, "renderer"),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            process.StartInfo.ArgumentList.Add("-m");
            process.StartInfo.ArgumentList.Add("astro_renderer.capabilities");
            process.StartInfo.Environment["MPLCONFIGDIR"] = Path.Combine(settings.WorkspaceRoot, ".tools", "matplotlib");
            process.StartInfo.Environment["PYTHONUTF8"] = "1";
            process.Start();
            var stdout = PythonRendererClient.ReadBoundedAsync(process.StandardOutput, 64_000, deadline.Token);
            var stderr = PythonRendererClient.ReadBoundedAsync(process.StandardError, 64_000, deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            string output = await stdout;
            await stderr; // Drain both bounded streams; never return raw process diagnostics to clients.
            if (process.ExitCode != 0) return new(false, "renderer_dependencies_unavailable", null);
            var capabilities = JsonSerializer.Deserialize<RendererCapabilities>(output, PythonRendererClient.JsonOptions);
            if (capabilities?.SchemaVersion != "2.1" || capabilities.ObservationCoverage is not { } coverage ||
                coverage.StartUtcInclusive >= coverage.EndUtcExclusive || capabilities.SceneTypes is not { Count: > 0 } ||
                capabilities.DiagramTemplates is not { Count: > 0 } || capabilities.ConstellationCodes is not { Count: > 0 })
                return new(false, "renderer_diagnostics_protocol", null);
            return new(true, null, capabilities);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(false, "renderer_diagnostics_timeout", null); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception or JsonException or RendererException)
        { return new(false, "renderer_diagnostics_unavailable", null); }
        finally
        {
            try { if (process.Id > 0 && !process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
    }

    public void Dispose() => gate.Dispose();
}
