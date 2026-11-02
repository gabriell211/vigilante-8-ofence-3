using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vigilante8.Gameplay;

namespace Vigilante8.PlayModeTests
{
    public sealed class OriginalSpecialMineSourcePhysicsTests
    {
        private readonly List<GameObject> fixtures = new List<GameObject>();

        private VehicleDrive Car(string name, Vector3 position, bool originalMarker = false)
        {
            var go = new GameObject(name);
            fixtures.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            var drive = go.AddComponent<VehicleDrive>();
            if (originalMarker) go.AddComponent<LegacyVehiclePlaytest>();
            return drive;
        }

        private (OriginalMineSourceTrap trap, OriginalSpecialMineSourceBehavior special)
            SpecialMine(VehicleDrive owner, Vector3 position,
                OriginalSpecialMineSourceBehavior.SourceVariant type, int charges)
        {
            var go = new GameObject("untextured source special mine physics fixture");
            fixtures.Add(go);
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = 0.25f;
            go.AddComponent<Rigidbody>();
            var trap = go.AddComponent<OriginalMineSourceTrap>();
            var special = go.AddComponent<OriginalSpecialMineSourceBehavior>();
            special.Configure(type, owner, charges);
            trap.Configure(owner);
            return (trap, special);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (GameObject fixture in fixtures)
                if (fixture != null) Object.Destroy(fixture);
            fixtures.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HovermineScansForNearestAuthenticCarAfterSourceThirtyOneTicks()
        {
            VehicleDrive owner = Car("source owner", Vector3.left * 45f);
            VehicleDrive authentic = Car("source marked enemy",
                new Vector3(8f, 0f, 0f), true);
            var item = SpecialMine(owner, new Vector3(0f, 0f, 0f),
                OriginalSpecialMineSourceBehavior.SourceVariant.Hovermine, 2);
            item.trap.GetComponent<Rigidbody>().useGravity = false;
            for (int frame = 0; frame < 34; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(item.special.Scans, Is.GreaterThanOrEqualTo(1));
            Assert.That(item.special.Target, Is.SameAs(authentic));
            Assert.That(item.special.GetComponent<Rigidbody>().linearVelocity.x,
                Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator BearhugPullsWithoutDestroyingMineOrChangingHealth()
        {
            VehicleDrive owner = Car("source owner", Vector3.left * 45f);
            VehicleDrive victim = Car("original test victim", new Vector3(2f, 0f, 0f));
            var item = SpecialMine(owner, Vector3.zero,
                OriginalSpecialMineSourceBehavior.SourceVariant.Bearhug, 2);
            float health = victim.Health;
            Assert.That(item.special.OnVehicleContact(victim), Is.False);
            yield return new WaitForFixedUpdate();
            Assert.That(victim.Health, Is.EqualTo(health));
            Assert.That(victim.GetComponent<Rigidbody>().linearVelocity.x,
                Is.LessThan(0f));
            Assert.That(item.trap != null, Is.True);
        }

        [UnityTest]
        public IEnumerator ArmedCactusPatchScattersOnlyItsAuthenticPaidChargeCount()
        {
            VehicleDrive owner = Car("source owner", Vector3.left * 45f);
            VehicleDrive victim = Car("source victim", new Vector3(10f, 0f, 0f));
            var floor = new GameObject("physics test floor");
            fixtures.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(12f, 1f, 12f);
            var item = SpecialMine(owner, new Vector3(0f, 1.5f, 0f),
                OriginalSpecialMineSourceBehavior.SourceVariant.CactusPatch, 4);
            for (int frame = 0; frame < 90 &&
                item.trap.State != OriginalMineSourceTrap.SourceState.Armed; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(item.trap.State, Is.EqualTo(OriginalMineSourceTrap.SourceState.Armed));
            float health = victim.Health;
            Assert.That(item.special.OnVehicleContact(victim), Is.True);
            Assert.That(item.special.FragmentsSpawned, Is.EqualTo(4));
            Assert.That(victim.Health, Is.EqualTo(health));
        }
    }
}
