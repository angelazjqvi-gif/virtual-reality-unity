using UnityEngine;

public class AttackEventEmitter : MonoBehaviour
{
    void Awake()
    {
        if (unit == null) unit = GetComponent<BattleUnit>();
        if (battleManager == null) battleManager = FindObjectOfType<BattleManager>();
    }

    [Header("Refs")]
    public BattleManager battleManager;
    public BattleUnit unit;

    public void AE_SpawnAttackFx()
    {
        if (battleManager == null || unit == null) return;
        battleManager.SpawnAttackFxNow(unit);
    }
}
