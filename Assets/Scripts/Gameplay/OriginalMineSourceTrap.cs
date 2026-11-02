#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Development-only Mine.cs OnCollision and FUN_46D70 grounded behavior
    /// from LegacySources/Vigilante2Unity/Assets/Scripts/Mine.cs.
    /// Has no original mesh. The source applies FUN_2B370(DAT_A5C,screen),
    /// an impulse, NOT a known numerical VehicleDrive damage value.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class OriginalMineSourceTrap : MonoBehaviour
    {
        public enum SourceState { Airborne, Armed, Triggered }
        public const int SourceMaxRegistered = 16;
        public const int SourceVerticalImpulseY = -0x20000;

        private static readonly List<OriginalMineSourceTrap> active = new List<OriginalMineSourceTrap>();
        private VehicleDrive owner;
        private Rigidbody body;
        private SourceState state;
        private bool initialized;

        public SourceState State => state;
        public VehicleDrive Owner => owner;
        public bool IsReleaseApproved => false;
        public static int ActiveCount
        {
            get
            {
                active.RemoveAll(x => x == null);
                return active.Count;
            }
        }

        /// <summary>
        /// Registers with the original GameManager.DAT_10C8 maximum of 16
        /// deployed mines. This uses Unity's scene lifetime, not PS1 worldObjs.
        /// </summary>
        public void Configure(VehicleDrive source)
        {
            if (initialized || source == null || !source.IsAlive)
                throw new ArgumentException("An unconfigured mine requires a real living car");
            body = GetComponent<Rigidbody>();
            Collider mineCollider = GetComponent<Collider>();
            if (body == null || mineCollider == null)
                throw new InvalidOperationException("Source mine physics is missing");

            owner = source;
            state = SourceState.Airborne;
            initialized = true;

            foreach (Collider ownCollider in owner.GetComponentsInChildren<Collider>())
                if (ownCollider != null && ownCollider.enabled)
                    Physics.IgnoreCollision(mineCollider, ownCollider);

            active.RemoveAll(x => x == null);
            if (active.Count >= SourceMaxRegistered)
            {
                OriginalMineSourceTrap oldest = active[0];
                active.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }
            active.Add(this);
        }

        /// <summary>
        /// Mine.cs returns early for terrain (type 0) but triggers on other
        /// object collision, especially a vehicle (type 2). Source uses
        /// DAT_A5C (negative PS1 vertical axis) as an upward push in Unity.
        /// The impulse magnitude is deliberately a physics diagnostic.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (!initialized || state == SourceState.Triggered) return;
            VehicleDrive victim = collision.collider.GetComponentInParent<VehicleDrive>();
            if (victim == owner) return;
            if (victim != null && victim.IsAlive)
            {
                var variant = GetComponent<OriginalSpecialMineSourceBehavior>();
                if (variant != null)
                {
                    if (variant.OnVehicleContact(victim))
                    {
                        state = SourceState.Triggered;
                        Destroy(gameObject);
                    }
                    else if (state == SourceState.Airborne)
                        state = SourceState.Armed;
                    return;
                }
                state = SourceState.Triggered;
                Rigidbody targetBody = victim.GetComponent<Rigidbody>();
                if (targetBody != null && !targetBody.isKinematic)
                    targetBody.AddForce(Vector3.up * 5f, ForceMode.Impulse);
                // Source Mine.OnCollision does not expose a directly verified
                // Health damage scalar. Do not fabricate damage/kill credit.
                Destroy(gameObject);
                return;
            }
            if (victim == null && state == SourceState.Airborne)
                state = SourceState.Armed;
        }

        private void OnDestroy() => active.Remove(this);
    }
}
#endif
