using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vigilante8.Gameplay;

namespace Vigilante8.PlayModeTests
{
    /// <summary>
    /// Real Unity physics fixtures only. They are deliberately not production
    /// cars or stage models and are excluded from the shipping build.
    /// Authored original V82 prefab coverage is checked in EditMode separately.
    /// </summary>
    public sealed class CombatPhysicsPlayModeTests
    {
        private readonly List<GameObject> fixtures = new List<GameObject>();

        private VehicleDrive NewPhysicsFixture(string name, Vector3 location)
        {
            var go = new GameObject(name);
            fixtures.Add(go);
            go.transform.position = location;
            var collider = go.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            return go.AddComponent<VehicleDrive>();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (GameObject fixture in fixtures)
                if (fixture != null) UnityEngine.Object.Destroy(fixture);
            fixtures.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealProjectilePhysicsDamagesOtherCarNotItsOwner()
        {
            VehicleDrive attacker = NewPhysicsFixture("physics-attacker", Vector3.zero);
            VehicleDrive victim = NewPhysicsFixture("physics-victim", new Vector3(0f, 0f, 3f));
            var bullet = new GameObject("physics-projectile");
            fixtures.Add(bullet);
            bullet.transform.position = new Vector3(0f, 0f, 1f);
            var sphere = bullet.AddComponent<SphereCollider>();
            sphere.radius = 0.15f;
            bullet.AddComponent<Rigidbody>();
            Projectile projectile = bullet.AddComponent<Projectile>();
            projectile.impactDamage = 25f;
            projectile.muzzleVelocity = 14f;
            projectile.lifetimeSeconds = 2f;

            float originalHealth = victim.Health;
            float attackerHealth = attacker.Health;
            Physics.SyncTransforms();
            projectile.Launch(attacker, Vector3.forward);
            for (int frame = 0; frame < 55 && victim.Health >= originalHealth; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(victim.Health, Is.LessThan(originalHealth),
                "A real Rigidbody collision must damage the other car");
            Assert.That(victim.Health, Is.EqualTo(originalHealth - 25f).Within(0.01f),
                "The hit must apply the configured projectile damage only once");
            Assert.That(attacker.Health, Is.EqualTo(attackerHealth),
                "An owner must never be damaged by its own launch");
        }

        [UnityTest]
        public IEnumerator DestroyedVehicleRaisesSingleEventAndStopsTakingDamage()
        {
            VehicleDrive victim = NewPhysicsFixture("physics-destruction", Vector3.zero);
            int destroyedEvents = 0;
            victim.Destroyed += _ => destroyedEvents++;
            float max = victim.Health;
            Assert.That(max, Is.GreaterThan(0f));
            victim.TakeDamage(float.NaN);
            victim.TakeDamage(float.PositiveInfinity);
            victim.TakeDamage(-10f);
            Assert.That(victim.Health, Is.EqualTo(max));

            victim.TakeDamage(max);
            yield return null;

            Assert.That(victim.IsAlive, Is.False);
            Assert.That(victim.Health, Is.EqualTo(0f));
            Assert.That(destroyedEvents, Is.EqualTo(1));
            victim.TakeDamage(max);
            Assert.That(destroyedEvents, Is.EqualTo(1));
            Assert.That(victim.GetComponent<Rigidbody>().isKinematic, Is.True);
        }
    }
}
