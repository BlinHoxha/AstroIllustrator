import math
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.patches import ConnectionPatch
from matplotlib.transforms import Bbox
from astropy.wcs import WCS

from .astronomy import observer_positions
from .contracts import ScientificError, selected_codes
from .localization import labels, catalogue, entity_name, angle, decimal, coordinate_note, time_note, attribution

AUTOMATIC_LABEL_MAGNITUDE = 1.5
REQUIRED_LABEL_HIPS = {27989, 24436, 25336, 27366, 26727, 26311, 25930,
                       11767, 72607, 75097, 91262, 102098, 97649}

def glyph_size(magnitude):
    return max(2, 38 * 10 ** (-0.28 * magnitude))

def project(stars, center=(83.8, -1.2)):
    wcs = WCS(naxis=2)
    wcs.wcs.crpix = [1, 1]
    wcs.wcs.cdelt = [-1, 1]
    wcs.wcs.crval = center
    wcs.wcs.ctype = ["RA---TAN", "DEC--TAN"]
    xy = wcs.wcs_world2pix([[s["raDeg"], s["decDeg"]] for s in stars], 0)
    if not np.isfinite(xy).all() or np.max(np.abs(xy)) > 1000:
        raise ScientificError("Selected constellations cannot share a bounded TAN chart; choose a smaller region")
    return xy

def chart_scene(request, catalog, definitions):
    chart = request["skyChart"]
    asterism = definitions["asterisms"][0] if chart.get("asterismCode") == "SummerTriangle" else None
    codes = asterism["constellationCodes"] if chart["sceneType"] == "Asterism" else selected_codes(chart)
    horizon = chart["sceneType"] == "HorizonView"
    all_sky = horizon and chart.get("constellationCode") is None and chart.get("constellationCodes") is None
    stars = [s for s in catalog["stars"] if s["magnitude"] <= chart["magnitudeLimit"] and
             (all_sky or s["constellation"] in codes or (asterism and s["hip"] in asterism["starHipIds"]))]
    if not stars:
        raise ScientificError("No catalog stars satisfy the request")
    ids = {s["hip"] for s in catalog["stars"]}
    edges = [edge for c in definitions["constellations"] if all_sky or c["code"] in codes for edge in c["edges"]]
    asterism_edges = asterism["edges"] if asterism else []
    if any(a not in ids or b not in ids for a, b in edges + asterism_edges):
        raise ScientificError("Line definition references an unavailable catalog identity")
    science = dict(epoch=catalog["epoch"], warnings=[])
    if horizon:
        altitudes, azimuths, provenance = observer_positions(stars, chart)
        center = chart.get("azimuthCenterDegrees", 180)
        fov = chart.get("fieldOfViewDegrees", 100)
        offsets = (azimuths - center + 180) % 360 - 180
        mask = (altitudes >= 0) & (np.abs(offsets) <= fov/2)
        xy = np.column_stack((offsets, altitudes))
        science.update(provenance)
        science.update(belowHorizonCount=int(np.sum(altitudes < 0)), outsideFieldCount=int(np.sum((altitudes >= 0) & ~mask)))
        if provenance["earthOrientationPredicted"]:
            science["warnings"].append("Bundled IERS Earth-orientation values include predictions.")
        if not np.any(mask):
            science["warnings"].append("No selected stars are above the horizon inside this field of view.")
        points = [dict(hip=s["hip"], name=s["name"], magnitude=s["magnitude"], x=round(float(p[0]),10), y=round(float(p[1]),10),
                       altitudeDeg=round(float(alt),10), azimuthDeg=round(float(az),10))
                  for s,p,alt,az,visible in zip(stars,xy,altitudes,azimuths,mask) if visible]
        projection = dict(name="AltitudeAzimuth", azimuthCenterDegrees=center, fieldOfViewDegrees=fov,
                          convention="Rectilinear azimuth offset vs altitude, not an equal-angle sky projection")
        bounds = [-fov/2, fov/2, 0, 90]
        title = "Horizon / Horizonti"
    else:
        ra, dec = np.radians([s["raDeg"] for s in stars]), np.radians([s["decDeg"] for s in stars])
        mean = np.mean(np.array([np.cos(dec)*np.cos(ra),np.cos(dec)*np.sin(ra),np.sin(dec)]), axis=1)
        if np.linalg.norm(mean) < 0.1:
            raise ScientificError("The selected sky region is too wide for TAN projection")
        center = (math.degrees(math.atan2(mean[1],mean[0])) % 360, math.degrees(math.atan2(mean[2],math.hypot(mean[0],mean[1]))))
        xy = project(stars, center)
        points = [dict(hip=s["hip"],name=s["name"],magnitude=s["magnitude"], x=round(float(p[0]),10),y=round(float(p[1]),10)) for s,p in zip(stars,xy)]
        low, high = np.min(xy,axis=0),np.max(xy,axis=0)
        padding = max(float(np.max(high-low))*0.12,1)
        bounds = [float(low[0]-padding),float(high[0]+padding),float(low[1]-padding),float(high[1]+padding)]
        projection = dict(name="FITS TAN", centerRaDeg=center[0],centerDecDeg=center[1],east="left",north="up")
        title = "Summer Triangle" if chart["sceneType"] == "Asterism" else " + ".join(codes)
        science["warnings"].append("Static J2000 catalog positions; not an observer-specific current sky.")
    visible_ids = {p["hip"] for p in points}
    geometry = dict(sceneType=chart["sceneType"], projection=projection, bounds=bounds,
        points=sorted(points,key=lambda p:p["hip"]), edges=sorted([e for e in edges if set(e)<=visible_ids]),
        asterismEdges=sorted([e for e in asterism_edges if set(e)<=visible_ids]))
    science.update(title=title, selectedConstellations=codes if not all_sky else "All bright catalog stars", plottedStarCount=len(points),
                   lineConvention=definitions["source"])
    return geometry, science

def draw_chart(geometry, science, request, palette):
    language = request["language"]
    terms = catalogue()
    text = labels(request, terms)
    chart = request["skyChart"]
    fig, ax = plt.subplots(figsize=(170 / 25.4, 247 / 25.4), facecolor=palette["background"])
    ax.set_facecolor(palette["background"])
    ax.set_position([0.10, 0.31, 0.80, 0.55])
    points = {p["hip"]: p for p in geometry["points"]}
    for kind, style in [("edges", "-"), ("asterismEdges", "--")]:
        for a, b in geometry[kind]:
            ax.plot([points[a]["x"], points[b]["x"]], [points[a]["y"], points[b]["y"]], style,
                    color=palette["line"], linewidth=1.5 if kind == "asterismEdges" else 0.8, zorder=1)
    x0, x1, y0, y1 = geometry["bounds"]
    ax.set(xlim=(x0, x1), ylim=(y0, y1), aspect="equal")
    if points:
        values = list(points.values())
        ax.scatter([p["x"] for p in values], [p["y"] for p in values],
                   s=[glyph_size(p["magnitude"]) for p in values], c=palette["foreground"], linewidths=0, zorder=2)
        catalogued_stars = {entry["hip"] for entry in terms["stars"]}
        labelled = [p for p in values if p["hip"] in REQUIRED_LABEL_HIPS or
                    (p["hip"] in catalogued_stars and p["magnitude"] <= AUTOMATIC_LABEL_MAGNITUDE)]
        # Larger glyphs get first placement; test candidate extents in display coordinates.
        labelled.sort(key=lambda p: (p["magnitude"], p["hip"]))
        fig.canvas.draw()
        renderer = fig.canvas.get_renderer()
        # Scatter sizes are marker areas in points squared. Reserve every glyph's
        # display extent so a name cannot cover a different star (or its own glyph).
        occupied = []
        bright_stars = []
        for star in values:
            px, py = ax.transData.transform((star["x"], star["y"]))
            radius = math.sqrt(glyph_size(star["magnitude"])) * fig.dpi / 144 + 2
            occupied.append(Bbox.from_bounds(px - radius, py - radius, 2 * radius, 2 * radius))
            if star["magnitude"] <= 4:
                bright_stars.append((star["hip"], px, py))
        offsets = [(7, 7), (7, -13), (-7, 7), (-7, -13), (15, 0), (-15, 0), (0, 17), (0, -18)]
        offsets += [(distance * sx, distance * sy) for distance in (10, 15, 20, 25, 35, 50, 70)
                    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1), (1, 0), (-1, 0), (0, 1), (0, -1))]
        belt_offsets = {25930: (18, 18), 26311: (-18, -7), 26727: (-18, -18)}
        for p in labelled:
            name = p["name"]  # The catalogue name is the canonical star label (A3).
            candidates = [belt_offsets[p["hip"]]] + offsets if p["hip"] in belt_offsets else offsets
            star_x, star_y = ax.transData.transform((p["x"], p["y"]))
            best = None
            for dx, dy in candidates:
                annotation = ax.annotate(name, (p["x"], p["y"]), xytext=(dx, dy), textcoords="offset points",
                                         ha="left" if dx >= 0 else "right", va="bottom" if dy >= 0 else "top",
                                         color=palette["foreground"], fontsize=8, zorder=3)
                box = annotation.get_window_extent(renderer)
                if (box.x0 < ax.bbox.x0 or box.x1 > ax.bbox.x1 or
                        box.y0 < ax.bbox.y0 or box.y1 > ax.bbox.y1):
                    annotation.remove()
                    continue
                nearest_x = min(max(star_x, box.x0), box.x1)
                nearest_y = min(max(star_y, box.y0), box.y1)
                distance = math.hypot(star_x - nearest_x, star_y - nearest_y) * 72 / fig.dpi
                overlap = sum(max(0, min(box.x1, other.x1) - max(box.x0, other.x0)) *
                              max(0, min(box.y1, other.y1) - max(box.y0, other.y0)) for other in occupied)
                # Nearby clear labels win first. A nearby crowded position with a
                # leader is preferable to a detached label in an empty region.
                rank = (0 if overlap == 0 and distance <= 20 else
                        1 if overlap == 0 and distance <= 35 else
                        2 if distance <= 35 else 3 if overlap == 0 else 4)
                score = (rank, overlap, distance)
                if best is None or score < best[0]:
                    best = (score, dx, dy, box)
                annotation.remove()
            if best is None:
                # Clamp a nearby trial into the plot when every preset offset
                # crosses an edge. This keeps the bounded fallback renderable.
                dx, dy = 7, 7
                trial = ax.annotate(name, (p["x"], p["y"]), xytext=(dx, dy), textcoords="offset points",
                                    ha="left", va="bottom", color=palette["foreground"], fontsize=8, zorder=3)
                box = trial.get_window_extent(renderer)
                shift_x = max(ax.bbox.x0 - box.x0, min(0, ax.bbox.x1 - box.x1))
                shift_y = max(ax.bbox.y0 - box.y0, min(0, ax.bbox.y1 - box.y1))
                trial.remove()
                dx += shift_x * 72 / fig.dpi
                dy += shift_y * 72 / fig.dpi
                trial = ax.annotate(name, (p["x"], p["y"]), xytext=(dx, dy), textcoords="offset points",
                                    ha="left", va="bottom", color=palette["foreground"], fontsize=8, zorder=3)
                box = trial.get_window_extent(renderer)
                trial.remove()
                ha, va = "left", "bottom"
            else:
                _, dx, dy, box = best
                ha, va = ("left" if dx >= 0 else "right"), ("bottom" if dy >= 0 else "top")
            nearest_x = min(max(star_x, box.x0), box.x1)
            nearest_y = min(max(star_y, box.y0), box.y1)
            distance_pixels = math.hypot(star_x - nearest_x, star_y - nearest_y)
            ambiguous = any(math.hypot(other_x - min(max(other_x, box.x0), box.x1),
                                       other_y - min(max(other_y, box.y0), box.y1)) <= distance_pixels
                            for hip, other_x, other_y in bright_stars if hip != p["hip"])
            distant = distance_pixels * 72 / fig.dpi > 20
            leader = (dict(arrowstyle="-", color=palette["foreground"], linewidth=0.8,
                           shrinkA=0, shrinkB=0) if distant else None)
            annotation = ax.annotate(name, (p["x"], p["y"]), xytext=(dx, dy), textcoords="offset points",
                                     ha=ha, va=va, color=palette["foreground"], fontsize=8,
                                     arrowprops=leader, zorder=3)
            if ambiguous and not distant:
                endpoint = ax.transData.inverted().transform((nearest_x, nearest_y))
                connector = ConnectionPatch((p["x"], p["y"]), endpoint, "data", "data",
                                            axesA=ax, axesB=ax, arrowstyle="-",
                                            color=palette["foreground"], linewidth=0.8,
                                            shrinkA=0, shrinkB=0, zorder=2.9)
                ax.add_artist(connector)
                annotation.leader_patch = connector
            occupied.append(box.expanded(1.08, 1.2))
    if geometry["sceneType"] == "HorizonView":
        center = geometry["projection"]["azimuthCenterDegrees"]
        ticks = np.linspace(x0, x1, 5)
        tick_labels = []
        for tick in ticks:
            azimuth = (tick + center) % 360
            cardinal = next((letter for letter, bearing in zip(text["cardinals"], (0, 90, 180, 270))
                             if abs((azimuth - bearing + 180) % 360 - 180) < 1e-8), None)
            tick_labels.append((cardinal + " " if cardinal else "") + angle(azimuth, language))
        ax.set_xticks(ticks, tick_labels)
        ax.set_xlabel(text["azimuth"], color=palette["foreground"], fontsize=9)
        ax.set_ylabel(text["altitude"] + " (°)", color=palette["foreground"], fontsize=9)
        ax.tick_params(colors=palette["foreground"], labelsize=8)
        for spine in ax.spines.values():
            spine.set_color(palette["line"])
        ax.grid(alpha=0.2, color=palette["line"])
        note = time_note(science, chart, language) + "\n" + coordinate_note(chart["observer"], language, text)
        note += "\n" + text["geometric"]
        fig.text(0.5, 0.235, note, ha="center", va="top", fontsize=8, color=palette["foreground"], linespacing=1.5)
        cardinal_line = "   |   ".join(f"{letter} {az}°" for letter, az in zip(text["cardinals"], (0, 90, 180, 270)))
        fig.text(0.5, 0.275, cardinal_line, ha="center", fontsize=8, color=palette["foreground"])
        title = text["horizon"]
        projection = f"{text['azimuth']} / {text['altitude']}"
    else:
        ax.axis("off")
        if geometry["sceneType"] == "Asterism":
            entry = next(e for e in terms["asterisms"] if e["id"] == "SUMMER_TRIANGLE")
            title = entity_name(entry, language)
        else:
            names = {e["appCode"]: e for e in terms["constellations"]}
            title = ", ".join(entity_name(names[code], language) for code in science["selectedConstellations"])
        fig.text(0.5, 0.265, text["static"], ha="center", fontsize=8, color=palette["foreground"])
        # The tangent-plane axes use projected degrees at the projection centre.
        width = x1 - x0
        bar_degrees = next(value for value in (10, 5, 2, 1, 0.5, 0.2) if value < width * 0.35)
        bar_length = bar_degrees
        bar_x, bar_y = x0 + width * 0.08, y0 + (y1 - y0) * 0.08
        ax.plot([bar_x, bar_x + bar_length], [bar_y, bar_y], color=palette["foreground"], linewidth=1.0)
        ax.plot([bar_x, bar_x], [bar_y - 0.1, bar_y + 0.1], color=palette["foreground"], linewidth=1.0)
        ax.plot([bar_x + bar_length, bar_x + bar_length], [bar_y - 0.1, bar_y + 0.1], color=palette["foreground"], linewidth=1.0)
        fig.text(0.10, 0.285, f"{text['angularScale']} {angle(bar_degrees, language)}",
                 color=palette["foreground"], fontsize=8)
        fig.text(0.84, 0.88, f"{text['northCompass']} ↑\n{text['eastCompass']} ←", ha="center",
                 fontsize=9, color=palette["foreground"])
        legend_values = tuple(value for value in (0, 2, 4, 6) if value <= chart["magnitudeLimit"])
        fig.text(0.10, 0.230, text["apparentMagnitude"], fontsize=8, color=palette["foreground"])
        for index, value in enumerate(legend_values):
            x = 0.14 + 0.15 * index
            ax.scatter([x], [0.205], s=[glyph_size(value)], color=palette["foreground"],
                       transform=fig.transFigure, clip_on=False)
            fig.text(x + 0.018, 0.205, str(value), va="center", fontsize=8, color=palette["foreground"])
        fig.text(0.10, 0.175, f"{text['limitingMagnitude']}: {decimal(chart['magnitudeLimit'], language)}",
                 fontsize=8, color=palette["foreground"])
        projection = text["static"].split("|")[-1].strip()
    fig.suptitle(title, color=palette["foreground"], fontsize=14, y=0.93)
    lines = attribution(science, text, projection,
                        j2000_positions=geometry["sceneType"] != "HorizonView")
    for index, line in enumerate(lines):
        fig.text(0.5, 0.125 - index * 0.018, line, ha="center", fontsize=7, color=palette["foreground"])
    # Retain the HYG attribution and licence verbatim.
    fig.text(0.5, 0.025, "HYG v4.1: David Nash / Astronexus (CC BY-SA 4.0)",
             ha="center", fontsize=8, color=palette["foreground"])
    return fig
