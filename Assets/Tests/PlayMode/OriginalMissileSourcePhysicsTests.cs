using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vigilante8.Gameplay;

namespace Vigilante8.PlayModeTests
{
    public sealed class OriginalMissileSourcePhysicsTests
    {
        private readonly List<GameObject> fixtures = new List<GameObject>();

        private VehicleDrive Car(string name, Vector3 position)
        {
            var go = new GameObject(name);
            fixtures.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            return go.AddComponent<VehicleDrive>();
        }

        private (Projectile projectile, OriginalMissileSourceFlight flight) BuildMissile()
        {
            var go = new GameObject("invisible source-guided-missile test fixture");
            fixtures.Add(go);
            go.AddComponent<SphereCollider>().radius = 0.1f;
            go.AddComponent<Rigidbody>();
            var projectile = go.AddComponent<Projectile>();
            projectile.impactDamage = OriginalMissileSourceFlight.OriginalDamage;
            projectile.muzzleVelocity = 16f;
            projectile.lifetimeSeconds =
                OriginalMissileSourceFlight.OriginalLifeTicks * Time.fixedDeltaTime;
            var flight = go.AddComponent<OriginalMissileSourceFlight>();
            return (projectile, flight);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var fixture in fixtures)
                if (fixture != null) Object.Destroy(fixture);
            fixtures.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealMissileRigidbodySteersTowardLockedLivingSourceCar()
        {
            VehicleDrive owner = Car("original-car-shooter", Vector3.back * 20f);
            VehicleDrive target = Car("other-source-car", new Vector3(25f, 0f, 35f));
            var missile = BuildMissile();
            missile.flight.Configure(target);
            missile.projectile.Launch(owner, Vector3.forward);
            missile.flight.ActivateProjectile();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(missile.flight.State,
                Is.EqualTo(OriginalMissileSourceFlight.SourceState.Projectile));
            Assert.That(missile.projectile.GetComponent<Rigidbody>().linearVelocity.x,
                Is.GreaterThan(0f), "Target at positive X must bend actual Rigidbody path");
            Assert.That(missile.flight.TicksRemaining, Is.LessThan(480));
            Assert.That(owner.IsAlive, Is.True);
        }

        [UnityTest]
        public IEnumerator LostOrDestroyedTargetCausesSafeExpiration()
        {
            VehicleDrive owner = Car("original-car-shooter", Vector3.back * 20f);
            VehicleDrive target = Car("original-car-target", new Vector3(25f, 0f, 35f));
            var missile = BuildMissile();
            missile.flight.Configure(target);
            missile.projectile.Launch(owner, Vector3.forward);
            missile.flight.ActivateProjectile();
            target.TakeDamage(target.Health);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(missile.projectile == null, Is.True,
                "Destroyed source-car target must not leave an endless active missile");
        }
    }
}
