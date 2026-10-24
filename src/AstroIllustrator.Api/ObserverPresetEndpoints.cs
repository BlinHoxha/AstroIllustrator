using AstroIllustrator.Core;
using AstroIllustrator.Infrastructure;
using Microsoft.EntityFrameworkCore;

public static class ObserverPresetEndpoints
{
    public static void MapObserverPresets(this WebApplication app)
    {
        app.MapGet("/api/observer-presets", async (AstroDbContext db, CancellationToken token) => Results.Ok(
            await db.ObserverPresets.AsNoTracking().OrderBy(x => x.Name).ToListAsync(token)));
        app.MapGet("/api/observer-presets/{id:guid}", async (Guid id, AstroDbContext db, CancellationToken token) =>
            await db.ObserverPresets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token) is { } preset ? Results.Ok(preset) : Results.NotFound());
        app.MapPost("/api/observer-presets", async (ObserverPresetRequest request, AstroDbContext db, CancellationToken token) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var preset = new ObserverPreset { Id = Guid.NewGuid() };
            Apply(preset, request);
            db.ObserverPresets.Add(preset);
            return await Save(db, token) ? Results.Created($"/api/observer-presets/{preset.Id}", preset) : Results.Problem(statusCode: 409, title: "An observer preset with this name already exists.");
        });
        app.MapPut("/api/observer-presets/{id:guid}", async (Guid id, ObserverPresetRequest request, AstroDbContext db, CancellationToken token) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var preset = await db.ObserverPresets.FindAsync([id], token);
            if (preset is null) return Results.NotFound();
            Apply(preset, request);
            return await Save(db, token) ? Results.Ok(preset) : Results.Problem(statusCode: 409, title: "An observer preset with this name already exists.");
        });
        app.MapDelete("/api/observer-presets/{id:guid}", async (Guid id, AstroDbContext db, CancellationToken token) =>
        {
            var preset = await db.ObserverPresets.FindAsync([id], token);
            if (preset is null) return Results.NotFound();
            db.ObserverPresets.Remove(preset);
            await db.SaveChangesAsync(token);
            return Results.NoContent();
        });
    }
    private static Dictionary<string, string[]> Validate(ObserverPresetRequest request)
    {
        var errors = IllustrationValidator.ValidateObserver(request.Observer).GroupBy(x => x.Field).ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100) errors["name"] = ["Name must contain 1–100 characters."];
        return errors;
    }
    private static void Apply(ObserverPreset preset, ObserverPresetRequest request)
    {
        preset.Name = request.Name.Trim();
        preset.Latitude = request.Observer.Latitude;
        preset.Longitude = request.Observer.Longitude;
        preset.ElevationMeters = request.Observer.ElevationMeters;
        preset.TimeZoneId = request.Observer.TimeZoneId;
    }
    private static async Task<bool> Save(AstroDbContext db, CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); return true; }
        catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 2067 }) { return false; }
    }
}
