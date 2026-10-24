"""Observer transforms with pinned, offline Earth-orientation data."""
import hashlib
from pathlib import Path
import warnings
from importlib.metadata import version

import astropy.units as u
from astropy.coordinates import SkyCoord, EarthLocation, AltAz
from astropy.time import Time
from astropy.utils import iers
import astropy_iers_data
from erfa import ErfaWarning
import numpy as np

from .contracts import ScientificError, resolve_time

iers.conf.auto_download = False
iers.conf.auto_max_age = None

def observer_positions(stars, payload):
    instant = resolve_time(payload)
    with warnings.catch_warnings(record=True) as time_warnings:
        time = Time(instant)
        _, status = time.get_delta_ut1_utc(return_status=True)
    if np.any(np.asarray(status) < 0):
        raise ScientificError("Requested time is outside the bundled IERS Earth-orientation coverage")
    if time_warnings:
        raise ScientificError("Requested time requires unsupported time-scale or Earth-orientation assumptions")
    location = payload["observer"]
    earth = EarthLocation.from_geodetic(location["longitude"]*u.deg, location["latitude"]*u.deg,
                                      location.get("elevationMeters", 0)*u.m, ellipsoid="WGS84")
    source = SkyCoord(ra=[s["raDeg"] for s in stars]*u.deg, dec=[s["decDeg"] for s in stars]*u.deg,
        pm_ra_cosdec=[s["pmRaCosDecMasYr"] for s in stars]*u.mas/u.yr,
        pm_dec=[s["pmDecMasYr"] for s in stars]*u.mas/u.yr, obstime=Time("J2000"), frame="icrs")
    # Angular propagation without distance does not model perspective acceleration/parallax.
    with warnings.catch_warnings():
        warnings.filterwarnings("ignore", category=ErfaWarning, message=".*distance overridden.*")
        evolved = source.apply_space_motion(new_obstime=time)
    apparent = SkyCoord(ra=evolved.ra, dec=evolved.dec, frame="icrs").transform_to(AltAz(obstime=time, location=earth, pressure=0*u.hPa))
    provenance = dict(observationUtc=instant.isoformat(), observer=location, refractionMode="None (pressure=0 hPa)",
        horizon="Geometric zero-altitude horizon; no terrain, dip or obstructions", properMotion="Angular propagation from J2000; stellar parallax and perspective acceleration omitted",
        azimuthConvention="True north=0, east=90, south=180, west=270; clockwise", iersVersion=astropy_iers_data.__version__,
        iersSha256=hashlib.sha256(Path(astropy_iers_data.IERS_A_FILE).read_bytes()).hexdigest(),
        iersStatus=int(status), earthOrientationPredicted=bool(status == iers.FROM_IERS_A_PREDICTION), timeScales="UTC input; Astropy UTC/TAI/TT/UT1 transformations")
    reference = payload.get("timeReference")
    if reference is not None and reference.get("type") == "LocalClock":
        provenance["timeZoneDataVersion"] = version("tzdata")
        provenance["timeZoneId"] = payload["timeReference"]["timeZoneId"]
    return apparent.alt.deg, apparent.az.deg, provenance
