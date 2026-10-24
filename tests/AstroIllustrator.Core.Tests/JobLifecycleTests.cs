using AstroIllustrator.Core;
using Xunit;

public sealed class JobLifecycleTests
{
    private sealed class Store : IRenderJobStore
    {
        public RenderJob? Job;
        public List<RenderStatus> Saves = [];
        public Task AddAsync(RenderJob job, CancellationToken token) { Job = job; Saves.Add(job.Status); return Task.CompletedTask; }
        public Task<RenderJob?> FindAsync(Guid id, CancellationToken token) => Task.FromResult(Job);
        public Task<RenderJobPage> ListAsync(int page, int pageSize, RenderStatus? status, CancellationToken token) => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken token) { Saves.Add(Job!.Status); return Task.CompletedTask; }
    }
    private sealed class Renderer(Exception? failure = null) : IRendererAdapter
    {
        public Task<RendererResult> RenderAsync(Guid id, IllustrationRequest request, CancellationToken token) =>
            failure is null ? Task.FromResult(new RendererResult("2.1", "test", "hash", [])) : Task.FromException<RendererResult>(failure);
    }

    [Fact]
    public async Task Successful_job_has_ordered_lifecycle_and_immutable_request()
    {
        var store = new Store();
        var job = await new RenderJobService(store, new Renderer()).RenderAsync(new() { SkyChart = new() }, "original request", default);
        Assert.Equal(new[] { RenderStatus.Queued, RenderStatus.Running, RenderStatus.Completed }, store.Saves);
        Assert.Equal("original request", job.RequestJson);
        Assert.Equal(64, job.RequestHash.Length);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.NotNull(job.ResultJson);
    }

    [Fact]
    public async Task Scientific_failure_is_saved_with_machine_readable_code()
    {
        var store = new Store();
        var job = await new RenderJobService(store, new Renderer(new RendererException("catalog_missing", "Missing catalog")))
            .RenderAsync(new() { SkyChart = new() }, "{}", default);
        Assert.Equal(RenderStatus.Failed, job.Status);
        Assert.Equal("catalog_missing", job.ErrorCode);
        Assert.Null(job.ResultJson);
    }

    [Fact]
    public async Task Cancellation_still_persists_terminal_state()
    {
        var store = new Store();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var job = await new RenderJobService(store, new Renderer(new OperationCanceledException(cancellation.Token)))
            .RenderAsync(new() { SkyChart = new() }, "{}", cancellation.Token);
        Assert.Equal(RenderStatus.Cancelled, job.Status);
        Assert.Equal(RenderStatus.Cancelled, store.Saves.Last());
    }
}
