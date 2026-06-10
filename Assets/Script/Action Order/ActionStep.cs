// ====== Script 1: ActionStep.cs ======
using UnityEngine;

public enum ActionType
{
    TurnOffValve,
    TurnOnValve,
    SwitchOff,
    SwitchOn,
    SwitchAuto,
    SwitchStop,
    CheckPSI,
    WaitLimitedTime
}

[CreateAssetMenu(fileName = "NewActionStep", menuName = "FirePump/Action Step")]
[System.Serializable]
public class ActionStep : ScriptableObject
{
    public string stepName;
    public string[] objectTags;
    public ActionType actionType;

    [SerializeField] private float pressureTarget;           // For CheckPSI
    [SerializeField] private float waitDuration;             // For WaitLimitedTime
    [SerializeField] private float requiredRotationDegrees;  // For TurnOnValve or TurnOffValve

    public float PressureTarget => pressureTarget;
    public float WaitDuration => waitDuration;
    public float RequiredRotationDegrees => requiredRotationDegrees;

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(ActionStep))]
    public class ActionStepEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ActionStep step = (ActionStep)target;

            if (step.actionType == ActionType.CheckPSI)
            {
                step.pressureTarget = UnityEditor.EditorGUILayout.FloatField("Pressure Target (PSI)", step.pressureTarget);
            }
            else if (step.actionType == ActionType.WaitLimitedTime)
            {
                step.waitDuration = UnityEditor.EditorGUILayout.FloatField("Wait Duration (seconds)", step.waitDuration);
            }
            else if (step.actionType == ActionType.TurnOnValve || step.actionType == ActionType.TurnOffValve)
            {
                step.requiredRotationDegrees = UnityEditor.EditorGUILayout.FloatField("Required Rotation (degrees)", step.requiredRotationDegrees);
            }

            if (GUI.changed)
            {
                UnityEditor.EditorUtility.SetDirty(step);
            }
        }
    }
#endif
}
