using UnityEngine;

public class BattleUnit : MonoBehaviour
{
    [Header("Stats")]
    public int maxHp = 30;
    public int hp = 30;

    public int atk = 10;
    public int def = 0;
    public int spd = 10;

    [Range(0f, 1f)]
    public float cr = 0.1f;     // Crit Rate
    public float cd = 1.5f;     // Crit Damage multiplier (1.5=150%)

    [Range(0f, 1f)]
    public float er = 0f;       // Effect Resist（预留：抵抗减速/被控等）

    [Header("Team")]
    public bool isPlayer = true; // true=玩家，false=敌人

    [Header("Animator")]
    public Animator animator;
    public string attackStateName;         // 攻击状态名
    public string deathStateName = "death";// 死亡状态名

    [Header("FX")]
    public GameObject attackFxPrefab;
    public Transform hitPoint;

    [Header("Death")]
    public bool destroyOnDeath = false;

    [Header("Animator - Ultimate")]
    public string ultimateStateName;

    [Header("FX - Ultimate")]
    public GameObject ultimateFxPrefab;

    [Header("Ultimate - Special")]
    public bool ultimateHealsParty = false;   
    public int ultimateHealFlat = 15;         
    public float ultimateHealAtkRatio = 0f;   

    [Header("FX - Ultimate Heal")]
    public GameObject ultimateHealFxPrefab;   


    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp < 0) hp = 0;
        Debug.Log($"{name} takes {dmg}, HP={hp}/{maxHp}");
    }

    public bool IsDead() => hp <= 0;

    public void TriggerAttack()
    {
        if (animator == null) return;
        animator.ResetTrigger("Attack");
        animator.SetTrigger("Attack");
    }

    public void TriggerDie()
    {
        if (animator == null) return;
        animator.ResetTrigger("Die");
        animator.SetTrigger("Die");
    }

    public void TriggerUltimate()
    {
        if (animator == null) return;
        animator.ResetTrigger("Ultimate");
        animator.SetTrigger("Ultimate");
    }

    public void HideOrDestroy()
    {
        if (destroyOnDeath) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        hp += amount;
        if (hp > maxHp) hp = maxHp;
        Debug.Log($"{name} heals {amount}, HP={hp}/{maxHp}");
    }


    public void OverrideStats(
    int _maxHp, int _hp,
    int _atk, int _def, int _spd,
    float _cr, float _cd
)
    {
        maxHp = _maxHp;
        hp = Mathf.Clamp(_hp, 0, _maxHp);
        atk = _atk;
        def = _def;
        spd = _spd;
        cr = _cr;
        cd = _cd;
    }
}
