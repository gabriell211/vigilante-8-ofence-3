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
        public void OriginalTerrainBattleUsesFourDistinctSourceCarsAndThreeBots()
        {
            const string stage = "v82_winter_games";
            OriginalNativeTerrainPlaytestBuilder.CreateBattle(stage, "v82_tsunami",
                new[] { "v82_thunderbolt", "v82_blue_burro_bus", "v82_moon_trekker" });
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var drivers = roots.SelectMany(root =>
                root.GetComponentsInChildren<LegacyVehiclePlaytest>(true)).ToArray();
            Assert.That(drivers.Select(x => x.OriginalVehicleId).Distinct().Count(),
                Is.EqualTo(4), "All combatants must use different genuine original meshes");
            Assert.That(roots.SelectMany(root =>
                root.GetComponentsInChildren<PlayerVehicleInput>(true)).Count(),
                Is.EqualTo(1));
            Assert.That(roots.SelectMany(root =>
                root.GetComponentsInChildren<BotVehicleInput>(true)).Count(),
                Is.EqualTo(3));
            var encounter = roots.SelectMany(root =>
                root.GetComponentsInChildren<OriginalBattleSession>(true)).Single();
            Assert.That(encounter.OriginalArenaId, Is.EqualTo(stage));
            Assert.That(encounter.OriginalPlayerId, Is.EqualTo("v82_tsunami"));
            Assert.That(encounter.TotalOpponents, Is.EqualTo(3));
            Assert.That(encounter.IsReleaseApproved, Is.False);
            var targeting = roots.SelectMany(root =>
                root.GetComponentsInChildren<OriginalBattleTargetCoordinator>(true)).Single();
            Assert.That(targeting.ParticipantCount, Is.EqualTo(4));
            Assert.That(targeting.IsReleaseApproved, Is.False);
            var terrain = roots.SelectMany(root =>
                root.GetComponentsInChildren<OriginalTerrainPreviewIdentity>(true))
                .SingleOrDefault();
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.StageId, Is.EqualTo(stage));
            Assert.That(terrain.IsReleaseApproved, Is.False);
            Assert.That(roots.SelectMany(root =>
                root.GetComponentsInChildren<MeshCollider>(true))
                .Any(c => c.sharedMesh != null && c.sharedMesh.vertexCount > 2),
                Is.True, "Physics must come from the source mesh, not a synthetic floor");
            Assert.That(EditorBuildSettings.scenes.Any(x =>
                x.enabled && x.path == OriginalNativeTerrainPlaytestBuilder.ScenePath), Is.False);
        }

        [Test]
        public void OriginalTerrainBattleRejectsDuplicateCarsAndPilots()
        {
            Assert.Throws<ArgumentException>(() =>
                OriginalNativeTerrainPlaytestBuilder.CreateBattle("v82_winter_games",
                    "v82_tsunami", new[] { "v82_tsunami" }));
            Assert.Throws<ArgumentException>(() =>
                OriginalNativeTerrainPlaytestBuilder.CreateBattle("v82_winter_games",
                    "v82_tsunami", new[] { "v82_thunderbolt", "v82_thunderbolt" }));
            Assert.Throws<ArgumentException>(() =>
                OriginalNativeTerrainPlaytestBuilder.CreateBattle("v82_meteor_crater",
                    "v82_tsunami", new[] { "v82_thunderbolt" }));
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
