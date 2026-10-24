# Scientific checks

## Static catalog foundation (renderer 0.1.0)

The static Orion chart uses the J2000 epoch/equinox coordinates documented in the pinned
HYG v4.1 source. It does not propagate proper motion or claim current apparent positions.

The test suite checks the HYG coordinates against two independent published SIMBAD
reference entries (ICRS, epoch J2000, reference 2007A&A...474..653V), inspected 22 October 2026:

- Betelgeuse / HIP 27989: 05 55 10.30536, +07 24 25.4304.
  https://simbad.cds.unistra.fr/simbad/sim-basic?Ident=Betelgeuse
- Rigel / HIP 24436: 05 14 32.27210, -08 12 05.8981.
  https://simbad.cds.unistra.fr/simbad/sim-basic?Ident=Rigel

The local tangent-plane catalog discrepancy must be below one arcsecond. This is an
identity/coordinate sanity gate, not a claim of subarcsecond accuracy for all catalog rows;
it accommodates catalog rounding/reference differences. Only these two independent
reference positions are tested so far; no SIMBAD database dump is redistributed.

Projection checks compare Astropy WCS TAN with an independently written analytic gnomonic
formula at Betelgeuse, check the center, and verify east-left/north-up orientation.
Astropy's FITS projection documentation:
https://docs.astropy.org/en/stable/wcs/supported_projections.html

Additional tests verify repeated file hashes, geometry invariance across presets, catalog
IDs, valid edge references, output hashes and unsupported-request rejection. A preview
was inspected for direction, line topology and label crowding. Full reference-chart
comparison, complete-catalog validation and editorial/print-size review remain release gates.

## Expanded backend (renderer 0.2.0)

The bright HIP catalog retains 8,870 stars with magnitude <=6.5, their proper motions and
source provenance. Constellation edges are checked against each endpoint's catalog
constellation. Summer Triangle identity tests explicitly require Vega/HIP 91262,
Deneb/HIP 102098 and Altair/HIP 97649, with exactly three distinct asterism edges.

Observer tests cover north/west azimuth wrapping, horizon clipping, changes across global
locations, southern-hemisphere Polaris below the horizon, time-dependent Polaris altitude
and the distinction from ideal NCP latitude. The observer transform also reproduces the
published Astropy Bear Mountain/M33 example: RA 23.46206906°, Dec 30.66017511°,
2012-07-13 03:00 UTC, latitude 41.3°, longitude -74°, elevation 390 m, expected altitude
0.13° within 0.01°. Source:
https://docs.astropy.org/en/stable/coordinates/example_gallery_plot_obs_planning.html
This is a published library benchmark, not a second independent astronomy engine. The
synthetic M33 target is test data; the public renderer does not claim M33 support.

Numeric tests independently derive spherical angular separation, check the 45° ray/arc
relationship and verify all four azimuth cardinal directions. English/Albanian labels and
visual presets preserve geometry. DST tests exercise the 2026 spring gap and autumn overlap,
including explicit earlier/later UTC choices. Dates outside bundled IERS coverage fail.

Scientific assumptions: HYG J2000 coordinates interpreted as ICRS; angular proper motion;
no stellar parallax/perspective acceleration; WGS84 observer; zero-pressure/unrefracted AltAz;
no terrain/horizon dip. IERS version/hash/status and resolved UTC are stored in manifests;
local clock resolution uses the locked tzdata package. Large-area TAN requests that cannot
project finitely are rejected rather than distorted into a plausible result.

These checks do not establish all-sky astrometric accuracy. Broader independent planetarium
comparisons at multiple dates/locations and final line-set/editorial review remain gates.
