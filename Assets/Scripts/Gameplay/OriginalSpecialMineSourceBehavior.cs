#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Source-informed variants from MineDr.cs, Bearhug.cs (class Brearhug),
    /// Hovermine.cs and CactusPatch.cs. These nonrelease colliders use no
    /// counterfeit meshes, health damage, audio, or PS1 fixed-point claims.
    /// The original class name is intentionally misspelled "Brearhug".
    /// </summary>
    [RequireComponent(typeof(OriginalMineSourceTrap), typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class OriginalSpecialMineSourceBehavior : MonoBehaviour
    {
        public enum SourceVariant { Standard, Bearhug, Hovermine, CactusPatch }

        public const int HoverScanInterval = 31; // Hovermine.UpdateW: 30 < M3
        public const int HoverOwnerNeutralAfterScans = 10; // DAT_19 > 10
        public const int CactusMaxCharges = 6; // MineDr: min(6,maxHalfHealth)
        private const float DiagnosticHoverRange = 35f; // NOT a PS1 distance conversion
        private const float DiagnosticImpulse = 5f; // NOT PS1 momentum conversion
        private readonly Collider[] nearby = new Collider[64];

        private SourceVariant variant;
        private VehicleDrive source;
        private int payload;
        private int scanTicks;
        private int scans;
        private int fragmentsSpawned;
        private bool configured;
        private Rigidbody body;
        private VehicleDrive target;

        public SourceVariant Variant => variant;
        public int Payload => payload;
        public int Scans => scans;
        public int FragmentsSpawned => fragmentsSpawned;
        public VehicleDrive Target => target;
        public bool IsReleaseApproved => false;

        /// <summary>Source charge costs: Bearhug/Hovermine 2, Cactus 2..6.</summary>
        public static int ChargeCost(SourceVariant type, int remaining)
        {
            if (remaining < 0 || remaining > OriginalMineSourceLauncher.InitialSourceCharges)
                throw new ArgumentOutOfRangeException(nameof(remaining));
            switch (type)
            {
                case SourceVariant.Standard: return remaining >= 1 ? 1 : 0;
                case SourceVariant.Bearhug:
                case SourceVariant.Hovermine: return remaining >= 2 ? 2 : 0;
                case SourceVariant.CactusPatch: return remaining >= 2 ?
                    Mathf.Min(CactusMaxCharges, remaining) : 0;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public void Configure(SourceVariant type, VehicleDrive owner, int spentCharges)
        {
            if (configured || (type != SourceVariant.Bearhug &&
                type != SourceVariant.Hovermine && type != SourceVariant.CactusPatch) ||
                owner == null || !owner.IsAlive ||
                spentCharges < 1 || spentCharges > CactusMaxCharges ||
                (type == SourceVariant.CactusPatch ? spentCharges < 2 : spentCharges != 2))
                throw new ArgumentException("Authentic source variant and charge cost are required");
            body = GetComponent<Rigidbody>();
            if (body == null) throw new InvalidOperationException("Missing source mine physics");
            variant = type;
            source = owner;
            payload = spentCharges;
            configured = true;
        }

        /// <summary>
        /// Called by OriginalMineSourceTrap during collision. Returns true to
        /// consume this mine. Bearhug's source repeatedly pulls and persists;
        /// Hovermine explodes on contact; CactusPatch scatters child mines.
        /// </summary>
        public bool OnVehicleContact(VehicleDrive victim)
        {
            if (!configured || victim == null || !victim.IsAlive || victim == source)
                return false;
            switch (variant)
            {
                case SourceVariant.Bearhug:
                    Rigidbody pulled = victim.GetComponent<Rigidbody>();
                    if (pulled != null && !pulled.isKinematic)
                    {
                        Vector3 towardMine = transform.position - victim.transform.position;
                        if (towardMine.sqrMagnitude > 0.001f)
                            pulled.AddForce(towardMine.normalized * DiagnosticImpulse,
                                ForceMode.Impulse);
                    }
                    // Bearhug.OnCollision returns 0; it does not destroy itself.
                    return false;

                case SourceVariant.Hovermine:
                    // Its authentic OnCollision calls DAT_A5C impulse just as Mine.
                    Rigidbody affected = victim.GetComponent<Rigidbody>();
                    if (affected != null && !affected.isKinematic)
                        affected.AddForce(Vector3.up * DiagnosticImpulse, ForceMode.Impulse);
                    return true;

                case SourceVariant.CactusPatch:
                    // Original CactusPatch returns without scattering while
                    // tags == 0. Here the source equivalent is Armed state.
                    if (GetComponent<OriginalMineSourceTrap>().State !=
                        OriginalMineSourceTrap.SourceState.Armed)
                        return false;
                    // Original CactusPatch emits maxHalfHealth child Mine objects.
                    // They have no original visuals and no invented health damage.
                    SpawnFragments();
                    return true;
                default:
                    return false;
            }
        }

        private void FixedUpdate()
        {
            if (!configured || variant != SourceVariant.Hovermine ||
                GetComponent<OriginalMineSourceTrap>().State ==
                    OriginalMineSourceTrap.SourceState.Triggered)
                return;
            scanTicks++;
            if (scanTicks >= HoverScanInterval)
            {
                scanTicks = 0;
                scans++;
                target = FindNearestOriginalEnemy();
            }
            if (target != null && target.IsAlive)
            {
                Vector3 movement = target.transform.position - transform.position;
                movement.y = 0f;
                if (movement.sqrMagnitude > 0.001f)
                {
                    Vector3 velocity = body.linearVelocity;
                    Vector3 desired = movement.normalized * 8f;
                    body.linearVelocity = new Vector3(
                        Mathf.Lerp(velocity.x, desired.x, 0.12f),
                        velocity.y,
                        Mathf.Lerp(velocity.z, desired.z, 0.12f));
                }
            }
            else target = null;
        }

        private VehicleDrive FindNearestOriginalEnemy()
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position,
                DiagnosticHoverRange, nearby, -1, QueryTriggerInteraction.Ignore);
            if (hits >= nearby.Length) return null;
            VehicleDrive selected = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                VehicleDrive candidate = nearby[i]?.GetComponentInParent<VehicleDrive>();
                if (candidate == null || candidate == source || !candidate.IsAlive ||
                    candidate.GetComponent<LegacyVehiclePlaytest>() == null) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                selected = candidate;
            }
            return selected;
        }

        private void SpawnFragments()
        {
            for (int i = 0; i < payload; i++)
            {
                var fragment = new GameObject("SOURCE CactusPatch Mine fragment - NO MESH");
                float angle = 2f * Mathf.PI * i / payload;
                Vector3 planar = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                fragment.transform.position = transform.position + planar * 0.45f + Vector3.up * 0.8f;
                fragment.AddComponent<SphereCollider>().radius = 0.2f;
                var rigidbody = fragment.AddComponent<Rigidbody>();
                rigidbody.mass = 0.25f;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.linearVelocity = planar * 4f + Vector3.up * 6f;
                fragment.AddComponent<OriginalMineSourceTrap>().Configure(source);
                fragmentsSpawned++;
            }
        }
    }
}
#endif
