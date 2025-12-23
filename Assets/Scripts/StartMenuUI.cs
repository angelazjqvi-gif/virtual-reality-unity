using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuUI : MonoBehaviour
{
    [Header("Scene Names")]
    public string dialogueSceneName = "dialogue"; 

    public void OnClickStartGame()
    {
        SceneManager.LoadScene(dialogueSceneName);
    }

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
