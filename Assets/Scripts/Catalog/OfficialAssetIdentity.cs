using System;
using UnityEngine;

namespace Vigilante8.Content
{
    /// <summary>
    /// Identifies a real, authored asset associated with exactly one official entry.
    /// Attaching this marker does not make a missing or placeholder model valid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficialAssetIdentity : MonoBehaviour
    {
        [SerializeField] private string canonicalId;
        [SerializeField] private string sourceGame;

        public string CanonicalId => canonicalId;
        public string SourceGame => sourceGame;

        public bool Matches(string id, string game)
        {
            return !string.IsNullOrWhiteSpace(canonicalId) &&
                string.Equals(canonicalId, id, StringComparison.Ordinal) &&
                string.Equals(sourceGame, game, StringComparison.Ordinal);
        }

#if UNITY_EDITOR
        public void Configure(string id, string game)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(game))
                throw new ArgumentException("A real official identity and source game are required");
            canonicalId = id;
            sourceGame = game;
        }
#endif
    }
}
