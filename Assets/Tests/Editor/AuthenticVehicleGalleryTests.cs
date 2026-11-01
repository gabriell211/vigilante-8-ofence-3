using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.EditorTests
{
    public sealed class AuthenticVehicleGalleryTests
    {
        [Test]
        public void EveryPreviewModelHasItsOwnSourceIdentityAndMesh()
        {
            var roster = CatalogRepository.Load().vehicles.Where(v => v.game == "v82").ToArray();
            Assert.That(roster.Length, Is.EqualTo(18));
            foreach (var vehicle in roster)
            {
                string path = "Assets/GameContent/PreviewPrefabsV82/" +
                              vehicle.id + ".prefab";
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null)
                    Assert.Inconclusive("Unity Editor has not generated inspection prefabs: " + path);
                var identity = source.GetComponent<Vigilante8.Editor.OriginalVehiclePreviewIdentity>();
                Assert.That(identity, Is.Not.Null);
                Assert.That(identity.VehicleId, Is.EqualTo(vehicle.id));
                Assert.That(source.GetComponentsInChildren<MeshFilter>(true)
                    .Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount >= 3), Is.True);
                Assert.That(source.GetComponent<OfficialAssetIdentity>(), Is.Null);
            }
        }

        [Test]
        public void InspectionSceneCannotShipAsPlayableBuildScene()
        {
            const string inspection = "Assets/Scenes/OriginalVehicleReview.unity";
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == inspection), Is.False);
        }
    }
}
