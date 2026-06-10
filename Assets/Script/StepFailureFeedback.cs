using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class StepFailureFeedback : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private float minSecondsBetweenPlays = 0.15f;

    private float lastPlayTime = -999f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        ActionOrderManager.OnStepFailed += PlayFailureFeedback;
    }

    private void OnDisable()
    {
        ActionOrderManager.OnStepFailed -= PlayFailureFeedback;
    }

    public void PlayFailureFeedback()
    {
        if (audioSource == null || Time.unscaledTime - lastPlayTime < minSecondsBetweenPlays)
            return;

        lastPlayTime = Time.unscaledTime;

        if (errorClip != null)
            audioSource.PlayOneShot(errorClip);
        else
            audioSource.Play();
    }
}
