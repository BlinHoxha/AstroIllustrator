import json
import unittest
from datetime import datetime, timedelta
from astropy.time import Time
from astropy.utils import iers

from astro_renderer.capabilities import capabilities


class CapabilityTests(unittest.TestCase):
    def test_reported_interval_is_conservative_and_uses_offline_runtime_data(self):
        report = capabilities()
        coverage = report["observationCoverage"]
        start = datetime.fromisoformat(coverage["startUtcInclusive"])
        end = datetime.fromisoformat(coverage["endUtcExclusive"])
        self.assertLess(start, end)
        for instant in [start, start + (end-start)/2, end-timedelta(seconds=1)]:
            _, status = Time(instant).get_delta_ut1_utc(return_status=True)
            self.assertGreaterEqual(int(status), 0)
        self.assertFalse(iers.conf.auto_download)
        self.assertEqual(64, len(coverage["iersSha256"]))
        self.assertTrue(coverage["timeZoneDataVersion"])

    def test_json_capabilities_describe_actual_supported_scenes(self):
        report = json.loads(json.dumps(capabilities(), allow_nan=False))
        self.assertEqual("2.1", report["schemaVersion"])
        self.assertEqual(5, len(report["constellationCodes"]))
        self.assertEqual(["SummerTriangle"], report["asterismCodes"])
        self.assertEqual(["svg", "png", "manifest"], report["outputFormats"])
        modes = {d["code"]: d["modes"] for d in report["diagramTemplates"]}
        self.assertEqual(["Computed"], modes["AngularSeparation"])
        self.assertEqual(["Schematic"], modes["AltitudeAzimuth"])


if __name__ == "__main__":
    unittest.main()
