using AIFramework.Core.Metering;
using AstroIllustrator.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class SqliteAiBudgetLedgerTests
{
    private static readonly AiUsageScope Scope = new("astroillustrator", "test", "local", "chart-proposal");
    private static readonly AiModelKey Model = new("azure-openai", "test-model");
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static AiReservationRequest Request(string key, decimal amount = 1m, string currency = "USD")
        => new(key, Scope, Model, amount, currency, "test-v1");
    private static AiUsageRecord Record(AiReservation reservation, AiReservationRequest request, decimal cost,
        AiOutcome outcome = AiOutcome.Completed) => new(reservation.ReservationId, request.RequestKey, request.Scope,
            request.ModelKey, null, 10, 20, request.Amount, cost, request.Currency, request.PriceVersion,
            outcome, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Parallel_reservations_never_exceed_monthly_limit()
    {
        for (int run = 0; run < 5; run++)
        {
            string root = NewRoot();
            try
            {
                string connection = await MigratedConnection(root);
                var ledger = new SqliteAiBudgetLedger(connection, Scope.Product, Scope.Environment, 20m, "USD", TimeProvider.System);
                var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(i =>
                    Task.Run(() => ledger.ReserveAsync(Request($"run:{run}:{i}"), CancellationToken.None))));
                Assert.Equal(20, results.Count(r => r.Granted));
                Assert.All(results.Where(r => !r.Granted), r => Assert.Equal("budget_exhausted", r.DenialCode));
            }
            finally { Cleanup(root); }
        }
    }

    [Fact]
    public async Task Duplicate_no_budget_commit_release_period_and_durability()
    {
        string root = NewRoot();
        try
        {
            string connection = await MigratedConnection(root);
            var clock = new Clock(new DateTimeOffset(2026, 1, 31, 23, 0, 0, TimeSpan.Zero));
            var ledger = new SqliteAiBudgetLedger(connection, Scope.Product, Scope.Environment, 3m, "USD", clock);
            var firstRequest = Request("first", 2m);
            var first = await ledger.ReserveAsync(firstRequest, CancellationToken.None);
            Assert.True(first.Granted);
            Assert.Equal("duplicate_request", (await ledger.ReserveAsync(firstRequest, CancellationToken.None)).DenialCode);
            Assert.Equal("no_budget", (await ledger.ReserveAsync(Request("wrong-currency", 1m, "EUR"), CancellationToken.None)).DenialCode);
            Assert.Equal("no_budget", (await ledger.ReserveAsync(new("wrong-scope", Scope with { Environment = "other" }, Model, 1m, "USD", "test-v1"), CancellationToken.None)).DenialCode);
            Assert.Equal("budget_exhausted", (await ledger.ReserveAsync(Request("too-much", 2m), CancellationToken.None)).DenialCode);
            clock.Now = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
            await ledger.CommitAsync(Record(first, firstRequest, 1m), CancellationToken.None);
            await ledger.CommitAsync(Record(first, firstRequest, 1m), CancellationToken.None);
            var february = await new SqliteAiBudgetLedger(connection, Scope.Product, Scope.Environment, 3m, "USD", clock)
                .ReserveAsync(Request("february", 3m), CancellationToken.None);
            Assert.True(february.Granted);
            Assert.Equal("budget_exhausted", (await ledger.ReserveAsync(Request("february-extra"), CancellationToken.None)).DenialCode);
            clock.Now = new DateTimeOffset(2026, 1, 31, 23, 0, 0, TimeSpan.Zero);
            var january = await ledger.ReserveAsync(Request("january", 2m), CancellationToken.None);
            Assert.True(january.Granted); // Commit in February kept the first reservation in January and freed one unit.
            await ledger.ReleaseAsync(january.ReservationId, AiOutcome.Cancelled, CancellationToken.None);
            await ledger.ReleaseAsync(january.ReservationId, AiOutcome.Cancelled, CancellationToken.None);
            Assert.True((await ledger.ReserveAsync(Request("after-release", 2m), CancellationToken.None)).Granted);
            await Assert.ThrowsAsync<ArgumentException>(() => ledger.CommitAsync(Record(new("missing", true, null), firstRequest, 1m), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => ledger.ReleaseAsync("missing", AiOutcome.Error, CancellationToken.None));
            var open = await ledger.ReserveAsync(Request("match", 0m), CancellationToken.None);
            await Assert.ThrowsAsync<ArgumentException>(() => ledger.CommitAsync(Record(open, Request("different", 0m), 0m), CancellationToken.None));
        }
        finally { Cleanup(root); }
    }

    private static string NewRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "astro-ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
    private static async Task<string> MigratedConnection(string root)
    {
        string connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "test.db") }.ToString();
        await using var db = new AstroDbContext(new DbContextOptionsBuilder<AstroDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        return connection;
    }
    private static void Cleanup(string root)
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, true);
    }
}
