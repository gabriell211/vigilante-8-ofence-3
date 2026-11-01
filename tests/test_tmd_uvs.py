import pathlib
import struct
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "scripts"))
from export_legacy_uvs import mapped_uvs, build_preview


def test_source():
    verts = b"".join(struct.pack("<hhhH", *v, 0)
                     for v in [(0,0,0), (256,0,0), (0,0,256)])
    face = bytearray(24)
    face[:4] = bytes((255,255,255,0x14))
    struct.pack_into("<HHH", face, 4, 0,8,16)
    face[12:14] = bytes((0,0))
    face[16:18] = bytes((31,0))
    face[20:22] = bytes((0,31))
    face[22] = 0
    return ("tmdList:\n"
            "  - vertices: 3\n    flag: 0\n    normals: 0\n"
            "    faces: 1\n    vertexStream: "+verts.hex()+"\n"
            "    normalStream: \n    faceStream: "+face.hex()+"\n"
            "  lastField: []\n")


class TexturePreviewTests(unittest.TestCase):
    def test_direct_ps1_material_id_is_associated_with_real_guid(self):
        path = "Assets/GameContent/LegacyTextureBank/SHARED/COMMON/Textures/test.bmp"
        obj, mtl, report = build_preview(test_source(), "v82_test", ["a"*32],
                                          {"a"*32: {"width":32,"height":32,"path":path}})
        self.assertIn("usemtl texture_000", obj)
        self.assertIn("vt 1.000000 1.000000", obj)
        self.assertIn("map_Kd ../LegacyTextureBank/", mtl)
        self.assertEqual(report["triangles"], 1)
        self.assertEqual(report["mappedOriginalBitmapMaterials"], 1)

    def test_missing_genuine_guid_rejected(self):
        with self.assertRaises(ValueError):
            build_preview(test_source(),"v82_test",["a"*32],{})

    def test_uv_orientation(self):
        record = bytearray(24)
        record[12:14] = bytes((0,0))
        record[16:18] = bytes((31,0))
        record[20:22] = bytes((0,31))
        self.assertEqual(mapped_uvs(record,12,32,32,False),
                         [(0.0,1.0),(1.0,1.0),(0.0,0.0)])


if __name__ == "__main__":
    unittest.main()
