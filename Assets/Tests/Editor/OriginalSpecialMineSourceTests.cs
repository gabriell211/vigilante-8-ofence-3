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
    public sealed class OriginalSpecialMineSourceTests
    {
        private GameObject owner;
        private GameObject enemy;
        private GameObject trapObject;

        [SetUp]
        public void Setup()
        {
            owner = new GameObject("original-car owner fixture");
            owner.AddComponent<Rigidbody>();
            owner.AddComponent<VehicleDrive>();
            enemy = new GameObject("other authentic car fixture");
            enemy.AddComponent<Rigidbody>();
            enemy.AddComponent<VehicleDrive>();
            trapObject = new GameObject("invisible special source test");
            trapObject.AddComponent<Rigidbody>();
            trapObject.AddComponent<SphereCollider>();
            trapObject.AddComponent<OriginalMineSourceTrap>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (trapObject != null) UnityEngine.Object.DestroyImmediate(trapObject);
            if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
        }

        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.Standard, 6, 1)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.Bearhug, 6, 2)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.Hovermine, 6, 2)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.CactusPatch, 6, 6)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.CactusPatch, 5, 5)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.CactusPatch, 3, 3)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.CactusPatch, 2, 2)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.CactusPatch, 1, 0)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.Bearhug, 1, 0)]
        [TestCase(OriginalSpecialMineSourceBehavior.SourceVariant.Hovermine, 0, 0)]
        public void ChargeConsumptionFollowsMineDrSourceSwitch(
            OriginalSpecialMineSourceBehavior.SourceVariant variant, int remaining, int expected)
        {
            Assert.That(OriginalSpecialMineSourceBehavior.ChargeCost(variant, remaining),
                Is.EqualTo(expected));
        }

        [Test]
        public void MalformedVariantOrChargeInventoryIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OriginalSpecialMineSourceBehavior.ChargeCost(
                    (OriginalSpecialMineSourceBehavior.SourceVariant)999, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OriginalSpecialMineSourceBehavior.ChargeCost(
                    OriginalSpecialMineSourceBehavior.SourceVariant.Standard, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OriginalSpecialMineSourceBehavior.ChargeCost(
                    OriginalSpecialMineSourceBehavior.SourceVariant.Standard, 7));
        }

        [Test]
        public void BearhugIsPersistentAndCannotFabricateHealthDamage()
        {
            var behavior = trapObject.AddComponent<OriginalSpecialMineSourceBehavior>();
            VehicleDrive ownerDrive = owner.GetComponent<VehicleDrive>();
            var victim = enemy.GetComponent<VehicleDrive>();
            behavior.Configure(OriginalSpecialMineSourceBehavior.SourceVariant.Bearhug,
                ownerDrive, 2);
            trapObject.GetComponent<OriginalMineSourceTrap>().Configure(ownerDrive);
            float health = victim.Health;
            Assert.That(behavior.OnVehicleContact(victim), Is.False,
                "The archived Brearhug.OnCollision returns 0 and persists");
            Assert.That(victim.Health, Is.EqualTo(health));
            Assert.That(behavior.IsReleaseApproved, Is.False);
            Assert.Throws<ArgumentException>(() => behavior.Configure(
                OriginalSpecialMineSourceBehavior.SourceVariant.Bearhug, ownerDrive, 2));
        }

        [Test]
        public void HovermineUsesArchivedThirtyOneTickScanAndImpulse()
        {
            var behavior = trapObject.AddComponent<OriginalSpecialMineSourceBehavior>();
            var ownerDrive = owner.GetComponent<VehicleDrive>();
            var victim = enemy.GetComponent<VehicleDrive>();
            behavior.Configure(OriginalSpecialMineSourceBehavior.SourceVariant.Hovermine,
                ownerDrive, 2);
            trapObject.GetComponent<OriginalMineSourceTrap>().Configure(ownerDrive);
            float health = victim.Health;
            Assert.That(OriginalSpecialMineSourceBehavior.HoverScanInterval, Is.EqualTo(31));
            Assert.That(OriginalSpecialMineSourceBehavior.HoverOwnerNeutralAfterScans,
                Is.EqualTo(10));
            Assert.That(behavior.OnVehicleContact(victim), Is.True);
            Assert.That(victim.Health, Is.EqualTo(health));
        }

        [Test]
        public void CactusPatchCannotScatterBeforeSourceTagsAreArmed()
        {
            var behavior = trapObject.AddComponent<OriginalSpecialMineSourceBehavior>();
            var ownerDrive = owner.GetComponent<VehicleDrive>();
            behavior.Configure(OriginalSpecialMineSourceBehavior.SourceVariant.CactusPatch,
                ownerDrive, 6);
            trapObject.GetComponent<OriginalMineSourceTrap>().Configure(ownerDrive);
            Assert.That(behavior.Payload, Is.EqualTo(6));
            Assert.That(behavior.OnVehicleContact(enemy.GetComponent<VehicleDrive>()), Is.False);
            Assert.That(behavior.FragmentsSpawned, Is.Zero);
        }

        [Test]
        public void AllEighteenOriginalCarPrototypesRetainTheSharedSixChargeLauncher()
        {
            LegacyVehiclePlaytestBuilder.PrepareAll();
            VehicleEntry[] cars = CatalogRepository.Load().vehicles
                .Where(x => x.game == "v82").ToArray();
            Assert.That(cars.Length, Is.EqualTo(18));
            foreach (VehicleEntry car in cars)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" + car.assetId + ".prefab");
                Assert.That(prefab, Is.Not.Null, car.id);
                Assert.That(prefab.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId,
                    Is.EqualTo(car.id));
                Assert.That(prefab.GetComponent<OriginalMineSourceLauncher>(), Is.Not.Null);
                Assert.That(OriginalMineSourceLauncher.InitialSourceCharges, Is.EqualTo(6));
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
            }
        }
    }
}
