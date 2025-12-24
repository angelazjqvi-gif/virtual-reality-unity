using UnityEngine;

[DisallowMultipleComponent]
public class AllySelectable : MonoBehaviour
{
    [Header("References")]
    public BattleManager battle;
    public BattleUnit unit;

    void Reset()
    {
        unit = GetComponent<BattleUnit>();
        if (battle == null) battle = FindObjectOfType<BattleManager>();
    }

    void Awake()
    {
        if (unit == null) unit = GetComponent<BattleUnit>();
        if (battle == null) battle = FindObjectOfType<BattleManager>();
    }

    void OnMouseDown()
    {
        if (battle == null || unit == null) return;
        if (!unit.isPlayer) return;

        battle.SelectAllyTarget(unit);
    }
}
