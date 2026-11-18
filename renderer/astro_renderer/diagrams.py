import math
import matplotlib.pyplot as plt
from matplotlib.patches import Arc
import astropy.units as u
from astropy.coordinates import SkyCoord

from .astronomy import observer_positions
from .contracts import ScientificError
from .localization import labels, catalogue, angle, coordinate_note, time_note, attribution

def diagram_scene(request, catalog):
    diagram=request["scientificDiagram"]
    p=diagram.get("parameters",{})
    kind,mode=diagram["diagramType"],diagram["mode"]
    science=dict(warnings=[])
    if kind=="AngularSeparation":
        stars={s["hip"]:s for s in catalog["stars"]}
        ids=p["starHipIds"]
        if not all(i in stars for i in ids):
            raise ScientificError("HIP pair must exist in the bundled magnitude <=6.5 catalog")
        coordinates=SkyCoord(ra=[stars[i]["raDeg"] for i in ids]*u.deg,dec=[stars[i]["decDeg"] for i in ids]*u.deg,frame="icrs")
        angle=float(coordinates[0].separation(coordinates[1]).deg)
        science.update(starHipIds=ids,starNames=[stars[i]["name"] for i in ids],angularSeparationDeg=angle,epoch="J2000.0")
        title="Angular separation / Ndarja këndore"
    elif mode=="Computed":
        star=next(s for s in catalog["stars"] if s["hip"]==11767)
        alt,az,provenance=observer_positions([star],diagram)
        angle=float(alt[0])
        science.update(provenance)
        science.update(starHipId=11767,altitudeDeg=angle,azimuthDeg=float(az[0]),northCelestialPoleAltitudeDeg=diagram["observer"]["latitude"])
        if angle<0: science["warnings"].append("Polaris is below the geometric horizon.")
        title="Polaris — computed / e llogaritur"
    elif kind=="PolarisLatitude":
        angle=diagram["observer"]["latitude"]
        science.update(northCelestialPoleAltitudeDeg=angle,approximation="Ideal NCP altitude equals observer latitude; this ray is not the instantaneous position of Polaris.")
        title="NCP latitude teaching diagram / Skemë mësimore"
        if angle<0: science["warnings"].append("The north celestial pole is below the horizon in the southern hemisphere.")
    else:
        angle=p["altitudeDegrees"]
        science.update(altitudeDeg=angle)
        title="Altitude / Lartësia këndore — schematic"
    radians=math.radians(angle)
    geometry=dict(diagramType=kind,mode=mode,angleDeg=angle,rayStart=[0,0],rayEnd=[math.cos(radians),math.sin(radians)],
                  arcStartDeg=0,arcEndDeg=angle)
    if kind!="AngularSeparation":
        geometry["zenithDistanceDeg"]=90-angle
    if kind=="AltitudeAzimuth":
        az=p["azimuthDegrees"]
        geometry.update(azimuthDeg=az,azimuthRayEnd=[math.sin(math.radians(az)),math.cos(math.radians(az))])
        science["azimuthConvention"]="True north=0, east=90, south=180, west=270; clockwise"
    science["title"]=title
    return geometry,science

def draw_diagram(geometry, science, request, palette):
    language = request["language"]
    text = labels(request, catalogue())
    dual = geometry["diagramType"] == "AltitudeAzimuth"
    separation = geometry["diagramType"] == "AngularSeparation"
    fig, axes = plt.subplots(1, 2 if dual else 1, figsize=(170 / 25.4, 247 / 25.4),
                             facecolor=palette["background"], squeeze=False)
    if dual:
        axes[0, 0].set_position([0.04, 0.35, 0.44, 0.43])
        axes[0, 1].set_position([0.52, 0.35, 0.44, 0.43])
    else:
        axes[0, 0].set_position([0.10, 0.32, 0.80, 0.53])
    for ax in axes.flat:
        ax.set_facecolor(palette["background"])
        ax.set(xlim=(-1.25, 1.3), ylim=(-1.25, 1.3), aspect="equal")
        ax.axis("off")
    ax = axes[0, 0]
    ax.plot([-1.1, 1.15], [0, 0], color=palette["line"], linewidth=0.8)
    if not separation:
        ax.plot([0, 0], [0, 1.1], "--", color=palette["line"], linewidth=0.8)
    x, y = geometry["rayEnd"]
    ax.plot([0, x], [0, y], color=palette["foreground"], linewidth=2)
    ax.scatter([x], [y], c=palette["foreground"], s=30)
    measured_angle = geometry["angleDeg"]
    ax.add_patch(Arc((0, 0), 0.75, 0.75, theta1=min(0, measured_angle), theta2=max(0, measured_angle),
                     color=palette["line"], linewidth=2))
    label_angle = math.radians(measured_angle / 2)
    ax.text(0.5 * math.cos(label_angle), 0.5 * math.sin(label_angle), angle(measured_angle, language),
            color=palette["foreground"], fontsize=11)
    if separation:
        # First HIP is the horizontal baseline; second HIP is the measured angled ray.
        ax.text(0.6, -0.13, science["starNames"][0], color=palette["foreground"], fontsize=9)
        ax.annotate(science["starNames"][1], (x, y), xytext=(8, 8), textcoords="offset points",
                    color=palette["foreground"], fontsize=9)
    else:
        ax.text(0.6, -0.13, text["horizon"], color=palette["foreground"], fontsize=9)
        ax.text(0.04, 1.12, text["zenith"], color=palette["foreground"], fontsize=9)
    if dual:
        az = geometry["azimuthDeg"]
        ax = axes[0, 1]
        ax.plot([-1.1, 1.1], [0, 0], color=palette["line"], linewidth=0.8)
        ax.plot([0, 0], [-1.1, 1.1], color=palette["line"], linewidth=0.8)
        ax.plot([0, geometry["azimuthRayEnd"][0]], [0, geometry["azimuthRayEnd"][1]],
                color=palette["foreground"], linewidth=2)
        ax.add_patch(Arc((0, 0), 0.7, 0.7, theta1=90 - az, theta2=90,
                         color=palette["line"], linewidth=1.5))
        for cardinal, (cx, cy) in zip(text["cardinals"], [(0, 1.15), (1.15, 0), (0, -1.15), (-1.15, 0)]):
            ax.text(cx, cy, cardinal, color=palette["foreground"], ha="center", fontsize=9)
        fig.text(0.5, 0.32, f"{text['azimuth']} = {angle(az, language)} | {text['clockwise']}",
                 color=palette["foreground"], ha="center", fontsize=8)
    title = text["separation"] if separation else text["computed"] if geometry["mode"] == "Computed" else \
        text["teaching"] if "approximation" in science else text["altitude"] + " — " + text["schematic"]
    fig.suptitle(title, color=palette["foreground"], fontsize=13, y=0.92)
    if geometry["mode"] == "Computed" and "observationUtc" in science:
        payload = request["scientificDiagram"]
        note = (time_note(science, payload, language) + "\n" +
                coordinate_note(payload["observer"], language, text) + "\n" +
                f"{text['azimuth']} {angle(science['azimuthDeg'], language)} | {text['noRefraction']}")
        projection = f"{text['azimuth']} / {text['altitude']}"
    elif "approximation" in science:
        note = text["ncp"]
        projection = text["teaching"]
    elif separation:
        note = text["separationNote"]
        projection = text["separation"]
    else:
        note = text["zenithDistance"]
        projection = text["schematic"]
    fig.text(0.5, 0.245, note, ha="center", va="top", fontsize=8,
             color=palette["foreground"], linespacing=1.5)
    for index, line in enumerate(attribution(
            science, text, projection, uses_catalogue=separation or geometry["mode"] == "Computed",
            j2000_positions=separation, include_method=geometry["mode"] == "Computed" and not separation)):
        fig.text(0.5, 0.135 - index * 0.018, line, ha="center", fontsize=7, color=palette["foreground"])
    if geometry["mode"] == "Computed" or separation:
        # Retain the HYG attribution and licence verbatim.
        fig.text(0.5, 0.025, "HYG v4.1: David Nash / Astronexus (CC BY-SA 4.0)",
                 ha="center", fontsize=8, color=palette["foreground"])
    return fig
