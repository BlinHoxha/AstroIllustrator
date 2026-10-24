"""Deterministic scientific renderer; offline, with no AI geometry."""
import os
from pathlib import Path

# Keep scientific-library configuration/cache local for packaged and sandboxed runs.
_local = Path(__file__).resolve().parents[2] / ".tools"
for _variable, _directory in [("XDG_CACHE_HOME", "cache"), ("XDG_CONFIG_HOME", "config")]:
    _path = Path(os.environ.setdefault(_variable, str(_local / _directory))) / "astropy"
    _path.mkdir(parents=True, exist_ok=True)
os.environ.setdefault("MPLCONFIGDIR", str(_local / "matplotlib"))
__version__ = "0.2.0"
