using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vigilante8.Gameplay;

namespace Vigilante8.PlayModeTests
{
    public sealed class OriginalBastionRocketFlightPhysicsTests
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

        private (Projectile shot, OriginalBastionRocketFlight flight) Rocket()
        {
            var go = new GameObject("Bastion physics fixture - NOT ORIGINAL MESH");
            fixtures.Add(go);
            go.AddComponent<SphereCollider>().radius = 0.1f;
            go.AddComponent<Rigidbody>();
            var projectile = go.AddComponent<Projectile>();
            var flight = go.AddComponent<OriginalBastionRocketFlight>();
            return (projectile, flight);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var go in fixtures)
                if (go != null) Object.Destroy(go);
            fixtures.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BastionActuallySteersRigidbodyTowardLivingCar()
        {
            var source = Car("attacker", Vector3.back * 20f);
            var target = Car("original car physics target", new Vector3(25f, 0f, 30f));
            var pair = Rocket();
            pair.shot.muzzleVelocity = 16f;
            pair.flight.Configure(target, 240);
            pair.shot.Launch(source, Vector3.forward);
            pair.flight.EnterBastion();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(pair.flight.State,
                Is.EqualTo(OriginalBastionRocketFlight.SourceState.Bastion));
            Assert.That(pair.shot.GetComponent<Rigidbody>().linearVelocity.x, Is.GreaterThan(0f),
                "The Rigidbody must steer toward the living target, not fly straight");
            Assert.That(pair.flight.RemainingSourceTicks, Is.LessThan(240));
        }

        [UnityTest]
        public IEnumerator ArchivedTargetValidationTerminatesLostTarget()
        {
            var source = Car("attacker", Vector3.back * 20f);
            var target = Car("destroyed rocket target", new Vector3(12f, 0f, 30f));
            var pair = Rocket();
            pair.flight.Configure(target, 224);
            pair.shot.Launch(source, Vector3.forward);
            pair.flight.EnterBastion();
            target.TakeDamage(target.Health);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(pair.shot == null, Is.True,
                "Invalid source target must not cause a missile to home forever");
        }
    }
}
