using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleUnit : MonoBehaviour
{
    [Header("Team")]
    public bool isPlayer = true; // true=玩家，false=敌人

    // =========================
    // Stats (Base in Inspector, Runtime changes via buffs)
    // =========================
    [Header("Stats (Base in Inspector)")]
    public int maxHp = 30;
    public int hp = 30;

    public int atk = 10;
    public int def = 0;
    public int spd = 10;

    [Range(0f, 1f)]
    public float cr = 0.1f;     // Crit Rate
    public float cd = 1.5f;     // Crit Damage (multiplier)

    [Tooltip("Energy regen bonus, reserved (not used by default).")]
    public float er = 0f;

    // Base snapshot (captured on Awake)
    [NonSerialized] public int baseMaxHp;
    [NonSerialized] public int baseAtk;
    [NonSerialized] public int baseDef;
    [NonSerialized] public int baseSpd;
    [NonSerialized] public float baseCr;
    [NonSerialized] public float baseCd;

    // =========================
    // Skill Data (Data-driven)
    // =========================
    [Header("Skill Data")]
    public SkillData skillData;       // 普通技能（对应技能键）
    public SkillData ultimateData;    // 大招（对应大招键）

    // =========================
    // Animator / FX (kept for compatibility)
    // =========================
    [Header("Animator")]
    public Animator animator;
    public string attackStateName;              // 攻击状态名（可空）
    public string deathStateName = "death";     // 死亡状态名

    [Header("FX")]
    public GameObject attackFxPrefab;
    public Transform hitPoint;

    // Optional anchor for UI popups (damage/heal/buff text). If not set, hitPoint or transform position is used.
    [Header("Popup Anchor (Optional)")]
    public Transform popupPoint;

    [Header("Death")]
    public bool destroyOnDeath = false;

    // A runtime-created world-space anchor used to lock hit/popup points for certain units (e.g. summoned clones).
    // Not saved in scenes/prefabs.
    private GameObject runtimeLockedPointGO = null;


    [Header("Boss - Flags")]
    public bool isSmallBoss = false;
    public bool isBigBoss = false;

    [Tooltip("True if this unit is a summoned clone (e.g., BigBoss summoned SmallBoss clone). Clones must NOT transform on death.")]
    public bool isSummonedClone = false;

    [Header("Boss - Transform")]
    public bool transformOnDeath = false;             // 小Boss死亡是否触发变身逻辑（由 BattleManager 处理生成大Boss）
    public bool smallBossSkipDeathAnim = true;        // 小Boss变身时跳过死亡动画
    public string transformTriggerName = "Transform"; // Animator Trigger
    public string transformStateName = "";            // 变身状态名（可空）
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

    [Header("Legacy Ultimate (Optional)")]
    public bool ultimateHealsParty = false; // if true, legacy ultimate will heal allies instead of dealing damage
    public int ultimateHealFlat = 0;         // flat heal amount
    public float ultimateHealAtkRatio = 0f;  // extra heal = atk * ratio
    public GameObject ultimateHealFxPrefab;  // optional heal fx

    // =========================
    // Energy
    // =========================
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
    private Image energyFillImage;
    private bool energyGlowActive;

    // Runtime cached values for scale-based fill
    private Vector3 energyBarScaleEmpty = new Vector3(0.001f, 1f, 1f);
    private Vector3 energyBarBaselineLocalPos = Vector3.zero;

    // Base alpha for the fill (0 when empty, 1 when non-empty). Glow modulates on top of this.
    private float energyFillBaseAlpha = 1f;

    private bool energyBarRefsReady = false;
    private Transform energyBarBackgroundRuntime;
    // If true, background existed in the prefab hierarchy and we must NOT overwrite its transform.
    private bool energyBarBackgroundIsPrefab = false;


    // Visual sync guard (fix: after ultimate, the fill object can remain hidden / desynced)
    private float _lastEnergyVisual = float.NaN;
    private float _lastEnergyMaxVisual = float.NaN;
    private bool _lastIsPlayerVisual;
    private bool _lastFillActiveVisual;

    // =========================
    // Buffs / Modifiers
    // =========================
    [Serializable]
    public class StatModifier
    {
        public StatType stat = StatType.Atk;
        public float flat = 0f;
        public float percent = 0f;
        public int turns = 1;
    }

    [Header("Runtime Modifiers (Read Only)")]
    public List<StatModifier> modifiers = new List<StatModifier>();

    void Awake()
    {
        // Capture base stats from current inspector values (keeps old prefabs working).
        baseMaxHp = Mathf.Max(1, maxHp);
        baseAtk = Mathf.Max(0, atk);
        baseDef = Mathf.Max(0, def);
        baseSpd = Mathf.Max(0, spd);
        baseCr = Mathf.Clamp01(cr);
        baseCd = Mathf.Max(1f, cd);

        // Ensure runtime is valid
        hp = Mathf.Clamp(hp, 0, maxHp);
    }

    void Start()
    {
        EnsureEnergyBarRefs();
        RefreshEnergyBar();
    }

    void OnEnable()
    {
        // Some battles toggle units active/inactive; refresh energy UI on enable.
        EnsureEnergyBarRefs();
        RefreshEnergyBar();
        StartCoroutine(DeferredEnergyBarRepair());
    }

    private System.Collections.IEnumerator DeferredEnergyBarRepair()
    {
        // Wait one frame so prefab references and renderers are fully initialized.
        yield return null;
        EnsureEnergyBarRefs();
        RefreshEnergyBar();
    }


    
    private void EnsureEnergyBarRefs()
    {
        // Keep energy bar robust across prefab variations:
        // - Some prefabs have separate Fill/BG SpriteRenderers under EnergyBar
        // - Some prefabs forget to assign the background sprite (Sprite = None)
        // - Some prefabs use Canvas Image instead of SpriteRenderer
        //
        // We initialize baseline only once (energyBarRefsReady), but we may repair missing background later.
        if (energyBarRefsReady)
        {
            // Repair background if missing
            if (energyFillRenderer != null && energyBarBackgroundRuntime == null)
            {
                EnsureEnergyBarBackgroundSprite();
            }
            return;
        }

        if (energyBarRoot == null)
        {
            Transform t = transform.Find("EnergyBar");
            if (t == null) t = transform.Find("EnergyBarRoot");
            energyBarRoot = t;
        }
        if (energyBarRoot == null) return;

        // ---------- Canvas UI Image ----------
        // Prefer an Image whose name suggests fill.
        Image fillImg = null;
        var imgs = energyBarRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            var img = imgs[i];
            if (img == null) continue;
            string n = img.gameObject.name.ToLowerInvariant();
            if (n.Contains("fill"))
            {
                fillImg = img;
                break;
            }
        }
        if (fillImg == null && imgs.Length > 0) fillImg = imgs[0];

        if (fillImg != null)
        {
            energyFillImage = fillImg;
            energyBarFill = energyFillImage.rectTransform;

            energyBarScaleFull = energyBarFill.localScale;
            energyBarScaleEmpty = new Vector3(0f, energyBarScaleFull.y, energyBarScaleFull.z);
            energyBarBaselineLocalPos = energyBarFill.localPosition;

            energyBarRefsReady = true;
            return;
        }

        // ---------- SpriteRenderer (world-space UI) ----------
        SpriteRenderer[] srs = energyBarRoot.GetComponentsInChildren<SpriteRenderer>(true);

        SpriteRenderer fillSr = null;

        // 1) Prefer a renderer with sprite whose name suggests fill (keeps correct size)
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null) continue;
            if (sr.sprite == null) continue;
            string n = sr.gameObject.name.ToLowerInvariant();
            if (n.Contains("fill"))
            {
                fillSr = sr;
                break;
            }
        }
        // 2) Otherwise, any renderer that already has a sprite (skip obvious background renderers)
        if (fillSr == null)
        {
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr == null) continue;
                if (sr.sprite == null) continue;

                string n = sr.gameObject.name.ToLowerInvariant();
                if (n.Contains("bg") || n.Contains("back") || n.Contains("empty")) continue;

                fillSr = sr;
                break;
            }
        }
        // 3) Otherwise, try a renderer whose name suggests fill (even if sprite is null)
        if (fillSr == null)
        {
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr == null) continue;
                string n = sr.gameObject.name.ToLowerInvariant();
                if (n.Contains("fill"))
                {
                    fillSr = sr;
                    break;
                }
            }
        }
        // 4) Fall back to root renderer (create if needed)
        if (fillSr == null)
        {
            fillSr = energyBarRoot.GetComponent<SpriteRenderer>();
            if (fillSr == null) fillSr = energyBarRoot.gameObject.AddComponent<SpriteRenderer>();
        }

        energyFillRenderer = fillSr;

        // Ensure fill has a sprite (avoid invisible bar when prefab sprite is None)
        if (energyFillRenderer.sprite == null)
        {
            CreateEnergyBarSpriteFallback(energyFillRenderer);
        }

        energyBarFill = energyFillRenderer.transform;

        // Ensure background exists and matches fill size
        EnsureEnergyBarBackgroundSprite();

        // Cache baseline (full) values once
        energyBarScaleFull = energyBarFill.localScale;
        energyBarScaleEmpty = new Vector3(0f, energyBarScaleFull.y, energyBarScaleFull.z);
        energyBarBaselineLocalPos = energyBarFill.localPosition;

        energyBarRefsReady = true;
    }

    private void EnsureEnergyBarBackgroundSprite()
    {
        if (energyBarRoot == null || energyFillRenderer == null) return;

        // Try to find an existing BG renderer under EnergyBarRoot.
        // IMPORTANT: If it exists in the prefab, DO NOT change its transform (scale/pos/rot).
        SpriteRenderer bgSr = null;
        var srs = energyBarRoot.GetComponentsInChildren<SpriteRenderer>(true);

        // 1) Name-based BG match
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null || sr == energyFillRenderer) continue;
            string n = sr.gameObject.name.ToLowerInvariant();
            if (n.Contains("bg") || n.Contains("back") || n.Contains("empty"))
            {
                bgSr = sr;
                break;
            }
        }

        // 2) Fallback: a non-fill renderer with missing sprite (common: EnergyBar root has Sprite=None)
        if (bgSr == null)
        {
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr == null || sr == energyFillRenderer) continue;
                string n = sr.gameObject.name.ToLowerInvariant();
                if (n.Contains("fill")) continue;
                if (sr.sprite == null)
                {
                    bgSr = sr;
                    break;
                }
            }
        }

        // 3) Reuse previously created runtime BG
        if (bgSr == null && energyBarBackgroundRuntime != null)
        {
            bgSr = energyBarBackgroundRuntime.GetComponent<SpriteRenderer>();
            if (bgSr == null) bgSr = energyBarBackgroundRuntime.gameObject.AddComponent<SpriteRenderer>();
            energyBarBackgroundIsPrefab = false;
        }

        // 4) Create a runtime BG only if no suitable prefab BG exists.
        if (bgSr == null)
        {
            GameObject bgObj = new GameObject("EnergyBar_BG_Runtime");
            bgObj.transform.SetParent(energyBarRoot, false);
            bgObj.transform.localPosition = energyFillRenderer.transform.localPosition;
            bgObj.transform.localRotation = energyFillRenderer.transform.localRotation;
            bgObj.transform.localScale = energyFillRenderer.transform.localScale;

            bgSr = bgObj.AddComponent<SpriteRenderer>();
            energyBarBackgroundRuntime = bgObj.transform;
            energyBarBackgroundIsPrefab = false;
        }
        else
        {
            // Found a prefab background.
            energyBarBackgroundRuntime = bgSr.transform;
            // Treat runtime-named BG as runtime so we can align it.
            string bn = bgSr.gameObject.name.ToLowerInvariant();
            energyBarBackgroundIsPrefab = !(bn.Contains("runtime") || bn.Contains("_rt"));
        }

        if (bgSr == null) return;

        // Match sprite & sorting
        if (bgSr.sprite == null) bgSr.sprite = energyFillRenderer.sprite;
        bgSr.sortingLayerID = energyFillRenderer.sortingLayerID;
        bgSr.sortingOrder = energyFillRenderer.sortingOrder - 1;
        bgSr.color = Color.white;

        // Only align transforms for runtime-created BG.
        // For prefab BG, keep authoring values untouched (prevents scale being overwritten to 1,1,1).
        if (!energyBarBackgroundIsPrefab)
        {
            bgSr.transform.localPosition = energyFillRenderer.transform.localPosition;
            bgSr.transform.localRotation = energyFillRenderer.transform.localRotation;
            bgSr.transform.localScale = energyFillRenderer.transform.localScale;
        }
    }

    private static Sprite s_energyFallbackSprite;

    private static Sprite GetEnergyFallbackSprite()
    {
        if (s_energyFallbackSprite != null) return s_energyFallbackSprite;

        // Texture2D.whiteTexture is always available at runtime.
        Texture2D tex = Texture2D.whiteTexture;
        Rect rect = new Rect(0, 0, tex.width, tex.height);
        // Use a neutral pivot; the fill behavior is handled by scaling + position in RefreshEnergyBar().
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        // Use pixelsPerUnit = 1 so this fallback sprite has a reasonable world size if used.
        s_energyFallbackSprite = Sprite.Create(tex, rect, pivot, 1f);
        return s_energyFallbackSprite;
    }

    private void CreateEnergyBarSpriteFallback(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.sprite = GetEnergyFallbackSprite();
        // Ensure it's visible even if the prefab forgot to assign material.
        if (sr.sharedMaterial == null) sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    private Transform CreateRuntimeEnergyBackground(Transform root, Transform fill)
    {
        if (root == null || fill == null) return null;

        // Avoid duplicates.
        var existing = root.Find("EnergyBarBgRuntime");
        if (existing != null) return existing;

        // UI Image background.
        var fillImg = fill.GetComponent<Image>();
        if (fillImg != null)
        {
            var go = new GameObject("EnergyBarBgRuntime", typeof(RectTransform), typeof(Image));
            var rt = go.transform as RectTransform;
            rt.SetParent(root, false);

            var rtFill = fill.GetComponent<RectTransform>();
            if (rtFill != null)
            {
                rt.anchorMin = rtFill.anchorMin;
                rt.anchorMax = rtFill.anchorMax;
                rt.pivot = rtFill.pivot;
                rt.anchoredPosition = rtFill.anchoredPosition;
                rt.sizeDelta = rtFill.sizeDelta;
                rt.localEulerAngles = rtFill.localEulerAngles;
                rt.localScale = rtFill.localScale;
            }
            else
            {
                rt.localPosition = fill.localPosition;
                rt.localRotation = fill.localRotation;
                rt.localScale = fill.localScale;
            }

            var img = go.GetComponent<Image>();
            img.sprite = fillImg.sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = fillImg.preserveAspect;
            img.color = Color.white;

            // Put behind the fill.
            go.transform.SetSiblingIndex(Mathf.Max(0, fill.GetSiblingIndex()));
            return go.transform;
        }

        // SpriteRenderer background.
        var fillSr = fill.GetComponent<SpriteRenderer>();
        if (fillSr != null)
        {
            var go = new GameObject("EnergyBarBgRuntime", typeof(SpriteRenderer));
            go.transform.SetParent(root, false);
            go.transform.localPosition = fill.localPosition;
            go.transform.localRotation = fill.localRotation;
            go.transform.localScale = fill.localScale;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = fillSr.sprite;
            sr.color = Color.white;
            sr.sortingLayerID = fillSr.sortingLayerID;
            sr.sortingOrder = fillSr.sortingOrder - 1;
            return go.transform;
        }

        return null;
    }

void Update()
    {
        if (energyFillRenderer != null)
        {
            var c = energyFillRenderer.color;
            float a = energyFillBaseAlpha;

            if (energyGlowActive && a > 0f)
            {
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * glowSpeed);
                a *= Mathf.Lerp(0.6f, 1f, t);
            }

            c.a = a;
            energyFillRenderer.color = c;
        }

        if (energyFillImage != null)
        {
            var c = energyFillImage.color;
            float a = energyFillBaseAlpha;

            if (energyGlowActive && a > 0f)
            {
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * glowSpeed);
                a *= Mathf.Lerp(0.6f, 1f, t);
            }

            c.a = a;
            energyFillImage.color = c;
        }

        // Keep energy bar in sync even if energy is modified elsewhere or animations/layout override active state.
        bool energyChanged = !Mathf.Approximately(energy, _lastEnergyVisual)
                             || !Mathf.Approximately(energyMax, _lastEnergyMaxVisual)
                             || (isPlayer != _lastIsPlayerVisual);

        bool fillActiveNow = (energyBarFill != null) ? energyBarFill.gameObject.activeSelf : false;
        bool fillActiveChanged = (fillActiveNow != _lastFillActiveVisual);

        if (energyChanged || fillActiveChanged)
        {
            RefreshEnergyBar();
            _lastEnergyVisual = energy;
            _lastEnergyMaxVisual = energyMax;
            _lastIsPlayerVisual = isPlayer;
            _lastFillActiveVisual = (energyBarFill != null) ? energyBarFill.gameObject.activeSelf : false;
        }
    }

    // =========================
    // Core HP / Damage / Heal
    // =========================
    public bool IsDead() => hp <= 0;
    public bool IsAlive() => hp > 0;

    public int TakeDamage(int rawDamage)
    {
        int dmg = Mathf.Max(0, rawDamage);
        hp -= dmg;
        if (hp < 0) hp = 0;
        Debug.Log($"{name} takes {dmg}, HP={hp}/{maxHp}");
        return dmg;
    }

    public int Heal(int rawHeal)
    {
        int heal = Mathf.Max(0, rawHeal);
        int before = hp;
        hp = Mathf.Clamp(hp + heal, 0, maxHp);
        int actual = hp - before;
        Debug.Log($"{name} heals {actual}, HP={hp}/{maxHp}");
        return actual;
    }

    // =========================
    // Energy helpers
    // =========================
    public bool HasFullEnergy()
    {
        return energyMax > 0.01f && energy >= energyMax - 0.001f;
    }

    public bool HasEnoughEnergy(float cost)
    {
        float c = Mathf.Max(0f, cost);
        if (c <= 0.01f) c = energyMax;
        return energyMax > 0.01f && energy >= c - 0.001f;
    }

    public void AddEnergy(float amount)
    {
        if (energyMax <= 0.01f) return;
        energy = Mathf.Clamp(energy + Mathf.Max(0f, amount), 0f, energyMax);
        RefreshEnergyBar();
    }

    public bool SpendEnergy(float amount)
    {
        if (energyMax <= 0.01f) return true;
        if (energy + 0.0001f < amount) return false;
        energy = Mathf.Clamp(energy - amount, 0f, energyMax);
        RefreshEnergyBar();
        return true;
    }

    public float Energy01()
    {
        if (energyMax <= 0.01f) return 0f;
        return Mathf.Clamp01(energy / energyMax);
    }

    public void RefreshEnergyBar()
    {
        EnsureEnergyBarRefs();

        bool showBar = (!energyBarOnlyForPlayers) || isPlayer;
        if (energyBarRoot != null)
            energyBarRoot.gameObject.SetActive(showBar);
        if (!showBar) return;

        // White base should always be visible when the bar is shown.
        if (energyBarBackgroundRuntime != null)
            energyBarBackgroundRuntime.gameObject.SetActive(true);

        if (energyBarFill == null) return;

        float t = (energyMax <= 0.0001f) ? 0f : Mathf.Clamp01(energy / energyMax);

        // Fill should always exist; hide it by alpha when empty so the white base remains.
        if (!energyBarFill.gameObject.activeSelf)
            energyBarFill.gameObject.SetActive(true);

        energyGlowActive = (t >= 0.999f);

        // Prefer UI filled image if present.
        if (energyFillImage != null && energyFillImage.type == Image.Type.Filled)
        {
            energyFillImage.fillAmount = t;
        }
        else
        {
            // Scale-based fill (works for both UI and Sprite).
            Vector3 s = Vector3.Lerp(energyBarScaleEmpty, energyBarScaleFull, t);
            energyBarFill.localScale = s;

            // Keep the fill centered as it scales.
            Vector3 p = energyBarBaselineLocalPos;
            p.x = energyBarBaselineLocalPos.x - (energyBarScaleFull.x * (1f - t) * 0.5f);
            energyBarFill.localPosition = p;
        }

        energyFillBaseAlpha = (t <= 0.001f) ? 0f : 1f;

        if (energyFillRenderer != null)
        {
            var c = energyFillRenderer.color;
            c.a = energyFillBaseAlpha;
            energyFillRenderer.color = c;
        }

        if (energyFillImage != null)
        {
            var c = energyFillImage.color;
            c.a = energyFillBaseAlpha;
            energyFillImage.color = c;
        }

        _lastEnergyVisual = energy;
        _lastEnergyMaxVisual = energyMax;
        _lastIsPlayerVisual = isPlayer;
        _lastFillActiveVisual = energyBarFill.gameObject.activeSelf;
    }


    // =========================
    // Modifiers
    // =========================
    public void ClearModifiers()
    {
        modifiers.Clear();
        RecalculateStats(keepHpRatio: true);
    }

    public void ApplyModifier(StatType stat, float flat, float percent, int turns)
    {
        int t = Mathf.Max(1, turns);

        // Editor input convention: percent=100 means +100%.
        // If you pass a fraction (0.25 for +25%), it is also supported.
        float pct = percent;
        if (Mathf.Abs(pct) > 2f) pct *= 0.01f;

        var m = new StatModifier { stat = stat, flat = flat, percent = pct, turns = t };
        modifiers.Add(m);
        RecalculateStats(keepHpRatio: true);
    }

    public void TickModifiersOnTurnEnd()
    {
        bool changed = false;
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            modifiers[i].turns -= 1;
            if (modifiers[i].turns <= 0)
            {
                modifiers.RemoveAt(i);
                changed = true;
            }
        }
        if (changed)
            RecalculateStats(keepHpRatio: true);
    }

    public void RecalculateStats(bool keepHpRatio)
    {
        float hpRatio = (maxHp <= 0) ? 1f : (float)hp / Mathf.Max(1, maxHp);

        maxHp = Mathf.Max(1, ComputeIntStat(StatType.MaxHp, baseMaxHp));
        atk = Mathf.Max(0, ComputeIntStat(StatType.Atk, baseAtk));
        def = Mathf.Max(0, ComputeIntStat(StatType.Def, baseDef));
        spd = Mathf.Max(1, ComputeIntStat(StatType.Spd, baseSpd));
        cr = Mathf.Clamp01(ComputeFloatStat(StatType.Cr, baseCr));
        cd = Mathf.Max(1f, ComputeFloatStat(StatType.Cd, baseCd));

        if (keepHpRatio)
            hp = Mathf.Clamp(Mathf.RoundToInt(maxHp * hpRatio), 0, maxHp);
        else
            hp = Mathf.Clamp(hp, 0, maxHp);
    }

    int ComputeIntStat(StatType stat, int baseValue)
    {
        float flatSum = 0f;
        float pctSum = 0f;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].stat != stat) continue;
            flatSum += modifiers[i].flat;
            pctSum += modifiers[i].percent;
        }
        float v = (baseValue + flatSum) * (1f + pctSum);
        return Mathf.RoundToInt(v);
    }

    float ComputeFloatStat(StatType stat, float baseValue)
    {
        float flatSum = 0f;
        float pctSum = 0f;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].stat != stat) continue;
            flatSum += modifiers[i].flat;
            pctSum += modifiers[i].percent;
        }
        return (baseValue + flatSum) * (1f + pctSum);
    }

    // =========================
    // Animation / FX triggers
    // =========================
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

    public void TriggerTransform()
    {
        if (animator == null) return;
        animator.ResetTrigger(transformTriggerName);
        animator.SetTrigger(transformTriggerName);
    }

    public Transform GetTransformFxPoint()
    {
        return transformFxPoint != null ? transformFxPoint : transform;
    }

    public void TriggerSummon()
    {
        if (animator == null) return;
        animator.ResetTrigger(summonTriggerName);
        animator.SetTrigger(summonTriggerName);
    }

    public Transform GetSummonFxPoint()
    {
        return summonFxPoint != null ? summonFxPoint : transform;
    }

    public void TriggerCustomAnim(string triggerName)
    {
        if (animator == null) return;
        if (string.IsNullOrEmpty(triggerName)) return;
        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }

    public void PlayFx(GameObject fxPrefab, Transform point)
    {
        if (fxPrefab == null) return;
        Transform p = point != null ? point : transform;
        Instantiate(fxPrefab, p.position, Quaternion.identity);
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
        spd = Mathf.Max(1, _spd);
        cr = Mathf.Clamp01(_cr);
        cd = Mathf.Max(1f, _cd);

        // Update base snapshot as well (for future buff computations)
        baseMaxHp = Mathf.Max(1, maxHp);
        baseAtk = Mathf.Max(0, atk);
        baseDef = Mathf.Max(0, def);
        baseSpd = Mathf.Max(1, spd);
        baseCr = cr;
        baseCd = cd;
    }

    // -------------------------
    // Compatibility wrappers
    // -------------------------

    // Some BattleManager versions call TickModifiers() (old name). Keep it as an alias.
    public void TickModifiers()
    {
        TickModifiersOnTurnEnd();
    }

    // Some BattleManager versions call ConsumeEnergy / ConsumeAllEnergy (old names). Keep aliases.
    public void ConsumeEnergy(float amount)
    {
        SpendEnergy(amount);
    }

    // Some BattleManager versions call UpdateEnergyBarVisual() (old name). Keep alias.
    void UpdateEnergyBarVisual()
    {
        RefreshEnergyBar();
    }

    public void ConsumeAllEnergy()
    {
        energy = 0f;
        UpdateEnergyBarVisual();
    }

    // Some BattleManager versions call HideOrDestroy() after death.
    public void HideOrDestroy()
    {
        if (destroyOnDeath)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }


    /// <summary>
    /// Lock this unit's hit/popup anchor to a fixed world position.
    /// Used for summoned clones when you want the impact point to stay at the summon position.
    /// </summary>
    public void LockHitAndPopupPointToWorld(Vector3 worldPos, bool lockPopupPointToo = true)
    {
        if (runtimeLockedPointGO == null)
        {
            runtimeLockedPointGO = new GameObject($"{name}_LockedPoint");
            // Don't save to scene/prefab.
            runtimeLockedPointGO.hideFlags = HideFlags.DontSave;
        }

        runtimeLockedPointGO.transform.position = worldPos;
        hitPoint = runtimeLockedPointGO.transform;
        if (lockPopupPointToo) popupPoint = runtimeLockedPointGO.transform;
    }

    private void OnDestroy()
    {
        if (runtimeLockedPointGO != null)
        {
            Destroy(runtimeLockedPointGO);
            runtimeLockedPointGO = null;
        }
    }

    // World position used for UI popup text.
    // Priority: popupPoint -> hitPoint -> transform position + up offset.
    public Vector3 GetPopupWorldPosition()
    {
        if (popupPoint != null) return popupPoint.position;
        if (hitPoint != null) return hitPoint.position;
        return transform.position + new Vector3(0f, 1.2f, 0f);
    }

}
