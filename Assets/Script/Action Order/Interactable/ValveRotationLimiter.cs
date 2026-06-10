using UnityEngine;

public class ValveRotationLimiter : MonoBehaviour
{
    public Transform valve;
    public Vector3 rotationAxis = Vector3.forward;

    private void Update()
    {
        if (valve == null)
            return;

        Vector3 currentEuler = valve.localEulerAngles;
        valve.localEulerAngles = new Vector3(0f, 0f, currentEuler.z);
    }
}
