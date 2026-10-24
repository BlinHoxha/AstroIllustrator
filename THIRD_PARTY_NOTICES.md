# Scientific data and dependencies

## HYG v4.1 Orion and bright-star subsets

Copyright David Nash / Astronexus and contributors.
Source: https://github.com/astronexus/HYG-Database
Pinned upstream commit: `3bf37f4b2d5460e1278286320d1d62fab9b493c1`.
License: Creative Commons Attribution-ShareAlike 4.0 International.
License text and upstream documentation are preserved beside the catalog.

Changes: select constellation Ori, magnitude <=6.5 and nonempty HIP identifiers; retain
names, HIP identifiers, magnitudes and equatorial coordinates; convert RA hours to degrees.
The derived data snapshot is distributed under CC BY-SA 4.0. Retain attribution and
identify changes when sharing it. Commercial use does not remove attribution/share-alike
obligations; review those obligations for distributed chart derivatives and atlas publication.

Coordinates use epoch/equinox J2000.0 as documented upstream. Provenance includes the
upstream CSV hash, commit, selection and local catalog hash in each render manifest.
Line drawings are a separately documented original minimal hourglass convention.

Renderer 0.2.0 additionally bundles `bright-hyg-v41.json`: all 8,870 entries with a HIP
identifier and magnitude <=6.5. It retains constellation codes, proper motions, distance
and radial velocity fields. The runtime currently uses angular proper motion only; keeping
distance/velocity provenance does not imply that perspective/parallax corrections are applied.
`scene-definitions-v1.json` contains original minimal constellation guides and the
Vega/Deneb/Altair asterism overlay; none are claimed to be IAU boundary definitions.

## Runtime dependencies

Python direct dependencies: Astropy, NumPy and Matplotlib; transitive dependencies are
captured in the environment lock. The renderer uses Matplotlib's DejaVu Sans font.
Preserve upstream notices and review font distribution/embedding terms for print packaging.
.NET dependencies include Microsoft EF Core SQLite, Microsoft.Extensions components and
their transitive SQLite native assets. This file is an inventory, not a replacement for
the licenses shipped in those packages. No application-wide license is assigned here.
