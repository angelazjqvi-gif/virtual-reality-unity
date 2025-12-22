using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldToDialoguePortal : MonoBehaviour
{
    [Header("Dialogue Scene")]
    public string dialogueSceneName = "dialogue";

    [Header("Trigger")]
    public string playerTag = "Player";
    public bool requireKey = false;
    public KeyCode key = KeyCode.E;

    [Header("Return Info (auto remember current scene)")]
    public string returnSpawnId = ""; 

    private bool _inside = false;

    private const string PREF_RETURN_SCENE = "PREF_RETURN_SCENE";
    private const string PREF_RETURN_SPAWN = "PREF_RETURN_SPAWN";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (!requireKey) Go();
        else _inside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        _inside = false;
    }

    private void Update()
    {
        if (!requireKey) return;
        if (_inside && Input.GetKeyDown(key)) Go();
    }

    private void Go()
    {
        string fromScene = SceneManager.GetActiveScene().name;

        PlayerPrefs.SetString(PREF_RETURN_SCENE, fromScene);
        PlayerPrefs.SetString(PREF_RETURN_SPAWN, returnSpawnId);
        PlayerPrefs.Save();

        
        SceneManager.LoadScene(dialogueSceneName);
    }
}
