import pathlib
import sys
import tempfile
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "scripts"))
from verify_unity_mesh_sources import parse_source_obj, valid_float


class OriginalObjContractTests(unittest.TestCase):
    def test_reject_nonfinite_position(self):
        with self.assertRaises(ValueError):
            valid_float("NaN")

    def test_reject_face_with_missing_original_uv(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / "source.obj"
            path.write_text(
                "g v82_test_tmd_000\n"
                "v 0 0 0\nv 1 0 0\nv 0 0 1\n"
                "usemtl texture_000\nf 1/1 2/2 3/3\n", encoding="utf-8")
            with self.assertRaises(ValueError):
                parse_source_obj(path)

    def test_source_requires_real_material(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / "source.obj"
            path.write_text("g v82_test_tmd_000\nv 0 0 0\nv 1 0 0\nv 0 0 1\nf 1 2 3\n")
            with self.assertRaises(ValueError):
                parse_source_obj(path)
