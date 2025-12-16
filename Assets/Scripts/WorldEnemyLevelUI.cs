using UnityEngine;
using TMPro;

public class WorldEnemyLevelUI : MonoBehaviour
{
    public WorldEnemy enemy;
    public TMP_Text levelText;

    public Vector3 offset = new Vector3(0, -0.6f, 0);

    void LateUpdate()
    {
        if (enemy == null || levelText == null) return;

        // 构建文本
        string text = $"Lv {enemy.enemyLevel}";

        switch (enemy.enemyRank)
        {
            case EnemyRank.Elite:
                text += "  Elite";
                levelText.color = new Color(1f, 0.8f, 0.2f); // 金色
                break;

            case EnemyRank.Boss:
                text += "  BOSS";
                levelText.color = Color.red;
                break;

            default:
                levelText.color = Color.white;
                break;
        }

        levelText.text = text;

        // 跟随敌人
        transform.position = enemy.transform.position + offset;
    }
}
