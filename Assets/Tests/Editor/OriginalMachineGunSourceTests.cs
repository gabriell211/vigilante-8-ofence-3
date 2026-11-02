using System;
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
    public sealed class OriginalMachineGunSourceTests
    {
        private GameObject car;
        private OriginalMachineGunSourceTest gun;

        [SetUp]
        public void Setup()
        {
            car = new GameObject("nonrelease MGun source state test");
            car.AddComponent<Rigidbody>();
            car.AddComponent<VehicleDrive>();
            gun = car.AddComponent<OriginalMachineGunSourceTest>();
            gun.ResetToSourceInitialState();
        }

        [TearDown]
        public void Cleanup()
        {
            if (car != null) UnityEngine.Object.DestroyImmediate(car);
        }

        [Test]
        public void FirstShotAndTagsFollowArchivedMGunUpdateW12Exactly()
        {
            Assert.That(OriginalMachineGunSourceTest.NormalBulletDamage, Is.EqualTo(7));
            Assert.That(OriginalMachineGunSourceTest.DoubleDamageBullet, Is.EqualTo(14));
            Assert.That(OriginalMachineGunSourceTest.BulletLifeSourceTicks, Is.EqualTo(45));
            Assert.That(gun.SourceHeat, Is.EqualTo(0x500));
            Assert.That(gun.TryFire(false), Is.False);
            Assert.That(gun.TryFire(true), Is.True);
            Assert.That(gun.SourceTags, Is.EqualTo(5));
            Assert.That(gun.SourceHeat, Is.EqualTo(0x520));
            Assert.That(gun.ShotsFired, Is.EqualTo(1));
            for (int i = 0; i < 5; i++)
                Assert.That(gun.TryFire(true), Is.False, "source tags delay next shot");
            Assert.That(gun.TryFire(true), Is.True);
            Assert.That(gun.SourceHeat, Is.EqualTo(0x540));
            Assert.That(gun.ShotsFired, Is.EqualTo(2));
        }

        [Test]
        public void TriggerReleaseDoesNotInventASourceWeaponSwitch()
        {
            gun.TryFire(true);
            Assert.That(gun.TryFire(false), Is.False);
            Assert.That(gun.SourceHeat, Is.EqualTo(0x520));
            Assert.That(gun.SourceTags, Is.EqualTo(5));
            gun.Holster();
            Assert.That(gun.SourceHeat, Is.EqualTo(0x500));
            Assert.That(gun.SourceTags, Is.EqualTo(4));
            gun.ResetToSourceInitialState();
            Assert.That(gun.ShotsFired, Is.Zero);
            Assert.That(gun.SourceHeat, Is.EqualTo(0x500));
            Assert.That(gun.SourceTags, Is.Zero);
        }

        [Test]
        public void EveryArchivedOriginalV82CarHasSourceMachineGunOnlyInPlaytest()
        {
            LegacyVehiclePlaytestBuilder.PrepareAll();
            var expected = CatalogRepository.Load().vehicles
                .Where(v => v.game == "v82").ToArray();
            Assert.That(expected.Length, Is.EqualTo(18));
            foreach (var vehicle in expected)
            {
                string path = LegacyVehiclePlaytestBuilder.PrefabDirectory +
                    "/" + vehicle.assetId + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, vehicle.id);
                Assert.That(prefab.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId,
                    Is.EqualTo(vehicle.id));
                Assert.That(prefab.GetComponent<OriginalMachineGunSourceTest>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<OriginalMachineGunSourceTest>().IsReleaseApproved,
                    Is.False);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
                Assert.That(prefab.GetComponent<VehicleDrive>()?.HasWeapon, Is.True);
            }
        }
    }
}
