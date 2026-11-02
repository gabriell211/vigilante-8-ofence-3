using System;
using NUnit.Framework;
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
        public void InvalidEnemyCountIsNeverTreatedAsVictory()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OriginalBattleSession.EvaluateOutcome(true, -1));
        }
    }
}
