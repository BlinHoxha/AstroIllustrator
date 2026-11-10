using AstroIllustrator.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AstroIllustrator.Infrastructure;

public sealed class AstroDbContext(DbContextOptions<AstroDbContext> options) : DbContext(options), IRenderJobStore
{
    public DbSet<RenderJob> RenderJobs => Set<RenderJob>();
    public DbSet<ObserverPreset> ObserverPresets => Set<ObserverPreset>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var job = modelBuilder.Entity<RenderJob>();
        job.HasKey(x => x.Id);
        job.Property(x => x.Status).HasConversion<string>();
        job.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        var preset = modelBuilder.Entity<ObserverPreset>();
        preset.HasKey(x => x.Id);
        preset.Property(x => x.Name).HasMaxLength(100);
        preset.HasIndex(x => x.Name).IsUnique();
    }
    public async Task AddAsync(RenderJob job, CancellationToken cancellationToken)
    {
        RenderJobs.Add(job);
        await SaveChangesAsync(cancellationToken);
    }
    public Task<RenderJob?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        RenderJobs.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task SaveAsync(CancellationToken cancellationToken) => await SaveChangesAsync(cancellationToken);
    public async Task<RenderJobPage> ListAsync(int page, int pageSize, RenderStatus? status, CancellationToken cancellationToken)
    {
        // SQLite cannot order DateTimeOffset through EF's translator. All stored instants
        // are UTC; their ISO text ordering is chronological. Parameterize the SQL boundary.
        string? state = status?.ToString();
        int count = await RenderJobs.CountAsync(x => status == null || x.Status == status, cancellationToken);
        var rows = await RenderJobs.FromSqlInterpolated($"""
            SELECT * FROM "RenderJobs" WHERE ({state} IS NULL OR "Status" = {state})
            ORDER BY "CreatedAtUtc" DESC, "Id" DESC LIMIT {pageSize} OFFSET {(page - 1) * pageSize}
            """).AsNoTracking().ToListAsync(cancellationToken);
        return new(rows.Select(x => new RenderJobSummary(x.Id, x.Status, x.CreatedAtUtc, x.CompletedAtUtc,
            x.ErrorCode, x.ErrorMessage, RenderSceneMetadata.FromRequestJson(x.RequestJson))).ToArray(), page, pageSize, count);
    }
}

[DbContext(typeof(AstroDbContext))]
[Migration("20261110000300_AiUsageLedger")]
public sealed class AddAiUsageLedger : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => ObserverPresetSchemaModel.Configure(modelBuilder);
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE "AiUsageEntries" (
          "ReservationId" TEXT NOT NULL PRIMARY KEY,
          "RequestKey" TEXT NOT NULL UNIQUE,
          "Product" TEXT NOT NULL, "Environment" TEXT NOT NULL, "TenantKey" TEXT NOT NULL,
          "Feature" TEXT NOT NULL, "Provider" TEXT NOT NULL, "Model" TEXT NOT NULL,
          "ResponseModel" TEXT NULL, "Period" TEXT NOT NULL, "State" TEXT NOT NULL,
          "ReservedAmount" TEXT NOT NULL, "Amount" TEXT NOT NULL,
          "Currency" TEXT NOT NULL, "PriceVersion" TEXT NOT NULL,
          "Outcome" TEXT NULL, "InputTokens" INTEGER NULL, "OutputTokens" INTEGER NULL,
          "UsageReported" INTEGER NULL, "ReservedAtUtc" TEXT NOT NULL,
          "StartedAtUtc" TEXT NULL, "CompletedAtUtc" TEXT NULL);
        CREATE INDEX "IX_AiUsageEntries_Product_Environment_Period"
          ON "AiUsageEntries" ("Product", "Environment", "Period");
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("AiUsageEntries");
}

[DbContext(typeof(AstroDbContext))]
[Migration("20261022000200_ObserverPresets")]
public sealed class AddObserverPresets : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => ObserverPresetSchemaModel.Configure(modelBuilder);
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "RenderJobs" ADD "ErrorMessage" TEXT NULL;
        CREATE TABLE "ObserverPresets" (
          "Id" TEXT NOT NULL CONSTRAINT "PK_ObserverPresets" PRIMARY KEY,
          "Name" TEXT NOT NULL, "Latitude" REAL NOT NULL, "Longitude" REAL NOT NULL,
          "ElevationMeters" REAL NOT NULL, "TimeZoneId" TEXT NULL);
        CREATE UNIQUE INDEX "IX_ObserverPresets_Name" ON "ObserverPresets" ("Name");
        """);
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ObserverPresets");
        migrationBuilder.DropColumn("ErrorMessage", "RenderJobs");
    }
}

[DbContext(typeof(AstroDbContext))]
[Migration("202610220001_InitialRenderJobs")]
public sealed class InitialRenderJobs : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => InitialSchemaModel.Configure(modelBuilder);
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE "RenderJobs" (
          "Id" TEXT NOT NULL CONSTRAINT "PK_RenderJobs" PRIMARY KEY,
          "Status" TEXT NOT NULL, "CreatedAtUtc" TEXT NOT NULL, "CompletedAtUtc" TEXT NULL,
          "RequestJson" TEXT NOT NULL, "RequestHash" TEXT NOT NULL,
          "ResultJson" TEXT NULL, "ErrorCode" TEXT NULL);
        CREATE INDEX "IX_RenderJobs_Status_CreatedAtUtc" ON "RenderJobs" ("Status", "CreatedAtUtc");
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("RenderJobs");
}
