using UnityEngine;
using UnityEngine.SceneManagement;
// MainMenu.cs
public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject loadingPanel;
    
    public void OnStartSimulation()
    {
        mainPanel.SetActive(false);
        loadingPanel.SetActive(true);
        SceneManager.LoadScene("SimulationScene");
    }
    
    public void OnOpenSettings()
    {
        settingsPanel.SetActive(true);
        mainPanel.SetActive(false);
    }
    
    public void OnExit()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
