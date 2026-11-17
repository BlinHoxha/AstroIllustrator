# Atlas terminology catalogue v1

This is editorial reference data for the Albanian astronomy atlas. It is not read by the renderer in AST-TERM-001. The 88 constellations and 103 named stars are extensible reference sets; they do not limit the scientific star catalogue. The catalogue is at [terminology-v1.json](../renderer/catalogs/terminology-v1.json).

## Three layers and approval

Each term has an immutable identifier, an international name where one is recorded, and an Albanian display name with provenance and status. The statuses are `attested`, `proposed`, `calque`, `international`, `approved`, and `rejected`. Renderers may show an Albanian display name only when its status is `approved`; otherwise they fall back to the international name. For standalone figure terms without a supplied international equivalent, the catalogue records `null` and must not invent one.

The owner is the final editorial approver. An approval decision is quoted in an EngineeringCoordination task record, then a separate approved task updates the catalogue. The Wave 1 approvals are dated 17 November 2026 and have `approvedBy`, `approvedOn`, and `approvalSource` fields. Existing renderer strings remain unchanged in this task.

For `reference` and `ray`, identified stars use their catalogue names (`labelRule: identifiedStarCatalogueName`). Abstract diagrams use "Objekti 2" for `reference` and "Objekti 1" for `ray`. Their full `approvedValue` remains the recorded Wave 1 rule; `displayName` is null so the English rule sentence cannot appear as a figure label.

## Distinct entities

The 88 IAU constellations are separate from named asterisms. The 13 astronomical zodiac constellations are IAU constellations crossed by the ecliptic; dates for the Sun in each require computed ephemerides. The 12 astrological signs are equal 30° sectors from the March equinox, shifted from their similarly named constellations by precession. They are `scientific: false` and belong only in an explicitly non-scientific comparison diagram. D-6 figure wording is still proposed, not approved.
The D-6 proposal is "yjësia Akrepi (IAU: Sco)" for the IAU constellation and "shenja astrologjike Akrepi" for the astrological sign. These labels remain proposed.

## Approved Wave 1 wording

The following sections reproduce the owner's approved values exactly as recorded in the coordination task. W1-E and F-1 remain open and unpublished.

### W1-A Conventions

| # | Rule |
| --- | --- |
| A1 | Constellation titles standing alone use the definite form ("Orioni", "Arusha e Vogël"); sentences inflect freely. |
| A2 | Multi-word proper names capitalise every word except particles ("Arusha e Vogël", "Trekëndëshi Veror"); general terms are lowercase ("poli qiellor verior", "rrethpolar") except at the start of a label. |
| A3 | Star labels are the catalogue name from `bright-hyg-v41.json`, uninflected. Never invent a star identity. Polaris: canonical scientific label "Polaris"; first educational mention "Polaris (Ylli Polar)" (Kap. 1 §1.8); "Polari" is an Albanian prose/search alias only, never the primary figure label. |
| A4 | Displayed angles: one decimal with an Albanian decimal comma ("42,7°"); full precision in manifests and calculations. |
| A5 | Dates: "15 janar 2027"; months lowercase: janar, shkurt, mars, prill, maj, qershor, korrik, gusht, shtator, tetor, nëntor, dhjetor. |
| A6 | Time: 24-hour clock, local time first, then UTC: "15 janar 2027, ora 21:00 (CET, UTC+1) = 20:00 UTC"; summer "(CEST, UTC+2)". |
| A7 | Calculations use the verified IANA time-zone rules (tzdb has no `Europe/Pristina`; Kosovo maps to `Europe/Belgrade`, verified with tzdata 2026e). Dated figures display the Albanian location name, CET/CEST and the resolved UTC offset; the IANA identifier appears in the manifest only. |
| A8 | Albanian location name "Prishtina". "42,7° V, 21,2° L, 600 m" are representative display values, shown only when they are the display rounding of the actual observation parameters. Calculations always use the full-precision request parameters, never display-rounded values. |

### W1-B Existing renderer strings

(`localization.py`, `sq`; 22 strings; approved values)

| # | Key | Current (f0f7007) | Approved value |
| --- | --- | --- | --- |
| 1 | horizon | Horizonti | Horizonti |
| 2 | altitude | Lartësia këndore | Lartësia këndore |
| 3 | azimuth | Azimuti | Azimuti |
| 4 | zenith | Zeniti | Zeniti |
| 5 | reference | Referenca | Identified stars: the star's catalogue name (A3). Abstract diagrams: "Objekti 2" |
| 6 | separation | Ndarja këndore | Distanca këndore |
| 7 | schematic | Gjeometri numerike skematike | Gjeometri numerike skematike |
| 8 | computed | Polaris — pozicioni i llogaritur | Polaris (Ylli Polar) — pozicioni i llogaritur |
| 9 | teaching | Poli qiellor verior dhe gjerësia — skemë mësimore | Poli qiellor verior dhe gjerësia gjeografike — skemë mësimore |
| 10 | static | J2000 \| Veriu lart / Lindja majtas \| TAN | J2000 \| Veriu lart, lindja majtas \| Projeksion gnomonik (TAN) |
| 11 | summer | Trekëndëshi veror | Trekëndëshi Veror |
| 12 | geometric | Horizont gjeometrik \| Pa përthyerje atmosferike | Horizont gjeometrik \| Pa përthyerje atmosferike |
| 13 | noRefraction | Pa përthyerje atmosferike | Pa përthyerje atmosferike |
| 14 | ncp | Lartësia ideale e polit qiellor verior = gjerësia. Nuk është lartësia e saktë e Polaris. | Lartësia ideale e polit qiellor verior = gjerësia gjeografike. Nuk është lartësia e saktë e Polarisit. |
| 15 | zenithDistance | Largësia zenitale = 90° − lartësia | Distanca zenitale = 90° − lartësia |
| 16 | clockwise | Në drejtim të akrepave nga veriu | Nga veriu, në drejtim të akrepave të orës |
| 17 | ray | Rrezja 1 | Identified stars: the star's catalogue name (A3). Abstract diagrams: "Objekti 1" |
| 18 | separationNote | Skemë këndore; rrezet tregojnë ndarjen e matur, jo projeksion qiellor. | Skemë këndore; rrezet tregojnë distancën këndore të matur, jo projeksion të qiellit. |
| 19-22 | cardinals | V, L, J, P | V, L, J, P (Kap. 1 §1.3) |

### W1-C Figure terms

Standardised terms (owner amendment): "Distanca këndore", "Distanca zenitale", "Lartësia
këndore", "Përthyerja atmosferike", "poli qiellor verior". Also approved: "Ora lokale"; "UTC"
(in text "Koha universale e koordinuar (UTC)"); zone labels "CET, UTC+1" / "CEST, UTC+2";
"Shkalla këndore"; "Magnituda e dukshme"; "Magnituda kufi" (for example "Magnituda kufi: 6,0");
schematic badge "Skemë — jo në shkallë"; "Burimi i të dhënave:"; "Metoda:"; "Epoka J2000";
"Të dhënat e orientimit të Tokës (IERS)"; "Përfshin vlera të parashikuara IERS"; compass
letters "V" (veriu), "L" (lindja); "rrethpolar" / "rrethpolare", always with the latitude
("rrethpolare për 42,7° V"); "lartësia mbidetare"; abstract-object labels "Objekti 1",
"Objekti 2".

### W1-D Constellations, asterism and stars

- Constellations: ORI (Ori, Orion) "Orioni"; UMI (UMi, Ursa Minor) "Arusha e Vogël";
  LYR (Lyr, Lyra) "Lira"; CYG (Cyg, Cygnus) "Mjellma"; AQL (Aql, Aquila) "Shqiponja".
- Asterism: `SUMMER_TRIANGLE` (Vega 91262, Deneb 102098, Altair 97649) "Trekëndëshi Veror".
- Star: HIP 11767 "Polaris", first educational mention "Polaris (Ylli Polar)", alias "Polari"
  (`use: prose, search`).
- Stars keeping catalogue names (written `approved`, display name equal to the IAU name):
  Betelgeuse 27989, Rigel 24436, Bellatrix 25336, Saiph 27366, Alnitak 26727, Alnilam 26311,
  Mintaka 25930, Kochab 72607, Pherkad 75097, Vega 91262, Deneb 102098, Altair 97649.
- Rule approved for stars added by AST-ATLAS-001's brightness rule (for example Sirius 32349,
  Capella 24608): catalogue name unchanged. Their Appendix B entries stay `international`,
  which renders the same name; they are not individually approved.
- All app codes, IAU abbreviations and HIP numbers are preserved unchanged.

## Open decisions and follow-ups

### W1-E Unresolved implementation details (not approved)

(not terminology; not approved)

- U-1 Which star of `starHipIds` labels which ray of the AngularSeparation diagram (proposal:
  index 0 on the lower ray, index 1 on the reference ray). Decided in AST-ATLAS-001.
- U-2 No abstract separation diagram exists today: `diagrams.py` at f0f7007 requires two
  catalogue HIP ids, and unnamed catalogue stars are already named "HIP <n>". "Objekti 1/2" is
  reserved for future abstract scenes.
- U-3 Applying W1-B to `localization.py` and its tests (`test_extended_scenes.py` asserts
  "Zeniti"), with geometry-hash invariance: AST-ATLAS-001, not this task.
- U-4 Ambiguous local times at the October DST change (02:00-03:00): explicit fold choice and
  printed resolved offset; AST-ATLAS-001.
- U-5 Label for observations whose parameters are not Prishtina's (A8): proposal, place name
  only when the parameters match; otherwise coordinates only. Owner decision pending.
- U-6 English-figure spelling "Prishtina" or "Pristina": owner decision pending.
- U-7 Manuscript alignment (Kap. 1 / Draft 01 use "refraksioni", "Lartësia (h)", "Poli
  Qiellor i Veriut", "cirkumpolare", "Polari"): author's edits outside AstroIllustrator; no
  task.

### F-1 Bilingual manifest titles (not published)

Owner decision 2026-11-17: leave unchanged the five hard-coded bilingual
`scientificResults.title` values at f0f7007: "Horizon / Horizonti" (`charts.py:56`), "Angular
separation / Ndarja këndore" (`diagrams.py:24`), "Polaris — computed / e llogaritur"
(`diagrams.py:32`), "NCP latitude teaching diagram / Skemë mësimore" (`diagrams.py:36`),
"Altitude / Lartësia këndore — schematic" (`diagrams.py:41`). Recorded inconsistencies:
"Ndarja këndore" versus approved "Distanca këndore"; the others are fragments, not catalogue
terms. Changing them alters manifests and the reproducibility invariants, so a separate
proposal must assess that impact first. Not in this task's scope.

### Other open decisions

- D-4 Seasonal convention (evening time and date per season); four seasonal maps first, twelve
  monthly maps later (owner decision 2026-11-17).
- D-6 Zodiac and sign labelling rule (proposal above).
- D-7 Location: resolved 2026-11-17. `renderer/catalogs/terminology-v1.json` and
  `docs/atlas-terminology.md` are the approved locations for the initial catalogue (owner); a
  future manuscript repository may become the source of truth (no repository created or
  moved).
- U-5, U-6 (section W1-E) and F-1 (follow-up proposal, unpublished).
- D-9 Chapter numbering: the file "Kapitulli 1" covers Draft 01 outline chapters 1-3 and
  previews 5 and 6; numbering stays an open author decision. No chapter text is invented.
- P-3 HYG v4.1 licensing (CC BY-SA 4.0) unresolved: internal development only; no commercial
  publication approval.
