using UnityEngine;

namespace Vigilante8.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 5f, -11f);
        [Min(0.1f)] public float smoothSpeed = 8f;
        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.TransformPoint(offset);
            float ratio = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, ratio);
            Vector3 aim = target.position + Vector3.up * 1.4f - transform.position;
            if (aim.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(aim), ratio);
        }
    }
}
