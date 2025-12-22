using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldPortal : MonoBehaviour
{
    [Header("Target World")]
    public string toWorldScene = "world2";
    public string toSpawnId = "Spawn_A";

    [Header("Move Who")]
    public bool moveWholeParty = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameSession.I != null)
        {
            GameSession.I.BeginWorldTransfer(toWorldScene, toSpawnId, moveWholeParty);
        }

        SceneManager.LoadScene(toWorldScene);
    }
}
