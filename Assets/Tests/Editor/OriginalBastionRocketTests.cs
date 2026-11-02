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
    public sealed class OriginalBastionRocketTests
    {
        private GameObject rocket;
        private GameObject target;

        [SetUp]
        public void Setup()
        {
            target = new GameObject("SOURCE TARGET TEST");
            target.AddComponent<Rigidbody>();
            target.AddComponent<VehicleDrive>();
            rocket = new GameObject("SOURCE ROCKET TEST");
            rocket.AddComponent<SphereCollider>();
            rocket.AddComponent<Rigidbody>();
            rocket.AddComponent<Projectile>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (rocket != null) UnityEngine.Object.DestroyImmediate(rocket);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        [Test]
        public void ArchivedRocketToBastionTransitionAndTwoGuidancePhases()
        {
            var flight = rocket.AddComponent<OriginalBastionRocketFlight>();
            var vehicle = target.GetComponent<VehicleDrive>();
            flight.Configure(vehicle, 240);
            Assert.That(flight.State, Is.EqualTo(OriginalBastionRocketFlight.SourceState.Rocket));
            Assert.That(flight.RemainingSourceTicks, Is.EqualTo(240));
            Assert.That(flight.LockedTarget, Is.SameAs(vehicle));
            Assert.That(flight.IsReleaseApproved, Is.False);
            flight.EnterBastion();
            Assert.That(flight.State, Is.EqualTo(OriginalBastionRocketFlight.SourceState.Bastion));
            Assert.That(OriginalBastionRocketFlight.SourceSteeringWeight(211),
                Is.EqualTo(1f / 16f));
            Assert.That(OriginalBastionRocketFlight.SourceSteeringWeight(210),
                Is.EqualTo(0.5f));
            Assert.That(OriginalBastionRocketFlight.TargetValidationInterval, Is.EqualTo(16));
            Assert.Throws<InvalidOperationException>(() => flight.EnterBastion());
        }

        [Test]
        public void CannotUseMissingTargetOrInvalidLifetime()
        {
            var flight = rocket.AddComponent<OriginalBastionRocketFlight>();
            Assert.Throws<ArgumentException>(() => flight.Configure(null, 240));
            Assert.Throws<ArgumentException>(() =>
                flight.Configure(target.GetComponent<VehicleDrive>(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OriginalBastionRocketFlight.SourceSteeringWeight(-1));
            Assert.Throws<InvalidOperationException>(() => flight.EnterBastion());
        }

        [Test]
        public void AllEighteenOriginalVehiclePrefabsHaveTheNonreleaseRocketLauncher()
        {
            LegacyVehiclePlaytestBuilder.PrepareAll();
            var vehicles = CatalogRepository.Load().vehicles.Where(v => v.game == "v82").ToArray();
            Assert.That(vehicles.Length, Is.EqualTo(18));
            foreach (var vehicle in vehicles)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" + vehicle.assetId + ".prefab");
                Assert.That(prefab, Is.Not.Null, vehicle.id);
                Assert.That(prefab.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId,
                    Is.EqualTo(vehicle.id));
                var launcher = prefab.GetComponent<OriginalBastionRocketLauncher>();
                Assert.That(launcher, Is.Not.Null, vehicle.id);
                Assert.That(launcher.IsReleaseApproved, Is.False);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
                Assert.That(prefab.GetComponent<VehicleDrive>().HasWeapon, Is.True);
            }
        }
    }
}
