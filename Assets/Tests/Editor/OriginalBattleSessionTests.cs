using System;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalBattleSessionTests
    {
        [TestCase(true, 3, OriginalBattleSession.BattleOutcome.InProgress)]
        [TestCase(true, 1, OriginalBattleSession.BattleOutcome.InProgress)]
        [TestCase(true, 0, OriginalBattleSession.BattleOutcome.Victory)]
        [TestCase(false, 3, OriginalBattleSession.BattleOutcome.Defeat)]
        [TestCase(false, 0, OriginalBattleSession.BattleOutcome.Defeat)]
        public void OutcomeReflectsActualPlayerAndEnemySurvival(
            bool playerAlive, int enemiesAlive, OriginalBattleSession.BattleOutcome expected)
        {
            Assert.That(OriginalBattleSession.EvaluateOutcome(playerAlive, enemiesAlive),
                Is.EqualTo(expected));
        }

        [Test]
        public void OnlyRealParticipantsReceiveKillsAndSelfHitsGiveNoPoints()
        {
            var objects = new GameObject[5];
            try
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    objects[i] = new GameObject("test-vehicle-" + i);
                    objects[i].AddComponent<Rigidbody>();
                }
                var player = objects[0].AddComponent<VehicleDrive>();
                var first = objects[1].AddComponent<VehicleDrive>();
                var second = objects[2].AddComponent<VehicleDrive>();
                var third = objects[3].AddComponent<VehicleDrive>();
                var external = objects[4].AddComponent<VehicleDrive>();
                var opponents = new[] { first, second, third };

                Assert.That(OriginalBattleSession.CreditedKillerSlot(first,
                    player, player, opponents), Is.EqualTo(0));
                Assert.That(OriginalBattleSession.CreditedKillerSlot(first,
                    second, player, opponents), Is.EqualTo(2));
                Assert.That(OriginalBattleSession.CreditedKillerSlot(player,
                    third, player, opponents), Is.EqualTo(3));
                Assert.That(OriginalBattleSession.CreditedKillerSlot(first,
                    first, player, opponents), Is.EqualTo(-1));
                Assert.That(OriginalBattleSession.CreditedKillerSlot(first,
                    null, player, opponents), Is.EqualTo(-1));
                Assert.That(OriginalBattleSession.CreditedKillerSlot(first,
                    external, player, opponents), Is.EqualTo(-1));
                Assert.That(OriginalBattleSession.CreditedKillerSlot(external,
                    player, player, opponents), Is.EqualTo(-1));
            }
            finally
            {
                foreach (GameObject obj in objects)
                    if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void InvalidEnemyCountIsNeverTreatedAsVictory()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OriginalBattleSession.EvaluateOutcome(true, -1));
        }
    }
}
