using UnityEngine;

namespace Vigilante8.Gameplay
{
    [RequireComponent(typeof(VehicleDrive))]
    public sealed class BotVehicleInput : MonoBehaviour
    {
        public Transform target;
        [Min(1f)] public float shootingRange = 75f;
        [Min(0.1f)] public float decisionInterval = 0.1f;
        [Min(1f)] public float obstacleDistance = 14f;
        [SerializeField] private LayerMask obstructionLayers = -1;

        private const int QueryCapacity = 32;
        private readonly RaycastHit[] obstacleHits = new RaycastHit[QueryCapacity];
        private readonly RaycastHit[] shotHits = new RaycastHit[QueryCapacity];
        private VehicleDrive vehicle;
        private float nextDecision;

        private void Awake() => vehicle = GetComponent<VehicleDrive>();

        /// <summary>Ground and driveable source slopes are not steering obstacles.</summary>
        public static bool IsDriveableSurface(Vector3 normal)
        {
            return normal.sqrMagnitude > 0.0001f && normal.normalized.y >= 0.7f;
        }

        /// <summary>
        /// Geometry-only aim test on the horizontal plane. Safe on steep source
        /// terrain and invalid directions; it is NOT original PS1 bot behavior.
        /// </summary>
        public static bool IsWithinAimCone(Vector3 forward, Vector3 direction, float maxDegrees)
        {
            if (float.IsNaN(maxDegrees) || float.IsInfinity(maxDegrees) ||
                maxDegrees <= 0f || maxDegrees > 180f)
                return false;
            forward.y = 0f;
            direction.y = 0f;
            if (!Finite(forward.x) || !Finite(forward.z) ||
                !Finite(direction.x) || !Finite(direction.z) ||
                forward.sqrMagnitude < 0.0001f || direction.sqrMagnitude < 0.0001f)
                return false;
            return Vector3.Angle(forward, direction) <= maxDegrees;
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

#if UNITY_EDITOR
        /// <summary>
        /// Uses the bot's CURRENT live target, existing firing range and
        /// obstacle raycast before diagnostic source weapons may launch.
        /// This explicitly does not certify the original opponent AI.
        /// </summary>
        public bool CanUseSourceWeapon(VehicleDrive intendedTarget)
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                vehicle == null || !vehicle.IsAlive ||
                intendedTarget == null || !intendedTarget.IsAlive ||
                target == null || target.GetComponentInParent<VehicleDrive>() != intendedTarget)
                return false;
            Vector3 offset = intendedTarget.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= 0.01f ||
                offset.sqrMagnitude >= shootingRange * shootingRange ||
                !IsWithinAimCone(transform.forward, offset, 12f))
                return false;
            return HasClearShot(intendedTarget);
        }
#endif

        private void FixedUpdate()
        {
            if (!vehicle.IsAlive) return;
            VehicleDrive targetDrive = target == null ? null : target.GetComponentInParent<VehicleDrive>();
            if (targetDrive == null || !targetDrive.IsAlive)
            {
                vehicle.SetControls(0f, 0f, 1f, false);
                return;
            }
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + decisionInterval;
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f)
            {
                vehicle.SetControls(0f, 0f, 1f, false);
                return;
            }

            Vector3 local = transform.InverseTransformDirection(delta.normalized);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float steering = Mathf.Clamp(angle / 45f, -1f, 1f);
            float gas = Mathf.Abs(angle) > 115f ? 0.45f : 1f;

            if (TryFindBlockingObstacle(out RaycastHit obstacle))
            {
                Vector3 localAvoid = transform.InverseTransformDirection(obstacle.normal);
                steering = localAvoid.x >= 0f ? 1f : -1f;
                gas = 0.55f;
            }

            bool aligned = Mathf.Abs(angle) < 12f;
            bool inRange = delta.sqrMagnitude < shootingRange * shootingRange;
            bool unobstructed = aligned && inRange && HasClearShot(targetDrive);
            vehicle.SetControls(gas, steering, 0f, unobstructed);
        }

        private bool TryFindBlockingObstacle(out RaycastHit nearest)
        {
            nearest = default;
            float closest = float.PositiveInfinity;
            Vector3 origin = transform.position + Vector3.up;
            int count = Physics.SphereCastNonAlloc(new Ray(origin, transform.forward), 0.75f,
                obstacleHits, obstacleDistance, obstructionLayers, QueryTriggerInteraction.Ignore);
            // When the fixed buffer fills, fail safe instead of steering on an
            // arbitrarily truncated set of source mesh / vehicle colliders.
            if (count == obstacleHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = obstacleHits[i];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(transform) ||
                    (target != null && hit.collider.transform.IsChildOf(target)) ||
                    IsDriveableSurface(hit.normal) ||
                    hit.distance >= closest)
                    continue;
                nearest = hit;
                closest = hit.distance;
            }
            return closest < float.PositiveInfinity;
        }

        private bool HasClearShot(VehicleDrive targetDrive)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 destination = targetDrive.transform.position + Vector3.up;
            Vector3 direction = destination - origin;
            float distance = direction.magnitude;
            if (distance < 0.1f) return false;
            int count = Physics.RaycastNonAlloc(origin, direction / distance, shotHits,
                distance, obstructionLayers, QueryTriggerInteraction.Ignore);
            if (count == shotHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider hit = shotHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) ||
                    hit.transform.IsChildOf(targetDrive.transform))
                    continue;
                return false;
            }
            return true;
        }
    }
}
