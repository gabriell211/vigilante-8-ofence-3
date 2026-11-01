using System;
using System.Collections.Generic;

namespace Vigilante8.Content
{
    public static class CatalogValidation
    {
        public static void RequireValid(GameCatalog catalog)
        {
            if (catalog == null || catalog.schemaVersion != 1 ||
                catalog.characters == null || catalog.vehicles == null || catalog.arenas == null ||
                catalog.games == null) throw new InvalidOperationException("Invalid catalog");

            var games = UniqueIds(catalog.games, game => game.id, "game");
            var characters = UniqueIds(catalog.characters, character => character.id, "character");
            UniqueIds(catalog.vehicles, vehicle => vehicle.id, "vehicle");
            UniqueIds(catalog.arenas, arena => arena.id, "arena");
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var character in catalog.characters)
            {
                if (string.IsNullOrWhiteSpace(character.name) || !aliases.Add(character.name))
                    throw new InvalidOperationException("Duplicate character name");
                if (character.aliases == null || character.appearances == null ||
                    character.appearances.Length == 0) throw new InvalidOperationException("Incomplete character");
                var appearances = new HashSet<string>(StringComparer.Ordinal);
                foreach (var game in character.appearances)
                    if (!games.Contains(game) || !appearances.Add(game))
                        throw new InvalidOperationException("Invalid or repeated character appearance");
                foreach (var alias in character.aliases)
                    if (string.IsNullOrWhiteSpace(alias) || !aliases.Add(alias))
                        throw new InvalidOperationException("Ambiguous character alias: " + alias);
            }

            var vehicleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var vehicle in catalog.vehicles)
            {
                if (!characters.Contains(vehicle.characterId) || !games.Contains(vehicle.game) ||
                    !IsSafeAssetId(vehicle.assetId) || !vehicleNames.Add(vehicle.name))
                    throw new InvalidOperationException("Invalid or duplicate vehicle: " + vehicle.id);
                CharacterEntry owner = null;
                foreach (var candidate in catalog.characters)
                    if (candidate.id == vehicle.characterId) { owner = candidate; break; }
                if (owner == null || Array.IndexOf(owner.appearances, vehicle.game) < 0)
                    throw new InvalidOperationException("Vehicle owner is absent from source game: " + vehicle.id);
            }
            foreach (var arena in catalog.arenas)
                if (!games.Contains(arena.game) || !IsSafeAssetId(arena.assetId))
                    throw new InvalidOperationException("Invalid arena: " + arena.id);
        }

        private static HashSet<string> UniqueIds<T>(IEnumerable<T> entries, Func<T, string> selector, string label)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var id = selector(entry);
                if (!IsSafeAssetId(id) || !ids.Add(id))
                    throw new InvalidOperationException("Duplicate or unsafe " + label + " id: " + id);
            }
            return ids;
        }

        private static bool IsSafeAssetId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            foreach (var ch in id)
                if (!(ch >= 'a' && ch <= 'z') && !(ch >= '0' && ch <= '9') && ch != '_')
                    return false;
            return true;
        }
    }
}
