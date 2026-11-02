using System;
using System.Linq;
using Vigilante8.Content;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Deterministic selection lists for the official dual-game catalog.
    /// Kept independent of UI Toolkit and PlayerPrefs so every character,
    /// vehicle and arena can be verified in Unity EditMode tests.
    /// </summary>
    public static class OfficialMenuSelections
    {
        public static CharacterEntry[] Pilots(GameCatalog catalog, string gameId = null)
        {
            RequireCatalog(catalog);
            RequireGame(catalog, gameId);
            return catalog.characters.Where(c =>
                gameId == null || (c.appearances != null && c.appearances.Contains(gameId)))
                .OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
        }

        public static VehicleEntry[] Cars(GameCatalog catalog, string gameId = null)
        {
            RequireCatalog(catalog);
            RequireGame(catalog, gameId);
            return catalog.vehicles.Where(v => gameId == null || v.game == gameId)
                .OrderBy(v => v.officialName, StringComparer.Ordinal).ToArray();
        }

        public static ArenaEntry[] Arenas(GameCatalog catalog, string gameId = null)
        {
            RequireCatalog(catalog);
            RequireGame(catalog, gameId);
            return catalog.arenas.Where(a => gameId == null || a.game == gameId)
                .OrderBy(a => a.name, StringComparer.Ordinal).ToArray();
        }

        public static VehicleEntry[] CarsForPilot(GameCatalog catalog, string pilotId)
        {
            RequireCatalog(catalog);
            if (string.IsNullOrWhiteSpace(pilotId) ||
                !catalog.characters.Any(c => c.id == pilotId))
                return Array.Empty<VehicleEntry>();
            return catalog.vehicles.Where(v => v.characterId == pilotId)
                .OrderBy(v => v.name, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Resolves the official driver directly from the selected vehicle, without a
        /// second pilot/faction selection. Rejects altered or unregistered vehicles.
        /// </summary>
        public static CharacterEntry PilotForCar(GameCatalog catalog, VehicleEntry vehicle)
        {
            RequireCatalog(catalog);
            if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.id)) return null;
            VehicleEntry official = catalog.vehicles.FirstOrDefault(v => v.id == vehicle.id);
            if (official == null || official.characterId != vehicle.characterId ||
                official.game != vehicle.game) return null;
            return catalog.characters.FirstOrDefault(c => c.id == official.characterId);
        }

        public static bool OwnsVehicle(GameCatalog catalog, string pilotId, string vehicleId)
        {
            RequireCatalog(catalog);
            return catalog.vehicles.Any(v => v.id == vehicleId && v.characterId == pilotId) &&
                catalog.characters.Any(c => c.id == pilotId);
        }

        private static void RequireCatalog(GameCatalog catalog)
        {
            if (catalog == null || catalog.characters == null || catalog.vehicles == null ||
                catalog.arenas == null || catalog.games == null)
                throw new ArgumentException("The official catalog must be loaded before creating selectors");
        }

        private static void RequireGame(GameCatalog catalog, string gameId)
        {
            if (gameId != null && !catalog.games.Any(g => g.id == gameId))
                throw new ArgumentException("Unknown official game filter", nameof(gameId));
        }
    }
}
