"""Strict scientific request validation; independently usable by the CLI."""
from datetime import datetime, timezone
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError, reset_tzpath
import math
from pathlib import Path
import tzdata

# Use the locked tzdata package on every OS rather than an unversioned system tzdb.
reset_tzpath([str(Path(tzdata.__file__).parent / "zoneinfo")])

CONSTELLATIONS = {"ORI", "UMI", "LYR", "CYG", "AQL"}
DIAGRAMS = {"StarAltitude", "PolarisLatitude", "AltitudeAzimuth", "AngularSeparation"}

class ScientificError(ValueError):
    pass

def require(condition, message):
    if not condition:
        raise ScientificError(message)

def finite(value, low, high):
    return not isinstance(value, bool) and isinstance(value, (int, float)) and math.isfinite(value) and low <= value <= high

def fields(value, allowed):
    require(isinstance(value, dict) and not set(value) - set(allowed.split()), "Unknown or invalid request fields")

def observer(value):
    fields(value, "latitude longitude elevationMeters timeZoneId")
    require(finite(value.get("latitude"), -90, 90) and finite(value.get("longitude"), -180, 180)
            and finite(value.get("elevationMeters", 0), -500, 10000), "Invalid WGS84 observer")
    if value.get("timeZoneId"):
        try:
            ZoneInfo(value["timeZoneId"])
        except (ZoneInfoNotFoundError, ValueError, TypeError) as error:
            raise ScientificError("Unknown IANA observer time zone") from error

def resolve_time(payload):
    utc, reference = payload.get("timeUtc"), payload.get("timeReference")
    require((utc is None) != (reference is None), "Supply exactly one timeUtc or timeReference")
    try:
        if utc is not None:
            result = datetime.fromisoformat(utc.replace("Z", "+00:00"))
            require(result.tzinfo is not None, "An explicit UTC offset is required")
        else:
            fields(reference, "type instantUtc localDateTime timeZoneId ambiguityPolicy")
            policy = reference.get("ambiguityPolicy", "Reject")
            if reference.get("type") == "AbsoluteUtc":
                require(reference.get("localDateTime") is None and reference.get("timeZoneId") is None and policy == "Reject", "Conflicting absolute-time fields")
                result = datetime.fromisoformat(reference["instantUtc"].replace("Z", "+00:00"))
                require(result.tzinfo is not None, "An explicit UTC offset is required")
            else:
                require(reference.get("type") == "LocalClock" and reference.get("instantUtc") is None and policy in {"Reject", "Earlier", "Later"}, "Invalid local-clock reference")
                local = datetime.fromisoformat(reference["localDateTime"])
                require(local.tzinfo is None, "LocalClock must omit UTC offset")
                zone = ZoneInfo(reference["timeZoneId"])
                candidates = sorted({local.replace(tzinfo=zone, fold=fold).astimezone(timezone.utc) for fold in [0, 1]
                    if local.replace(tzinfo=zone, fold=fold).astimezone(timezone.utc).astimezone(zone).replace(tzinfo=None) == local})
                require(len(candidates) > 0, "Local time falls in a DST gap")
                require(len(candidates) == 1 or policy != "Reject", "Ambiguous local time requires Earlier or Later")
                result = candidates[-1] if policy == "Later" else candidates[0]
        result = result.astimezone(timezone.utc)
        require(2000 <= result.year <= 2030, "Observer time must be in 2000–2030, subject to IERS coverage")
        return result
    except (KeyError, TypeError, ValueError, ZoneInfoNotFoundError) as error:
        raise ScientificError(str(error)) from error

def selected_codes(chart):
    return chart.get("constellationCodes") if chart.get("constellationCodes") is not None else [chart.get("constellationCode") or "ORI"]

def validate(request):
    fields(request, "schemaVersion illustrationType language preset skyChart scientificDiagram")
    require(request.get("schemaVersion") == "2.1", "Only schema 2.1 is supported")
    require(request.get("preset") in {"DarkAtlas", "PrintMonochrome"} and request.get("language") in {"en", "sq"}, "Unsupported preset or language")
    chart, diagram = request.get("skyChart"), request.get("scientificDiagram")
    if request.get("illustrationType") == "SkyChart":
        require(chart is not None and diagram is None, "Supply exactly one matching payload")
        fields(chart, "sceneType constellationCode constellationCodes asterismCode projection magnitudeLimit observer timeUtc timeReference azimuthCenterDegrees fieldOfViewDegrees")
        require(not (chart.get("constellationCode") is not None and chart.get("constellationCodes") is not None), "Conflicting constellation selectors")
        codes = selected_codes(chart)
        require(isinstance(codes, list) and 1 <= len(codes) <= 5 and all(isinstance(c, str) and c in CONSTELLATIONS for c in codes) and len(set(codes)) == len(codes), "Invalid constellation codes")
        scene = chart.get("sceneType")
        require(scene in {"SingleConstellation", "MultipleConstellations", "Asterism", "HorizonView"}, "Unsupported scene")
        require(scene != "SingleConstellation" or len(codes) == 1, "SingleConstellation needs one code")
        require(scene != "MultipleConstellations" or len(codes) >= 2, "MultipleConstellations needs at least two codes")
        require(chart.get("asterismCode") in {None, "SummerTriangle"}, "Unsupported asterism")
        require(scene != "Asterism" or (chart.get("asterismCode") == "SummerTriangle" and chart.get("constellationCode") is None and chart.get("constellationCodes") is None), "Asterism selects its own constellations")
        require(finite(chart.get("magnitudeLimit"), 2, 6.5), "Magnitude limit must be between 2 and 6.5")
        if scene == "HorizonView":
            require(chart.get("projection") == "AltitudeAzimuth", "HorizonView requires AltitudeAzimuth")
            observer(chart.get("observer"))
            resolve_time(chart)
            require(finite(chart.get("azimuthCenterDegrees", 180), 0, 359.999999999) and finite(chart.get("fieldOfViewDegrees", 100), 10, 180), "Invalid horizon framing")
        else:
            require(chart.get("projection") == "Gnomonic", "Static charts require Gnomonic")
            require(all(chart.get(k) is None for k in ["observer", "timeUtc", "timeReference"]), "Static charts cannot accept observer/time")
    elif request.get("illustrationType") == "ScientificDiagram":
        require(diagram is not None and chart is None, "Supply exactly one matching payload")
        fields(diagram, "diagramType mode observer timeUtc timeReference parameters")
        kind, mode = diagram.get("diagramType"), diagram.get("mode")
        require(kind in DIAGRAMS and mode in {"Schematic", "Computed"}, "Unsupported diagram/mode")
        p = diagram.get("parameters", {})
        fields(p, "altitudeDegrees azimuthDegrees starId starHipIds")
        if diagram.get("observer") is not None:
            observer(diagram["observer"])
        if kind == "AngularSeparation":
            ids = p.get("starHipIds")
            require(mode == "Computed" and isinstance(ids, list) and len(ids) == 2 and all(type(i) is int and i > 0 for i in ids) and ids[0] != ids[1], "Separation requires two distinct HIP IDs")
            require(all(diagram.get(k) is None for k in ["observer", "timeUtc", "timeReference"]) and all(p.get(k) is None for k in ["altitudeDegrees", "azimuthDegrees", "starId"]), "Separation accepts static HIP IDs only")
        elif mode == "Computed":
            require(kind != "AltitudeAzimuth" and p.get("starId") == "Polaris" and all(p.get(k) is None for k in ["altitudeDegrees", "azimuthDegrees", "starHipIds"]), "Computed altitude accepts Polaris without supplied geometry")
            observer(diagram.get("observer"))
            resolve_time(diagram)
        else:
            require(p.get("starId") is None and p.get("starHipIds") is None and diagram.get("timeUtc") is None and diagram.get("timeReference") is None, "Schematic diagrams cannot accept star/time inputs")
            if kind == "PolarisLatitude":
                observer(diagram.get("observer"))
                require(p.get("altitudeDegrees") is None and p.get("azimuthDegrees") is None, "NCP teaching angle comes from latitude")
            else:
                require(diagram.get("observer") is None and finite(p.get("altitudeDegrees"), -90, 90), "Numeric schematic requires altitude without observer")
                if kind == "AltitudeAzimuth":
                    require(finite(p.get("azimuthDegrees"), 0, 359.999999999), "Azimuth must be in [0,360)")
                else:
                    require(p.get("azimuthDegrees") is None, "This template does not use azimuth")
    else:
        raise ScientificError("Unsupported illustration type")
