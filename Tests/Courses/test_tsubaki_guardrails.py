"""Run with python -m unittest discover -s Tests/Courses -p test_tsubaki_guardrails.py."""
import json
from pathlib import Path
import struct
import sys
import unittest

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'Tools'))
from tsubaki_guardrails import FIRST, LAST, rail_segments

ROOT = REPO / 'RuntimeAssets/TSUBAKI'


class TsubakiGuardrails(unittest.TestCase):
    def test_all_weather_beams_have_identical_geometry(self):
        reference = None
        for variant in ('', 'day_wet', 'night_dry', 'night_wet'):
            geometry = {tuple(sorted(tuple(round(v, 4) for v in p) for p in (a, b)))
                        for a, b, _, _ in rail_segments(ROOT / variant)}
            if reference is None:
                reference = geometry
            self.assertEqual(geometry, reference, variant)

    def test_center_heights_and_unaffected_edges_preserved(self):
        b = (ROOT / 'road.bin').read_bytes()
        count = struct.unpack_from('<I', b, 4)[0]
        source = [[struct.unpack_from('<3f', b, 8 + (s * count + i) * 12)
                   for i in range(count)] for s in range(3)]
        converted = []
        for suffix in ('', '_l', '_r'):
            path = (ROOT / ('tsubaki_path' + suffix + '.bin')).read_bytes()
            self.assertEqual(struct.unpack_from('<II', path), (count, 3))
            converted.append(list(struct.iter_unpack('<3f', path[8:])))
        self.assertEqual(converted[0], source[0])
        for side in converted[1:]:
            original = next(s for s in source[1:] if s[0] == side[0])
            self.assertEqual([p[1] for p in side], [p[1] for p in original])
            self.assertEqual(side[:FIRST], original[:FIRST])
            self.assertEqual(side[LAST + 1:], original[LAST + 1:])

    def test_collision_indices_and_endpoints_fit_native_format(self):
        b = (ROOT / 'tsubaki.rcl').read_bytes()
        header = struct.unpack_from('<12I', b)
        vertices, triangles, at = header[4], header[6], header[7]
        self.assertLess(vertices, 32768)
        self.assertLess(triangles, 32768)
        self.assertGreater(vertices, 4112 * 4)  # Extra rail-end cross sections.
        for i in range(triangles):
            t = struct.unpack_from('<8h', b, at + i * 16)
            self.assertTrue(all(0 <= v < vertices for v in t[:3]))
            self.assertTrue(all(-1 <= n < triangles for n in t[3:6]))
        manifest = json.loads((ROOT / 'scene.json').read_text())
        self.assertEqual(manifest['checkpoints'], [164, 1108, 2076, 2996, 3820])


if __name__ == '__main__':
    unittest.main()
