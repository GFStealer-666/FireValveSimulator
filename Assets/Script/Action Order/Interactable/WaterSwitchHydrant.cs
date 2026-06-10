using UnityEngine;

public class WaterSwitchHydrant : MonoBehaviour
{
    public void OnRotate()
    {
        this.transform.Rotate(0, 0, -90);
    }
}
