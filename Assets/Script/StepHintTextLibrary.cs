using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StepHintTextLibrary", menuName = "FirePump/Step Hint Text Library")]
public class StepHintTextLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ActionStep step;
        public string title;
        [TextArea(2, 6)] public string trainingText;
        [TextArea(2, 8)] public string learningDetailText;
    }

    [SerializeField] private Entry[] entries;

    public Entry[] Entries => entries;

    public Entry Find(ActionStep step)
    {
        if (step == null || entries == null)
            return null;

        foreach (Entry entry in entries)
        {
            if (entry != null && entry.step == step)
                return entry;
        }

        return null;
    }
}
