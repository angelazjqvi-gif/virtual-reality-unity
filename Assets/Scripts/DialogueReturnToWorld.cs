using UnityEngine;
using UnityEngine.SceneManagement;

public class DialogueReturnToWorld : MonoBehaviour
{
    public KeyCode exitKey = KeyCode.Escape;

    private const string PREF_RETURN_SCENE = "PREF_RETURN_SCENE";
    private const string PREF_RETURN_SPAWN = "PREF_RETURN_SPAWN";

    void Update()
    {
        if (Input.GetKeyDown(exitKey))
            ExitToWorld();
    }

    public void ExitToWorld()
    {
        string worldScene = PlayerPrefs.GetString(PREF_RETURN_SCENE, "world1");
        string spawnId   = PlayerPrefs.GetString(PREF_RETURN_SPAWN, "");

        if (GameSession.I != null && !string.IsNullOrEmpty(spawnId))
        {
            GameSession.I.BeginWorldTransfer(worldScene, spawnId, true);
        }

        // 清理缓存
        PlayerPrefs.DeleteKey(PREF_RETURN_SCENE);
        PlayerPrefs.DeleteKey(PREF_RETURN_SPAWN);
        PlayerPrefs.Save();

        // 直接回场景
        SceneManager.LoadScene(worldScene);
    }
}
