"""Approved figure wording and display-only formatting."""
from datetime import datetime, timezone
import json
from pathlib import Path
from zoneinfo import ZoneInfo

from . import __version__

TERMINOLOGY = Path(__file__).resolve().parents[1] / "catalogs" / "terminology-v1.json"

# English values are the owner's exact approved fallback strings.
EN = dict(horizon="Horizon", altitude="Altitude", azimuth="Azimuth", zenith="Zenith",
    reference="Reference", separation="Angular separation", schematic="Schematic numeric geometry",
    computed="Polaris — computed", teaching="NCP latitude teaching diagram",
    static="J2000 | North up, east left | Gnomonic projection (TAN)",
    summer="Summer Triangle", geometric="Geometric horizon | No refraction",
    noRefraction="No refraction", ncp="Ideal NCP altitude = observer latitude. Not exact Polaris altitude.",
    zenithDistance="Zenith distance = 90° − altitude", clockwise="Clockwise from north",
    ray="Ray 1", separationNote="Angular diagram; rays illustrate measured separation, not a sky projection.",
    cardinals=["N", "E", "S", "W"], localTime="Local time",
    utcExpanded="Coordinated Universal Time (UTC)", angularScale="Angular scale",
    apparentMagnitude="Apparent magnitude", limitingMagnitude="Limiting magnitude",
    dataSource="Data source:", method="Method:", epochJ2000="Epoch J2000",
    earthOrientationData="Earth orientation data (IERS)",
    iersPredictions="Includes predicted IERS values", northCompass="N", eastCompass="E")

MONTHS = {
    "en": ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"],
    "sq": ["janar", "shkurt", "mars", "prill", "maj", "qershor", "korrik", "gusht", "shtator", "tetor", "nëntor", "dhjetor"],
}

def catalogue():
    """Read editorial status at draw time; never cache an approval across renders."""
    return json.loads(TERMINOLOGY.read_text(encoding="utf-8"))

def approved(entry):
    return entry.get("status") == "approved" and entry.get("displayName") is not None

def labels(request, terms=None):
    if request["language"] == "en":
        return EN
    entries = (terms or catalogue())["figureTerms"]
    result = {key: entries[key]["displayName"] if key in entries and approved(entries[key]) else value
              for key, value in EN.items() if key != "cardinals"}
    result["cardinals"] = [entries[f"cardinals[{index}]"]["displayName"]
                           if approved(entries[f"cardinals[{index}]"]) else EN["cardinals"][index]
                           for index in range(4)]
    return result

def entity_name(entry, language):
    return entry["displayName"] if language == "sq" and approved(entry) else entry["internationalName"]

def decimal(value, language, places=1):
    result = f"{value:.{places}f}"
    return result.replace(".", ",") if language == "sq" else result

def angle(value, language):
    return decimal(value, language) + "°"

def coordinate_note(observer, language, text):
    latitude, longitude = observer["latitude"], observer["longitude"]
    north, east = text["cardinals"][0], text["cardinals"][1]
    south, west = text["cardinals"][2], text["cardinals"][3]
    elevation = f"{observer.get('elevationMeters', 0):g}"
    if language == "sq":
        elevation = elevation.replace(".", ",")
    return (f"{angle(abs(latitude), language)} {north if latitude >= 0 else south}, "
            f"{angle(abs(longitude), language)} {east if longitude >= 0 else west}, "
            f"{elevation} m")

def time_note(science, payload, language):
    instant = datetime.fromisoformat(science["observationUtc"].replace("Z", "+00:00"))
    reference = payload.get("timeReference") or {}
    zone_id = reference.get("timeZoneId") if reference.get("type") == "LocalClock" else payload["observer"].get("timeZoneId")
    if zone_id:
        local = instant.astimezone(ZoneInfo(zone_id))
        offset = local.utcoffset()
        total_minutes = int(offset.total_seconds() // 60)
        sign = "+" if total_minutes >= 0 else "-"
        minutes = abs(total_minutes)
        offset_text = f"UTC{sign}{minutes // 60}" + (f":{minutes % 60:02d}" if minutes % 60 else "")
        abbreviation = local.tzname()
        zone_text = f"{abbreviation}, {offset_text}" if abbreviation and abbreviation.isalpha() else offset_text
        date = f"{local.day} {MONTHS[language][local.month-1]} {local.year}"
        clock = local.strftime("%H:%M")
        utc_clock = instant.astimezone(timezone.utc).strftime("%H:%M")
        hour_word = "ora " if language == "sq" else ""
        return f"{date}, {hour_word}{clock} ({zone_text}) = {utc_clock} UTC"
    utc = instant.astimezone(timezone.utc)
    date = f"{utc.day} {MONTHS[language][utc.month-1]} {utc.year}"
    return f"{date}, {'ora ' if language == 'sq' else ''}{utc:%H:%M} UTC"

def attribution(science, text, projection, *, uses_catalogue=True, j2000_positions=True,
                include_method=True):
    method = f"{text['method']} {projection}"
    if j2000_positions:
        method += f"; {text['epochJ2000']}"
    if "iersVersion" in science:
        method += f"; {text['earthOrientationData']} {science['iersVersion']}"
    lines = [f"{text['dataSource']} HYG v4.1"] if uses_catalogue else []
    if include_method:
        lines.append(method)
    elif j2000_positions:
        lines.append(text["epochJ2000"])
    if science.get("earthOrientationPredicted"):
        lines.append(text["iersPredictions"])
    lines.append(f"AstroIllustrator {__version__}")
    return lines
