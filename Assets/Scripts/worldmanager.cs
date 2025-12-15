using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string worldSceneName = "world1";

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != worldSceneName) return;
        if (GameSession.I == null) { Debug.LogWarning("[WorldManager] GameSession.I null"); return; }
        if (GameSession.I.currentEnemyId < 0) return; // 没有战斗返回

        int id = GameSession.I.currentEnemyId;
        bool won = GameSession.I.playerWon;
        bool lost = GameSession.I.playerLost;

        Debug.Log($"[WorldManager] Back to {worldSceneName}, enemyId={id}, won={won}, lost={lost}");

        // 找对应敌人
        WorldEnemy target = null;
        foreach (var e in FindObjectsOfType<WorldEnemy>())
        {
            if (e.enemyId == id) { target = e; break; }
        }

        if (target == null)
        {
            Debug.LogError($"[WorldManager] Can't find WorldEnemy with enemyId={id}. 你是不是没挂WorldEnemy或ID重复/没填？");
            GameSession.I.ClearBattleLink();
            return;
        }

        if (won)
        {
            Debug.Log("[WorldManager] Destroy enemy: " + target.name);
            Destroy(target.gameObject);
        }
        else if (lost)
        {
            Debug.Log("[WorldManager] Player lost, apply cooldown to enemy: " + target.name);
            target.ApplyPlayerLoseCooldown();
        }

        GameSession.I.ClearBattleLink();
    }
}
