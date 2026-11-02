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
    public sealed class OriginalMissileSourceTests
    {
        private GameObject shot;
        private GameObject target;

        [SetUp]
        public void Setup()
        {
            target = new GameObject("AUTHENTIC TARGET TEST");
            target.AddComponent<Rigidbody>();
            target.AddComponent<VehicleDrive>();
            shot = new GameObject("MISSILE STATES TEST ONLY");
            shot.AddComponent<SphereCollider>();
            shot.AddComponent<Rigidbody>();
            shot.AddComponent<Projectile>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        [Test]
        public void ShellTransitionsOnceToGuidedProjectileWithSourceLifetime()
        {
            var flight = shot.AddComponent<OriginalMissileSourceFlight>();
            var enemy = target.GetComponent<VehicleDrive>();
            Assert.That(OriginalMissileSourceFlight.OriginalDamage, Is.EqualTo(60));
            Assert.That(OriginalMissileSourceFlight.OriginalLifeTicks, Is.EqualTo(480));
            flight.Configure(enemy);
            Assert.That(flight.State, Is.EqualTo(OriginalMissileSourceFlight.SourceState.Shell));
            Assert.That(flight.LockedTarget, Is.SameAs(enemy));
            Assert.That(flight.TicksRemaining, Is.EqualTo(480));
            Assert.That(flight.IsReleaseApproved, Is.False);
            flight.ActivateProjectile();
            Assert.That(flight.State, Is.EqualTo(
                OriginalMissileSourceFlight.SourceState.Projectile));
            Assert.Throws<InvalidOperationException>(() => flight.ActivateProjectile());
            Assert.Throws<ArgumentException>(() => flight.Configure(enemy));
        }

        [Test]
        public void GuidedErrorCorrectionUsesOriginalSixteenAndEightDivisors()
        {
            Vector3 correction = OriginalMissileSourceFlight.CalculateSteeringDelta(
                new Vector3(0f, 0f, 16f), new Vector3(16f, 8f, 0f));
            Assert.That(OriginalMissileSourceFlight.HorizontalErrorDivisor, Is.EqualTo(16));
            Assert.That(OriginalMissileSourceFlight.VerticalErrorDivisor, Is.EqualTo(8));
            Assert.That(correction.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(correction.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(correction.z, Is.EqualTo(-1f).Within(0.001f));
            Assert.Throws<ArgumentException>(() =>
                OriginalMissileSourceFlight.CalculateSteeringDelta(
                    new Vector3(float.NaN, 0, 0), Vector3.one));
            Assert.Throws<ArgumentException>(() =>
                OriginalMissileSourceFlight.CalculateSteeringDelta(
                    Vector3.zero, new Vector3(float.PositiveInfinity, 0, 0)));
        }

        [Test]
        public void AllEighteenRealCarsCarryOnlyDevelopmentMissileLaunchers()
        {
            LegacyVehiclePlaytestBuilder.PrepareAll();
            var catalog = CatalogRepository.Load();
            var cars = catalog.vehicles.Where(x => x.game == "v82").ToArray();
            Assert.That(cars.Length, Is.EqualTo(18));
            foreach (VehicleEntry entry in cars)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" + entry.assetId + ".prefab");
                Assert.That(prefab, Is.Not.Null, entry.id);
                Assert.That(prefab.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId,
                    Is.EqualTo(entry.id));
                var launcher = prefab.GetComponent<OriginalMissileSourceLauncher>();
                Assert.That(launcher, Is.Not.Null);
                Assert.That(launcher.IsReleaseApproved, Is.False);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
                Assert.That(prefab.GetComponent<OriginalMachineGunSourceTest>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<OriginalBastionRocketLauncher>(), Is.Not.Null);
            }
        }
    }
}
