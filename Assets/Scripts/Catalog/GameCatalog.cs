using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vigilante8.Content
{
    [Serializable]
    public sealed class GameCatalog
    {
        public int schemaVersion;
        public GameEntry[] games;
        public CharacterEntry[] characters;
        public VehicleEntry[] vehicles;
        public ArenaEntry[] arenas;
    }

    [Serializable]
    public sealed class GameEntry { public string id; public string name; }

    [Serializable]
    public sealed class FactionEntry { public string game; public string name; }

    [Serializable]
    public sealed class CharacterEntry
    {
        public string id;
        public string name;
        public string[] aliases;
        public string[] appearances;
        public FactionEntry[] factions;
    }

    [Serializable]
    public sealed class VehicleEntry
    {
        public string id;
        public string name;
        public string game;
        public string characterId;
        public string assetId;
    }

    [Serializable]
    public sealed class ArenaEntry
    {
        public string id;
        public string name;
        public string game;
        public string assetId;
    }

    public static class CatalogRepository
    {
        private static GameCatalog cached;

        public static GameCatalog Load()
        {
            if (cached != null) return cached;
            var source = Resources.Load<TextAsset>("Catalog/roster");
            if (source == null) throw new InvalidOperationException("Missing Resources/Catalog/roster.json");
            var catalog = JsonUtility.FromJson<GameCatalog>(source.text);
            if (catalog == null || catalog.schemaVersion != 1)
                throw new InvalidOperationException("Unsupported or invalid catalog schema");
            CatalogValidation.RequireValid(catalog);
            cached = catalog;
            return catalog;
        }

        public static IEnumerable<VehicleEntry> VehiclesFor(string characterId)
        {
            foreach (var vehicle in Load().vehicles)
                if (string.Equals(vehicle.characterId, characterId, StringComparison.Ordinal))
                    yield return vehicle;
        }

        public static GameObject LoadVehiclePrefab(VehicleEntry vehicle)
        {
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            return Resources.Load<GameObject>("Vehicles/" + vehicle.assetId);
        }

        public static GameObject LoadArenaPrefab(ArenaEntry arena)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            return Resources.Load<GameObject>("Arenas/" + arena.assetId);
        }
    }
}
