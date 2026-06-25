using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRBaseInteractable))]
public class ValveRotationDebugLogger : MonoBehaviour
{
    private const string Tag = "[VALVE-DBG]";

    private enum HandPoint { Pinch, Grip, Palm, IndexTip }
    private enum HandActivation { Pinch, Grasp, None }

    [Header("Mirror your TwoHandValveRotator settings")]
    [SerializeField] private Transform rotationCenter;
    [SerializeField] private Vector3 localRotationAxis = Vector3.forward;
    [SerializeField] private Vector3 localReferenceDirection = Vector3.up;
    [SerializeField] private HandPoint trackedHandPoint = HandPoint.Pinch;
    [SerializeField] private HandActivation trackedHandActivation = HandActivation.Pinch;
    [SerializeField] private float handActivationRadius = 0.25f;
    [SerializeField, Range(0f, 1f)] private float pinchActivationThreshold = 0.65f;
    [SerializeField, Range(0f, 1f)] private float graspActivationThreshold = 0.65f;
    [SerializeField] private float fallbackPinchMaxDistance = 0.035f;

    [Header("Log throttling")]
    [SerializeField] private float periodicLogInterval = 0.5f;

    private XRBaseInteractable interactable;
    private XROrigin xrOrigin;
    private XRHandSubsystem handSubsystem;
    private static readonly List<XRHandSubsystem> SubsystemBuffer = new List<XRHandSubsystem>();

    private bool lastSubsystemRunning;
    private readonly HandState leftState = new HandState("L");
    private readonly HandState rightState = new HandState("R");

    private class HandState
    {
        public readonly string label;
        public bool tracked;
        public bool activationHeld;
        public bool inRange;
        public bool hasAngle;
        public float lastAngle;
        public float lastPeriodicLogTime;
        public HandState(string label) { this.label = label; }
    }

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (rotationCenter == null) rotationCenter = transform;
        xrOrigin = FindAnyObjectByType<XROrigin>();
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);

        Debug.Log($"{Tag} '{name}' enabled. Colliders={interactable.colliders.Count}, " +
                  $"Layers=0x{interactable.interactionLayers.value:X}, SelectMode={interactable.selectMode}, " +
                  $"InteractionManager={(interactable.interactionManager != null ? "set" : "NULL (will auto-find)")}");

        if (interactable.colliders.Count == 0)
            Debug.LogWarning($"{Tag} '{name}' has NO entries in the Colliders list — controller interactors cannot select this object.");
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs e) =>
        Debug.Log($"{Tag} '{name}' HOVER ENTER by {InteractorName(e.interactorObject)}");
    private void OnHoverExited(HoverExitEventArgs e) =>
        Debug.Log($"{Tag} '{name}' HOVER EXIT  by {InteractorName(e.interactorObject)}");
    private void OnSelectEntered(SelectEnterEventArgs e) =>
        Debug.Log($"{Tag} '{name}' SELECT ENTER by {InteractorName(e.interactorObject)}");
    private void OnSelectExited(SelectExitEventArgs e) =>
        Debug.Log($"{Tag} '{name}' SELECT EXIT  by {InteractorName(e.interactorObject)}");

    private static string InteractorName(IXRInteractor i) =>
        i?.transform != null ? i.transform.name : "<null>";

    private void Update()
    {
        bool running = TryFindRunningSubsystem();
        if (running != lastSubsystemRunning)
        {
            lastSubsystemRunning = running;
            if (running) Debug.Log($"{Tag} XRHandSubsystem is RUNNING.");
            else Debug.LogWarning($"{Tag} XRHandSubsystem NOT running — hand-tracking fallback cannot fire. Enable Hand Tracking Subsystem in OpenXR.");
        }
        if (!running) return;

        Probe(leftState, handSubsystem.leftHand, handSubsystem.leftHandCommonGestures);
        Probe(rightState, handSubsystem.rightHand, handSubsystem.rightHandCommonGestures);
    }

    private void Probe(HandState s, XRHand hand, XRCommonHandGestures gestures)
    {
        bool tracked = hand.isTracked;
        if (tracked != s.tracked)
        {
            s.tracked = tracked;
            Debug.Log($"{Tag} hand {s.label} tracked={tracked}");
            if (!tracked) { ResetHand(s); return; }
        }
        if (!tracked) return;

        bool activationHeld = IsActivationHeld(hand, gestures, out string activationDetail);
        if (activationHeld != s.activationHeld)
        {
            s.activationHeld = activationHeld;
            Debug.Log($"{Tag} hand {s.label} activation={activationHeld}  [{activationDetail}]");
        }
        if (!activationHeld) return;

        if (!TryGetWorldPoint(hand, gestures, out Vector3 worldPos))
        {
            Periodic(s, $"{Tag} hand {s.label} activation held but '{trackedHandPoint}' pose unavailable.");
            return;
        }

        bool inRange = IsNearValve(worldPos, out float closestDist);
        if (inRange != s.inRange)
        {
            s.inRange = inRange;
            Debug.Log($"{Tag} hand {s.label} inRange={inRange} closest={closestDist:F3}m (radius={handActivationRadius:F3})");
        }
        if (!inRange) return;

        if (!TryGetAngle(worldPos, out float angle))
        {
            Periodic(s, $"{Tag} hand {s.label} near valve but angle could not be computed (axis/reference vectors degenerate).");
            return;
        }

        float delta = s.hasAngle ? Mathf.DeltaAngle(s.lastAngle, angle) : 0f;
        s.lastAngle = angle;
        s.hasAngle = true;

        Periodic(s, $"{Tag} hand {s.label} READY angle={angle:F1}° delta={delta:F2}° dist={closestDist:F3}m " +
                    $"isSelected={interactable.isSelected} " +
                    $"(fallback only rotates while NOT selected by a controller)");
    }

    private bool IsActivationHeld(XRHand hand, XRCommonHandGestures gestures, out string detail)
    {
        switch (trackedHandActivation)
        {
            case HandActivation.None:
                detail = "activation=None";
                return true;

            case HandActivation.Grasp:
                if (gestures != null && gestures.TryGetGraspValue(out float g))
                {
                    detail = $"grasp={g:F2} threshold={graspActivationThreshold:F2}";
                    return g >= graspActivationThreshold;
                }
                detail = "no grasp value from gestures";
                return false;

            case HandActivation.Pinch:
            default:
                bool pinchOk = false;
                detail = "no pinch value from gestures";
                if (gestures != null && gestures.TryGetPinchValue(out float p))
                {
                    detail = $"pinch={p:F2} threshold={pinchActivationThreshold:F2}";
                    pinchOk = p >= pinchActivationThreshold;
                }
                if (pinchOk) return true;

                XRHandJoint thumb = hand.GetJoint(XRHandJointID.ThumbTip);
                XRHandJoint index = hand.GetJoint(XRHandJointID.IndexTip);
                if (thumb.TryGetPose(out Pose tp) && index.TryGetPose(out Pose ip))
                {
                    float d = Vector3.Distance(tp.position, ip.position);
                    bool fb = fallbackPinchMaxDistance > 0f && d <= fallbackPinchMaxDistance;
                    detail += $"; thumb-index={d * 1000f:F0}mm max={fallbackPinchMaxDistance * 1000f:F0}mm";
                    return fb;
                }
                return false;
        }
    }

    private bool TryGetWorldPoint(XRHand hand, XRCommonHandGestures gestures, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        Vector3 local;

        if (trackedHandPoint == HandPoint.Pinch && gestures != null && gestures.TryGetPinchPose(out Pose pp))
            local = pp.position;
        else if (trackedHandPoint == HandPoint.Grip && gestures != null && gestures.TryGetGripPose(out Pose gp))
            local = gp.position;
        else
        {
            XRHandJointID jid = trackedHandPoint == HandPoint.IndexTip ? XRHandJointID.IndexTip : XRHandJointID.Palm;
            if (!hand.GetJoint(jid).TryGetPose(out Pose jp)) return false;
            local = jp.position;
        }

        Transform o = xrOrigin != null && xrOrigin.Origin != null ? xrOrigin.Origin.transform : null;
        worldPosition = o != null ? o.TransformPoint(local) : local;
        return true;
    }

    private bool IsNearValve(Vector3 worldPosition, out float closest)
    {
        closest = float.PositiveInfinity;

        for (int i = 0; i < interactable.colliders.Count; i++)
        {
            var c = interactable.colliders[i];
            if (c == null || !c.enabled) continue;
            Vector3 point = SupportsClosestPoint(c) ? c.ClosestPoint(worldPosition) : c.bounds.ClosestPoint(worldPosition);
            float d = Vector3.Distance(point, worldPosition);
            if (d < closest) closest = d;
        }

        if (handActivationRadius > 0f && closest <= handActivationRadius)
            return true;

        float centerDist = Vector3.Distance(rotationCenter.position, worldPosition);
        if (centerDist < closest) closest = centerDist;
        return handActivationRadius <= 0f || centerDist <= handActivationRadius;
    }

    private static bool SupportsClosestPoint(Collider c)
    {
        if (c is MeshCollider mc) return mc.convex;
        return c is BoxCollider || c is SphereCollider || c is CapsuleCollider;
    }

    private bool TryGetAngle(Vector3 worldPosition, out float angle)
    {
        angle = 0f;
        Vector3 center = rotationCenter.position;
        Vector3 normal = transform.TransformDirection(localRotationAxis.normalized);
        Vector3 reference = Vector3.ProjectOnPlane(transform.TransformDirection(localReferenceDirection), normal);
        Vector3 radial = Vector3.ProjectOnPlane(worldPosition - center, normal);

        if (reference.sqrMagnitude < 0.0001f)
            reference = Vector3.ProjectOnPlane(transform.up, normal);
        if (reference.sqrMagnitude < 0.0001f || radial.sqrMagnitude < 0.0001f) return false;

        angle = Vector3.SignedAngle(reference.normalized, radial.normalized, normal);
        return true;
    }

    private void ResetHand(HandState s)
    {
        s.activationHeld = false;
        s.inRange = false;
        s.hasAngle = false;
    }

    private void Periodic(HandState s, string msg)
    {
        if (Time.unscaledTime - s.lastPeriodicLogTime < periodicLogInterval) return;
        s.lastPeriodicLogTime = Time.unscaledTime;
        Debug.Log(msg);
    }

    private bool TryFindRunningSubsystem()
    {
        if (handSubsystem != null && handSubsystem.running) return true;
        SubsystemManager.GetSubsystems(SubsystemBuffer);
        for (int i = 0; i < SubsystemBuffer.Count; i++)
        {
            var sub = SubsystemBuffer[i];
            if (sub != null && sub.running) { handSubsystem = sub; return true; }
        }
        handSubsystem = null;
        return false;
    }
}
