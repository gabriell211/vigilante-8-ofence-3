using UnityEngine;

namespace Vigilante8.Editor
{
    /// <summary>
    /// The original TMD geometry and materials are available for inspection.
    /// This component deliberately has no vehicle controls, weapon, Rigidbody
    /// or OfficialAssetIdentity: a preview is not a gameplay-ready car.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OriginalVehiclePreviewIdentity : MonoBehaviour
    {
        [SerializeField] private string vehicleId;
        [SerializeField] private int originalTriangles;

        public string VehicleId => vehicleId;
        public int OriginalTriangles => originalTriangles;

#if UNITY_EDITOR
        public void Configure(string id, int triangles)
        {
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("v82_") || triangles <= 0)
                throw new System.ArgumentException("Only verified original TMD preview meshes are supported");
            vehicleId = id;
            originalTriangles = triangles;
        }
#endif
    }
}
