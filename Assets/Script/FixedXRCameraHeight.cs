namespace FireValveSimulator
{
    using Unity.XR.CoreUtils;
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// Keeps the simulated eye height fixed while preserving headset rotation and horizontal tracking.
    /// The camera offset also contains the hands, so they stay aligned with the headset.
    /// </summary>
    public class FixedXRCameraHeight : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField, Min(0f)] private float eyeHeight = 1.65f;
        [SerializeField] private bool showHeightGuide = true;

        private void OnEnable()
        {
            Application.onBeforeRender += ApplyHeight;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyHeight;
        }

        private void LateUpdate()
        {
            ApplyHeight();
        }

        private void ApplyHeight()
        {
            if (xrOrigin == null)
                xrOrigin = FindAnyObjectByType<XROrigin>();

            if (xrOrigin == null || xrOrigin.Camera == null || xrOrigin.CameraFloorOffsetObject == null)
                return;

            Transform offset = xrOrigin.CameraFloorOffsetObject.transform;
            Vector3 position = offset.localPosition;
            float correction = eyeHeight - xrOrigin.CameraInOriginSpaceHeight;

            if (Mathf.Abs(correction) < 0.0001f)
                return;

            position.y += correction;
            offset.localPosition = position;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showHeightGuide)
                return;

            XROrigin origin = xrOrigin != null ? xrOrigin : FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Origin == null || origin.Camera == null)
                return;

            Transform originTransform = origin.Origin.transform;
            Vector3 cameraPosition = originTransform.InverseTransformPoint(origin.Camera.transform.position);
            Vector3 floor = new Vector3(cameraPosition.x, 0f, cameraPosition.z);
            Vector3 eye = floor + Vector3.up * eyeHeight;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = originTransform.localToWorldMatrix;
            Gizmos.color = new Color(0f, 0.9f, 1f, 0.85f);
            Gizmos.DrawWireCube(floor + Vector3.up * (eyeHeight * 0.5f),
                new Vector3(0.45f, eyeHeight, 0.3f));
            Gizmos.DrawWireSphere(eye, 0.07f);
            Gizmos.DrawLine(eye + Vector3.left * 0.32f, eye + Vector3.right * 0.32f);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;

            Handles.Label(originTransform.TransformPoint(eye + Vector3.up * 0.12f),
                $"Eye level: {eyeHeight:0.00} m");
        }
#endif
    }
}
