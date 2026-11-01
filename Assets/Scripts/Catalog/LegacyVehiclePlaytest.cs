using System;
using UnityEngine;

namespace Vigilante8.Content
{
    /// <summary>
    /// Marks a source-derived vehicle as a development-only physics prototype.
    /// A real mesh and original sound bank do not certify original weapons,
    /// proportions, transform hierarchies, gameplay or redistribution.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LegacyVehiclePlaytest : MonoBehaviour
    {
        [SerializeField] private string originalVehicleId;
        [SerializeField] private string upstreamCommit;
        [SerializeField] private bool sourceGeometryOnly = true;

        public string OriginalVehicleId => originalVehicleId;
        public bool IsReleaseApproved => false;
        public bool IsSourceGeometryOnly => sourceGeometryOnly;

#if UNITY_EDITOR
        public void Configure(string id, string revision)
        {
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("v82_", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(revision))
                throw new ArgumentException("A real 2nd Offense source identifier and revision are required");
            originalVehicleId = id;
            upstreamCommit = revision;
            sourceGeometryOnly = true;
        }
#endif
    }
}
