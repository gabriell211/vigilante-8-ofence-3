import pathlib
import sys
import unittest

sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/"scripts"))
from probe_legacy_stage_geometry import analyze_scene

class StageGeometryProbeTests(unittest.TestCase):
    def test_source_has_actual_serialized_tmd_streams(self):
        data=(b"%YAML 1.1\n  tmdList:\n  - vertices: 1\n"
              b"    vertexStream: 01020304\n    faceStream: 0102\n")
        result=analyze_scene(data)
        self.assertTrue(result["hasRawTmdGeometry"])
        self.assertEqual(result["tmdListBlocks"],1)
        self.assertEqual(result["tmdVertexBuffers"],1)

    def test_mesh_reference_alone_is_not_source_tmd(self):
        result=analyze_scene(b"  m_Mesh: {fileID: 444, guid: aaa, type: 3}\n")
        self.assertFalse(result["hasRawTmdGeometry"])
        self.assertEqual(result["meshFilterReferences"],1)
