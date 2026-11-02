using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;
using Vigilante8.Editor;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class LegacyPlayableVehicleIntegrationTests
    {
        [OneTimeSetUp]
        public void GenerateOriginalSourcePhysicsVehicles()
        {
            // This is a real Unity Editor integration test. It performs import,
            // mesh/material assembly and prefab serialization, unlike the
            // source-only Python/Roslyn CI.
            LegacyVehiclePlaytestBuilder.PrepareAll();
        }

        [Test]
        public void EighteenDistinctAuthenticCarsHavePhysicsAndIndexedOriginalAudio()
        {
            var official = CatalogRepository.Load().vehicles
                .Where(v => v.game == "v82").ToArray();
            Assert.That(official.Length, Is.EqualTo(18));
            string[] prefabs = Directory.GetFiles(
                LegacyVehiclePlaytestBuilder.PrefabDirectory, "v82_*.prefab");
            Assert.That(prefabs.Length, Is.EqualTo(18));

            foreach (VehicleEntry entry in official)
            {
                string path = LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" +
                    entry.assetId + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var marker = prefab.GetComponent<LegacyVehiclePlaytest>();
                Assert.That(marker, Is.Not.Null, entry.id);
                Assert.That(marker.OriginalVehicleId, Is.EqualTo(entry.id));
                Assert.That(marker.IsReleaseApproved, Is.False);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null,
                    "A test car must not claim official production certification");
                Assert.That(prefab.GetComponent<OriginalAudioBank>()?.Count, Is.GreaterThan(0));
                var handling = prefab.GetComponent<LegacyVehicleHandlingProfile>();
                Assert.That(handling, Is.Not.Null, entry.id);
                Assert.That(handling.VehicleId, Is.EqualTo(entry.id));
                Assert.That(handling.RawHalfHealth, Is.GreaterThan(0), entry.id);
                Assert.That(handling.RawLightness, Is.GreaterThan(0), entry.id);
                Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<VehicleDrive>()?.HasWeapon, Is.True);
                Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 2),
                    Is.True, "Original source mesh must not be replaced with a generic primitive");
            }
        }

        [Test]
        public void SourceBasedPrototypesDoNotAllShareOneGenericHandlingProfile()
        {
            VehicleEntry[] vehicles = CatalogRepository.Load().vehicles
                .Where(v => v.game == "v82").ToArray();
            var distinctSettings = new System.Collections.Generic.HashSet<string>();
            foreach (VehicleEntry entry in vehicles)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" + entry.assetId + ".prefab");
                Assert.That(prefab, Is.Not.Null, entry.id);
                VehicleDrive drive = prefab.GetComponent<VehicleDrive>();
                LegacyVehicleHandlingProfile source = prefab.GetComponent<LegacyVehicleHandlingProfile>();
                Assert.That(source, Is.Not.Null);
                Assert.That(drive.massKg, Is.GreaterThan(0f));
                Assert.That(drive.maximumHealth, Is.GreaterThan(0f));
                distinctSettings.Add(drive.massKg + ":" + drive.maximumSpeed + ":" +
                    drive.acceleration + ":" + drive.turningSpeed);
            }
            Assert.That(distinctSettings.Count, Is.EqualTo(18),
                "Each of the 18 genuine cars needs its individual source-informed handling.");
        }

        [Test]
        public void DevelopmentSceneCanBeGeneratedWithoutReleasingAFakeArena()
        {
            LegacyVehiclePlaytestBuilder.CreateScene("v82_tsunami");
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                LegacyVehiclePlaytestBuilder.ScenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Any(x =>
                x.enabled && x.path == LegacyVehiclePlaytestBuilder.ScenePath), Is.False);
            Assert.Throws<ArgumentException>(() =>
                LegacyVehiclePlaytestBuilder.CreateScene("v8_nonexistent"));
        }
    }
}
