using System;
using System.Linq;
using NUnit.Framework;
using Vigilante8.Content;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    public sealed class OfficialMenuSelectionsTests
    {
        private GameCatalog catalog;

        [SetUp]
        public void LoadSourceCatalog() => catalog = CatalogRepository.Load();

        [Test]
        public void EveryOfficialCarIsVisibleAndBelongsToExactlyOnePilot()
        {
            var cars = OfficialMenuSelections.Cars(catalog);
            Assert.That(cars.Length, Is.EqualTo(31));
            Assert.That(cars.Select(c => c.id).Distinct().Count(), Is.EqualTo(31));
            foreach (var car in cars)
            {
                Assert.That(OfficialMenuSelections.OwnsVehicle(
                    catalog, car.characterId, car.id), Is.True, car.id);
                Assert.That(OfficialMenuSelections.CarsForPilot(catalog, car.characterId)
                    .Any(c => c.id == car.id), Is.True, car.id);
                Assert.That(OfficialMenuSelections.OwnsVehicle(
                    catalog, "__other_pilot__", car.id), Is.False, car.id);
            }
        }

        [Test]
        public void VersionFilterPreservesExactOfficialCars()
        {
            Assert.That(OfficialMenuSelections.Cars(catalog, "v8").Length, Is.EqualTo(13));
            Assert.That(OfficialMenuSelections.Cars(catalog, "v82").Length, Is.EqualTo(18));
            Assert.Throws<ArgumentException>(() => OfficialMenuSelections.Cars(catalog, "invented"));
        }

        [Test]
        public void EveryOfficialPilotIsSelectable()
        {
            var pilots = OfficialMenuSelections.Pilots(catalog);
            Assert.That(pilots.Length, Is.EqualTo(23));
            foreach (var pilot in pilots)
                Assert.That(OfficialMenuSelections.CarsForPilot(catalog, pilot.id).Length,
                    Is.GreaterThan(0), pilot.name);
            Assert.That(OfficialMenuSelections.CarsForPilot(catalog, "unknown").Length,
                Is.Zero);
        }

        [Test]
        public void BonusArenaAppearsOnlyInV8OriginalCatalog()
        {
            Assert.That(OfficialMenuSelections.Arenas(catalog).Length, Is.EqualTo(19));
            Assert.That(OfficialMenuSelections.Arenas(catalog, "v8").Length, Is.EqualTo(11));
            Assert.That(OfficialMenuSelections.Arenas(catalog, "v82").Length, Is.EqualTo(8));
            var bonus = OfficialMenuSelections.Arenas(catalog)
                .Where(a => a.bonus).ToArray();
            Assert.That(bonus.Length, Is.EqualTo(1));
            Assert.That(bonus[0].id, Is.EqualTo("v8_super_dreamland_64"));
            Assert.That(bonus[0].platforms, Is.EquivalentTo(new[] { "n64" }));
        }
    }
}
