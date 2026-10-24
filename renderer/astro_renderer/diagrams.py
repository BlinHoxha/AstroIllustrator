import math
import matplotlib.pyplot as plt
from matplotlib.patches import Arc
import astropy.units as u
from astropy.coordinates import SkyCoord

from .astronomy import observer_positions
from .contracts import ScientificError
from .localization import labels

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
    text=labels(request)
    dual=geometry["diagramType"]=="AltitudeAzimuth"
    fig,axes=plt.subplots(1,2 if dual else 1,figsize=(11,6) if dual else (8,7),facecolor=palette["background"],squeeze=False)
    for ax in axes.flat:
        ax.set_facecolor(palette["background"])
        ax.set(xlim=(-1.25,1.3),ylim=(-1.25,1.3),aspect="equal")
        ax.axis("off")
    ax=axes[0,0]
    ax.plot([-1.1,1.15],[0,0],color=palette["line"])
    ax.plot([0,0],[0,1.1],"--",color=palette["line"])
    x,y=geometry["rayEnd"]
    ax.plot([0,x],[0,y],color=palette["foreground"],linewidth=2)
    ax.scatter([x],[y],c=palette["foreground"],s=30)
    angle=geometry["angleDeg"]
    ax.add_patch(Arc((0,0),0.75,0.75,theta1=min(0,angle),theta2=max(0,angle),color=palette["line"],linewidth=2))
    label_angle=math.radians(angle/2)
    ax.text(0.5*math.cos(label_angle),0.5*math.sin(label_angle),f"{angle:.3f}°",color=palette["foreground"],fontsize=12)
    separation=geometry["diagramType"]=="AngularSeparation"
    ax.text(0.6,-0.13,text["ray"] if separation else text["horizon"],color=palette["foreground"],fontsize=9)
    ax.text(0.04,1.12,text["reference"] if separation else text["zenith"],color=palette["foreground"],fontsize=9)
    if dual:
        az=geometry["azimuthDeg"]
        ax=axes[0,1]
        ax.plot([-1.1,1.1],[0,0],color=palette["line"])
        ax.plot([0,0],[-1.1,1.1],color=palette["line"])
        ax.plot([0,geometry["azimuthRayEnd"][0]],[0,geometry["azimuthRayEnd"][1]],color=palette["foreground"],linewidth=2)
        ax.add_patch(Arc((0,0),0.7,0.7,theta1=90-az,theta2=90,color=palette["line"]))
        for cardinal,(x,y) in zip(text["cardinals"],[(0,1.15),(1.15,0),(0,-1.15),(-1.15,0)]):
            ax.text(x,y,cardinal,color=palette["foreground"],ha="center")
        ax.text(-0.9,-0.8,f"{text['azimuth']} = {az:g}°\n{text['clockwise']}",color=palette["foreground"],fontsize=9)
    title=text["separation"] if separation else text["computed"] if geometry["mode"]=="Computed" else text["teaching"] if "approximation" in science else text["altitude"]+" — "+text["schematic"]
    fig.suptitle(title,color=palette["foreground"],fontsize=13)
    if geometry["mode"]=="Computed" and "observationUtc" in science:
        observer=science["observer"]
        note=science["observationUtc"]+f" | {observer['latitude']:+.4f}°, {observer['longitude']:+.4f}°\n{text['azimuth']} {science['azimuthDeg']:.3f}° | {text['noRefraction']}\nHYG v4.1: David Nash / Astronexus (CC BY-SA 4.0)"
    elif "approximation" in science:
        note=text["ncp"]
    elif separation:
        note=" / ".join(science["starNames"])+" | J2000 | HYG v4.1: David Nash / Astronexus (CC BY-SA 4.0)\n"+text["separationNote"]
    else:
        note=text["schematic"]+" | "+text["zenithDistance"]
    fig.text(0.5,0.045,note,ha="center",fontsize=8,color=palette["foreground"])
    fig.subplots_adjust(bottom=0.15)
    return fig
