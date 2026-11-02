#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Source-derived Rocket.RoadRunner mode from archived
    /// LegacySources/Vigilante2Unity/Assets/Scripts/Rocket.cs.
    /// On a car collision, this diagnostic rocket attaches to the victim for
    /// 360 source ticks, then requests the legacy -40 damage through
    /// VehicleDrive. Original vehicle body segmentation, audio and 3D model
    /// remain unavailable and are not asserted as commercial equivalence.
    /// </summary>
    [RequireComponent(typeof(Projectile), typeof(Rigidbody), typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class OriginalRoadRunnerSourceCharge : MonoBehaviour
    {
        public enum SourceState { Rocket, RoadRunner, Expired }
        public const int AttachedSourceTicks = 360;
        public const int OriginalDamageMagnitude = 40;

        private VehicleDrive owner;
        private VehicleDrive attachedTarget;
        private Rigidbody body;
        private bool configured;
        private int remainingTicks;
        private SourceState state;

        public SourceState State => state;
        public VehicleDrive AttachedTarget => attachedTarget;
        public int RemainingTicks => remainingTicks;
        public bool IsReleaseApproved => false;

        public void Configure(VehicleDrive source)
        {
            if (configured || source == null || !source.IsAlive)
                throw new ArgumentException("A new RoadRunner test requires a live source vehicle");
            if (GetComponent<Projectile>() == null || GetComponent<Rigidbody>() == null ||
                GetComponent<Collider>() == null)
                throw new InvalidOperationException("An original rocket requires a physics projectile");
            owner = source;
            body = GetComponent<Rigidbody>();
            state = SourceState.Rocket;
            configured = true;
        }

        /// <summary>
        /// Called exclusively from Projectile.OnCollisionEnter. Returns true if
        /// a car absorbed the rocket, suppressing its regular immediate hit.
        /// Rocket.cs checks the 0x1000000 mode flag before attaching.
        /// This component is the corresponding explicit source-mode opt-in.
        /// </summary>
        public bool TryAttach(VehicleDrive victim)
        {
            if (!configured || state != SourceState.Rocket ||
                victim == null || !victim.IsAlive || victim == owner)
                return false;

            attachedTarget = victim;
            state = SourceState.RoadRunner;
            remainingTicks = AttachedSourceTicks;
            var collider = GetComponent<Collider>();
            collider.enabled = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            transform.SetParent(victim.transform, true);
            return true;
        }

        private void FixedUpdate()
        {
            if (!configured || state != SourceState.RoadRunner) return;
            if (attachedTarget == null || !attachedTarget.IsAlive)
            {
                Expire();
                return;
            }
            remainingTicks--;
            if (remainingTicks > 0) return;

            state = SourceState.Expired;
            // Rocket.RoadRunner UpdateW(0) uses FUN_3A020(-40, position, true)
            // once, then destroys the attachment. VehicleDrive health is
            // provisional: source damage segmentation is not ported.
            attachedTarget.TakeDamage(OriginalDamageMagnitude, owner);
            Destroy(gameObject);
        }

        private void Expire()
        {
            state = SourceState.Expired;
            Destroy(gameObject);
        }
    }
}
#endif
