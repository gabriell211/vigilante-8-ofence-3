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
    public sealed class OriginalMissileHaloSourceTests
    {
        private GameObject car;
        private GameObject shot;

        [SetUp]
        public void Setup()
        {
            car = new GameObject("MissileL original-car source test");
            car.AddComponent<Rigidbody>();
            car.AddComponent<VehicleDrive>();
            car.AddComponent<OriginalMissileSourceInventory>();
            shot = new GameObject("invisible Halo flight test");
            shot.AddComponent<Rigidbody>();
            shot.AddComponent<SphereCollider>();
            shot.AddComponent<Projectile>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
            if (car != null) UnityEngine.Object.DestroyImmediate(car);
        }

        [Test]
        public void TwelveOriginalSourceChargesKeepLastChargeForHalo()
        {
            var ammo = car.GetComponent<OriginalMissileSourceInventory>();
            Assert.That(ammo.RemainingCharges, Is.EqualTo(12));
            Assert.That(OriginalMissileSourceInventory.InitialSourceCharges, Is.EqualTo(12));
            Assert.That(OriginalMissileSourceInventory.HaloSpecialCost, Is.EqualTo(1));
            Assert.That(ammo.IsReleaseApproved, Is.False);
            for (int i = 0; i < 11; i++)
                Assert.That(ammo.TrySpendHalo(), Is.True);
            Assert.That(ammo.RemainingCharges, Is.EqualTo(1));
            Assert.That(ammo.CanFireHalo, Is.False);
            Assert.That(ammo.TrySpendHalo(), Is.False);
            Assert.That(ammo.RemainingCharges, Is.EqualTo(1),
                "Archived UpdateW(10,0x442) checks maxHalfHealth > 1");
        }

        [Test]
        public void DestroyedVehicleCannotSpendHaloCharges()
        {
            var ammo = car.GetComponent<OriginalMissileSourceInventory>();
            var vehicle = car.GetComponent<VehicleDrive>();
            vehicle.TakeDamage(vehicle.Health);
            Assert.That(ammo.CanFireHalo, Is.False);
            Assert.That(ammo.TrySpendHalo(), Is.False);
            Assert.That(ammo.RemainingCharges, Is.EqualTo(12));
        }

        [Test]
        public void HaloIsNotAGuidedProjectileAndChangesModeOnce()
        {
            var halo = shot.AddComponent<OriginalMissileSourceFlight>();
            halo.Configure(car.GetComponent<VehicleDrive>());
            Assert.That(halo.State, Is.EqualTo(OriginalMissileSourceFlight.SourceState.Shell));
            halo.ActivateHalo();
            Assert.That(halo.State, Is.EqualTo(OriginalMissileSourceFlight.SourceState.Halo));
            Assert.That(halo.TicksRemaining, Is.EqualTo(480));
            Assert.That(OriginalMissileSourceFlight.HaloYawSourceAngle, Is.EqualTo(0x100));
            Assert.That(OriginalMissileSourceFlight.HaloYawDegreesPerTick,
                Is.EqualTo(22.5f).Within(0.001f));
            Assert.Throws<InvalidOperationException>(() => halo.ActivateProjectile());
            Assert.Throws<InvalidOperationException>(() => halo.ActivateHalo());
            Assert.That(halo.IsReleaseApproved, Is.False);
        }

        [Test]
        public void AllEighteenAuthenticV82CarsGetMissileLSourceInventoryOnlyInEditor()
        {
            LegacyVehiclePlaytestBuilder.PrepareAll();
            VehicleEntry[] cars = CatalogRepository.Load().vehicles
                .Where(v => v.game == "v82").ToArray();
            Assert.That(cars.Length, Is.EqualTo(18));
            foreach (VehicleEntry entry in cars)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" + entry.assetId + ".prefab");
                Assert.That(prefab, Is.Not.Null, entry.id);
                Assert.That(prefab.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId,
                    Is.EqualTo(entry.id));
                Assert.That(prefab.GetComponents<OriginalMissileSourceInventory>().Length,
                    Is.EqualTo(1));
                Assert.That(prefab.GetComponent<OriginalMissileSourceLauncher>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
            }
        }
    }
}
