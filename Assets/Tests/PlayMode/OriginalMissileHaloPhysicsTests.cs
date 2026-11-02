using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vigilante8.Gameplay;

namespace Vigilante8.PlayModeTests
{
    public sealed class OriginalMissileHaloPhysicsTests
    {
        private readonly List<GameObject> fixtures = new List<GameObject>();

        private VehicleDrive Car()
        {
            var go = new GameObject("original source car for Halo physics");
            fixtures.Add(go);
            go.transform.position = Vector3.back * 30f;
            go.AddComponent<BoxCollider>();
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            return go.AddComponent<VehicleDrive>();
        }

        private (Projectile shot, OriginalMissileSourceFlight halo) Halo(
            VehicleDrive owner)
        {
            var go = new GameObject("no original model - Halo physics test");
            fixtures.Add(go);
            go.AddComponent<SphereCollider>().radius = 0.1f;
            go.AddComponent<Rigidbody>();
            var projectile = go.AddComponent<Projectile>();
            projectile.muzzleVelocity = 12f;
            projectile.lifetimeSeconds =
                OriginalMissileSourceFlight.OriginalLifeTicks * Time.fixedDeltaTime;
            var flight = go.AddComponent<OriginalMissileSourceFlight>();
            flight.Configure(owner);
            projectile.Launch(owner, Vector3.forward);
            flight.ActivateHalo();
            return (projectile, flight);
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
        public IEnumerator HaloMovesActualRigidbodyWithSourceYawNotGuidedTarget()
        {
            VehicleDrive car = Car();
            var missile = Halo(car);
            float initialVelocityX = missile.shot.GetComponent<Rigidbody>().linearVelocity.x;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(missile.halo.State, Is.EqualTo(
                OriginalMissileSourceFlight.SourceState.Halo));
            Assert.That(missile.halo.TicksRemaining,
                Is.LessThan(OriginalMissileSourceFlight.OriginalLifeTicks));
            Assert.That(missile.shot.GetComponent<Rigidbody>().linearVelocity.x,
                Is.GreaterThan(initialVelocityX),
                "The real physics trajectory must yaw instead of sharing normal homing");
        }

        [UnityTest]
        public IEnumerator HaloIgnoresOtherDiagnosticProjectileClassOnly()
        {
            VehicleDrive car = Car();
            var halo = Halo(car);
            var otherObject = new GameObject("other source projectile");
            fixtures.Add(otherObject);
            otherObject.transform.position = Vector3.right * 30f;
            var otherCollider = otherObject.AddComponent<SphereCollider>();
            otherObject.AddComponent<Rigidbody>().useGravity = false;
            otherObject.AddComponent<Projectile>();
            Assert.That(halo.halo.IgnoreDiagnosticProjectileContact(otherCollider), Is.True);

            var vehicleCollider = car.GetComponent<BoxCollider>();
            Assert.That(halo.halo.IgnoreDiagnosticProjectileContact(vehicleCollider), Is.False);
            Assert.That(halo.halo.IgnoreDiagnosticProjectileContact(null), Is.False);
            yield return null;
        }
    }
}
