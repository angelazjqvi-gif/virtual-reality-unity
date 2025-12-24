using UnityEngine;

public class EnemySelectable : MonoBehaviour
{
    public BattleManager battle;
    public BattleUnit unit;

    void Reset()
    {
        unit = GetComponent<BattleUnit>();
        if (battle == null) battle = FindObjectOfType<BattleManager>();
    }

    void OnMouseDown()
    {
        if (unit == null) return;
        if (battle == null) battle = FindObjectOfType<BattleManager>();
        if (battle == null) return;
        battle.SelectEnemyTarget(unit);
    }
}
