"""Presentation checks that keep scientific geometry and request contracts untouched."""
import copy
from io import BytesIO
import json
import math
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

from astro_renderer.__main__ import CATALOGS, render
from astro_renderer.charts import (AUTOMATIC_LABEL_MAGNITUDE, REQUIRED_LABEL_HIPS,
                                   chart_scene, draw_chart, glyph_size)
from astro_renderer.diagrams import diagram_scene, draw_diagram
from astro_renderer.localization import (EN, angle, attribution, catalogue, coordinate_note,
                                         entity_name, labels, time_note)
import matplotlib.pyplot as plt
from matplotlib.text import Annotation, Text
from matplotlib.transforms import Bbox

ROOT = Path(__file__).resolve().parents[2]
PALETTE = dict(background="white", foreground="black", line="#555555")

def distance_to_box(x, y, box):
    return math.hypot(x - min(max(x, box.x0), box.x1),
                      y - min(max(y, box.y0), box.y1))

def example(name, language="en"):
    request = json.loads((ROOT / "examples" / f"{name}.json").read_text(encoding="utf-8"))
    request["language"] = language
    return request

class AtlasPresentationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.stars = json.loads((CATALOGS / "bright-hyg-v41.json").read_text(encoding="utf-8"))
        cls.definitions = json.loads((CATALOGS / "scene-definitions-v1.json").read_text(encoding="utf-8"))

    def assert_identifiable_or_connected(self, label, point, bright_points, ax, renderer):
        label.get_window_extent(renderer)
        box = Text.get_window_extent(label, renderer)
        star_x, star_y = ax.transData.transform((point["x"], point["y"]))
        own_distance = distance_to_box(star_x, star_y, box)
        ambiguous = any(distance_to_box(*ax.transData.transform((other["x"], other["y"])), box)
                        <= own_distance for other in bright_points if other["hip"] != point["hip"])
        if ambiguous:
            leader = label.arrow_patch or getattr(label, "leader_patch", None)
            self.assertIsNotNone(leader, label.get_text())
            self.assertTrue(leader.get_visible(), label.get_text())
            self.assertGreater(leader.get_linewidth(), 0)
            vertices = leader.get_transform().transform(leader.get_path().vertices)
            self.assertLessEqual(min(math.hypot(vertex[0] - star_x, vertex[1] - star_y)
                                     for vertex in vertices), 1, label.get_text())
            self.assertGreater(math.hypot(*(vertices[0] - vertices[-1])), 1, label.get_text())

    def test_orion_belt_labels_identify_their_own_stars(self):
        for language in ("en", "sq"):
            request = example("orion", language)
            geometry, science = chart_scene(request, self.stars, self.definitions)
            fig = draw_chart(geometry, science, request, PALETTE)
            try:
                fig.canvas.draw()
                ax = fig.axes[0]
                renderer = fig.canvas.get_renderer()
                by_name = {p["name"]: p for p in geometry["points"]}
                bright = [p for p in geometry["points"] if p["magnitude"] <= 4]
                annotations = {item.get_text(): item for item in ax.texts if isinstance(item, Annotation)}
                for name in ("Mintaka", "Alnilam", "Alnitak"):
                    with self.subTest(language=language, star=name):
                        self.assertIn(name, annotations)
                        self.assert_identifiable_or_connected(annotations[name], by_name[name],
                                                              bright, ax, renderer)
            finally:
                plt.close(fig)

    def test_approval_gates_fall_back_to_english_and_international(self):
        terms = catalogue()
        self.assertEqual(entity_name(next(x for x in terms["constellations"] if x["appCode"] == "ORI"), "sq"), "Orioni")
        for entry in terms["figureTerms"].values():
            entry["status"] = "proposed"
        for section in ("constellations", "asterisms", "stars"):
            for entry in terms[section]:
                entry["status"] = "proposed"
        fallback = labels({"language": "sq"}, terms)
        for key, value in EN.items():
            self.assertEqual(fallback[key], value)
        self.assertEqual(entity_name(next(x for x in terms["constellations"] if x["appCode"] == "ORI"), "sq"), "Orion")
        self.assertEqual(entity_name(terms["asterisms"][0], "sq"), "Summer Triangle")
        self.assertEqual(entity_name(next(x for x in terms["stars"] if x["hip"] == 11767), "sq"), "Polaris")

        request = example("orion", "sq")
        geometry, science = chart_scene(request, self.stars, self.definitions)
        with patch("astro_renderer.charts.catalogue", return_value=terms):
            fig = draw_chart(geometry, science, request, PALETTE)
        try:
            output = BytesIO()
            fig.savefig(output, format="svg", metadata={"Date": None})
            svg = output.getvalue().decode("utf-8")
            self.assertIn("Orion", svg)
            self.assertIn("J2000 | North up, east left | Gnomonic projection (TAN)", svg)
            self.assertNotIn("Orioni", svg)
            self.assertNotIn("Shkalla këndore", svg)
            self.assertNotIn("Magnituda e dukshme", svg)
        finally:
            plt.close(fig)

    def test_local_clock_and_utc_formats_include_dst_policy_result(self):
        payload = example("southern-horizon", "sq")["skyChart"]
        reference = payload["timeReference"]
        cases = [
            ("2027-01-15T20:00:00+00:00", "2027-01-15T21:00:00", "15 janar 2027, ora 21:00 (CET, UTC+1) = 20:00 UTC"),
            ("2027-07-15T19:00:00+00:00", "2027-07-15T21:00:00", "15 korrik 2027, ora 21:00 (CEST, UTC+2) = 19:00 UTC"),
            ("2027-10-31T00:30:00+00:00", "2027-10-31T02:30:00", "31 tetor 2027, ora 02:30 (CEST, UTC+2) = 00:30 UTC"),
            ("2027-10-31T01:30:00+00:00", "2027-10-31T02:30:00", "31 tetor 2027, ora 02:30 (CET, UTC+1) = 01:30 UTC"),
        ]
        for utc, local, expected in cases:
            with self.subTest(utc=utc):
                reference["localDateTime"] = local
                self.assertEqual(time_note({"observationUtc": utc}, payload, "sq"), expected)
        self.assertEqual(time_note({"observationUtc": cases[0][0]}, payload, "en"),
                         "15 January 2027, 21:00 (CET, UTC+1) = 20:00 UTC")
        payload.pop("timeReference")
        payload["observer"].pop("timeZoneId")
        self.assertEqual(time_note({"observationUtc": cases[0][0]}, payload, "sq"), "15 janar 2027, ora 20:00 UTC")

    def test_number_and_coordinate_display_uses_request_not_rounded_geometry(self):
        request = example("southern-horizon", "sq")
        text = labels(request)
        self.assertEqual(coordinate_note(request["skyChart"]["observer"], "sq", text), "42,7° V, 21,2° L, 600 m")
        self.assertEqual(angle(42.6629, "sq"), "42,7°")
        self.assertEqual(angle(42.6629, "en"), "42.7°")

    def test_chart_titles_scale_legend_and_label_policy(self):
        for scene, title in (("orion", "Orioni"), ("ursa-minor", "Arusha e Vogël"),
                             ("summer-triangle", "Trekëndëshi Veror")):
            request = example(scene, "sq")
            geometry, science = chart_scene(request, self.stars, self.definitions)
            fig = draw_chart(geometry, science, request, PALETTE)
            try:
                all_text = [artist.get_text() for artist in fig.findobj(match=Text)]
                self.assertIn(title, all_text)
                limit = request["skyChart"]["magnitudeLimit"]
                self.assertIn(f"Magnituda kufi: {limit},0", all_text)
                self.assertTrue(any("Shkalla këndore" in value for value in all_text))
                self.assertTrue(any("V ↑" in value for value in all_text))
                self.assertFalse(any("Skemë — jo në shkallë" in value for value in all_text))
                names = {p["name"] for p in geometry["points"] if p["hip"] in REQUIRED_LABEL_HIPS or
                         (p["hip"] in {x["hip"] for x in catalogue()["stars"]} and
                          p["magnitude"] <= AUTOMATIC_LABEL_MAGNITUDE)}
                self.assertTrue(names.issubset(set(all_text)))
                # A labelled angular bar uses the centre tangent-plane scale.
                bar = [line for line in fig.axes[0].lines if len(line.get_xdata()) == 2 and
                       line.get_ydata()[0] == line.get_ydata()[1]][0]
                scale_label = next(value for value in all_text if "Shkalla këndore" in value)
                stated_degrees = float(scale_label.split()[-1].rstrip("°").replace(",", "."))
                self.assertLessEqual(abs((bar.get_xdata()[1] - bar.get_xdata()[0]) /
                                         stated_degrees - 1), .01)
                self.assertEqual(glyph_size(2), max(2, 38 * 10 ** (-0.28 * 2)))
            finally:
                plt.close(fig)

    def test_sirius_label_on_january_southern_horizon(self):
        request = example("southern-horizon", "en")
        payload = request["skyChart"]
        payload.pop("timeReference")
        payload["timeUtc"] = "2027-01-15T20:00:00Z"
        geometry, science = chart_scene(request, self.stars, self.definitions)
        self.assertIn(32349, {p["hip"] for p in geometry["points"]})
        fig = draw_chart(geometry, science, request, PALETTE)
        try:
            self.assertIn("Sirius", [artist.get_text() for artist in fig.findobj(match=Text)])
        finally:
            plt.close(fig)

    def test_separation_ray_order_and_no_vertical_guide(self):
        request = example("angular-separation", "sq")
        geometry, science = diagram_scene(request, self.stars)
        fig = draw_diagram(geometry, science, request, PALETTE)
        try:
            axis_text = [artist.get_text() for artist in fig.axes[0].texts]
            self.assertIn(science["starNames"][0], axis_text)
            self.assertIn(science["starNames"][1], axis_text)
            self.assertFalse(any(list(line.get_xdata()) == [0, 0] for line in fig.axes[0].lines))
            self.assertFalse(any("Skemë — jo në shkallë" in x.get_text() for x in fig.findobj(match=Text)))
        finally:
            plt.close(fig)

    def test_iers_prediction_is_conditional_and_print_minima_hold(self):
        text = labels({"language": "sq"})
        self.assertFalse(any("IERS" in line and "parashikuara" in line for line in attribution({}, text, "TAN")))
        self.assertTrue(any("parashikuara" in line for line in attribution({"earthOrientationPredicted": True}, text, "TAN")))
        request = example("altitude-azimuth", "sq")
        geometry, science = diagram_scene(request, self.stars)
        fig = draw_diagram(geometry, science, request, PALETTE)
        try:
            self.assertAlmostEqual(fig.get_figwidth(), 170 / 25.4)
            self.assertAlmostEqual(fig.get_figheight(), 247 / 25.4)
            self.assertGreaterEqual(min(x.get_fontsize() for x in fig.findobj(match=Text) if x.get_text()), 7)
            self.assertGreaterEqual(min(line.get_linewidth() for ax in fig.axes for line in ax.lines), 0.25)
        finally:
            plt.close(fig)

    def test_png_is_300_dpi_with_matching_dimensions(self):
        with tempfile.TemporaryDirectory() as temporary:
            render(example("orion"), temporary)
            png = (Path(temporary) / "chart.png").read_bytes()
            self.assertEqual(struct.unpack(">II", png[16:24]),
                             (int(170 / 25.4 * 300), int(247 / 25.4 * 300)))
            offset = 8
            while offset < len(png):
                length = struct.unpack(">I", png[offset:offset + 4])[0]
                if png[offset + 4:offset + 8] == b"pHYs":
                    self.assertEqual(struct.unpack(">IIB", png[offset + 8:offset + 17]),
                                     (11811, 11811, 1))
                    break
                offset += length + 12
            else:
                self.fail("PNG pHYs resolution chunk missing")

    def test_attribution_and_method_match_each_figure_kind(self):
        cases = (
            ("orion", True, True, "Gnomonic projection (TAN)"),
            ("southern-horizon", True, False, "Azimuth / Altitude"),
            ("polaris-computed", True, False, "Azimuth / Altitude"),
            ("angular-separation", True, True, None),
            ("polaris-latitude", False, False, None),
            ("altitude-45", False, False, None),
            ("altitude-azimuth", False, False, None),
        )
        for language in ("en", "sq"):
            for name, source, epoch, method in cases:
                with self.subTest(name=name, language=language):
                    request = example(name, language)
                    if request.get("skyChart"):
                        geometry, science = chart_scene(request, self.stars, self.definitions)
                        fig = draw_chart(geometry, science, request, PALETTE)
                    else:
                        geometry, science = diagram_scene(request, self.stars)
                        fig = draw_diagram(geometry, science, request, PALETTE)
                    try:
                        lines = [artist.get_text() for artist in fig.texts]
                        display = labels(request)
                        self.assertEqual(any(line.startswith(display["dataSource"]) for line in lines), source)
                        self.assertEqual(any(display["epochJ2000"] in line for line in lines), epoch)
                        methods = [line for line in lines if line.startswith(display["method"])]
                        self.assertEqual(len(methods), 0 if method is None else 1)
                        if method is not None:
                            expected = (f"{display['azimuth']} / {display['altitude']}"
                                        if method == "Azimuth / Altitude" else
                                        display["static"].split("|")[-1].strip())
                            self.assertIn(expected, methods[0])
                            self.assertNotIn("AltitudeAzimuth", methods[0])
                    finally:
                        plt.close(fig)

    def test_scale_bar_legend_and_fractional_elevation(self):
        request = example("orion", "sq")
        geometry, science = chart_scene(request, self.stars, self.definitions)
        fig = draw_chart(geometry, science, request, PALETTE)
        try:
            bar = next(line for line in fig.axes[0].lines if len(line.get_xdata()) == 2 and
                       line.get_ydata()[0] == line.get_ydata()[1])
            self.assertLessEqual(abs((bar.get_xdata()[1] - bar.get_xdata()[0]) / 10 - 1), .01)
        finally:
            plt.close(fig)
        request["skyChart"]["magnitudeLimit"] = 3
        geometry, science = chart_scene(request, self.stars, self.definitions)
        fig = draw_chart(geometry, science, request, PALETTE)
        try:
            self.assertEqual([item.get_text() for item in fig.texts if item.get_text() in ("0", "2", "4", "6")],
                             ["0", "2"])
        finally:
            plt.close(fig)
        observer = copy.deepcopy(example("southern-horizon", "sq")["skyChart"]["observer"])
        observer["elevationMeters"] = 600.5
        self.assertTrue(coordinate_note(observer, "sq", labels({"language": "sq"})).endswith("600,5 m"))
        self.assertTrue(coordinate_note(observer, "en", labels({"language": "en"})).endswith("600.5 m"))

    def test_orion_labels_clear_glyphs_and_scale_text(self):
        for language in ("en", "sq"):
            request = example("orion", language)
            geometry, science = chart_scene(request, self.stars, self.definitions)
            fig = draw_chart(geometry, science, request, PALETTE)
            try:
                fig.canvas.draw()
                ax = fig.axes[0]
                renderer = fig.canvas.get_renderer()
                glyphs = []
                for point in geometry["points"]:
                    x, y = ax.transData.transform((point["x"], point["y"]))
                    radius = math.sqrt(glyph_size(point["magnitude"])) * fig.dpi / 144 + 2
                    glyphs.append(Bbox.from_bounds(x - radius, y - radius, 2 * radius, 2 * radius))
                annotations = [item for item in ax.texts if isinstance(item, Annotation)]
                self.assertGreaterEqual(len(annotations), 7)
                for label in annotations:
                    box = label.get_window_extent(renderer)
                    self.assertFalse(any(box.overlaps(glyph) for glyph in glyphs), label.get_text())
                scale = next(item for item in fig.texts if labels(request)["angularScale"] in item.get_text())
                scale_box = scale.get_window_extent(renderer)
                self.assertFalse(any(scale_box.overlaps(glyph) for glyph in glyphs))
            finally:
                plt.close(fig)

    def test_dense_wide_horizon_renders_with_required_labels(self):
        for language in ("en", "sq"):
            for center in (0, 90, 180):
                with self.subTest(language=language, azimuth=center):
                    request = example("southern-horizon", language)
                    chart = request["skyChart"]
                    chart.pop("timeReference")
                    chart.update(magnitudeLimit=6.5, azimuthCenterDegrees=center,
                                 fieldOfViewDegrees=180, timeUtc="2027-01-15T20:00:00Z")
                    geometry, science = chart_scene(request, self.stars, self.definitions)
                    fig = draw_chart(geometry, science, request, PALETTE)
                    try:
                        all_text = {artist.get_text() for artist in fig.findobj(match=Text)}
                        labelled = {p["name"] for p in geometry["points"] if p["hip"] in REQUIRED_LABEL_HIPS or
                                    (p["hip"] in {x["hip"] for x in catalogue()["stars"]} and
                                     p["magnitude"] <= AUTOMATIC_LABEL_MAGNITUDE)}
                        self.assertTrue(labelled.issubset(all_text))
                        output = BytesIO()
                        fig.savefig(output, format="svg", metadata={"Date": None})
                        self.assertGreater(len(output.getvalue()), 0)
                    finally:
                        plt.close(fig)

    def test_dense_horizon_labels_stay_in_plot_and_tied_to_their_stars(self):
        for language in ("en", "sq"):
            for center in (0, 90, 180, 270):
                for field in (100, 180):
                    for instant in ("2027-01-15T20:00:00Z", "2027-07-15T19:00:00Z"):
                        with self.subTest(language=language, azimuth=center, field=field, time=instant):
                            request = example("southern-horizon", language)
                            chart = request["skyChart"]
                            chart.pop("timeReference")
                            chart.update(magnitudeLimit=6.5, azimuthCenterDegrees=center,
                                         fieldOfViewDegrees=field, timeUtc=instant)
                            geometry, science = chart_scene(request, self.stars, self.definitions)
                            fig = draw_chart(geometry, science, request, PALETTE)
                            try:
                                fig.canvas.draw()
                                ax = fig.axes[0]
                                renderer = fig.canvas.get_renderer()
                                expected = {p["name"]: p for p in geometry["points"] if
                                            p["hip"] in REQUIRED_LABEL_HIPS or
                                            (p["hip"] in {x["hip"] for x in catalogue()["stars"]} and
                                             p["magnitude"] <= AUTOMATIC_LABEL_MAGNITUDE)}
                                bright = [p for p in geometry["points"] if p["magnitude"] <= 4]
                                annotations = [item for item in ax.texts if isinstance(item, Annotation)]
                                self.assertEqual({item.get_text() for item in annotations}, set(expected))
                                for label in annotations:
                                    point = expected[label.get_text()]
                                    self.assertEqual(tuple(label.xy), (point["x"], point["y"]))
                                    label.get_window_extent(renderer)
                                    box = Text.get_window_extent(label, renderer)
                                    self.assertGreaterEqual(box.x0, ax.bbox.x0 - 1)
                                    self.assertLessEqual(box.x1, ax.bbox.x1 + 1)
                                    self.assertGreaterEqual(box.y0, ax.bbox.y0 - 1)
                                    self.assertLessEqual(box.y1, ax.bbox.y1 + 1)
                                    star_x, star_y = ax.transData.transform(label.xy)
                                    nearest_x = min(max(star_x, box.x0), box.x1)
                                    nearest_y = min(max(star_y, box.y0), box.y1)
                                    distance = math.hypot(star_x - nearest_x, star_y - nearest_y) * 72 / fig.dpi
                                    if distance > 20:
                                        self.assertIsNotNone(label.arrow_patch, label.get_text())
                                        self.assertTrue(label.arrow_patch.get_visible())
                                        self.assertGreater(label.arrow_patch.get_linewidth(), 0)
                                        self.assertLessEqual(min(math.hypot(vertex[0] - star_x,
                                                                            vertex[1] - star_y)
                                                                 for vertex in label.arrow_patch.get_path().vertices),
                                                             1, label.get_text())
                                    self.assert_identifiable_or_connected(label, point, bright, ax, renderer)
                            finally:
                                plt.close(fig)
