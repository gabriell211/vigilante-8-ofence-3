#if UNITY_EDITOR
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Editor-only MGun.UpdateW(1,4,12) *state-machine* port from the
    /// archived 2nd Offense implementation, source:
    /// LegacySources/Vigilante2Unity/Assets/Scripts/MGun.cs.
    /// Bullet visuals/physics, owner velocity conversion, sound and timing
    /// relative to original PS1 ticks are NOT certified.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleDrive))]
    public sealed class OriginalMachineGunSourceTest : MonoBehaviour
    {
        public const int InitialHeat = 0x500;
        public const int BulletLifeSourceTicks = 45;
        public const int NormalBulletDamage = 7;
        public const int DoubleDamageBullet = 14;

        [SerializeField, HideInInspector] private ushort sourceHeat = InitialHeat;
        [SerializeField, HideInInspector] private sbyte sourceTags;
        [SerializeField, HideInInspector] private int shotsFired;

        public int SourceHeat => sourceHeat;
        public int SourceTags => sourceTags;
        public int ShotsFired => shotsFired;
        public bool IsReleaseApproved => false;

        private void Awake() => ResetToSourceInitialState();

        public void ResetToSourceInitialState()
        {
            // Original MGun.UpdateW(arg1=1): maxHalfHealth=1280.
            sourceHeat = InitialHeat;
            sourceTags = 0;
            shotsFired = 0;
        }

        /// <summary>
        /// One call represents one diagnostic fire-event attempt (MGun arg1=12).
        /// Do not invent shots when the input is released. The original engine's
        /// UpdateW dispatch rate is still unknown, so these are Unity physics ticks.
        /// </summary>
        public bool TryFire(bool held)
        {
            if (!held) return false;
            sourceTags = unchecked((sbyte)(sourceTags - 1));
            if (sourceTags != -1) return false;

            // Original after a shot: tags = maxHalfHealth >> 8; heat += 32.
            sourceTags = unchecked((sbyte)(sourceHeat >> 8));
            sourceHeat = unchecked((ushort)(sourceHeat + 32));
            shotsFired++;
            return true;
        }

        /// <summary>
        /// Source MGun.UpdateW(arg1=4): cooldown on holstering, not
        /// automatically once per frame or when releasing the trigger.
        /// </summary>
        public void Holster()
        {
            sourceHeat = (ushort)Mathf.Max(InitialHeat, sourceHeat - 64);
            if (sourceTags != 0)
                sourceTags = unchecked((sbyte)(sourceTags - 1));
        }

        private void OnDisable() => Holster();
    }
}
#endif
