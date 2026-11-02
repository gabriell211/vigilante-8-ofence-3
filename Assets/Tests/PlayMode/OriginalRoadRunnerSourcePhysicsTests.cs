using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vigilante8.Gameplay;

namespace Vigilante8.PlayModeTests
{
    public sealed class OriginalRoadRunnerSourcePhysicsTests
    {
        private readonly List<GameObject> fixtures = new List<GameObject>();

        private VehicleDrive Car(string name, Vector3 position)
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

        private (Projectile shot, OriginalRoadRunnerSourceCharge charge)
            Launch(VehicleDrive owner, float speed = 16f)
        {
            var go = new GameObject("invisible RoadRunner physical collision fixture");
            fixtures.Add(go);
            go.transform.position = new Vector3(0f, 0f, 1f);
            go.AddComponent<SphereCollider>().radius = 0.15f;
            go.AddComponent<Rigidbody>();
            var shot = go.AddComponent<Projectile>();
            shot.muzzleVelocity = speed;
            shot.lifetimeSeconds =
                OriginalRoadRunnerSourceCharge.AttachedSourceTicks * Time.fixedDeltaTime + 2f;
            var charge = go.AddComponent<OriginalRoadRunnerSourceCharge>();
            charge.Configure(owner);
            Physics.SyncTransforms();
            shot.Launch(owner, Vector3.forward);
            return (shot, charge);
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
        public IEnumerator PhysicalRocketCollisionAttachesAndDamagesOnlyAfter360Ticks()
        {
            VehicleDrive owner = Car("genuine-shooter-fixture", Vector3.zero);
            VehicleDrive victim = Car("genuine-victim-fixture", new Vector3(0f, 0f, 3f));
            float initialHealth = victim.Health;
            var pair = Launch(owner);
            for (int frame = 0; frame < 30 &&
                pair.charge.State != OriginalRoadRunnerSourceCharge.SourceState.RoadRunner;
                frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(pair.charge.State,
                Is.EqualTo(OriginalRoadRunnerSourceCharge.SourceState.RoadRunner));
            Assert.That(victim.Health, Is.EqualTo(initialHealth),
                "Direct projectile damage must be suppressed on sticky attachment");

            for (int frame = 0; frame < OriginalRoadRunnerSourceCharge.AttachedSourceTicks + 2;
                frame++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(victim.Health,
                Is.EqualTo(initialHealth - OriginalRoadRunnerSourceCharge.OriginalDamageMagnitude)
                    .Within(0.01f), "Exactly one delayed source hit must occur");
            Assert.That(pair.shot == null, Is.True);
            Assert.That(owner.Health, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator EliminatedHostCannotReceiveSecondDelayedHit()
        {
            VehicleDrive owner = Car("genuine-shooter-fixture", Vector3.zero);
            VehicleDrive victim = Car("genuine-victim-fixture", new Vector3(0f, 0f, 3f));
            var pair = Launch(owner);
            for (int frame = 0; frame < 30 &&
                pair.charge.State != OriginalRoadRunnerSourceCharge.SourceState.RoadRunner;
                frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(pair.charge.State,
                Is.EqualTo(OriginalRoadRunnerSourceCharge.SourceState.RoadRunner));

            int events = 0;
            victim.Eliminated += (_, _) => events++;
            victim.TakeDamage(victim.Health);
            for (int frame = 0; frame < 3; frame++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(victim.IsAlive, Is.False);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(pair.shot == null, Is.True,
                "Attached explosive should expire if host died before the timer");
        }
    }
}
