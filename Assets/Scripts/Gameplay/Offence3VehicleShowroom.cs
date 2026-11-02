using System;
using System.Linq;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Renders real approved vehicle prefabs into the garage UI as an isolated
    /// rotating 3D showroom. Missing official assets never get substitutes.
    /// </summary>
    public sealed class Offence3VehicleShowroom : MonoBehaviour
    {
        private const int PreviewLayer = 30;
        private static readonly Vector3 PreviewCenter = new Vector3(0f, -10000f, 0f);

        private RenderTexture previewTexture;
        private GameObject pivot;
        private GameObject vehicle;
        private GameObject cameraObject;
        private GameObject lightObject;
        private Camera previewCamera;
        private string selectedId;
        private bool active;
        private float rotationAngle;

        public RenderTexture Texture => previewTexture;

        private void Awake()
        {
            previewTexture = new RenderTexture(704, 400, 24, RenderTextureFormat.ARGB32)
            {
                name = "Offence3 Original Vehicle Garage Preview",
                antiAliasing = 2,
                useMipMap = false
            };
            previewTexture.Create();
            pivot = new GameObject("Official vehicle preview pivot");
            pivot.transform.position = PreviewCenter;
            pivot.layer = PreviewLayer;

            cameraObject = new GameObject("Offence3 showroom camera");
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.targetTexture = previewTexture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.07f, 0.10f, 0.13f);
            previewCamera.fieldOfView = 34f;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.nearClipPlane = 0.03f;
            previewCamera.farClipPlane = 250f;
            previewCamera.enabled = false;

            lightObject = new GameObject("Offence3 showroom light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.75f;
            light.cullingMask = 1 << PreviewLayer;
            lightObject.transform.rotation = Quaternion.Euler(38f, -48f, 0f);
        }

        public void SetVisible(bool visible)
        {
            active = visible;
            if (previewCamera != null)
                previewCamera.enabled = visible && vehicle != null;
        }

        public bool ShowOfficialVehicle(VehicleEntry descriptor)
        {
            if (descriptor == null)
            {
                ClearVehicle();
                return false;
            }
            if (selectedId == descriptor.id)
                return vehicle != null;

            ClearVehicle();
            selectedId = descriptor.id;
            GameObject source = CatalogRepository.LoadVehiclePrefab(descriptor);
            if (source == null || source.GetComponent<LegacyVehiclePlaytest>() != null)
                return false;

            var identity = source.GetComponent<OfficialAssetIdentity>();
            if (identity == null || !identity.Matches(descriptor.id, descriptor.game) ||
                source.GetComponent<VehicleDrive>() == null)
                return false;

            try
            {
                vehicle = Instantiate(source);
                vehicle.name = "OFFICIAL SOURCE PREVIEW - " + descriptor.id;
                foreach (MonoBehaviour behaviour in vehicle.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;
                foreach (Rigidbody body in vehicle.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                    body.useGravity = false;
                }
                foreach (Collider collider in vehicle.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;

                foreach (Transform item in vehicle.GetComponentsInChildren<Transform>(true))
                    item.gameObject.layer = PreviewLayer;

                vehicle.transform.SetParent(pivot.transform, true);
                vehicle.transform.position = PreviewCenter;
                Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.enabled && r.sharedMaterials != null &&
                                r.sharedMaterials.Length > 0).ToArray();
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Official car has no visible authored mesh");

                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1))
                    bounds.Encapsulate(renderer.bounds);
                if (bounds.size.sqrMagnitude < 0.001f ||
                    float.IsNaN(bounds.size.magnitude))
                    throw new InvalidOperationException("Official car has invalid preview geometry");

                vehicle.transform.position += PreviewCenter - bounds.center;
                float dimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float distance = Mathf.Clamp(dimension / Mathf.Tan(previewCamera.fieldOfView *
                    Mathf.Deg2Rad * 0.5f) + 1f, 4f, 150f);
                cameraObject.transform.position = PreviewCenter +
                    new Vector3(0f, dimension * 0.20f, -distance);
                cameraObject.transform.LookAt(PreviewCenter);
                previewCamera.farClipPlane = Mathf.Max(250f, distance * 3f);
                rotationAngle = 0f;
                SetVisible(active);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Official showroom unavailable for " + descriptor.id +
                    ": " + ex.Message);
                ClearVehicle();
                return false;
            }
        }

        private void Update()
        {
            if (!active || vehicle == null || pivot == null) return;
            rotationAngle = Mathf.Repeat(rotationAngle + 16f * Time.unscaledDeltaTime, 360f);
            pivot.transform.rotation = Quaternion.Euler(0f, rotationAngle, 0f);
        }

        private void ClearVehicle()
        {
            if (previewCamera != null) previewCamera.enabled = false;
            if (vehicle != null) Destroy(vehicle);
            vehicle = null;
            selectedId = null;
            if (pivot != null) pivot.transform.rotation = Quaternion.identity;
        }

        private void OnDestroy()
        {
            ClearVehicle();
            if (pivot != null) Destroy(pivot);
            if (cameraObject != null) Destroy(cameraObject);
            if (lightObject != null) Destroy(lightObject);
            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
                previewTexture = null;
            }
        }
    }
}
