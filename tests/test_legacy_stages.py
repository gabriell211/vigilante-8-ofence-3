import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]/"scripts"))
from audit_legacy_stages import inspect_unity_scene


class StageSourceTests(unittest.TestCase):
    def test_accepts_serialized_scene_with_real_objects(self):
        stage = (b"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
                 b"--- !u!1 &12\nGameObject:\n  m_Name: TEST\n"
                 b"--- !u!4 &24\nTransform:\n  m_GameObject: {fileID: 12}\n"
                 b"guid: 0123456789abcdef0123456789abcdef\n")
        info = inspect_unity_scene(stage)
        self.assertEqual(info["gameObjects"], 1)
        self.assertEqual(info["transforms"], 1)
        self.assertEqual(info["sceneReferencedAssetGuids"], 1)

    def test_rejects_unity_scene_without_real_objects(self):
        with self.assertRaises(ValueError):
            inspect_unity_scene(b"%YAML 1.1\n" + b" " * 100)

    def test_rejects_unity_scene_without_transforms(self):
        with self.assertRaises(ValueError):
            inspect_unity_scene(b"%YAML 1.1\n--- !u!1 &4\nGameObject:\n" + b" " * 101)
