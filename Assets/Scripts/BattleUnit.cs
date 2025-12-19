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
    public float cd = 1.5f;     

    [Range(0f, 1f)]
    public float er = 0f;       

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

    [Header("Boss - Flags")]
    public bool isSmallBoss = false;
    public bool isBigBoss = false;

    [Header("Boss - Transform")]
    public bool transformOnDeath = false;             
    public bool smallBossSkipDeathAnim = true;        
    public string transformTriggerName = "Transform"; 
    public string transformStateName = "";            
    public GameObject transformFxPrefab = null;       
    public Transform transformFxPoint = null; 

    [Header("Boss - Summon (Skill2)")]
    public bool bigBossUseSummonOnce = true;        
    public string summonTriggerName = "Summon";     
    public string summonStateName = "";             
    public GameObject summonFxPrefab = null;        
    public Transform summonFxPoint = null; 

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

    [Header("Energy")]
    public float energy = 0f;          
    public float energyMax = 100f;

    [Header("Energy Bar Visual")]
    public Transform energyBarRoot;               
    public Transform energyBarFill;               
    public bool energyBarOnlyForPlayers = true;   
    public Vector3 energyBarScaleFull = new Vector3(1f, 1f, 1f);

    [Header("Energy Glow")]
    public bool glowWhenEnergyFull = true;
    public float glowSpeed = 6f;


    private Vector3 energyFillScaleFullRuntime;
    private bool energyFillScaleInited = false;
    private Vector3 energyFillPosFullRuntime;
    private bool energyFillInit = false;

    private SpriteRenderer energyFillRenderer;
    private bool energyGlowActive;


    void Start()
    {
        RefreshEnergyBar();
        if (energyBarFill != null)
        {
            energyFillRenderer = energyBarFill.GetComponent<SpriteRenderer>();
        }

    }


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

    public void TriggerTransform()
    {
        if (animator == null) return;
        if (string.IsNullOrEmpty(transformTriggerName)) return;
        animator.ResetTrigger(transformTriggerName);
        animator.SetTrigger(transformTriggerName);
    }

    public Transform GetTransformFxPoint()
    {
        if (transformFxPoint != null) return transformFxPoint;
        if (hitPoint != null) return hitPoint;
        return transform;
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

    public bool HasFullEnergy()
    {
        return energy >= energyMax;
    }

    public void AddEnergy(float amount)
    {
        energy += amount;
        if (energy > energyMax) energy = energyMax;
        if (energy < 0f) energy = 0f;
        RefreshEnergyBar();
    }

    public bool SpendEnergy(float amount)
    {
        if (energy < amount) return false;
        energy -= amount;
        if (energy < 0f) energy = 0f;
        RefreshEnergyBar();
        return true;
    }

    public float Energy01()
    {
        if (energyMax <= 0f) return 0f;
        return Mathf.Clamp01(energy / energyMax);
    }

    public void RefreshEnergyBar()
    {
        if (energyBarRoot == null || energyBarFill == null) return;

        if (energyBarOnlyForPlayers && !isPlayer)
        {
            energyBarRoot.gameObject.SetActive(false);
            return;
        }
        energyBarRoot.gameObject.SetActive(true);

        if (!energyFillInit)
        {
            energyFillScaleFullRuntime = energyBarFill.localScale;     
            energyFillPosFullRuntime = energyBarFill.localPosition;  
            energyFillInit = true;
        }

        float t = Energy01(); // 0~1
        energyGlowActive = glowWhenEnergyFull && t >= 0.999f;
        energyBarFill.gameObject.SetActive(t > 0.001f);
        if (t <= 0.001f) return;

        Vector3 s = energyFillScaleFullRuntime;
        s.x *= t;
        energyBarFill.localScale = s;
        Vector3 p = energyFillPosFullRuntime;
        p.x = energyFillPosFullRuntime.x - (energyFillScaleFullRuntime.x * (1f - t) * 0.5f);
        energyBarFill.localPosition = p;
    }

    public void TriggerSummon()
    {
        if (animator == null) return;
        if (string.IsNullOrEmpty(summonTriggerName)) return;
        animator.ResetTrigger(summonTriggerName);
        animator.SetTrigger(summonTriggerName);
    }

    public Transform GetSummonFxPoint()
    {
        if (summonFxPoint != null) return summonFxPoint;
        if (hitPoint != null) return hitPoint;
        return transform;
    }

    void Update()
    {
        if (energyFillRenderer == null) return;

        if (energyGlowActive)
        {
            float t = (Mathf.Sin(Time.time * glowSpeed) + 1f) * 0.5f;
            energyFillRenderer.color = Color.Lerp(
                new Color(0.3f, 1f, 0.3f),   
                Color.white,               
                t
            );
        }
        else
        {
            energyFillRenderer.color = Color.green;
        }
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
