"""Read-only runtime diagnostics, using the same offline table as observer transforms."""
import hashlib
import json
from datetime import datetime, timezone
from importlib.metadata import version
from pathlib import Path

import astropy_iers_data
import matplotlib  # Verify the rendering package without initializing pyplot/font caches.
from astropy.time import Time
from astropy.utils import iers

from . import __version__
from .contracts import CONSTELLATIONS
from . import astronomy  # Apply the exact runtime IERS configuration; never download data.


def capabilities():
    catalogs = Path(__file__).resolve().parents[1] / "catalogs"
    stars = json.loads((catalogs / "bright-hyg-v41.json").read_bytes())
    definitions = json.loads((catalogs / "scene-definitions-v1.json").read_bytes())
    if not stars["stars"] or set(c["code"] for c in definitions["constellations"]) != CONSTELLATIONS:
        raise ValueError("Catalog definitions are incomplete")
    table = iers.earth_orientation_table.get()
    # Conservative half-open interval: no promise of support at/beyond the final row.
    first = max(datetime(2000, 1, 1, tzinfo=timezone.utc),
                Time(float(table["MJD"][0].value), format="mjd", scale="utc").to_datetime(timezone.utc))
    last = min(datetime(2031, 1, 1, tzinfo=timezone.utc),
               Time(float(table["MJD"][-1].value), format="mjd", scale="utc").to_datetime(timezone.utc))
    if first >= last:
        raise ValueError("No supported Earth-orientation interval")
    return dict(schemaVersion="2.1", rendererVersion=__version__,
        sceneTypes=["SingleConstellation", "MultipleConstellations", "Asterism", "HorizonView"],
        constellationCodes=[c["code"] for c in definitions["constellations"]],
        asterismCodes=[c["code"] for c in definitions["asterisms"]],
        diagramTemplates=[dict(code="StarAltitude", modes=["Schematic", "Computed"]),
            dict(code="PolarisLatitude", modes=["Schematic", "Computed"]),
            dict(code="AltitudeAzimuth", modes=["Schematic"]),
            dict(code="AngularSeparation", modes=["Computed"])],
        languages=["en", "sq"], presets=["DarkAtlas", "PrintMonochrome"], outputFormats=["svg", "png", "manifest"],
        observationCoverage=dict(nominalStartYear=2000, nominalEndYear=2030,
            startUtcInclusive=first.isoformat(), endUtcExclusive=last.isoformat(),
            iersVersion=astropy_iers_data.__version__,
            iersSha256=hashlib.sha256(Path(astropy_iers_data.IERS_A_FILE).read_bytes()).hexdigest(),
            timeZoneDataVersion=version("tzdata"), predictionsIncluded=bool(any(table["UT1Flag"] == "P")),
            note="Conservative bundled UT1 interval, intersected with nominal validation bounds. Predictions may be used; the renderer still validates each request. Civil-time resolution alone does not establish scientific support."))


if __name__ == "__main__":
    print(json.dumps(capabilities(), allow_nan=False))
