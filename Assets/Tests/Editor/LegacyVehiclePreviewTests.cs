using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    public sealed class LegacyVehiclePreviewTests
    {
        [Test]
        public void AllEighteenAuthenticSourceObjFilesAreImported()
        {
            string directory = "Assets/GameContent/TexturedPreviewV82";
            string[] files = Directory.GetFiles(directory, "v82_*.obj");
            Assert.That(files.Length, Is.EqualTo(18), "No generic or missing source vehicle geometry");
            foreach (string file in files)
            {
                string unityPath = file.Replace('\\', '/');
                GameObject originalModel = AssetDatabase.LoadAssetAtPath<GameObject>(unityPath);
                Assert.That(originalModel, Is.Not.Null, unityPath);
                MeshFilter[] meshes = originalModel.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(meshes.Any(x => x.sharedMesh != null && x.sharedMesh.vertexCount >= 3),
                    Is.True, "Empty imported original source geometry: " + unityPath);
            }
        }

        [Test]
        public void PreviewPrefabsMustNotBeMistakenForRuntimeOfficialCars()
        {
            var previewGuids = AssetDatabase.FindAssets("t:Prefab",
                new[] { "Assets/GameContent/PreviewPrefabsV82" });
            foreach (var guid in previewGuids)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(prefab.GetComponent<Vigilante8.Content.OfficialAssetIdentity>(), Is.Null);
                Assert.That(prefab.GetComponent<Vigilante8.Gameplay.VehicleDrive>(), Is.Null);
                Assert.That(prefab.GetComponent<OriginalVehiclePreviewIdentity>(), Is.Not.Null);
            }
        }
    }
}
