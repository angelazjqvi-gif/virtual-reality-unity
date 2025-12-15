using UnityEngine;

public class AttackEventEmitter : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battleManager;
    public BattleUnit unit;

    public void AE_SpawnAttackFx()
    {
        if (battleManager == null || unit == null) return;
        battleManager.SpawnAttackFxNow(unit);
    }
}
