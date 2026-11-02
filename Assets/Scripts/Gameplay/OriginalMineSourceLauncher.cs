#if UNITY_EDITOR
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// MineDr.UpdateW(arg1=1,12) authentic initial six charges and
    /// source Mine.cs physics contact, solely for genuine V82 car test scenes.
    /// Does not create a commercial mine model, audio, or fabricated damage.
    /// </summary>
    [RequireComponent(typeof(VehicleDrive), typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class OriginalMineSourceLauncher : MonoBehaviour
    {
        public const int InitialSourceCharges = 6;
        private int remainingCharges = InitialSourceCharges;
        private int deployedCount;
        private VehicleDrive vehicle;

        public int RemainingCharges => remainingCharges;
        public int DeployedCount => deployedCount;
        public bool IsReleaseApproved => false;

        private void Awake()
        {
            vehicle = GetComponent<VehicleDrive>();
            remainingCharges = InitialSourceCharges;
        }

        public bool TryDeploy()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                vehicle == null || !vehicle.IsAlive ||
                GetComponent<LegacyVehiclePlaytest>() == null ||
                remainingCharges <= 0)
                return false;

            // Invisible inspection-only collider; NO fabricated original mesh.
            GameObject objectInstance = new GameObject("SOURCE Mine.cs - NO ORIGINAL MESH");
            objectInstance.transform.position = transform.position -
                transform.forward * 2.5f + Vector3.up * 0.6f;
            var sphere = objectInstance.AddComponent<SphereCollider>();
            sphere.radius = 0.3f;
            var rigidbody = objectInstance.AddComponent<Rigidbody>();
            rigidbody.mass = 0.4f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.linearVelocity = GetComponent<Rigidbody>().linearVelocity -
                transform.forward * 2f + Vector3.up * 1f;
            var sourceMine = objectInstance.AddComponent<OriginalMineSourceTrap>();
            sourceMine.Configure(vehicle);
            remainingCharges--;
            deployedCount++;
            return true;
        }
    }
}
#endif
