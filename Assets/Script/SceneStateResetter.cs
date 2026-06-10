using System.Collections.Generic;
using UnityEngine;

public class SceneStateResetter : MonoBehaviour
{
    [SerializeField] private Transform[] resetRoots;
    [SerializeField] private GameObject[] extraActiveStateObjects;
    [SerializeField] private bool captureOnAwake = true;

    private readonly List<TransformSnapshot> transformSnapshots = new List<TransformSnapshot>();
    private readonly List<GameObjectSnapshot> gameObjectSnapshots = new List<GameObjectSnapshot>();

    private void Awake()
    {
        if (captureOnAwake)
            CaptureState();
    }

    public void CaptureState()
    {
        transformSnapshots.Clear();
        gameObjectSnapshots.Clear();

        HashSet<Transform> capturedTransforms = new HashSet<Transform>();
        if (resetRoots != null)
        {
            foreach (Transform root in resetRoots)
                CaptureTransformTree(root, capturedTransforms);
        }

        if (extraActiveStateObjects != null)
        {
            foreach (GameObject obj in extraActiveStateObjects)
            {
                if (obj != null)
                    gameObjectSnapshots.Add(new GameObjectSnapshot(obj));
            }
        }
    }

    public void ResetState()
    {
        ResetKnownTrainingComponents();

        foreach (TransformSnapshot snapshot in transformSnapshots)
            snapshot.Restore();

        foreach (GameObjectSnapshot snapshot in gameObjectSnapshots)
            snapshot.Restore();
    }

    private void CaptureTransformTree(Transform root, HashSet<Transform> capturedTransforms)
    {
        if (root == null || capturedTransforms.Contains(root))
            return;

        capturedTransforms.Add(root);
        transformSnapshots.Add(new TransformSnapshot(root));

        foreach (Transform child in root)
            CaptureTransformTree(child, capturedTransforms);
    }

    private void ResetKnownTrainingComponents()
    {
        foreach (ValveStateTracker valve in FindObjectsByType<ValveStateTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            valve.ResetState();

        foreach (SwitchHitDetector switchHitDetector in FindObjectsByType<SwitchHitDetector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            switchHitDetector.ReTrigger();

        foreach (ValveRotationTracker valveRotationTracker in FindObjectsByType<ValveRotationTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            valveRotationTracker.Reset();

        foreach (TwoHandValveRotator valveRotator in FindObjectsByType<TwoHandValveRotator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            valveRotator.ResetProgress();

        foreach (WaitTimer waitTimer in FindObjectsByType<WaitTimer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            waitTimer.isActive = false;
            waitTimer.ResetTimer();
        }

        foreach (PressureSimulator pressureSimulator in FindObjectsByType<PressureSimulator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            pressureSimulator.isActive = false;
            pressureSimulator.ResetPressure();
        }
    }

    private sealed class TransformSnapshot
    {
        private readonly Transform transform;
        private readonly GameObject gameObject;
        private readonly Rigidbody rigidbody;
        private readonly Vector3 localPosition;
        private readonly Quaternion localRotation;
        private readonly Vector3 localScale;
        private readonly bool activeSelf;

        public TransformSnapshot(Transform transform)
        {
            this.transform = transform;
            gameObject = transform.gameObject;
            rigidbody = transform.GetComponent<Rigidbody>();
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
            localScale = transform.localScale;
            activeSelf = gameObject.activeSelf;
        }

        public void Restore()
        {
            if (transform == null)
                return;

            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;

            if (gameObject != null)
                gameObject.SetActive(activeSelf);

            if (rigidbody != null)
                rigidbody.Sleep();
        }
    }

    private sealed class GameObjectSnapshot
    {
        private readonly GameObject gameObject;
        private readonly bool activeSelf;

        public GameObjectSnapshot(GameObject gameObject)
        {
            this.gameObject = gameObject;
            activeSelf = gameObject.activeSelf;
        }

        public void Restore()
        {
            if (gameObject != null)
                gameObject.SetActive(activeSelf);
        }
    }
}
