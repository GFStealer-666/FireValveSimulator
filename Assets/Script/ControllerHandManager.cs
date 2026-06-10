using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerHandManager : MonoBehaviour
{
    public InputActionReference triggerActionReference;
    public InputActionReference gripActionReference;
    public Animator handAnimator;

    private void Awake()
    {
        handAnimator = GetComponent<Animator>();
        SetupInputAction();
    }
    private void OnEnable()
    {
        triggerActionReference?.action.Enable();
        gripActionReference?.action.Enable();
    }
    private void OnDisable()
    {
        triggerActionReference?.action.Disable();
        gripActionReference?.action.Disable();
    }
    private void SetupInputAction()
    {
        if(triggerActionReference != null && gripActionReference != null)
        {
            triggerActionReference.action.performed += ctx => UpdateHandAnimation("Trigger", ctx.ReadValue<float>());
            triggerActionReference.action.canceled  += ctx => UpdateHandAnimation("Trigger",0);

            gripActionReference.action.performed += ctx => UpdateHandAnimation("Grip", ctx.ReadValue<float>());
            gripActionReference.action.canceled += ctx => UpdateHandAnimation("Grip", 0);
        }
    }
    private void UpdateHandAnimation(string paremeterName, float value)
    {
        if(handAnimator != null)
        {
            handAnimator.SetFloat(paremeterName, value);
        }
    }
}
