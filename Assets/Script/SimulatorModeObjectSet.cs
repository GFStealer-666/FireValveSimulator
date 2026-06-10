using UnityEngine;

public class SimulatorModeObjectSet : MonoBehaviour
{
    [SerializeField] private SimulatorModeManager modeManager;
    [SerializeField] private GameObject[] menuObjects;
    [SerializeField] private GameObject[] learningObjects;
    [SerializeField] private GameObject[] trainingObjects;
    [SerializeField] private GameObject[] examObjects;
    [SerializeField] private GameObject[] gameplayObjects;
    [SerializeField] private bool syncOnStart = true;

    private void Awake()
    {
        if (modeManager == null)
            modeManager = FindAnyObjectByType<SimulatorModeManager>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        SimulatorModeManager.OnModeChanged += ApplyMode;
    }

    private void OnDisable()
    {
        SimulatorModeManager.OnModeChanged -= ApplyMode;
    }

    private void Start()
    {
        if (syncOnStart && modeManager != null)
            ApplyMode(modeManager.CurrentMode);
    }

    public void ApplyMode(SimulatorMode mode)
    {
        bool gameplay = mode != SimulatorMode.Menu;

        SetActive(menuObjects, mode == SimulatorMode.Menu);
        SetActive(learningObjects, mode == SimulatorMode.Learning);
        SetActive(trainingObjects, mode == SimulatorMode.Training);
        SetActive(examObjects, mode == SimulatorMode.Exam);
        SetActive(gameplayObjects, gameplay);
    }

    private void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }
}
