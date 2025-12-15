using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession I;

    [Header("Battle Link")]
    public int currentEnemyId = -1;   // 当前战斗对应的主世界敌人ID
    public bool playerWon = false;
    public bool playerLost = false;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BeginBattle(int enemyId)
    {
        currentEnemyId = enemyId;
        playerWon = false;
        playerLost = false;
        Debug.Log("[GameSession] BeginBattle enemyId=" + enemyId);
    }

    public void EndBattle_PlayerWin()
    {
        playerWon = true;
        playerLost = false;
        Debug.Log("[GameSession] EndBattle_PlayerWin enemyId=" + currentEnemyId);
    }

    public void EndBattle_PlayerLose()
    {
        playerWon = false;
        playerLost = true;
        Debug.Log("[GameSession] EndBattle_PlayerLose enemyId=" + currentEnemyId);
    }

    public void ClearBattleLink()
    {
        Debug.Log("[GameSession] ClearBattleLink");
        currentEnemyId = -1;
        playerWon = false;
        playerLost = false;
    }
}
