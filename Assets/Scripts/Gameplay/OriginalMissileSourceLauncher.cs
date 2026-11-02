#if UNITY_EDITOR
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Fire-event control for the original MissileL/Missile source prototype.
    /// No invented ammo counter, weapon model, rarity or weapon sound.
    /// </summary>
    [RequireComponent(typeof(VehicleDrive))]
    [DisallowMultipleComponent]
    public sealed class OriginalMissileSourceLauncher : MonoBehaviour
    {
        private VehicleDrive vehicle;
        private VehicleDrive lockedTarget;
        private int launches;

        public VehicleDrive LockedTarget => lockedTarget;
        public int Launches => launches;
        public bool IsReleaseApproved => false;

        private void Awake() => vehicle = GetComponent<VehicleDrive>();

        public void SetTarget(VehicleDrive target)
        {
            lockedTarget = target != vehicle && target != null && target.IsAlive ? target : null;
        }

        public bool TryLaunch()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                vehicle == null || !vehicle.IsAlive ||
                GetComponent<LegacyVehiclePlaytest>() == null ||
                lockedTarget == null || !lockedTarget.IsAlive ||
                !vehicle.HasWeapon)
                return false;

            if (!vehicle.LaunchSourceMissile(lockedTarget)) return false;
            launches++;
            return true;
        }
    }
}
#endif
