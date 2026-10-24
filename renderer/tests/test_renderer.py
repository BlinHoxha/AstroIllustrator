import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import math

from astro_renderer.__main__ import CATALOGS, project, render, validate

ROOT = Path(__file__).resolve().parents[2]

class RendererTests(unittest.TestCase):
    def setUp(self):
        self.request = json.loads((ROOT / "examples/orion.json").read_text())

    def test_projection_center_and_orientation(self):
        points = project([dict(raDeg=83.8, decDeg=-1.2), dict(raDeg=84.8, decDeg=-1.2), dict(raDeg=83.8, decDeg=-0.2)])
        self.assertAlmostEqual(points[0][0], 0, places=10)
        self.assertAlmostEqual(points[0][1], 0, places=10)
        self.assertLess(points[1][0], 0)  # east left
        self.assertGreater(points[2][1], 0)  # north up

    def test_known_catalog_identification(self):
        stars = {s["hip"]: s for s in json.loads((CATALOGS / "orion-hyg-v41.json").read_text())["stars"]}
        self.assertEqual(stars[27989]["name"], "Betelgeuse")
        self.assertAlmostEqual(stars[27989]["raDeg"], 88.79294, delta=0.001)
        self.assertAlmostEqual(stars[24436]["decDeg"], -8.20164, delta=0.001)

    def test_catalog_against_independent_simbad_j2000_reference(self):
        # SIMBAD ICRS J2000, van Leeuwen 2007; URLs and tolerance rationale in docs/scientific-validation.md.
        references = {27989: ((5 + 55/60 + 10.30536/3600)*15, 7 + 24/60 + 25.4304/3600),
                      24436: ((5 + 14/60 + 32.27210/3600)*15, -(8 + 12/60 + 5.8981/3600))}
        stars = {s["hip"]: s for s in json.loads((CATALOGS / "orion-hyg-v41.json").read_text())["stars"]}
        for hip, (ra, dec) in references.items():
            star = stars[hip]
            residual_arcsec = math.hypot((star["raDeg"]-ra)*math.cos(math.radians(dec)), star["decDeg"]-dec)*3600
            self.assertLess(residual_arcsec, 1.0)

    def test_tan_matches_independent_analytic_formula(self):
        ra, dec = math.radians(88.79294), math.radians(7.40706)
        ra0, dec0 = math.radians(83.8), math.radians(-1.2)
        denominator = math.sin(dec0)*math.sin(dec)+math.cos(dec0)*math.cos(dec)*math.cos(ra-ra0)
        x = -math.degrees(math.cos(dec)*math.sin(ra-ra0)/denominator)
        y = math.degrees((math.cos(dec0)*math.sin(dec)-math.sin(dec0)*math.cos(dec)*math.cos(ra-ra0))/denominator)
        actual = project([dict(raDeg=math.degrees(ra), decDeg=math.degrees(dec))])[0]
        self.assertAlmostEqual(actual[0], x, places=9)
        self.assertAlmostEqual(actual[1], y, places=9)

    def test_presets_preserve_geometry_and_outputs_are_repeatable(self):
        with tempfile.TemporaryDirectory() as tmp:
            a = render(self.request, Path(tmp) / "a")
            b = render(self.request, Path(tmp) / "b")
            self.assertEqual(a, b)
            request = copy.deepcopy(self.request)
            request["preset"] = "PrintMonochrome"
            c = render(request, Path(tmp) / "c")
            self.assertEqual(a["geometryHash"], c["geometryHash"])
            for item in a["outputs"]:
                content = (Path(tmp) / "a" / item["fileName"]).read_bytes()
                self.assertEqual(hashlib.sha256(content).hexdigest(), item["sha256"])

    def test_reject_unsupported_and_nonfinite_parameters(self):
        for value in [float("nan"), float("inf"), 7, True]:
            request = copy.deepcopy(self.request)
            request["skyChart"]["magnitudeLimit"] = value
            with self.assertRaises(ValueError): validate(request)
        self.request["skyChart"]["coordinates"] = [1, 2]
        with self.assertRaises(ValueError): validate(self.request)

if __name__ == "__main__":
    unittest.main()
