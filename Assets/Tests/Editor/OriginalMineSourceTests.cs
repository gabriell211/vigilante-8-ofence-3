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
    public sealed class OriginalMineSourceTests
    {
        private GameObject car;
        private GameObject mine;

        [SetUp]
        public void Setup()
        {
            car = new GameObject("genuine mine source owner test");
            car.AddComponent<Rigidbody>();
            car.AddComponent<BoxCollider>();
            car.AddComponent<VehicleDrive>();
            mine = new GameObject("invisible mine physics fixture");
            mine.AddComponent<Rigidbody>();
            mine.AddComponent<SphereCollider>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (mine != null) UnityEngine.Object.DestroyImmediate(mine);
            if (car != null) UnityEngine.Object.DestroyImmediate(car);
        }

        [Test]
        public void SourceMineConfiguresWithoutInventingDamageOrCertifiedVisuals()
        {
            var trap = mine.AddComponent<OriginalMineSourceTrap>();
            Assert.That(OriginalMineSourceTrap.SourceMaxRegistered, Is.EqualTo(16));
            Assert.That(OriginalMineSourceTrap.SourceVerticalImpulseY, Is.EqualTo(-0x20000));
            Assert.That(trap.IsReleaseApproved, Is.False);
            trap.Configure(car.GetComponent<VehicleDrive>());
            Assert.That(trap.Owner, Is.SameAs(car.GetComponent<VehicleDrive>()));
            Assert.That(trap.State, Is.EqualTo(OriginalMineSourceTrap.SourceState.Airborne));
            Assert.Throws<ArgumentException>(() => trap.Configure(car.GetComponent<VehicleDrive>()));
        }

        [Test]
        public void UnconfiguredMineCannotBeArmed()
        {
            var trap = mine.AddComponent<OriginalMineSourceTrap>();
            Assert.Throws<ArgumentException>(() => trap.Configure(null));
            Assert.That(trap.IsReleaseApproved, Is.False);
        }

        [Test]
        public void AllEighteenRealCarsCarrySixChargeNonReleaseLauncher()
        {
            LegacyVehiclePlaytestBuilder.PrepareAll();
            var cars = CatalogRepository.Load().vehicles.Where(v => v.game == "v82").ToArray();
            Assert.That(cars.Length, Is.EqualTo(18));
            foreach (VehicleEntry carEntry in cars)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" + carEntry.assetId + ".prefab");
                Assert.That(prefab, Is.Not.Null, carEntry.id);
                Assert.That(prefab.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId,
                    Is.EqualTo(carEntry.id));
                var launcher = prefab.GetComponent<OriginalMineSourceLauncher>();
                Assert.That(launcher, Is.Not.Null);
                Assert.That(launcher.IsReleaseApproved, Is.False);
                Assert.That(OriginalMineSourceLauncher.InitialSourceCharges, Is.EqualTo(6));
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
            }
        }
    }
}
