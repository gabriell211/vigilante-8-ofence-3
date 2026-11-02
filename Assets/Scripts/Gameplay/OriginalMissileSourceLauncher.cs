#if UNITY_EDITOR
using UnityEngine;
using Vigilante8.Content;

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

        /// <summary>
        /// Archived MissileL 0x442: one charge only if maxHalfHealth > 1.
        /// This does not modify the normal missile's unverified ammo rules.
        /// </summary>
        public bool TryLaunchHalo()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                vehicle == null || !vehicle.IsAlive ||
                GetComponent<LegacyVehiclePlaytest>() == null ||
                !vehicle.HasWeapon)
                return false;
            var ammo = GetComponent<OriginalMissileSourceInventory>();
            if (ammo == null || !ammo.CanFireHalo ||
                !vehicle.LaunchHaloSourceMissile() || !ammo.TrySpendHalo())
                return false;
            launches++;
            return true;
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
