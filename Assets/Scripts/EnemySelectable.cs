using UnityEngine;

public class EnemySelectable : MonoBehaviour
{
    public BattleManager battle;
    public BattleUnit unit;

    void Reset()
    {
        unit = GetComponent<BattleUnit>();
    }

    void OnMouseDown()
    {
        if (battle == null || unit == null) return;
        battle.SelectEnemyTarget(unit);
    }
}
