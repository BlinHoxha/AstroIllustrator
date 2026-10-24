"""Small reviewed-by-editor terminology surface; scientific values stay separate."""
TEXT = {
    "en": dict(horizon="Horizon", altitude="Altitude", azimuth="Azimuth", zenith="Zenith", reference="Reference",
        separation="Angular separation", schematic="Schematic numeric geometry", computed="Polaris — computed",
        teaching="NCP latitude teaching diagram", static="J2000 | North up / East left | TAN",
        summer="Summer Triangle", geometric="Geometric horizon | No refraction", noRefraction="No refraction",
        ncp="Ideal NCP altitude = observer latitude. Not exact Polaris altitude.",
        zenithDistance="Zenith distance = 90° − altitude", clockwise="Clockwise from north", ray="Ray 1",
        separationNote="Angular diagram; rays illustrate measured separation, not a sky projection.",cardinals=["N","E","S","W"]),
    "sq": dict(horizon="Horizonti", altitude="Lartësia këndore", azimuth="Azimuti", zenith="Zeniti", reference="Referenca",
        separation="Ndarja këndore", schematic="Gjeometri numerike skematike", computed="Polaris — pozicioni i llogaritur",
        teaching="Poli qiellor verior dhe gjerësia — skemë mësimore", static="J2000 | Veriu lart / Lindja majtas | TAN",
        summer="Trekëndëshi veror", geometric="Horizont gjeometrik | Pa përthyerje atmosferike", noRefraction="Pa përthyerje atmosferike",
        ncp="Lartësia ideale e polit qiellor verior = gjerësia. Nuk është lartësia e saktë e Polaris.",
        zenithDistance="Largësia zenitale = 90° − lartësia", clockwise="Në drejtim të akrepave nga veriu", ray="Rrezja 1",
        separationNote="Skemë këndore; rrezet tregojnë ndarjen e matur, jo projeksion qiellor.",cardinals=["V","L","J","P"])
}

def labels(request):
    return TEXT[request["language"]]
