using UnityEngine;

public class UIFaceCamera : MonoBehaviour
{
    public Transform cameraTransform;
    public float rotationSpeed = 5f;

    void Update()
    {
        Vector3 direction = cameraTransform.position - transform.position;
        direction.y = 0;  // Lock to horizontal rotation

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            // Add this line to flip 180 degrees
            targetRotation *= Quaternion.Euler(0, 180, 0);  
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
}
