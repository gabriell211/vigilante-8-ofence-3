using System;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    public sealed class ArenaSpawnPoints : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        public int Count => spawnPoints == null ? 0 : spawnPoints.Length;

        public bool HasValidSpawns(int minimum)
        {
            if (Count < minimum) return false;
            foreach (var point in spawnPoints)
                if (point == null) return false;
            return true;
        }

        public Transform GetSpawn(int index)
        {
            if (index < 0 || index >= Count || spawnPoints[index] == null)
                throw new InvalidOperationException("Invalid arena spawn point at " + index);
            return spawnPoints[index];
        }
    }
}
