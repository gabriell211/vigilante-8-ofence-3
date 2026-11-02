using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vigilante8.Gameplay;

namespace Vigilante8.PlayModeTests
{
    /// <summary>Real Unity Rigidbody and damage events; fixture geometry never ships.</summary>
    public sealed class CombatEliminationAttributionTests
    {
        private readonly List<GameObject> fixtures = new List<GameObject>();

        private VehicleDrive MakeVehicle(string name, Vector3 position)
        {
            var go = new GameObject(name);
            fixtures.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            return go.AddComponent<VehicleDrive>();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (GameObject go in fixtures)
                if (go != null) UnityEngine.Object.Destroy(go);
            fixtures.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProjectilePhysicsCreditsActualFiringVehicleOnce()
        {
            VehicleDrive attacker = MakeVehicle("attacker", Vector3.zero);
            VehicleDrive victim = MakeVehicle("victim", new Vector3(0f, 0f, 3f));
            var bullet = new GameObject("projectile-fixture");
            fixtures.Add(bullet);
            bullet.transform.position = new Vector3(0f, 0f, 1f);
            bullet.AddComponent<SphereCollider>().radius = 0.15f;
            bullet.AddComponent<Rigidbody>();
            var projectile = bullet.AddComponent<Projectile>();
            projectile.impactDamage = victim.Health + 5f;
            projectile.muzzleVelocity = 14f;
            projectile.lifetimeSeconds = 3f;

            int events = 0;
            VehicleDrive actualKiller = null;
            victim.Eliminated += (eliminated, killer) =>
            {
                Assert.That(eliminated, Is.SameAs(victim));
                events++;
                actualKiller = killer;
            };
            Physics.SyncTransforms();
            projectile.Launch(attacker, Vector3.forward);
            for (int frame = 0; frame < 55 && victim.IsAlive; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(victim.IsAlive, Is.False, "Real projectile hit must destroy the victim");
            Assert.That(events, Is.EqualTo(1));
            Assert.That(actualKiller, Is.SameAs(attacker));
            Assert.That(attacker.IsAlive, Is.True);
            victim.TakeDamage(9999f, attacker);
            Assert.That(events, Is.EqualTo(1), "A wreck must never grant duplicate kills");
        }

        [UnityTest]
        public IEnumerator EnvironmentDamageAndInvalidHitsNeverInventKiller()
        {
            VehicleDrive attacker = MakeVehicle("attacker", Vector3.zero);
            VehicleDrive victim = MakeVehicle("victim", Vector3.forward * 5f);
            int events = 0;
            VehicleDrive actualKiller = attacker;
            victim.Eliminated += (_, killer) =>
            {
                events++;
                actualKiller = killer;
            };
            victim.TakeDamage(float.NaN, attacker);
            victim.TakeDamage(float.PositiveInfinity, attacker);
            victim.TakeDamage(-10f, attacker);
            Assert.That(events, Is.Zero);
            Assert.That(victim.Health, Is.EqualTo(victim.maximumHealth));
            victim.TakeDamage(5f, attacker);
            victim.TakeDamage(victim.Health); // No attribution on final environmental hit
            yield return null;

            Assert.That(victim.IsAlive, Is.False);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(actualKiller, Is.Null);
        }

        [UnityTest]
        public IEnumerator SplashAndDirectHitsCanCreditDifferentCarsWithoutDuplicateDeaths()
        {
            VehicleDrive first = MakeVehicle("first-attacker", Vector3.zero);
            VehicleDrive last = MakeVehicle("fatal-attacker", Vector3.forward * 7f);
            VehicleDrive victim = MakeVehicle("victim", Vector3.forward * 4f);
            int events = 0;
            VehicleDrive actualKiller = null;
            victim.Eliminated += (_, killer) => { events++; actualKiller = killer; };
            victim.TakeDamage(20f, first);
            victim.TakeDamage(victim.Health, last);
            yield return null;

            Assert.That(events, Is.EqualTo(1));
            Assert.That(actualKiller, Is.SameAs(last),
                "Only the source of the lethal valid hit earns the elimination");
        }
    }
}
