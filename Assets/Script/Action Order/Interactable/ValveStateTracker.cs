using UnityEngine;
using UnityEngine.Events;

public class ValveStateTracker : MonoBehaviour
{
    public enum ValveState { On, Off }

    public UnityEvent OnValveRotate;
    public ValveState currentState = ValveState.Off;
    [SerializeField] private ValveState initialState = ValveState.Off;
    [SerializeField] private bool captureInitialStateOnAwake = true;

    private ActionOrderManager actionOrderManager;

    private void Awake()
    {
        actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (captureInitialStateOnAwake)
            initialState = currentState;
    }

    public void ToggleValve()
    {
        currentState = currentState == ValveState.On ? ValveState.Off : ValveState.On;
        Debug.Log($"Valve {tag} now: {currentState}");
        OnValveRotate?.Invoke();

        ActionType actionType = currentState == ValveState.On ? ActionType.TurnOnValve : ActionType.TurnOffValve;
        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (actionOrderManager != null)
        {
            actionOrderManager.RegisterAction(tag, actionType);
        }
        else
        {
            Debug.LogWarning($"Valve {tag} cannot report action because no ActionOrderManager was found.");
        }
    }

    public void ResetState()
    {
        currentState = initialState;
    }
}
