using UnityEngine;

public class PlayerPoseResetter : MonoBehaviour
{
    [SerializeField] private Transform xrOriginRoot;
    [SerializeField] private Camera xrCamera;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private bool alignHeadYawToSpawn = true;
    [SerializeField] private bool matchSpawnHeight = false;

    public void ResetPlayerPose()
    {
        if (spawnPoint == null)
        {
            Debug.LogWarning("Cannot reset player pose because no spawn point is assigned.");
            return;
        }

        if (xrCamera == null)
            xrCamera = Camera.main;

        if (xrOriginRoot == null)
        {
            if (xrCamera != null)
                xrOriginRoot = xrCamera.transform.root;
            else
                xrOriginRoot = transform;
        }

        if (xrOriginRoot == null)
            return;

        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
            characterController.enabled = false;

        if (xrCamera != null)
        {
            if (alignHeadYawToSpawn)
                AlignCameraYawToSpawn();

            Vector3 positionDelta = spawnPoint.position - xrCamera.transform.position;
            if (!matchSpawnHeight)
                positionDelta.y = 0f;

            xrOriginRoot.position += positionDelta;
        }
        else
        {
            xrOriginRoot.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        if (controllerWasEnabled)
            characterController.enabled = true;
    }

    private void AlignCameraYawToSpawn()
    {
        Vector3 cameraForward = Vector3.ProjectOnPlane(xrCamera.transform.forward, Vector3.up);
        Vector3 spawnForward = Vector3.ProjectOnPlane(spawnPoint.forward, Vector3.up);

        if (cameraForward.sqrMagnitude < 0.0001f || spawnForward.sqrMagnitude < 0.0001f)
            return;

        float yawDelta = Vector3.SignedAngle(cameraForward.normalized, spawnForward.normalized, Vector3.up);
        xrOriginRoot.RotateAround(xrCamera.transform.position, Vector3.up, yawDelta);
    }
}
