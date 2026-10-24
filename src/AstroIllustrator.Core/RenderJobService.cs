using System.Security.Cryptography;
using System.Text;

namespace AstroIllustrator.Core;

public sealed class RenderJobService(IRenderJobStore store, IRendererAdapter renderer)
{
    // Serialization belongs to the protocol boundary; the exact submitted JSON is immutable metadata.
    public async Task<RenderJob> RenderAsync(IllustrationRequest request, string requestJson, CancellationToken token)
    {
        if (IllustrationValidator.Validate(request).Count != 0)
            throw new ArgumentException("Validate the illustration request before rendering.", nameof(request));
        var job = new RenderJob { Id = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow,
            RequestJson = requestJson, RequestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson))) };
        await store.AddAsync(job, token);
        try
        {
            job.Status = RenderStatus.Running;
            await store.SaveAsync(token);
            job.ResultJson = System.Text.Json.JsonSerializer.Serialize(await renderer.RenderAsync(job.Id, request, token),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            job.Status = RenderStatus.Completed;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            job.Status = RenderStatus.Cancelled;
            job.ErrorCode = "request_cancelled";
            job.ErrorMessage = "Rendering was cancelled by the caller.";
        }
        catch (RendererException error)
        {
            job.Status = RenderStatus.Failed;
            job.ErrorCode = error.Code;
            job.ErrorMessage = error.Message;
        }
        catch
        {
            job.Status = RenderStatus.Failed;
            job.ErrorCode = "renderer_unavailable";
            job.ErrorMessage = "The scientific renderer could not complete this request.";
            throw;
        }
        finally
        {
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await store.SaveAsync(deadline.Token);
        }
        return job;
    }
}
