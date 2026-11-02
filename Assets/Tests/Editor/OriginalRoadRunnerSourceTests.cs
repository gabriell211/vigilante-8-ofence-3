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
    public sealed class OriginalRoadRunnerSourceTests
    {
        private GameObject owner;
        private GameObject target;
        private GameObject projectile;

        [SetUp]
        public void Setup()
        {
            owner = new GameObject("source-car owner fixture");
            owner.AddComponent<Rigidbody>();
            owner.AddComponent<VehicleDrive>();
            target = new GameObject("source-car victim fixture");
            target.AddComponent<Rigidbody>();
            target.AddComponent<BoxCollider>();
            target.AddComponent<VehicleDrive>();
            projectile = new GameObject("RoadRunner invisible source test");
            projectile.AddComponent<Rigidbody>();
            projectile.AddComponent<SphereCollider>();
            projectile.AddComponent<Projectile>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (projectile != null) UnityEngine.Object.DestroyImmediate(projectile);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
        }

        [Test]
        public void SourceRocketAttachesOnceAndSuppressesImmediateDamage()
        {
            var charge = projectile.AddComponent<OriginalRoadRunnerSourceCharge>();
            VehicleDrive ownerDrive = owner.GetComponent<VehicleDrive>();
            VehicleDrive victim = target.GetComponent<VehicleDrive>();
            charge.Configure(ownerDrive);
            Assert.That(charge.State, Is.EqualTo(
                OriginalRoadRunnerSourceCharge.SourceState.Rocket));
            Assert.That(OriginalRoadRunnerSourceCharge.AttachedSourceTicks, Is.EqualTo(360));
            Assert.That(OriginalRoadRunnerSourceCharge.OriginalDamageMagnitude, Is.EqualTo(40));
            float health = victim.Health;
            Assert.That(charge.TryAttach(victim), Is.True);
            Assert.That(charge.State, Is.EqualTo(
                OriginalRoadRunnerSourceCharge.SourceState.RoadRunner));
            Assert.That(charge.AttachedTarget, Is.SameAs(victim));
            Assert.That(charge.RemainingTicks, Is.EqualTo(360));
            Assert.That(charge.GetComponent<Collider>().enabled, Is.False);
            Assert.That(charge.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(projectile.transform.parent, Is.SameAs(target.transform));
            Assert.That(victim.Health, Is.EqualTo(health));
            Assert.That(charge.TryAttach(victim), Is.False, "Cannot stick twice");
            Assert.That(charge.IsReleaseApproved, Is.False);
        }

        [Test]
        public void SourceRocketNeverSticksToItselfOrDeadTarget()
        {
            var charge = projectile.AddComponent<OriginalRoadRunnerSourceCharge>();
            Assert.Throws<ArgumentException>(() => charge.Configure(null));
            VehicleDrive ownerDrive = owner.GetComponent<VehicleDrive>();
            charge.Configure(ownerDrive);
            Assert.That(charge.TryAttach(ownerDrive), Is.False);
            Assert.That(charge.TryAttach(null), Is.False);
            target.GetComponent<VehicleDrive>().TakeDamage(99999f);
            Assert.That(charge.TryAttach(target.GetComponent<VehicleDrive>()), Is.False);
        }

        [Test]
        public void AllEighteenRealCarMeshesHaveNonreleaseRoadRunnerSourceLauncher()
        {
            LegacyVehiclePlaytestBuilder.PrepareAll();
            VehicleEntry[] vehicles = CatalogRepository.Load().vehicles
                .Where(v => v.game == "v82").ToArray();
            Assert.That(vehicles.Length, Is.EqualTo(18));
            foreach (VehicleEntry entry in vehicles)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    LegacyVehiclePlaytestBuilder.PrefabDirectory + "/" + entry.assetId + ".prefab");
                Assert.That(prefab, Is.Not.Null, entry.id);
                Assert.That(prefab.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId,
                    Is.EqualTo(entry.id));
                Assert.That(prefab.GetComponent<OriginalRoadRunnerSourceLauncher>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<OriginalRoadRunnerSourceLauncher>().IsReleaseApproved,
                    Is.False);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
            }
        }
    }
}
