#if UNITY_EDITOR
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Separate source Rocket.RoadRunner mode for original V82 TMD cars.
    /// Uses exactly the archived RocketL initial charges and 0x422 cost,
    /// without inventing sticky rocket meshes, pickups or PS1 sounds.
    /// </summary>
    [RequireComponent(typeof(VehicleDrive))]
    [DisallowMultipleComponent]
    public sealed class OriginalRoadRunnerSourceLauncher : MonoBehaviour
    {
        private VehicleDrive vehicle;
        private int launches;

        public int Launches => launches;
        public bool IsReleaseApproved => false;

        private void Awake() => vehicle = GetComponent<VehicleDrive>();

        public bool TryLaunch()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                vehicle == null || !vehicle.IsAlive ||
                GetComponent<LegacyVehiclePlaytest>() == null ||
                !vehicle.HasWeapon)
                return false;

            var inventory = GetComponent<OriginalRocketSourceInventory>();
            if (inventory == null || !inventory.CanSpend(
                OriginalRocketSourceInventory.RoadRunnerCost) ||
                !vehicle.LaunchSourceRoadRunner() ||
                !inventory.TrySpend(OriginalRocketSourceInventory.RoadRunnerCost))
                return false;
            launches++;
            return true;
        }
    }
}
#endif
