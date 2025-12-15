using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldEnemy : MonoBehaviour
{
    public string battleSceneName = "battle";
    public float reTeleportCooldown = 3f;

    [Header("ID (set in Inspector, unique)")]
    public int enemyId = 1; 

    float nextCanBattleTime = 0f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time < nextCanBattleTime) return;

        GameSession.I.BeginBattle(enemyId);
        SceneManager.LoadScene(battleSceneName);
    }

    public void ApplyPlayerLoseCooldown()
    {
        nextCanBattleTime = Time.time + reTeleportCooldown;
    }
}
