using AstroIllustrator.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AstroIllustrator.Infrastructure;

[DbContext(typeof(AstroDbContext))]
public sealed class AstroDbContextModelSnapshot : Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => ObserverPresetSchemaModel.Configure(modelBuilder);
}

internal static class ObserverPresetSchemaModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        InitialSchemaModel.Configure(modelBuilder);
        modelBuilder.Entity("AstroIllustrator.Core.RenderJob").Property<string>("ErrorMessage").HasColumnType("TEXT");
        modelBuilder.Entity("AstroIllustrator.Core.ObserverPreset", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("TEXT");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            b.Property<double>("Latitude").HasColumnType("REAL");
            b.Property<double>("Longitude").HasColumnType("REAL");
            b.Property<double>("ElevationMeters").HasColumnType("REAL");
            b.Property<string>("TimeZoneId").HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("Name").IsUnique();
            b.ToTable("ObserverPresets");
        });
    }
}

// Frozen initial model shared with its migration; future generated snapshots may replace
// the snapshot above without changing the target model of an already released migration.
internal static class InitialSchemaModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.13");
        modelBuilder.Entity("AstroIllustrator.Core.RenderJob", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("TEXT");
            b.Property<DateTimeOffset?>("CompletedAtUtc").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("TEXT");
            b.Property<string>("ErrorCode").HasColumnType("TEXT");
            b.Property<string>("RequestHash").IsRequired().HasColumnType("TEXT");
            b.Property<string>("RequestJson").IsRequired().HasColumnType("TEXT");
            b.Property<string>("ResultJson").HasColumnType("TEXT");
            b.Property<string>("Status").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("Status", "CreatedAtUtc");
            b.ToTable("RenderJobs");
        });
    }
}
