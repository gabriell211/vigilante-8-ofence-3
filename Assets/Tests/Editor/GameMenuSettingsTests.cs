using System;
using NUnit.Framework;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    public sealed class GameMenuSettingsTests
    {
        [TestCase(-2f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(2f, 1f)]
        public void MasterVolumeIsBounded(float raw, float expected)
        {
            Assert.That(GameMenuSettings.NormalizeVolume(raw), Is.EqualTo(expected));
        }

        [Test]
        public void MalformedVolumeFallsBackToASafeDefault()
        {
            Assert.That(GameMenuSettings.NormalizeVolume(float.NaN), Is.EqualTo(0.85f));
            Assert.That(GameMenuSettings.NormalizeVolume(float.PositiveInfinity), Is.EqualTo(0.85f));
            Assert.That(GameMenuSettings.NormalizeVolume(float.NegativeInfinity), Is.EqualTo(0.85f));
        }

        [TestCase(-1, 4, 0)]
        [TestCase(0, 4, 0)]
        [TestCase(2, 4, 2)]
        [TestCase(99, 4, 3)]
        [TestCase(99, 0, 0)]
        public void QualityNeverEscapesTheUnityConfiguredLevels(int raw, int available, int expected)
        {
            Assert.That(GameMenuSettings.NormalizeQuality(raw, available), Is.EqualTo(expected));
        }

        [Test]
        public void UnofficialOrUnsafeSelectionsCannotBePersisted()
        {
            string[] official = { "v82_tsunami", "v8_rattler" };
            Assert.Throws<ArgumentException>(() =>
                GameMenuSettings.StoreSelection(GameMenuSettings.VehicleKey, "fictional_vehicle", official));
            Assert.Throws<ArgumentException>(() =>
                GameMenuSettings.StoreSelection("arbitrary.key", "v82_tsunami", official));
            Assert.Throws<ArgumentException>(() =>
                GameMenuSettings.StoreSelection(GameMenuSettings.VehicleKey, null, official));
        }
    }
}
