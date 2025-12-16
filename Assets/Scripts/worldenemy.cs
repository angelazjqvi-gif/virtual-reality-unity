using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldEnemy : MonoBehaviour
{
    public string battleSceneName = "battle";
    public float reTeleportCooldown = 3f;

    [Header("ID (set in Inspector, unique)")]
    public int enemyId = 1;

    [Header("Battle Reward")]
    public int expPerEnemy = 5;

    [Header("World Display")]
    public int enemyLevel = 1;

    public EnemyRank enemyRank = EnemyRank.Normal;

    float nextCanBattleTime = 0f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time < nextCanBattleTime) return;

        GameSession.I.BeginBattle(enemyId);
        GameSession.I.expPerEnemy = expPerEnemy;

        SceneManager.LoadScene(battleSceneName);
    }

    public void ApplyPlayerLoseCooldown()
    {
        nextCanBattleTime = Time.time + reTeleportCooldown;
    }
}

public enum EnemyRank
{
    Normal,
    Elite,
    Boss
}
