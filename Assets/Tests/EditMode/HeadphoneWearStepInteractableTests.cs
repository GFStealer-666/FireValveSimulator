using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HeadphoneWearStepInteractableTests
{
    [Test]
    public void AutoConfigureXRComponentsAddsRequiredGrabSetup()
    {
        GameObject headphones = new GameObject("Headphones");

        try
        {
            Type interactableType = Type.GetType("HeadphoneWearStepInteractable, Assembly-CSharp");
            Assert.That(interactableType, Is.Not.Null);

            Component interactable = headphones.AddComponent(interactableType);
            MethodInfo configureMethod = interactableType.GetMethod("AutoConfigureXRComponents", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(configureMethod, Is.Not.Null);

            configureMethod.Invoke(interactable, Array.Empty<object>());

            Assert.That(headphones.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(headphones.GetComponentsInChildren<Collider>(true), Has.Some.Matches<Collider>(collider => collider != null && !collider.isTrigger));

            Type grabInteractableType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
            Assert.That(grabInteractableType, Is.Not.Null);
            Assert.That(headphones.GetComponent(grabInteractableType), Is.Not.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(headphones);
        }
    }

    [Test]
    public void IsWithinWearDistanceUsesConfiguredTarget()
    {
        GameObject target = new GameObject("Wear Target");
        GameObject headphones = new GameObject("Headphones");

        try
        {
            target.transform.position = new Vector3(0f, 1.6f, 0f);
            headphones.transform.position = new Vector3(0.2f, 1.55f, 0.1f);

            Type interactableType = Type.GetType("HeadphoneWearStepInteractable, Assembly-CSharp");
            Assert.That(interactableType, Is.Not.Null);

            Component interactable = headphones.AddComponent(interactableType);
            Invoke(interactable, "SetWearTargetForTesting", target.transform);
            Invoke(interactable, "SetWearDistanceForTesting", 0.35f);

            Assert.That(Invoke(interactable, "IsWithinWearDistance"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(headphones);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static object Invoke(Component component, string methodName, params object[] parameters)
    {
        MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(component, parameters);
    }
}
