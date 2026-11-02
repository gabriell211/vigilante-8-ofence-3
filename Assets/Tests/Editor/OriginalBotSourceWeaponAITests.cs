using NUnit.Framework;
using UnityEngine;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalBotSourceWeaponAITests
    {
        [Test]
        public void ShootingConeIsHorizontalAndRejectsInvalidDirections()
        {
            Assert.That(BotVehicleInput.IsWithinAimCone(
                Vector3.forward, new Vector3(0f, 20f, 10f), 12f), Is.True);
            Assert.That(BotVehicleInput.IsWithinAimCone(
                Vector3.forward, Vector3.back, 12f), Is.False);
            Assert.That(BotVehicleInput.IsWithinAimCone(
                Vector3.forward, Vector3.right, 12f), Is.False);
            Assert.That(BotVehicleInput.IsWithinAimCone(
                Vector3.up, Vector3.forward, 12f), Is.False);
            Assert.That(BotVehicleInput.IsWithinAimCone(
                Vector3.forward, Vector3.zero, 12f), Is.False);
            Assert.That(BotVehicleInput.IsWithinAimCone(
                Vector3.forward, new Vector3(float.NaN, 0f, 1f), 12f), Is.False);
            Assert.That(BotVehicleInput.IsWithinAimCone(
                Vector3.forward, Vector3.forward, float.PositiveInfinity), Is.False);
        }

        [Test]
        public void TacticalSourceWeaponsNeverBecomeApprovedProductionContent()
        {
            var go = new GameObject("test only - no real source car");
            try
            {
                go.AddComponent<Rigidbody>();
                go.AddComponent<VehicleDrive>();
                var bot = go.AddComponent<BotVehicleInput>();
                var tactics = go.AddComponent<OriginalBotSourceWeaponAI>();
                Assert.That(tactics.IsReleaseApproved, Is.False);
                Assert.That(bot.CanUseSourceWeapon(null), Is.False);
                Assert.That(go.GetComponent<OriginalRocketSourceInventory>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
