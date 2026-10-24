import copy
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import tempfile
import unittest

import astropy.units as u
from astropy.coordinates import SkyCoord
import numpy as np

from astro_renderer.__main__ import render, CATALOGS
from astro_renderer.charts import chart_scene
from astro_renderer.diagrams import diagram_scene
from astro_renderer.astronomy import observer_positions
from astro_renderer.contracts import validate, resolve_time, ScientificError

ROOT=Path(__file__).resolve().parents[2]

def example(name):
    return json.loads((ROOT/'examples'/f'{name}.json').read_text())

class ExtendedSceneTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog=json.loads((CATALOGS/'bright-hyg-v41.json').read_text())
        cls.definitions=json.loads((CATALOGS/'scene-definitions-v1.json').read_text())

    def test_every_example_exports_with_valid_geometry(self):
        with tempfile.TemporaryDirectory() as tmp:
            for path in (ROOT/'examples').glob('*.json'):
                with self.subTest(path=path.name):
                    result=render(json.loads(path.read_text()),Path(tmp)/path.stem)
                    manifest=json.loads((Path(tmp)/path.stem/'manifest.json').read_text())
                    self.assertEqual(result['geometryHash'],manifest['geometryHash'])
                    self.assertEqual(len(result['outputs']),3)

    def test_summer_triangle_uses_exact_identities_and_distinct_edges(self):
        geometry,_=chart_scene(example('summer-triangle'),self.catalog,self.definitions)
        names={p['hip']:p['name'] for p in geometry['points']}
        self.assertEqual([names[i] for i in [91262,102098,97649]],['Vega','Deneb','Altair'])
        self.assertEqual({tuple(sorted(e)) for e in geometry['asterismEdges']},{(91262,102098),(97649,102098),(91262,97649)})
        self.assertEqual(len(names),len(geometry['points']))
        self.assertTrue(all(set(e)<=set(names) for e in geometry['edges']))

    def test_all_constellation_edge_identities_belong_to_expected_constellations(self):
        stars={s['hip']:s for s in self.catalog['stars']}
        for definition in self.definitions['constellations']:
            for edge in definition['edges']:
                self.assertTrue(all(stars[i]['constellation']==definition['code'] for i in edge))

    def test_polar_chart_is_finite_and_contains_polaris(self):
        geometry,_=chart_scene(example('ursa-minor'),self.catalog,self.definitions)
        self.assertIn(11767,{p['hip'] for p in geometry['points']})
        self.assertTrue(all(math.isfinite(p['x']) and math.isfinite(p['y']) for p in geometry['points']))

    def test_forty_five_degrees_is_drawn_as_forty_five_degrees(self):
        geometry,_=diagram_scene(example('altitude-45'),self.catalog)
        x,y=geometry['rayEnd']
        self.assertAlmostEqual(math.degrees(math.atan2(y,x)),45,places=12)
        self.assertEqual(geometry['arcEndDeg'],45)
        self.assertEqual(geometry['zenithDistanceDeg'],45)

    def test_azimuth_is_clockwise_from_north(self):
        request=example('altitude-azimuth')
        for az,expected in [(0,[0,1]),(90,[1,0]),(180,[0,-1]),(270,[-1,0])]:
            request['scientificDiagram']['parameters']['azimuthDegrees']=az
            geometry,_=diagram_scene(request,self.catalog)
            np.testing.assert_allclose(geometry['azimuthRayEnd'],expected,atol=1e-12)

    def test_polaris_computed_is_not_ncp_latitude_and_varies_with_time(self):
        request=example('polaris-computed')
        _,a=diagram_scene(request,self.catalog)
        request['scientificDiagram']['timeUtc']='2026-07-16T03:00:00Z'
        _,b=diagram_scene(request,self.catalog)
        self.assertGreater(abs(a['altitudeDeg']-a['northCelestialPoleAltitudeDeg']),0.01)
        self.assertGreater(abs(a['altitudeDeg']-b['altitudeDeg']),0.05)
        self.assertLess(abs(a['altitudeDeg']-42.6629),1)

    def test_south_hemisphere_marks_polaris_below_horizon(self):
        request=example('polaris-computed')
        request['scientificDiagram']['observer']=dict(latitude=-33.8688,longitude=151.2093,elevationMeters=0)
        _,science=diagram_scene(request,self.catalog)
        self.assertLess(science['altitudeDeg'],0)
        self.assertTrue(science['warnings'])

    def test_horizon_clip_and_wrap_at_north_and_west(self):
        request=example('southern-horizon')
        for center in [0,180,270]:
            request['skyChart']['azimuthCenterDegrees']=center
            geometry,science=chart_scene(request,self.catalog,self.definitions)
            self.assertGreater(science['belowHorizonCount'],0)
            self.assertTrue(geometry['points'])
            for p in geometry['points']:
                self.assertGreaterEqual(p['altitudeDeg'],0)
                self.assertLessEqual(abs(p['x']),50)
                self.assertAlmostEqual((p['x']+center)%360,p['azimuthDeg'],places=8)

    def test_global_observer_changes_positions(self):
        star=[s for s in self.catalog['stars'] if s['hip']==91262]
        payload=example('southern-horizon')['skyChart']
        a,_,_=observer_positions(star,payload)
        payload=copy.deepcopy(payload)
        payload['observer']=dict(latitude=-33.8688,longitude=151.2093,elevationMeters=0)
        b,_,_=observer_positions(star,payload)
        self.assertGreater(abs(a[0]-b[0]),10)

    def test_observer_transform_matches_published_astropy_bear_mountain_example(self):
        # Independent published benchmark; this synthetic validation target is not
        # a bundled catalog star or a supported public M33 rendering capability.
        target=[dict(raDeg=23.46206906,decDeg=30.66017511,pmRaCosDecMasYr=0,pmDecMasYr=0)]
        payload=dict(observer=dict(latitude=41.3,longitude=-74,elevationMeters=390),timeUtc='2012-07-13T03:00:00Z')
        altitude,_,_=observer_positions(target,payload)
        self.assertAlmostEqual(float(altitude[0]),0.13,delta=0.01)

    def test_out_of_earth_orientation_coverage_fails_instead_of_extrapolating(self):
        request=example('polaris-computed')
        request['scientificDiagram']['timeUtc']='2030-07-15T21:00:00Z'
        with self.assertRaisesRegex(ScientificError,'IERS'):
            diagram_scene(request,self.catalog)

    def test_albanian_labels_do_not_change_scientific_geometry(self):
        with tempfile.TemporaryDirectory() as tmp:
            request=example('altitude-45')
            a=render(request,Path(tmp)/'en')
            request['language']='sq'
            b=render(request,Path(tmp)/'sq')
            self.assertEqual(a['geometryHash'],b['geometryHash'])
            svg=(Path(tmp)/'sq'/'chart.svg').read_text(encoding='utf-8')
            self.assertIn('Zeniti',svg)
            self.assertNotIn('>Zenith<',svg)

    def test_angular_separation_matches_independent_spherical_cosine(self):
        geometry,science=diagram_scene(example('angular-separation'),self.catalog)
        stars={s['hip']:s for s in self.catalog['stars']}
        a,b=stars[91262],stars[102098]
        ra1,dec1,ra2,dec2=map(math.radians,[a['raDeg'],a['decDeg'],b['raDeg'],b['decDeg']])
        expected=math.degrees(math.acos(math.sin(dec1)*math.sin(dec2)+math.cos(dec1)*math.cos(dec2)*math.cos(ra1-ra2)))
        self.assertAlmostEqual(science['angularSeparationDeg'],expected,places=10)
        self.assertAlmostEqual(geometry['angleDeg'],expected,places=10)

    def test_local_time_dst_and_utc_are_equivalent(self):
        payload=example('southern-horizon')['skyChart']
        self.assertEqual(resolve_time(payload),datetime(2026,7,15,21,tzinfo=timezone.utc))
        reference=payload['timeReference']
        reference['localDateTime']='2026-03-29T02:30:00'
        with self.assertRaises(ScientificError): resolve_time(payload)
        reference['localDateTime']='2026-10-25T02:30:00'
        with self.assertRaises(ScientificError): resolve_time(payload)
        reference['ambiguityPolicy']='Earlier'
        self.assertEqual(resolve_time(payload),datetime(2026,10,25,0,30,tzinfo=timezone.utc))
        reference['ambiguityPolicy']='Later'
        self.assertEqual(resolve_time(payload),datetime(2026,10,25,1,30,tzinfo=timezone.utc))

    def test_invalid_payloads_and_injected_positions_fail(self):
        request=example('polaris-computed')
        request['scientificDiagram']['parameters']['altitudeDegrees']=42
        with self.assertRaises(ScientificError): validate(request)
        request=example('summer-triangle')
        request['skyChart']['observer']=dict(latitude=42,longitude=21)
        with self.assertRaises(ScientificError): validate(request)

    def test_diagram_and_horizon_geometry_survive_style_change(self):
        with tempfile.TemporaryDirectory() as tmp:
            for name in ['southern-horizon','polaris-computed','altitude-azimuth']:
                request=example(name)
                a=render(request,Path(tmp)/f'{name}-dark')
                request['preset']='PrintMonochrome'
                b=render(request,Path(tmp)/f'{name}-mono')
                self.assertEqual(a['geometryHash'],b['geometryHash'])

if __name__=='__main__': unittest.main()
