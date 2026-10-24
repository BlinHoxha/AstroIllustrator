import math
import numpy as np
import matplotlib.pyplot as plt
from astropy.wcs import WCS

from .astronomy import observer_positions
from .contracts import ScientificError, selected_codes
from .localization import labels

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
    text=labels(request)
    fig, ax = plt.subplots(figsize=(10,8),facecolor=palette["background"])
    ax.set_facecolor(palette["background"])
    points = {p["hip"]:p for p in geometry["points"]}
    for kind, style in [("edges","-"),("asterismEdges","--")]:
        for a,b in geometry[kind]:
            ax.plot([points[a]["x"],points[b]["x"]],[points[a]["y"],points[b]["y"]],style,
                    color=palette["line"],linewidth=1.5 if kind=="asterismEdges" else 0.8,zorder=1)
    if points:
        values=list(points.values())
        ax.scatter([p["x"] for p in values],[p["y"] for p in values],
            s=[max(2,38*10**(-0.28*p["magnitude"])) for p in values],c=palette["foreground"],linewidths=0,zorder=2)
        label_ids={27989,24436,25336,27366,26727,26311,25930,11767,72607,75097,91262,102098,97649}
        for p in values:
            if p["hip"] in label_ids:
                offset={26727:(-48,-12),26311:(18,-4),25930:(16,15)}.get(p["hip"],(6,6))
                ax.annotate(p["name"],(p["x"],p["y"]),xytext=offset,textcoords="offset points",color=palette["foreground"],fontsize=8,
                    arrowprops=dict(arrowstyle="-",color=palette["line"],linewidth=0.5) if p["hip"] in {26727,26311,25930} else None)
    x0,x1,y0,y1=geometry["bounds"]
    ax.set(xlim=(x0,x1),ylim=(y0,y1),aspect="equal")
    if geometry["sceneType"] == "HorizonView":
        center=geometry["projection"]["azimuthCenterDegrees"]
        ticks=np.linspace(x0,x1,5)
        ax.set_xticks(ticks,[f"{(t+center)%360:g}°" for t in ticks])
        convention=", ".join(f"{c}={a}°" for c,a in zip(text["cardinals"],[0,90,180,270]))
        ax.set_xlabel(text["azimuth"]+" ("+convention+")",color=palette["foreground"])
        ax.set_ylabel(text["altitude"]+" (°)",color=palette["foreground"])
        ax.tick_params(colors=palette["foreground"])
        for spine in ax.spines.values(): spine.set_color(palette["line"])
        ax.grid(alpha=0.2,color=palette["line"])
        observer=science["observer"]
        note=science["observationUtc"]+f" | {observer['latitude']:+.4f}°, {observer['longitude']:+.4f}°, {observer.get('elevationMeters',0):g} m\n"+text["geometric"]
    else:
        ax.axis("off")
        note=text["static"]
    title=text["horizon"] if geometry["sceneType"]=="HorizonView" else text["summer"] if geometry["sceneType"]=="Asterism" else science["title"]
    ax.set_title(title,color=palette["foreground"],fontsize=16)
    fig.text(0.5,0.025,note+"\nHYG v4.1: David Nash / Astronexus (CC BY-SA 4.0)",ha="center",fontsize=8,color=palette["foreground"])
    fig.subplots_adjust(bottom=0.16)
    return fig
