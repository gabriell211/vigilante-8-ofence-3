import pathlib
import struct
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "scripts"))
from export_legacy_geometry import Tmd, read_tmds, decode_faces, export_model


def build_sample(mode=0x20, truncated=False):
    vertices = b"".join(struct.pack("<hhhH", *point, 0) for point in
                        ((0, 0, 0), (256, 0, 0), (0, 0, 256)))
    face = bytearray((200, 100, 50, mode))
    face.extend(struct.pack("<HHH", 0, 8, 16))
    face.extend(b"\0" * (16 - len(face)))
    if truncated:
        face = face[:9]
    yaml = ("tmdList:\n"
            "  - vertices: 3\n"
            "    flag: 0\n"
            "    normals: 0\n"
            "    faces: 1\n"
            f"    vertexStream: {vertices.hex()}\n"
            "    normalStream: \n"
            f"    faceStream: {face.hex()}\n"
            "  anotherField: []\n")
    return yaml


class LegacyGeometryTests(unittest.TestCase):
    def test_read_real_ps1_vertex_stream_format(self):
        parts = read_tmds(build_sample())
        self.assertEqual(len(parts), 1)
        self.assertEqual(parts[0].count, 3)

    def test_export_authentic_triangle_without_primitive_stand_in(self):
        obj, stats = export_model(build_sample(), "v82_test_vehicle")
        self.assertIn("v 1.000000 0.000000 0.000000", obj)
        self.assertIn("f 1 2 3", obj)
        self.assertEqual(stats["triangles"], 1)
        self.assertFalse(stats["prefabReady"])

    def test_reject_truncated_face_record(self):
        with self.assertRaises(ValueError):
            export_model(build_sample(truncated=True), "v82_test_vehicle")

    def test_reject_unknown_source_geometry(self):
        with self.assertRaises(ValueError):
            read_tmds("not an original XOBF prefab")

    def test_reject_vertex_offset_outside_source_mesh(self):
        part = Tmd(3, 1, bytes(24),
                   bytes((0, 0, 0, 0x20)) + struct.pack("<HHH", 0, 8, 64) + bytes(6))
        with self.assertRaises(ValueError):
            decode_faces(part)


if __name__ == "__main__":
    unittest.main()
