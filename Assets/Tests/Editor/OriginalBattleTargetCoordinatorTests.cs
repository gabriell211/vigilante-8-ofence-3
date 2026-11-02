using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalBattleTargetCoordinatorTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        private VehicleDrive MakeCar(string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            created.Add(gameObject);
            gameObject.transform.position = position;
            gameObject.AddComponent<Rigidbody>();
            return gameObject.AddComponent<VehicleDrive>();
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (GameObject go in created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }

        [Test]
        public void BotTargetsNearestLivingOpponentNotAlwaysPlayer()
        {
            VehicleDrive bot = MakeCar("test-bot", Vector3.zero);
            VehicleDrive player = MakeCar("test-player", new Vector3(100f, 0f, 0f));
            VehicleDrive otherBot = MakeCar("test-opponent", new Vector3(5f, 0f, 0f));
            Assert.That(OriginalBattleTargetCoordinator.SelectTarget(bot,
                new[] { player, bot, otherBot }), Is.SameAs(otherBot));
        }

        [Test]
        public void DeadTargetIsSkippedAndNextLiveCarIsSelected()
        {
            VehicleDrive bot = MakeCar("test-bot", Vector3.zero);
            VehicleDrive near = MakeCar("test-near", new Vector3(2f, 0f, 0f));
            VehicleDrive far = MakeCar("test-far", new Vector3(6f, 0f, 0f));
            near.TakeDamage(near.Health);
            Assert.That(OriginalBattleTargetCoordinator.SelectTarget(bot,
                new[] { near, bot, far }), Is.SameAs(far));
            far.TakeDamage(far.Health);
            Assert.That(OriginalBattleTargetCoordinator.SelectTarget(bot,
                new[] { near, far, bot }), Is.Null);
        }

        [Test]
        public void TiesRespectOfficialParticipantOrder()
        {
            VehicleDrive seeker = MakeCar("test-seeker", Vector3.zero);
            VehicleDrive first = MakeCar("test-first", new Vector3(-5f, 0f, 0f));
            VehicleDrive second = MakeCar("test-second", new Vector3(5f, 0f, 0f));
            Assert.That(OriginalBattleTargetCoordinator.SelectTarget(seeker,
                new[] { second, first }), Is.SameAs(second));
        }

        [Test]
        public void InvalidCandidatesAndDeadSeekerCannotReceiveTargets()
        {
            VehicleDrive seeker = MakeCar("test-seeker", Vector3.zero);
            VehicleDrive opponent = MakeCar("test-opponent", Vector3.forward);
            Assert.That(OriginalBattleTargetCoordinator.SelectTarget(null,
                new[] { opponent }), Is.Null);
            Assert.That(OriginalBattleTargetCoordinator.SelectTarget(seeker, null), Is.Null);
            seeker.TakeDamage(seeker.Health);
            Assert.That(OriginalBattleTargetCoordinator.SelectTarget(seeker,
                new[] { opponent }), Is.Null);
        }
    }
}
