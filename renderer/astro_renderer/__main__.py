"""Versioned JSON process entry point. All astronomy stays in Python."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import astropy
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

from . import __version__
from .contracts import validate, ScientificError
from .charts import project, chart_scene, draw_chart
from .diagrams import diagram_scene, draw_diagram

CATALOGS = Path(__file__).resolve().parents[1] / "catalogs"

def digest(value):
    return hashlib.sha256(value).hexdigest()

def render(request, output):
    validate(request)
    raw = (CATALOGS / "bright-hyg-v41.json").read_bytes()
    lines_raw = (CATALOGS / "scene-definitions-v1.json").read_bytes()
    catalog, definitions = json.loads(raw), json.loads(lines_raw)
    is_chart = request["illustrationType"] == "SkyChart"
    geometry, science = chart_scene(request, catalog, definitions) if is_chart else diagram_scene(request, catalog)
    geometry_hash = digest(json.dumps(geometry, sort_keys=True, separators=(",", ":"), allow_nan=False).encode())
    mono = request["preset"] == "PrintMonochrome"
    palette = dict(background="white" if mono else "#071426", foreground="black" if mono else "white", line="#555555" if mono else "#79aabb")
    plt.rcParams.update({"svg.hashsalt": geometry_hash, "svg.fonttype": "none", "font.family": "DejaVu Sans"})
    fig = draw_chart(geometry, science, request, palette) if is_chart else draw_diagram(geometry, science, request, palette)
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    try:
        fig.savefig(output / "chart.svg", metadata={"Date": None, "Creator": "AstroIllustrator " + __version__})
        fig.savefig(output / "chart.png", dpi=200, metadata={"Software": "AstroIllustrator " + __version__})
    finally:
        plt.close(fig)
    outputs = [dict(format=fmt, fileName=f"chart.{fmt}", sha256=digest((output / f"chart.{fmt}").read_bytes())) for fmt in ["svg", "png"]]
    manifest = dict(schemaVersion="2.1", rendererVersion=__version__, request=request, geometryHash=geometry_hash,
        geometry=geometry, scientificResults=science, catalog={k:v for k,v in catalog.items() if k != "stars"},
        catalogSha256=digest(raw), lineSet=definitions, lineSetSha256=digest(lines_raw),
        scientificAssumptions=["Catalog interpreted as ICRS J2000; static charts are not current observed skies.",
            "Observer transforms use angular proper motion; no stellar parallax or perspective acceleration.",
            "Geometric horizon, no terrain, no refraction; no guaranteed naked-eye visibility.",
            "Original line conventions are not IAU boundaries. Star glyphs are not to scale."],
        libraries=dict(astropy=astropy.__version__, numpy=np.__version__, matplotlib=matplotlib.__version__), outputs=outputs)
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    outputs = outputs + [dict(format="manifest", fileName="manifest.json", sha256=digest((output / "manifest.json").read_bytes()))]
    return dict(schemaVersion="2.1", rendererVersion=__version__, geometryHash=geometry_hash, outputs=outputs)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True)
    args = parser.parse_args()
    try:
        raw = sys.stdin.buffer.read(64_001)
        if len(raw) > 64_000:
            raise ScientificError("Request exceeds 64 KB")
        result = render(json.loads(raw), args.output_dir)
        print(json.dumps(result))
    except ScientificError as error:
        print(json.dumps(dict(error=type(error).__name__, message=str(error))), file=sys.stderr)
        sys.exit(2)
    except Exception as error:
        print(json.dumps(dict(error=type(error).__name__, message=str(error))), file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
