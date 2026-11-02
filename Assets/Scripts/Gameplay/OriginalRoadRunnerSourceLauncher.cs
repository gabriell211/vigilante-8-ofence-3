#if UNITY_EDITOR
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Separate source Rocket.RoadRunner mode for original V82 TMD cars.
    /// No invented ammo count, sticky rocket mesh or PS1 sound.
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

            if (!vehicle.LaunchSourceRoadRunner()) return false;
            launches++;
            return true;
        }
    }
}
#endif
