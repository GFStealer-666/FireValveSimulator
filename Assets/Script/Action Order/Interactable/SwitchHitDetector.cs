using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

public class SwitchHitDetector : MonoBehaviour
{
    public ActionType switchActionType;
    public UnityEvent onSwitchHit;

    private bool isTriggered = false;
    private ActionOrderManager actionOrderManager;

    private void Awake()
    {
        actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Entering {tag}");
        Debug.Log($"Other collider tag: {other.tag}");
    }

    private void OnTriggerStay(Collider other)
    {
        if (isTriggered || !other.CompareTag("PlayerHand") || !IsTriggerPressed())
            return;

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (actionOrderManager == null)
        {
            Debug.LogWarning($"Switch {tag} cannot report action because no ActionOrderManager was found.");
            return;
        }

        Debug.Log($"Switch hit and trigger pressed. Action: {switchActionType}");
        actionOrderManager.RegisterAction(tag, switchActionType);
        onSwitchHit?.Invoke();
        isTriggered = true;
    }

    public void ReTrigger()
    {
        isTriggered = false;
    }

    private static bool IsTriggerPressed()
    {
        return IsTriggerPressed(XRNode.LeftHand) || IsTriggerPressed(XRNode.RightHand);
    }

    private static bool IsTriggerPressed(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        return device.isValid &&
               device.TryGetFeatureValue(CommonUsages.triggerButton, out bool isPressed) &&
               isPressed;
    }
}
