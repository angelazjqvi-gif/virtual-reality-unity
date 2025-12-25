using UnityEngine;
using UnityEngine.UI;

public class QuitButton : MonoBehaviour
{
    void Awake()
    {
        var btn = GetComponent<Button>();
        btn.onClick.AddListener(QuitGame);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
