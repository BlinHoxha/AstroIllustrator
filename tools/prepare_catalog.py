"""Fetch a pinned HYG snapshot, preserving provenance and source license."""
import csv
import hashlib
import io
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
COMMIT = "3bf37f4b2d5460e1278286320d1d62fab9b493c1"
BASE = f"https://raw.githubusercontent.com/astronexus/HYG-Database/{COMMIT}"

def main():
    target = ROOT / "renderer" / "catalogs"
    target.mkdir(parents=True, exist_ok=True)
    source = BASE + "/hyg/CURRENT/hygdata_v41.csv"
    cache = ROOT / ".tools" / "hygdata_v41.csv"
    if cache.exists():
        raw = cache.read_bytes()
    else:
        with urllib.request.urlopen(source, timeout=120) as response:
            raw = response.read()
        cache.parent.mkdir(parents=True, exist_ok=True)
        cache.write_bytes(raw)
    if hashlib.sha256(raw).hexdigest() != "d9f69fd86bbf90a4e4d52b4c5c53eacfa6dfc0bfdef85bfd94f095e0bebe4ebd":
        raise ValueError("Pinned HYG source checksum mismatch")
    rows = []
    for row in csv.DictReader(io.StringIO(raw.decode("utf-8-sig"))):
        if row["con"] == "Ori" and row["hip"] and float(row["mag"]) <= 6.5:
            rows.append(dict(hip=int(row["hip"]), name=row["proper"] or row["bayer"] or f'HIP {row["hip"]}',
                             raDeg=float(row["ra"]) * 15, decDeg=float(row["dec"]), magnitude=float(row["mag"])))
    required = {27989, 24436, 25336, 27366, 26727, 26311, 25930}
    if not required.issubset({r["hip"] for r in rows}):
        raise ValueError("Catalog lacks required Orion identities")
    catalog = dict(name="HYG v4.1 Orion subset", source=source, upstreamCommit=COMMIT,
                   upstreamSha256=hashlib.sha256(raw).hexdigest(), license="CC-BY-SA-4.0",
                   attribution="David Nash / Astronexus and contributors", epoch="J2000.0", frame="J2000 equatorial",
                   selection="con=Ori, HIP identifier present, magnitude <=6.5; RA hours converted to degrees",
                   stars=sorted(rows, key=lambda r: r["hip"]))
    (target / "orion-hyg-v41.json").write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")
    bright = []
    for row in csv.DictReader(io.StringIO(raw.decode("utf-8-sig"))):
        if row["hip"] and float(row["mag"]) <= 6.5:
            bright.append(dict(hip=int(row["hip"]), name=row["proper"] or row["bf"].strip() or f'HIP {row["hip"]}',
                constellation=row["con"].upper(), raDeg=float(row["ra"])*15, decDeg=float(row["dec"]),
                magnitude=float(row["mag"]), pmRaCosDecMasYr=float(row["pmra"] or 0), pmDecMasYr=float(row["pmdec"] or 0),
                distancePc=float(row["dist"]), radialVelocityKmS=float(row["rv"] or 0)))
    expanded = {k:v for k,v in catalog.items() if k != "stars"}
    expanded.update(name="HYG v4.1 bright HIP subset", selection="All sky, magnitude <=6.5, HIP identifier present; RA converted to degrees; retain catalog proper motions, distances and radial velocities", stars=sorted(bright,key=lambda r:r["hip"]))
    if len({r["hip"] for r in bright}) != len(bright):
        raise ValueError("Duplicate HIP identifiers in snapshot")
    (target / "bright-hyg-v41.json").write_text(json.dumps(expanded, indent=2) + "\n", encoding="utf-8")
    for remote, local in [("LICENSE", "HYG-LICENSE.txt"), ("hyg/README.md", "HYG-README.md")]:
        if not (target / local).exists():
            with urllib.request.urlopen(BASE + "/" + remote, timeout=60) as response:
                (target / local).write_bytes(response.read())
    print(f"Prepared {len(rows)} Orion stars and {len(bright)} all-sky stars; source SHA256 {catalog['upstreamSha256']}")

if __name__ == "__main__":
    main()
