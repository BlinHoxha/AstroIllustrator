using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AstroIllustrator.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AstroIllustrator.Infrastructure;

public sealed class RendererOptions
{
    public string WorkspaceRoot { get; set; } = "";
    public string PythonExecutable { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
}

public sealed class PythonRendererClient(IOptions<RendererOptions> options, ILogger<PythonRendererClient> logger)
    : IRendererAdapter, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RendererResult> RenderAsync(Guid jobId, IllustrationRequest request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var settings = options.Value;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            using Process process = new();
            process.StartInfo = new ProcessStartInfo(settings.PythonExecutable)
            {
                WorkingDirectory = Path.Combine(settings.WorkspaceRoot, "renderer"),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            process.StartInfo.ArgumentList.Add("-m");
            process.StartInfo.ArgumentList.Add("astro_renderer");
            process.StartInfo.ArgumentList.Add("--output-dir");
            process.StartInfo.ArgumentList.Add(Path.Combine(settings.WorkspaceRoot, "artifacts", jobId.ToString("N")));
            process.StartInfo.Environment["MPLCONFIGDIR"] = Path.Combine(settings.WorkspaceRoot, ".tools", "matplotlib");
            process.StartInfo.Environment["PYTHONUTF8"] = "1";
            try
            {
                process.Start();
                Task<string> stdout = ReadBoundedAsync(process.StandardOutput, 64_000, deadline.Token);
                Task<string> stderr = ReadBoundedAsync(process.StandardError, 64_000, deadline.Token);
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request, JsonOptions).AsMemory(), deadline.Token);
                process.StandardInput.Close();
                await process.WaitForExitAsync(deadline.Token);
                string output = await stdout;
                string error = await stderr;
                if (process.ExitCode != 0)
                {
                    logger.LogWarning("Renderer failed for {RenderJobId}: {Stderr}", jobId, error);
                    if (process.ExitCode == 2)
                    {
                        try
                        {
                            using var details = JsonDocument.Parse(error);
                            if (details.RootElement.GetProperty("error").GetString() == "ScientificError")
                                throw new RendererException("scientific_unsupported", details.RootElement.GetProperty("message").GetString() ?? "Unsupported scientific request.");
                        }
                        catch (JsonException) { }
                        catch (KeyNotFoundException) { }
                    }
                    throw new RendererException("renderer_failed", "Scientific renderer failed; inspect the correlated server log.");
                }
                var result = JsonSerializer.Deserialize<RendererResult>(output, JsonOptions);
                if (result is null || result.SchemaVersion != "2.1" || result.Outputs is null ||
                    result.Outputs.Count != 3 || string.IsNullOrWhiteSpace(result.GeometryHash) ||
                    !result.Outputs.Select(x => x.FileName).Order().SequenceEqual(new[] { "chart.png", "chart.svg", "manifest.json" }))
                    throw new RendererException("renderer_protocol", "Renderer returned an invalid result.");
                foreach (var item in result.Outputs)
                {
                    string path = Path.Combine(settings.WorkspaceRoot, "artifacts", jobId.ToString("N"), item.FileName);
                    if (!File.Exists(path) || new FileInfo(path).Length > 32_000_000)
                        throw new RendererException("renderer_protocol", "Renderer output missing or too large.");
                    await using var stream = File.OpenRead(path);
                    string actualHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, deadline.Token));
                    if (actualHash != item.Sha256)
                        throw new RendererException("renderer_protocol", "Renderer output hash mismatch.");
                }
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new RendererException("renderer_timeout", "Scientific renderer exceeded its time limit.");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new RendererException("renderer_unavailable", "Scientific renderer could not be started.");
            }
            catch (JsonException)
            {
                throw new RendererException("renderer_protocol", "Scientific renderer returned malformed JSON.");
            }
            finally
            {
                try { if (process.Id > 0 && !process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
        }
        finally { gate.Release(); }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var result = new System.Text.StringBuilder();
        char[] buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (result.Length + count > limit) throw new RendererException("renderer_protocol", "Renderer log/output limit exceeded.");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
    public void Dispose() => gate.Dispose();
}
