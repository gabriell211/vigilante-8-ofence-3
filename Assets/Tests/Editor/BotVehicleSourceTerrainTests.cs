using NUnit.Framework;
using UnityEngine;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class BotVehicleSourceTerrainTests
    {
        [Test]
        public void GroundAndDriveableSlopesDoNotCountAsWalls()
        {
            Assert.That(BotVehicleInput.IsDriveableSurface(Vector3.up), Is.True);
            Assert.That(BotVehicleInput.IsDriveableSurface(
                new Vector3(0.5f, 0.866f, 0f)), Is.True);
        }

        [Test]
        public void VerticalFacesAndInvalidNormalsRemainObstructions()
        {
            Assert.That(BotVehicleInput.IsDriveableSurface(Vector3.right), Is.False);
            Assert.That(BotVehicleInput.IsDriveableSurface(Vector3.zero), Is.False);
            Assert.That(BotVehicleInput.IsDriveableSurface(
                new Vector3(0.866f, 0.5f, 0f)), Is.False);
        }
    }
}
