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
    public sealed class OriginalRocketInventorySourceTests
    {
        private GameObject fixture;

        [SetUp]
        public void Setup()
        {
            fixture = new GameObject("original RocketL source ammo test");
            fixture.AddComponent<Rigidbody>();
            fixture.AddComponent<VehicleDrive>();
            fixture.AddComponent<OriginalRocketSourceInventory>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
        }

        [Test]
        public void RealRocketLStartsAtTenAndSpendsOneOrTwoCharges()
        {
            var ammo = fixture.GetComponent<OriginalRocketSourceInventory>();
            Assert.That(OriginalRocketSourceInventory.InitialCharges, Is.EqualTo(10));
            Assert.That(OriginalRocketSourceInventory.RocketDamage, Is.EqualTo(80));
            Assert.That(OriginalRocketSourceInventory.OriginalRocketLifeTicks, Is.EqualTo(240));
            Assert.That(ammo.RemainingCharges, Is.EqualTo(10));
            Assert.That(ammo.IsReleaseApproved, Is.False);

            Assert.That(OriginalRocketSourceInventory.CostForMode(false), Is.EqualTo(1));
            Assert.That(OriginalRocketSourceInventory.CostForMode(true), Is.EqualTo(2));
            Assert.That(ammo.TrySpend(1), Is.True);
            Assert.That(ammo.RemainingCharges, Is.EqualTo(9));
            Assert.That(ammo.TrySpend(2), Is.True);
            Assert.That(ammo.RemainingCharges, Is.EqualTo(7));
            Assert.That(ammo.CanSpend(0), Is.False);
            Assert.That(ammo.CanSpend(11), Is.False);
            for (int i = 0; i < 7; i++)
                Assert.That(ammo.TrySpend(1), Is.True);
            Assert.That(ammo.RemainingCharges, Is.Zero);
            Assert.That(ammo.TrySpend(2), Is.False,
                "A source rocket launcher must never allow negative ammunition");
            Assert.That(ammo.RemainingCharges, Is.Zero);
        }

        [Test]
        public void DestroyedCarCannotConsumeSourceRockets()
        {
            var car = fixture.GetComponent<VehicleDrive>();
            var ammo = fixture.GetComponent<OriginalRocketSourceInventory>();
            car.TakeDamage(car.Health);
            Assert.That(car.IsAlive, Is.False);
            Assert.That(ammo.TrySpend(1), Is.False);
            Assert.That(ammo.RemainingCharges, Is.EqualTo(10));
        }

        [Test]
        public void AllEighteenAuthenticCarPrototypesShareOneRocketLInventory()
        {
            LegacyVehiclePlaytestBuilder.PrepareAll();
            var cars = CatalogRepository.Load().vehicles
                .Where(v => v.game == "v82").ToArray();
            Assert.That(cars.Length, Is.EqualTo(18));
            foreach (VehicleEntry car in cars)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" + car.assetId + ".prefab");
                Assert.That(prefab, Is.Not.Null, car.id);
                Assert.That(prefab.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId,
                    Is.EqualTo(car.id));
                Assert.That(prefab.GetComponents<OriginalRocketSourceInventory>().Length,
                    Is.EqualTo(1));
                Assert.That(prefab.GetComponent<OriginalBastionRocketLauncher>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<OriginalRoadRunnerSourceLauncher>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
            }
        }
    }
}
