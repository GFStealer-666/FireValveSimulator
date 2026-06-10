using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneReloader : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    public void ReloadCurrentScene()
    {
        SimulatorModeManager modeManager = FindAnyObjectByType<SimulatorModeManager>();
        if (modeManager != null)
        {
            modeManager.ResetCurrentMode();
            return;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void ReloadSceneByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogWarning("Cannot load a scene with an empty name.");
            return;
        }

        SimulatorModeManager modeManager = FindAnyObjectByType<SimulatorModeManager>();
        if (modeManager != null && modeManager.TryStartModeBySceneName(name))
            return;

        SceneManager.LoadScene(name);
    }

    public void LoadMainmenu()
    {
        SimulatorModeManager modeManager = FindAnyObjectByType<SimulatorModeManager>();
        if (modeManager != null)
        {
            modeManager.ShowMenu();
            return;
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
