namespace AstroIllustrator.Core;

public static class IllustrationValidator
{
    public static readonly IReadOnlyList<string> ConstellationCodes = ["ORI", "UMI", "LYR", "CYG", "AQL"];
    public static readonly IReadOnlyList<string> DiagramTypes = ["StarAltitude", "PolarisLatitude", "AltitudeAzimuth", "AngularSeparation"];

    public static IReadOnlyList<ValidationIssue> Validate(IllustrationRequest request)
    {
        List<ValidationIssue> issues = [];
        void Error(string field, string message) => issues.Add(new(field, message));
        if (request.SchemaVersion != "2.1") Error("schemaVersion", "Only schema 2.1 is supported.");
        if (request.Language is not ("en" or "sq")) Error("language", "Choose en or sq.");
        if (request.Preset is not ("DarkAtlas" or "PrintMonochrome")) Error("preset", "Unknown preset.");
        if (request.IllustrationType == "SkyChart" && request.SkyChart is { } chart && request.ScientificDiagram is null)
        {
            string[] codes = chart.ConstellationCodes?.ToArray() ?? [chart.ConstellationCode ?? "ORI"];
            if (chart.ConstellationCode is not null && chart.ConstellationCodes is not null)
                Error("skyChart", "Use either constellationCode or constellationCodes.");
            if (chart.SceneType is not ("SingleConstellation" or "MultipleConstellations" or "Asterism" or "HorizonView"))
                Error("skyChart.sceneType", "Unsupported sky scene.");
            if (codes.Length is < 1 or > 5 || codes.Any(c => !ConstellationCodes.Contains(c)) || codes.Distinct().Count() != codes.Length)
                Error("skyChart.constellationCodes", "Choose one to five distinct supported constellation codes.");
            if (chart.SceneType == "SingleConstellation" && codes.Length != 1)
                Error("skyChart.constellationCodes", "SingleConstellation requires one code.");
            if (chart.SceneType == "MultipleConstellations" && codes.Length < 2)
                Error("skyChart.constellationCodes", "MultipleConstellations requires at least two codes.");
            if (chart.AsterismCode is not null && chart.AsterismCode != "SummerTriangle")
                Error("skyChart.asterismCode", "Only SummerTriangle is supported.");
            if (chart.SceneType == "Asterism" && chart.AsterismCode is null)
                Error("skyChart.asterismCode", "Select an asterism.");
            if (chart.SceneType == "Asterism" && (chart.ConstellationCode is not null || chart.ConstellationCodes is not null))
                Error("skyChart", "Asterism selects its catalog stars automatically; omit constellation selectors.");
            if (!FiniteRange(chart.MagnitudeLimit, 2, 6.5)) Error("skyChart.magnitudeLimit", "Magnitude limit must be between 2 and 6.5.");
            if (chart.SceneType == "HorizonView")
            {
                if (chart.Projection != "AltitudeAzimuth") Error("skyChart.projection", "HorizonView requires AltitudeAzimuth.");
                issues.AddRange(ValidateObserver(chart.Observer));
                ValidateTime(chart.TimeUtc, chart.TimeReference, issues);
                if (!FiniteRange(chart.AzimuthCenterDegrees, 0, 360) || chart.AzimuthCenterDegrees == 360)
                    Error("skyChart.azimuthCenterDegrees", "Azimuth must be in [0,360).");
                if (!FiniteRange(chart.FieldOfViewDegrees, 10, 180)) Error("skyChart.fieldOfViewDegrees", "Field of view must be 10–180 degrees.");
            }
            else
            {
                if (chart.Projection != "Gnomonic") Error("skyChart.projection", "Static charts require Gnomonic.");
                if (chart.Observer is not null || chart.TimeUtc is not null || chart.TimeReference is not null)
                    Error("skyChart", "Static J2000 charts cannot accept observer/time inputs; choose HorizonView.");
            }
        }
        else if (request.IllustrationType == "ScientificDiagram" && request.ScientificDiagram is { } diagram && request.SkyChart is null)
        {
            if (!DiagramTypes.Contains(diagram.DiagramType)) Error("scientificDiagram.diagramType", "Unsupported diagram template.");
            if (diagram.Mode is not ("Schematic" or "Computed")) Error("scientificDiagram.mode", "Choose Schematic or Computed.");
            if (diagram.Parameters is not { } p) { Error("scientificDiagram.parameters", "Parameters must be an object."); return issues; }
            if (diagram.Observer is not null) issues.AddRange(ValidateObserver(diagram.Observer));
            if (diagram.DiagramType == "AngularSeparation")
            {
                if (diagram.Mode != "Computed" || p.StarHipIds is not { Count: 2 } ids || ids[0] <= 0 || ids[1] <= 0 || ids[0] == ids[1])
                    Error("scientificDiagram.parameters.starHipIds", "Computed angular separation requires two distinct catalog HIP identifiers.");
                if (diagram.Observer is not null || diagram.TimeUtc is not null || diagram.TimeReference is not null || p.StarId is not null || p.AltitudeDegrees is not null || p.AzimuthDegrees is not null)
                    Error("scientificDiagram", "AngularSeparation uses static catalog IDs only.");
            }
            else if (diagram.Mode == "Computed")
            {
                if (diagram.DiagramType == "AltitudeAzimuth") Error("scientificDiagram.mode", "AltitudeAzimuth currently supports Schematic mode only.");
                if (p.StarId != "Polaris" || p.AltitudeDegrees is not null || p.AzimuthDegrees is not null || p.StarHipIds is not null)
                    Error("scientificDiagram.parameters", "Computed altitude requires starId=Polaris, without supplied angles or HIP pairs.");
                if (diagram.Observer is null) issues.AddRange(ValidateObserver(null));
                ValidateTime(diagram.TimeUtc, diagram.TimeReference, issues);
            }
            else
            {
                if (p.StarId is not null || p.StarHipIds is not null || diagram.TimeUtc is not null || diagram.TimeReference is not null)
                    Error("scientificDiagram", "Schematic mode cannot accept computed-star or time inputs.");
                if (diagram.DiagramType == "PolarisLatitude")
                {
                    if (diagram.Observer is null) issues.AddRange(ValidateObserver(null));
                    if (p.AltitudeDegrees is not null || p.AzimuthDegrees is not null) Error("scientificDiagram.parameters", "The NCP teaching angle comes from latitude.");
                }
                else
                {
                    if (diagram.Observer is not null) Error("scientificDiagram.observer", "Numeric schematic diagrams do not use an observer.");
                    if (p.AltitudeDegrees is not { } altitude || !FiniteRange(altitude, -90, 90))
                        Error("scientificDiagram.parameters.altitudeDegrees", "Provide altitude in [-90,90].");
                    if (diagram.DiagramType == "AltitudeAzimuth")
                    {
                        if (p.AzimuthDegrees is not { } azimuth || !FiniteRange(azimuth, 0, 360) || azimuth == 360)
                            Error("scientificDiagram.parameters.azimuthDegrees", "Provide azimuth in [0,360).");
                    }
                    else if (p.AzimuthDegrees is not null) Error("scientificDiagram.parameters.azimuthDegrees", "This template does not use azimuth.");
                }
            }
        }
        else Error("illustrationType", "Supply exactly one matching SkyChart or ScientificDiagram payload.");
        return issues;
    }

    public static bool FiniteRange(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
    public static IReadOnlyList<ValidationIssue> ValidateObserver(ObserverLocation? observer)
    {
        List<ValidationIssue> issues = [];
        if (observer is null) { issues.Add(new("observer", "Observer coordinates are required.")); return issues; }
        if (!FiniteRange(observer.Latitude, -90, 90))
            issues.Add(new("observer.latitude", "Latitude must be a finite number between -90 and 90 degrees."));
        if (!FiniteRange(observer.Longitude, -180, 180))
            issues.Add(new("observer.longitude", "Longitude must be a finite number between -180 and 180 degrees."));
        if (!FiniteRange(observer.ElevationMeters, -500, 10000))
            issues.Add(new("observer.elevationMeters", "Elevation must be a finite number between -500 and 10000 metres."));
        if (observer.TimeZoneId is not null)
        {
            try
            {
                if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(observer.TimeZoneId, out _)) throw new ArgumentException();
                TimeZoneInfo.FindSystemTimeZoneById(observer.TimeZoneId);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
            { issues.Add(new("observer.timeZoneId", "Unknown time zone.")); }
        }
        return issues;
    }
    private static void ValidateTime(DateTimeOffset? utc, TimeReference? reference, List<ValidationIssue> issues)
    {
        try { ObserverTime.Resolve(utc, reference); }
        catch (ArgumentException e) { issues.Add(new("timeReference", e.Message)); }
    }
}

public static class ObserverTime
{
    public static DateTimeOffset Resolve(DateTimeOffset? utc, TimeReference? reference)
    {
        if ((utc is null) == (reference is null)) throw new ArgumentException("Supply exactly one timeUtc or timeReference.");
        DateTimeOffset result;
        if (utc is { } instant) result = instant.ToUniversalTime();
        else if (reference!.Type == "AbsoluteUtc" && reference.InstantUtc is { } absolute && reference.LocalDateTime is null && reference.TimeZoneId is null && reference.AmbiguityPolicy == "Reject")
            result = absolute.ToUniversalTime();
        else if (reference.Type == "LocalClock" && reference.LocalDateTime is { Kind: DateTimeKind.Unspecified } local && reference.InstantUtc is null && !string.IsNullOrWhiteSpace(reference.TimeZoneId))
        {
            TimeZoneInfo zone;
            try
            {
                if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(reference.TimeZoneId, out _)) throw new ArgumentException();
                zone = TimeZoneInfo.FindSystemTimeZoneById(reference.TimeZoneId);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
            { throw new ArgumentException("Unknown local-clock time zone."); }
            if (reference.AmbiguityPolicy is not ("Reject" or "Earlier" or "Later")) throw new ArgumentException("Unknown ambiguity policy.");
            if (zone.IsInvalidTime(local)) throw new ArgumentException("Local time falls in a daylight-saving gap.");
            if (zone.IsAmbiguousTime(local))
            {
                if (reference.AmbiguityPolicy == "Reject") throw new ArgumentException("Local time is ambiguous; select Earlier or Later.");
                var candidates = zone.GetAmbiguousTimeOffsets(local).Select(offset => new DateTimeOffset(local, offset).ToUniversalTime()).Order().ToArray();
                result = reference.AmbiguityPolicy == "Earlier" ? candidates[0] : candidates[^1];
            }
            else result = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
        }
        else throw new ArgumentException("Unsupported or conflicting time reference fields.");
        if (result.Year is < 2000 or > 2030) throw new ArgumentException("Observer computations are restricted to 2000–2030, subject to bundled IERS coverage.");
        return result;
    }
}
