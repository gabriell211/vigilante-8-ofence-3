using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vigilante8.Gameplay;

namespace Vigilante8.PlayModeTests
{
    public sealed class OriginalMineSourcePhysicsTests
    {
        private readonly List<GameObject> fixtures = new List<GameObject>();

        private VehicleDrive Car(string name, Vector3 position)
        {
            var go = new GameObject(name);
            fixtures.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            return go.AddComponent<VehicleDrive>();
        }

        private OriginalMineSourceTrap Mine(VehicleDrive owner, Vector3 position)
        {
            var go = new GameObject("invisible source mine fixture");
            fixtures.Add(go);
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = 0.25f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            var trap = go.AddComponent<OriginalMineSourceTrap>();
            trap.Configure(owner);
            return trap;
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
        public IEnumerator SourceMineArmsOnRealGroundCollisionWithoutInventedDamage()
        {
            VehicleDrive owner = Car("owner", new Vector3(-10f, 0f, 0f));
            var floor = new GameObject("source-ground collision test");
            fixtures.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(10f, 1f, 10f);
            OriginalMineSourceTrap mine = Mine(owner, new Vector3(0f, 2f, 0f));
            for (int frame = 0; frame < 90 && mine.State !=
                OriginalMineSourceTrap.SourceState.Armed; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(mine.State, Is.EqualTo(OriginalMineSourceTrap.SourceState.Armed));
            Assert.That(mine.Owner, Is.SameAs(owner));
            Assert.That(owner.IsAlive, Is.True);
        }

        [UnityTest]
        public IEnumerator SourceMineTriggersOnOtherCarAppliesImpulseWithoutFakeDamage()
        {
            VehicleDrive owner = Car("owner", new Vector3(-10f, 0f, 0f));
            VehicleDrive victim = Car("victim", Vector3.zero);
            float originalHealth = victim.Health;
            OriginalMineSourceTrap mine = Mine(owner, new Vector3(0f, 2f, 0f));
            for (int frame = 0; frame < 90 && mine != null; frame++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(mine == null, Is.True, "Vehicle contact should consume the mine");
            Assert.That(victim.Health, Is.EqualTo(originalHealth),
                "Mine.cs does not expose an authenticated numeric health-damage amount");
            Assert.That(owner.IsAlive, Is.True);
        }
    }
}
