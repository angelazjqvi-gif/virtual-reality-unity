using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    public enum TurnState { WaitingInput, Busy, End }
    private int turnToken = 0;          
    private int busyOwnerToken = 0;     

    [System.Serializable]
    public class PlayerSlot
    {
        public BattleUnit unit;
        public Button attackButton;
        public Button ultimateButton;
    }

    [Header("Players (N)")]
    public List<PlayerSlot> players = new List<PlayerSlot>();

    [Header("Enemies (N)")]
    public List<BattleUnit> enemies = new List<BattleUnit>();

    [Header("Boss Transform")]
    public BattleUnit bigBossPrefab;        
    public Transform bigBossSpawnPoint;

    [Header("BigBoss - Summon Minions (Skill2)")]
    public BattleUnit minionPrefab;                 
    public GameObject minionSpawnFxPrefab;          
    public Transform[] minionSpawnPoints;           
    public Vector3 fallbackSpawnOffset1 = new Vector3(-1.2f, 0f, 0f);
    public Vector3 fallbackSpawnOffset2 = new Vector3( 1.2f, 0f, 0f);




    [Tooltip("Maximum number of minions spawned in one summon when using manager spawn points. If <= 0, uses all available spawn points.")]
    public int bigBossSummonMaxCount = 2;

    [Tooltip("If a spawn point is occupied, try to offset it to avoid stacking units.")]
    public float summonOccupyRadius = 0.6f;

    private bool bigBossSummonUsedThisBattle = false;
    private bool bigBossSpawnedThisBattle = false;
    private bool bigBossSummonInProgress = false;

    // Guards to prevent duplicate death handling / boss spawns.
    // In Unity coroutines, a unit can be processed by multiple flows (e.g., direct kill + later sweep),
    // so we protect critical paths to guarantee only one BigBoss exists.
    private readonly HashSet<BattleUnit> deathInProgress = new HashSet<BattleUnit>();
    private bool bigBossSpawnInProgress = false;
    private BattleUnit cachedBigBoss = null;

[Header("Summon Position")]
public float summonPointJitterX = 0.0f;
public float summonPointJitterY = 0.0f;
private int summonSpawnCursor = 0;

    // =========================
    // (NEW) Data-driven Skills
    // =========================
    [Header("Data-driven Skills (Optional Defaults)")]
    public SkillData defaultPlayerSkill;          // used if a unit.skillData is null
    public SkillData defaultPlayerUltimate;       // used if a unit.ultimateData is null

    [Header("Ultimate Cut-in (Energy Full)")]
    public bool allowCutInUltimate = true;        // allow energy-full ultimate to cut-in (cast out of turn)
    public bool allowQueueCutInWhenBusy = true;   // if busy, queue the cut-in until current action ends

    [Header("Target Selection (Optional)")]
    public BattleUnit selectedAllyTarget;

    [Header("Wait Safety - Summon")]
    public float maxWaitEnterSummon = 0.5f;
    public float maxWaitSummonTotal = 3.0f;

    private readonly HashSet<BattleUnit> bigBossSummonUsed = new HashSet<BattleUnit>();     

    [Header("Wait Safety - Transform")]
    public float maxWaitEnterTransform = 0.5f;
    public float maxWaitTransformTotal = 3.0f;

    [Header("UI Overlay")]
    public BattleUIOverlay uiOverlay;

    [Header("Enemy Timing")]
    public float enemyDelayBeforeAttack = 0.4f;

    [Header("FX fallback")]
    public float fxFallbackTime = 0.6f;

    [Header("Wait Safety")]
    public float maxWaitEnterAttack = 0.5f;
    public float maxWaitAttackTotal = 2.0f;
    public float maxWaitFx = 2.0f;
    public float maxWaitDeath = 2.0f;

    [Header("Scene Names")]
    public string worldSceneName = "world1";

    [Header("Damage Popup")]
    public DamagePopup damagePopupPrefab;
    public Canvas popupCanvas;
    public Camera worldCamera;
    public Vector3 popupWorldOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Damage Popup - Queue")]
    [Tooltip("When multiple effects happen in a short time on the same unit, queue popups so they show one-by-one instead of stacking.")]
    public bool enablePopupQueue = true;

    [Tooltip("Delay between queued popups for the same unit (seconds).")]
    public float popupQueueInterval = 0.18f;

    [Header("Watchdog (Anti-freeze)")]
    public float busyTimeout = 20f;   
    private float busyTimer = 0f;

    [Header("Cut-in Safeguards")]
    [Tooltip("Single action window: max real-time seconds spent processing queued cut-ins before we bail out to avoid turn freeze.")]
    public float cutInHardLimitSec = 4f;

    [Tooltip("Single action window: max number of cut-ins processed before we bail out to avoid long Busy states.")]
    public int cutInMaxPerWindow = 6;

    [Header("Wait Safety - Ultimate")]
    public float maxWaitEnterUltimate = 0.5f;
    public float maxWaitUltimateTotal = 2.5f;

    [Header("Target Arrow")]
    public GameObject targetArrowPrefab;          
    public GameObject allyTargetArrowPrefab;
    public Vector3 targetArrowWorldOffset = new Vector3(0f, 1.2f, 0f);
    public Vector3 allyTargetArrowWorldOffset = new Vector3(0f, 1.2f, 0f);
    public bool allowClickToSelectEnemy = true;
    [Tooltip("Allow mouse click to change ally target when a single-target ally skill is active.")]
    public bool allowClickToSelectAlly = true;

    [Tooltip("Fallback radius (world units) used when allies have no collider or a click hits a non-unit collider.")]
    public float clickSelectRadius = 0.75f;

    // ---------------------
    // Popup queue (per target)
    // ---------------------
    enum PopupKind { Damage, Heal, Text }

    class PopupRequest
    {
        public PopupKind kind;
        public BattleUnit target;
        public int intValue;
        public bool crit;
        public string text;
    }

    readonly Dictionary<BattleUnit, Queue<PopupRequest>> _popupQueues = new Dictionary<BattleUnit, Queue<PopupRequest>>();
    readonly HashSet<BattleUnit> _popupProcessing = new HashSet<BattleUnit>();

    private GameObject targetArrowInstance = null;
    private BattleUnit arrowTarget = null;

    private GameObject allyTargetArrowInstance = null;
    private BattleUnit allyArrowTarget = null;

    // Group target arrows (for TargetScope.All)
    private readonly List<GameObject> enemyGroupArrowPool = new List<GameObject>();
    private readonly List<GameObject> allyGroupArrowPool = new List<GameObject>();

    [Header("Active Player Aura")]
    public GameObject activePlayerAuraPrefab;        
    public Vector3 activePlayerAuraOffset = new Vector3(0f, -0.45f, 0f); 
    public bool auraFollowPlayer = true;

    private GameObject activePlayerAuraInstance = null;
    private BattleUnit auraOwner = null;


    private TurnState state = TurnState.WaitingInput;

    
    private bool battleLoopStarted = false;
private BattleUnit selectedEnemyTarget = null;

    private readonly List<BattleUnit> allUnits = new List<BattleUnit>();
    private readonly List<BattleUnit> speedQueue = new List<BattleUnit>();
    
    bool _queueManuallyReorderedThisAction = false;
private int queueIndex = 0;
    private BattleUnit currentActor = null;

    private readonly Dictionary<BattleUnit, BattleUnit> lockedTargetForThisAction = new Dictionary<BattleUnit, BattleUnit>();

    // =========================
    // (NEW) Cut-in queue
    // =========================
    class PendingCutIn
    {
        public BattleUnit caster;
        public SkillData skill;
    }
    Queue<PendingCutIn> pendingCutIns = new Queue<PendingCutIn>();
    HashSet<BattleUnit> pendingCutInCasters = new HashSet<BattleUnit>();

    private readonly Dictionary<BattleUnit, GameObject> lockedFxPrefabForThisAction = new Dictionary<BattleUnit, GameObject>();

    [Header("Ultimate Damage")]
    [Range(1f, 10f)]
    public float ultimateDamageMultiplier = 2.0f;
    public int ultimateFlatBonus = 0;

    [Header("Energy Rules (NEW)")]
    public float energyGainOnNormalAttack = 50f;   
    public float energyCostUltimate = 100f;        

    struct DamageResult
    {
        public int dmg;
        public bool crit;
    }

    void Start()
    {
        if (worldCamera == null) worldCamera = Camera.main;

        // Avoid the common "There are 2 audio listeners" runtime warning.
        // Prefer keeping the listener on the main/world camera, disable others.
        EnsureSingleAudioListener();

        BindPlayerButtons();
        RebuildAllUnits();

        // Ensure only one big boss exists at battle start (prevents duplicate queue entries / duplicate objects).
        CacheAndEnforceBigBossAtStart();
        if (GameSession.I != null)
        {
            GameSession.I.EnsurePartySize(players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == null || players[i].unit == null)
                    continue;

                var pd = GameSession.I.GetPlayerDataByIndex(i);

                players[i].unit.OverrideStats(
                    pd.baseMaxHp,
                    pd.currentHp,
                    pd.baseAtk,
                    pd.baseDef,
                    pd.baseSpd,
                    pd.baseCr,
                    pd.baseCd
                );
            }
        }
        // During scene load, some enemies/UI may finish initializing in their Start() (or get instantiated) after this script.
        // Boot the turn loop on the next frame so the initial speed queue and action bar are consistent.
        RefreshButtons();
        StartCoroutine(BootstrapBattleNextFrame());
    }


    IEnumerator BootstrapBattleNextFrame()
    {
        if (battleLoopStarted) yield break;
        battleLoopStarted = true;

        // Wait one frame so all Start() calls (units/UI) and any runtime spawns/stat overrides settle.
        yield return null;

        if (uiOverlay == null) uiOverlay = FindObjectOfType<BattleUIOverlay>();

        // Some setups spawn/enable enemies during their Start().
        // Sync once at battle boot so the initial queue includes them (prevents UI/turn token mismatches).
        SyncEnemiesFromScene();

        BuildSpeedQueueNewRound();
        AdvanceToNextActor();
    }

    void SyncEnemiesFromScene()
    {
        // Include inactive objects too; filter out assets/prefabs.
        BattleUnit[] found = Resources.FindObjectsOfTypeAll<BattleUnit>();
        if (found == null) return;

        for (int i = 0; i < found.Length; i++)
        {
            BattleUnit u = found[i];
            if (u == null) continue;
            if (u.isPlayer) continue;

            // Ignore prefabs/assets; keep only scene instances.
            if (!u.gameObject.scene.IsValid()) continue;

            if (!enemies.Contains(u))
                enemies.Add(u);
        }

        // Enforce boss invariants after we potentially added units.
        CacheAndEnforceBigBossAtStart();
    }

    void EnsureSingleAudioListener()
    {
        var listeners = FindObjectsOfType<AudioListener>();
        if (listeners == null || listeners.Length <= 1) return;

        AudioListener keep = null;
        if (worldCamera != null) keep = worldCamera.GetComponent<AudioListener>();
        if (keep == null && Camera.main != null) keep = Camera.main.GetComponent<AudioListener>();
        if (keep == null) keep = listeners[0];

        for (int i = 0; i < listeners.Length; i++)
        {
            var l = listeners[i];
            if (l == null) continue;
            if (l == keep) continue;
            l.enabled = false;
        }
    }

    void Update()
    {
        if (state == TurnState.Busy)
        {
            busyTimer += Time.unscaledDeltaTime;
            if (busyTimer > busyTimeout)
            {
                if (busyOwnerToken == turnToken)
                {
                    Debug.LogWarning($"[WATCHDOG] Busy timeout -> force next turn token={turnToken}");
                    busyTimer = 0f;

                    queueIndex++;
                    AdvanceToNextActor();
                }
                else
                {
                    Debug.LogWarning($"[WATCHDOG] stale busy timeout ignored busyOwnerToken={busyOwnerToken} turnToken={turnToken}");
                    busyTimer = 0f;
                }
            }
        }
        else
        {
            busyTimer = 0f;
        }

        TryMouseSelectAlly();
        TryMouseSelectEnemy();
        UpdateTargetArrowDuringPlayerInput();
        UpdateAllyTargetArrowDuringPlayerInput();
        UpdateActiveAuraByTurn();

    }

    void BindPlayerButtons()
    {
        for (int i = 0; i < players.Count; i++)
        {
            int idx = i;
            if (players[i].attackButton == null) continue;

            players[i].attackButton.onClick.RemoveAllListeners();
            players[i].attackButton.onClick.AddListener(() => OnClickAttack(idx));

            if (players[i].ultimateButton != null)
            {
                players[i].ultimateButton.onClick.RemoveAllListeners();
                players[i].ultimateButton.onClick.AddListener(() => OnClickUltimate(idx));
            }
        }
    }

    void RebuildAllUnits()
    {
        allUnits.Clear();
        for (int i = 0; i < players.Count; i++)
            if (players[i].unit != null) allUnits.Add(players[i].unit);

        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null) allUnits.Add(enemies[i]);
    }

    bool IsAlive(BattleUnit u)
    {
        return u != null && u.gameObject.activeInHierarchy && !u.IsDead();
    }

    void CleanupEnemiesList()
    {
        if (enemies == null) return;
        // remove nulls
        for (int i = enemies.Count - 1; i >= 0; i--)
            if (enemies[i] == null) enemies.RemoveAt(i);

        // remove duplicates (same instance referenced multiple times)
        var seen = new HashSet<int>();
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            if (e == null) { enemies.RemoveAt(i); continue; }
            int id = e.GetInstanceID();
            if (seen.Contains(id)) enemies.RemoveAt(i);
            else seen.Add(id);
        }
    }

    void RemoveEnemyFromRuntimeLists(BattleUnit u)
    {
        if (u == null) return;

        // Remove from enemies list.
        if (enemies != null)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i] == u) enemies.RemoveAt(i);
            }
        }

        // Remove from turn queue, keeping queueIndex consistent.
        RemoveUnitFromTurnQueue(u);

        if (cachedBigBoss == u) cachedBigBoss = null;
    }

    // Remove a unit from the runtime turn queue (speedQueue) while keeping queueIndex stable.
    // We use this for BOTH enemies and players.
    // Rationale: dead players used to remain in speedQueue, and when the queue pointer reached them,
    // AdvanceToNextActor() could rebuild-and-return early, leaving the battle stuck in Busy until watchdog.
    void RemoveUnitFromTurnQueue(BattleUnit u)
    {
        if (u == null) return;
        if (speedQueue == null) return;

        for (int i = speedQueue.Count - 1; i >= 0; i--)
        {
            if (speedQueue[i] != u) continue;
            speedQueue.RemoveAt(i);
            if (i <= queueIndex && queueIndex > 0) queueIndex--;
        }
    }

    BattleUnit FindFirstAliveBigBossActive()
    {
        // Active objects only. This is enough to stop the "two big boss" problem.
        var units = FindObjectsOfType<BattleUnit>();
        for (int i = 0; i < units.Length; i++)
        {
            var u = units[i];
            if (u == null) continue;
            if (!u.isBigBoss) continue;
            if (!IsAlive(u)) continue;
            return u;
        }
        return null;
    }

    void EnforceSingleBigBoss(BattleUnit keep)
    {
        // Disable/destroy any extra big bosses that might have been spawned.
        var units = FindObjectsOfType<BattleUnit>();
        for (int i = 0; i < units.Length; i++)
        {
            var u = units[i];
            if (u == null) continue;
            if (!u.isBigBoss) continue;
            if (u == keep) continue;
            if (!IsAlive(u)) continue;

            Debug.LogWarning($"[BOSS] Duplicate BigBoss detected -> remove {u.name}");
            CleanupDeadTargetRefs(u);
            u.HideOrDestroy();
        }

        CleanupEnemiesList();
        if (keep != null && enemies != null && !enemies.Contains(keep))
            enemies.Add(keep);
    }

    void CacheAndEnforceBigBossAtStart()
    {
        CleanupEnemiesList();

        // Prefer a big boss already tracked by enemies list.
        BattleUnit keep = null;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null) continue;
            if (!e.isBigBoss) continue;
            if (!IsAlive(e)) continue;
            keep = e;
            break;
        }
        // If not found in list, scan scene.
        if (keep == null) keep = FindFirstAliveBigBossActive();

        cachedBigBoss = keep;
        bigBossSpawnedThisBattle = (cachedBigBoss != null);

        // Runtime safety:
        // - BigBoss must never be treated as SmallBoss (otherwise it may trigger the SmallBoss transform logic on death)
        // - BigBoss should be destroyed on death (per requirement)
        EnsureBigBossRuntimeFlags(cachedBigBoss);

        // Runtime-instantiated prefabs will have the "(Clone)" suffix.
        // Requirement: BigBoss should never display "(Clone)".
        NormalizeBigBossRuntimeName(cachedBigBoss);

        if (cachedBigBoss != null)
            EnforceSingleBigBoss(cachedBigBoss);
    }

    void EnsureBigBossRuntimeFlags(BattleUnit boss)
    {
        if (boss == null) return;
        if (!boss.isBigBoss) return;

        // BigBoss is always an enemy-side unit.
        boss.isPlayer = false;

        // Hard-override flags to prevent the "SmallBoss -> BigBoss" transform path from triggering on BigBoss.
        boss.isBigBoss = true;
        boss.isSmallBoss = false;
        boss.isSummonedClone = false;
        boss.transformOnDeath = false;

        // Requirement: BigBoss should be destroyed on death.
        boss.destroyOnDeath = true;

        // Optional safety: BigBoss is the only unit allowed to summon.
        boss.bigBossUseSummonOnce = true;
    }

    // ----------------------
    // Boss naming helpers
    // ----------------------
    static string StripCloneSuffix(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Unity default suffix
        const string suf = "(Clone)";
        if (s.EndsWith(suf))
        {
            s = s.Substring(0, s.Length - suf.Length);
        }
        return s.Trim();
    }

    void NormalizeBigBossRuntimeName(BattleUnit boss)
    {
        if (boss == null) return;
        if (!boss.isBigBoss) return;

        // Prefer prefab name if available.
        string desired = bigBossPrefab != null ? StripCloneSuffix(bigBossPrefab.name) : StripCloneSuffix(boss.name);
        if (string.IsNullOrEmpty(desired)) desired = StripCloneSuffix(boss.name);
        boss.gameObject.name = desired;
    }

    void BuildSpeedQueueNewRound()
    {
        RebuildAllUnits();
        speedQueue.Clear();
        for (int i = 0; i < allUnits.Count; i++)
        {
            var u = allUnits[i];
            if (IsAlive(u)) speedQueue.Add(u);
        }

        speedQueue.Sort((a, b) =>
        {
            int cmp = b.spd.CompareTo(a.spd);
            if (cmp != 0) return cmp;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        queueIndex = 0;
        RefreshUI();
    }

    // Rebuild the whole queue while keeping the currentActor positioned correctly.
    // This is used when enemies are spawned/removed mid-action (e.g., boss transform).
    void RebuildTurnQueuePreserveCurrentActor()
    {
        BattleUnit keep = currentActor;
        RebuildAllUnits();

        speedQueue.Clear();
        for (int i = 0; i < allUnits.Count; i++)
        {
            var u = allUnits[i];
            if (IsAlive(u)) speedQueue.Add(u);
        }

        speedQueue.Sort((a, b) =>
        {
            int cmp = b.spd.CompareTo(a.spd);
            if (cmp != 0) return cmp;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        if (keep != null && IsAlive(keep))
        {
            int idx = speedQueue.IndexOf(keep);
            queueIndex = (idx >= 0) ? idx : 0;
        }
        else
        {
            queueIndex = 0;
        }
    }

    // Re-sort turn order after a stat change (especially SPD) while keeping currentActor as the first element.
    // This makes mid-round speed buffs feel responsive.
    void ResortQueueKeepCurrentFirst()
    {
        // Reorder ONLY the upcoming part of the queue (after the current actor).
        // This keeps turn progression stable and prevents fast enemies from monopolizing the queue.
        if (speedQueue == null || speedQueue.Count == 0) return;

        if (currentActor == null || !IsAlive(currentActor))
        {
            BuildSpeedQueueNewRound();
            return;
        }

        int n = speedQueue.Count;

        // Ensure queueIndex points at currentActor if possible.
        if (queueIndex < 0 || queueIndex >= n || speedQueue[queueIndex] != currentActor)
        {
            int idx = speedQueue.IndexOf(currentActor);
            if (idx >= 0) queueIndex = idx;
        }

        int start = Mathf.Clamp(queueIndex + 1, 0, n);
        if (start >= n) { RefreshUI(); return; }

        var tail = new List<BattleUnit>();
        for (int i = start; i < n; i++) tail.Add(speedQueue[i]);

        tail.Sort((a, b) =>
        {
            int aDead = (a == null || !IsAlive(a)) ? 1 : 0;
            int bDead = (b == null || !IsAlive(b)) ? 1 : 0;
            int cmp = aDead.CompareTo(bDead); // alive first
            if (cmp != 0) return cmp;

            if (aDead == 1) return 0; // both dead / null

            cmp = b.spd.CompareTo(a.spd);
            if (cmp != 0) return cmp;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        for (int i = 0; i < tail.Count; i++)
            speedQueue[start + i] = tail[i];

        RefreshUI();
    }


    void RefreshUI()
    {
        if (uiOverlay != null)
            uiOverlay.Render(BuildDisplayQueue(), 0, allUnits);

        RefreshButtons();
    }

    void RefreshButtons()
    {
        for (int i = 0; i < players.Count; i++)
        {
            var btn = players[i].attackButton;
            if (btn == null) continue;

            bool isMyTurn =
                (state == TurnState.WaitingInput) &&
                (currentActor != null) &&
                (players[i].unit == currentActor) &&
                IsAlive(players[i].unit);

            btn.interactable = isMyTurn;
            if (players[i].attackButton != null)
                players[i].attackButton.interactable = isMyTurn;
            bool canUltimate = false;
            if (players[i].unit != null && IsAlive(players[i].unit))
            {
                var u = players[i].unit;
                var ult = u.ultimateData != null ? u.ultimateData : defaultPlayerUltimate;
                float cost = (ult != null && ult.isUltimate) ? Mathf.Max(0f, ult.energyCost) : u.energyMax;
                if (cost <= 0.01f) cost = u.energyMax;
                bool enoughEnergy = u.HasEnoughEnergy(cost);

                // On own turn: always allow if enough energy
                // Off turn: allow only if global cut-in is enabled and the skill allows cut-in
                bool allowOffTurn = allowCutInUltimate && (ult != null) && ult.allowCutInWhenEnergyFull;
                canUltimate = enoughEnergy && (isMyTurn || allowOffTurn);
            }

            if (players[i].ultimateButton != null)
                players[i].ultimateButton.interactable = canUltimate;

        }
    }

    public void SelectEnemyTarget(BattleUnit enemy)
    {
        if (!CanSelectEnemyNow()) return;
        if (enemy == null) return;
        if (!IsAlive(enemy)) return;
        if (enemy.isPlayer) return;

        selectedEnemyTarget = enemy;
        Debug.Log($"Selected Target => {enemy.name}");
    }

    public void SelectAllyTarget(BattleUnit ally)
    {
        if (!CanSelectAllyNow()) return;
        if (ally == null) return;
        if (!IsAlive(ally)) return;

        selectedAllyTarget = ally;
        ShowAllyTargetArrowOn(ally);
        Debug.Log($"Selected Ally => {ally.name}");
    }

    void EnsureTargetArrow()
    {
        if (targetArrowInstance != null) return;
        if (targetArrowPrefab == null) return;

        targetArrowInstance = Instantiate(targetArrowPrefab);
        targetArrowInstance.SetActive(false);

        var sr = targetArrowInstance.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 2000;
    }

    void HideTargetArrow()
    {
        if (targetArrowInstance != null)
            targetArrowInstance.SetActive(false);
        arrowTarget = null;
    }

    void ShowTargetArrowOn(BattleUnit target)
    {
        EnsureTargetArrow();
        if (targetArrowInstance == null) return;

        if (target == null || !IsAlive(target) || target.isPlayer)
        {
            HideTargetArrow();
            return;
        }

        arrowTarget = target;
        targetArrowInstance.SetActive(true);

        UpdateTargetArrowPosition();
    }

    void UpdateTargetArrowPosition()
    {
        if (targetArrowInstance == null || !targetArrowInstance.activeSelf) return;

        if (arrowTarget == null || !IsAlive(arrowTarget) || arrowTarget.isPlayer)
        {
            HideTargetArrow();
            return;
        }

        Vector3 basePos = arrowTarget.transform.position;
        targetArrowInstance.transform.position = basePos + targetArrowWorldOffset;
    }


void EnsureAllyTargetArrow()
{
    if (allyTargetArrowInstance != null) return;

    var prefab = allyTargetArrowPrefab != null ? allyTargetArrowPrefab : targetArrowPrefab;
    if (prefab == null) return;

    allyTargetArrowInstance = Instantiate(prefab);
    allyTargetArrowInstance.SetActive(false);

    var sr = allyTargetArrowInstance.GetComponentInChildren<SpriteRenderer>();
    if (sr != null) sr.sortingOrder = 999;
}

void HideAllyTargetArrow()
{
    if (allyTargetArrowInstance != null)
        allyTargetArrowInstance.SetActive(false);
    allyArrowTarget = null;
}

void ShowAllyTargetArrowOn(BattleUnit target)
{
    EnsureAllyTargetArrow();
    if (allyTargetArrowInstance == null) return;

    if (target == null || !IsAlive(target) || !target.isPlayer)
    {
        HideAllyTargetArrow();
        return;
    }

    allyArrowTarget = target;
    allyTargetArrowInstance.SetActive(true);
    UpdateAllyTargetArrowPosition();
}

void UpdateAllyTargetArrowPosition()
{
    if (allyTargetArrowInstance == null || !allyTargetArrowInstance.activeSelf) return;

    if (allyArrowTarget == null || !IsAlive(allyArrowTarget) || !allyArrowTarget.isPlayer)
    {
        HideAllyTargetArrow();
        return;
    }

    Vector3 basePos = allyArrowTarget.transform.position;
    allyTargetArrowInstance.transform.position = basePos + allyTargetArrowWorldOffset;
}

    // =========================================================
    // Target Selection Gating (SkillData targetSide/targetScope)
    // =========================================================
    SkillData GetCurrentPlayerInputSkill()
    {
        if (state != TurnState.WaitingInput) return null;
        if (currentActor == null || !currentActor.isPlayer) return null;
        return (currentActor.skillData != null) ? currentActor.skillData : defaultPlayerSkill;
    }

    bool CanSelectEnemyNow()
    {
        // Legacy attack path: allow selecting enemies.
        var s = GetCurrentPlayerInputSkill();
        if (s == null) return true;
        if (!s.requireManualSelect) return false;
        if (s.targetSide != SkillData.TargetSide.Enemies) return false;
        if (s.targetScope != SkillData.TargetScope.Single) return false;
        return true;
    }

    bool CanSelectAllyNow()
    {
        var s = GetCurrentPlayerInputSkill();
        if (s == null) return false;
        if (s.targetSide != SkillData.TargetSide.Allies) return false;
        if (s.targetScope != SkillData.TargetScope.Single) return false;
        return true;
    }

    // =========================================================
    // Group arrows (TargetScope.All)
    // =========================================================
    void EnsureEnemyGroupArrowPool(int count)
    {
        if (count <= 0) return;
        if (targetArrowPrefab == null) return;

        while (enemyGroupArrowPool.Count < count)
        {
            var go = Instantiate(targetArrowPrefab);
            go.SetActive(false);
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 2000;
            enemyGroupArrowPool.Add(go);
        }
    }

    void EnsureAllyGroupArrowPool(int count)
    {
        if (count <= 0) return;
        var prefab = allyTargetArrowPrefab != null ? allyTargetArrowPrefab : targetArrowPrefab;
        if (prefab == null) return;

        while (allyGroupArrowPool.Count < count)
        {
            var go = Instantiate(prefab);
            go.SetActive(false);
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 2000;
            allyGroupArrowPool.Add(go);
        }
    }

    void HideEnemyGroupArrows()
    {
        for (int i = 0; i < enemyGroupArrowPool.Count; i++)
        {
            if (enemyGroupArrowPool[i] != null) enemyGroupArrowPool[i].SetActive(false);
        }
    }

    void HideAllyGroupArrows()
    {
        for (int i = 0; i < allyGroupArrowPool.Count; i++)
        {
            if (allyGroupArrowPool[i] != null) allyGroupArrowPool[i].SetActive(false);
        }
    }

    void ShowEnemyGroupArrows(List<BattleUnit> targets)
    {
        if (targets == null || targets.Count == 0)
        {
            HideEnemyGroupArrows();
            return;
        }
        EnsureEnemyGroupArrowPool(targets.Count);

        for (int i = 0; i < enemyGroupArrowPool.Count; i++)
        {
            var go = enemyGroupArrowPool[i];
            if (go == null) continue;

            if (i >= targets.Count || targets[i] == null || !IsAlive(targets[i]) || targets[i].isPlayer)
            {
                go.SetActive(false);
                continue;
            }

            go.SetActive(true);
            go.transform.position = targets[i].transform.position + targetArrowWorldOffset;
        }
    }

    void ShowAllyGroupArrows(List<BattleUnit> targets)
    {
        if (targets == null || targets.Count == 0)
        {
            HideAllyGroupArrows();
            return;
        }
        EnsureAllyGroupArrowPool(targets.Count);

        for (int i = 0; i < allyGroupArrowPool.Count; i++)
        {
            var go = allyGroupArrowPool[i];
            if (go == null) continue;

            if (i >= targets.Count || targets[i] == null || !IsAlive(targets[i]) || !targets[i].isPlayer)
            {
                go.SetActive(false);
                continue;
            }

            go.SetActive(true);
            go.transform.position = targets[i].transform.position + allyTargetArrowWorldOffset;
        }
    }

void UpdateAllyTargetArrowDuringPlayerInput()
{
    if (state != TurnState.WaitingInput || currentActor == null || !currentActor.isPlayer)
    {
        HideAllyTargetArrow();
        HideAllyGroupArrows();
        return;
    }

    var skill = GetCurrentPlayerInputSkill();
    if (skill == null || skill.targetSide != SkillData.TargetSide.Allies)
    {
        HideAllyTargetArrow();
        HideAllyGroupArrows();
        return;
    }

    // ALL allies: show arrows on every valid ally
    if (skill.targetScope == SkillData.TargetScope.All)
    {
        HideAllyTargetArrow();
        var allies = GetAliveAlliesOf(currentActor);
        if (!skill.includeSelfWhenAlliesAll) allies.Remove(currentActor);
        ShowAllyGroupArrows(allies);
        return;
    }

    // SINGLE ally: show exactly one arrow (selected target if valid, otherwise default target)
    HideAllyGroupArrows();

    if (selectedAllyTarget != null && !IsAlive(selectedAllyTarget))
        selectedAllyTarget = null;

    BattleUnit target = null;
    var resolved = ResolveTargetsForSkill(currentActor, skill);
    if (resolved != null && resolved.Count > 0) target = resolved[0];

    if (target == null)
    {
        HideAllyTargetArrow();
        return;
    }

    if (allyArrowTarget != target || allyTargetArrowInstance == null || !allyTargetArrowInstance.activeSelf)
        ShowAllyTargetArrowOn(target);
    else
        UpdateAllyTargetArrowPosition();
}


    void UpdateTargetArrowDuringPlayerInput()
    {
        if (state != TurnState.WaitingInput || currentActor == null || !currentActor.isPlayer)
        {
            HideTargetArrow();
            HideEnemyGroupArrows();
            return;
        }

        var skill = GetCurrentPlayerInputSkill();

        // If the current basic skill targets allies, enemies should NOT show arrows.
        if (skill != null && skill.targetSide != SkillData.TargetSide.Enemies)
        {
            HideTargetArrow();
            HideEnemyGroupArrows();
            return;
        }

        // ALL enemies: show arrows on every valid enemy
        if (skill != null && skill.targetScope == SkillData.TargetScope.All)
        {
            HideTargetArrow();
            ShowEnemyGroupArrows(GetAliveOpponentsOf(currentActor));
            return;
        }

        HideEnemyGroupArrows();

        BattleUnit target = null;
        if (skill != null)
        {
            if (selectedEnemyTarget != null && !IsAlive(selectedEnemyTarget))
                selectedEnemyTarget = null;

            var resolved = ResolveTargetsForSkill(currentActor, skill);
            if (resolved != null && resolved.Count > 0) target = resolved[0];
        }
        else
        {
            // Legacy path
            if (selectedEnemyTarget != null && !IsAlive(selectedEnemyTarget))
                selectedEnemyTarget = null;

            target = (IsAlive(selectedEnemyTarget) && !selectedEnemyTarget.isPlayer)
                ? selectedEnemyTarget
                : GetFirstAliveEnemy();
        }

        if (target == null)
        {
            HideTargetArrow();
            return;
        }

        if (arrowTarget != target || targetArrowInstance == null || !targetArrowInstance.activeSelf)
            ShowTargetArrowOn(target);
        else
            UpdateTargetArrowPosition();
    }

    void TryMouseSelectEnemy()
    {
        if (!allowClickToSelectEnemy) return;
        if (state != TurnState.WaitingInput) return;
        if (currentActor == null || !currentActor.isPlayer) return;
        if (!CanSelectEnemyNow()) return;

        if (!Input.GetMouseButtonDown(0)) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        Vector2 worldPos = worldCamera.ScreenToWorldPoint(Input.mousePosition);

        // Use RaycastAll so UI/effect colliders (e.g., arrows/FX) won't block unit selection.
        RaycastHit2D[] hits = Physics2D.RaycastAll(worldPos, Vector2.zero);
        BattleUnit u = null;
        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                var cand = hits[i].collider.GetComponentInParent<BattleUnit>();
                if (cand == null) continue;
                if (cand.isPlayer) continue;
                if (!IsAlive(cand)) continue;
                u = cand;
                break;
            }
        }

        if (u == null) return;
        SelectEnemyTarget(u);
    }

    void TryMouseSelectAlly()
    {
        if (!allowClickToSelectAlly) return;
        if (state != TurnState.WaitingInput) return;
        if (currentActor == null || !currentActor.isPlayer) return;
        if (!CanSelectAllyNow()) return;

        if (!Input.GetMouseButtonDown(0)) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        Vector2 worldPos = worldCamera.ScreenToWorldPoint(Input.mousePosition);

        // Primary path: physics hit.
        RaycastHit2D[] hits = Physics2D.RaycastAll(worldPos, Vector2.zero);
        BattleUnit u = null;
        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                var cand = hits[i].collider.GetComponentInParent<BattleUnit>();
                if (cand == null) continue;
                if (!cand.isPlayer) continue;
                if (!IsAlive(cand)) continue;
                u = cand;
                break;
            }
        }

        // Fallback: some ally sprites might have no collider or the click might hit a non-unit collider.
        // In that case, pick the nearest alive ally within a small radius.
        if (u == null)
        {
            float r2 = Mathf.Max(0.05f, clickSelectRadius) * Mathf.Max(0.05f, clickSelectRadius);
            var pool = GetAliveAlliesOf(currentActor);
            float best = float.MaxValue;
            BattleUnit bestU = null;
            for (int i = 0; i < pool.Count; i++)
            {
                var cand = pool[i];
                if (cand == null || !IsAlive(cand) || !cand.isPlayer) continue;
                float d2 = (cand.transform.position - (Vector3)worldPos).sqrMagnitude;
                if (d2 <= r2 && d2 < best)
                {
                    best = d2;
                    bestU = cand;
                }
            }
            u = bestU;
        }

        if (u == null) return;
        SelectAllyTarget(u);
    }

    void EnsureActiveAura()
    {
        if (activePlayerAuraInstance != null) return;
        if (activePlayerAuraPrefab == null) return;

        activePlayerAuraInstance = Instantiate(activePlayerAuraPrefab);
        activePlayerAuraInstance.SetActive(false);

        var sr = activePlayerAuraInstance.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 0;
    }

    void HideActiveAura()
    {
        if (activePlayerAuraInstance != null)
            activePlayerAuraInstance.SetActive(false);

        auraOwner = null;
    }

    void ShowActiveAuraOn(BattleUnit unit)
    {
        EnsureActiveAura();
        if (activePlayerAuraInstance == null) return;

        if (unit == null || !unit.isPlayer || !IsAlive(unit))
        {
            HideActiveAura();
            return;
        }

        auraOwner = unit;
        activePlayerAuraInstance.SetActive(true);
        UpdateActiveAuraPosition();
    }

    void UpdateActiveAuraPosition()
    {
        if (activePlayerAuraInstance == null || !activePlayerAuraInstance.activeSelf) return;

        if (auraOwner == null || !IsAlive(auraOwner) || !auraOwner.isPlayer)
        {
            HideActiveAura();
            return;
        }

        Vector3 basePos = auraOwner.transform.position;
        activePlayerAuraInstance.transform.position = basePos + activePlayerAuraOffset;
    }

    void UpdateActiveAuraByTurn()
    {
        if (state == TurnState.WaitingInput && currentActor != null && currentActor.isPlayer && IsAlive(currentActor))
        {
            if (auraOwner != currentActor || activePlayerAuraInstance == null || !activePlayerAuraInstance.activeSelf)
                ShowActiveAuraOn(currentActor);
            else if (auraFollowPlayer)
                UpdateActiveAuraPosition();
        }
        else
        {
            HideActiveAura();
        }
    }


    void AdvanceToNextActor()
    {
        if (state == TurnState.End) return;

        // 胜负判断
        if (AllEnemiesDead())
        {
            StartCoroutine(EndBattleAndReturn(true));
            return;
        }
        if (AllPlayersDead())
        {
            StartCoroutine(EndBattleAndReturn(false));
            return;
        }

        // IMPORTANT:
        // Never early-return after rebuilding the queue.
        // If we stop here, the battle can get stuck in Busy state until the watchdog forces a turn.
        // This happens most often when a *player* dies (players used to stay in speedQueue as dead entries).
        int safety = 0;
        while (safety++ < 3)
        {
            if (speedQueue == null || speedQueue.Count == 0)
                BuildSpeedQueueNewRound();

            if (queueIndex >= speedQueue.Count)
                BuildSpeedQueueNewRound();

            while (queueIndex < speedQueue.Count && !IsAlive(speedQueue[queueIndex]))
                queueIndex++;

            if (queueIndex < speedQueue.Count)
                break;

            // Queue had only dead entries at the tail. Start a new round and try again.
            BuildSpeedQueueNewRound();
        }

        if (speedQueue == null || speedQueue.Count == 0)
        {
            Debug.LogWarning("[TURN] speedQueue empty after rebuild, cannot advance.");
            return;
        }

        if (queueIndex >= speedQueue.Count)
            queueIndex = 0;

        currentActor = speedQueue[queueIndex];
        turnToken++;
        Debug.Log($"[TURN] idx={queueIndex}/{speedQueue.Count} next={currentActor.name} isPlayer={currentActor.isPlayer}");

        if (currentActor.isPlayer)
        {
            // If this "player" unit has no UI buttons bound (e.g., summoned ally),
            // auto-drive it so the battle doesn't get stuck waiting for input.
            if (!IsHumanControllablePlayer(currentActor))
            {
                state = TurnState.Busy;
                busyTimer = 0f;
                busyOwnerToken = turnToken;
                RefreshUI();
                StartCoroutine(AutoPlayerUnitFlow(currentActor, turnToken));
                return;
            }

            state = TurnState.WaitingInput;

            if (selectedEnemyTarget != null && !IsAlive(selectedEnemyTarget))
                selectedEnemyTarget = null;

            if (selectedAllyTarget != null && !IsAlive(selectedAllyTarget))
                selectedAllyTarget = null;

            RefreshUI();
            return;
        }

        state = TurnState.Busy;
        busyTimer = 0f;
        busyOwnerToken = turnToken;
        RefreshUI();
        StartCoroutine(EnemyAutoAttackFlow(currentActor, turnToken));
    }

    void OnClickAttack(int playerIndex)
    {
        if (state != TurnState.WaitingInput) return;
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        BattleUnit attacker = players[playerIndex].unit;
        if (attacker == null || attacker != currentActor) return;

        // Prefer data-driven skill
        SkillData skill = attacker.skillData != null ? attacker.skillData : defaultPlayerSkill;
        if (skill != null)
        {
            StartCoroutine(CastSkillAsTurn(attacker, skill, turnToken));
            return;
        }

        // Fallback: legacy normal attack
        StartCoroutine(PlayerAttackFlow(attacker, turnToken));
    }

    void OnClickUltimate(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        BattleUnit attacker = players[playerIndex].unit;
        if (attacker == null) return;

        SkillData ult = attacker.ultimateData != null ? attacker.ultimateData : defaultPlayerUltimate;

        // If we have a data-driven ultimate, use it
        if (ult != null)
        {
            // On own turn: cast normally
            if (state == TurnState.WaitingInput && attacker == currentActor)
            {
                StartCoroutine(CastSkillAsTurn(attacker, ult, turnToken));
                return;
            }

            // Cut-in when energy full
            if (!allowCutInUltimate) return;
            if (!ult.allowCutInWhenEnergyFull) return;
            float cost = (ult != null && ult.isUltimate) ? Mathf.Max(0f, ult.energyCost) : attacker.energyMax;
            if (!attacker.HasEnoughEnergy(cost)) return;

            if (state == TurnState.WaitingInput)
            {
                // Cast immediately, then resume the original current actor (true cut-in)
                StartCoroutine(CastSkillAsCutIn(attacker, ult, turnToken, resumeActor: currentActor));
                return;
            }

            if (allowQueueCutInWhenBusy)
            {
                RequestCutInUltimate(attacker, ult);
            }
            return;
        }

        // Legacy ultimate: only on own turn
        if (state != TurnState.WaitingInput) return;
        if (attacker != currentActor) return;

        StartCoroutine(PlayerUltimateFlow(attacker, turnToken));
    }

    IEnumerator PlayerAttackFlow(BattleUnit attacker, int token)
    {
        _queueManuallyReorderedThisAction = false;
        state = TurnState.Busy;
        HideTargetArrow();
        HideActiveAura();
        busyOwnerToken = token;
        RefreshUI();

        BattleUnit target =
            (IsAlive(selectedEnemyTarget) && !selectedEnemyTarget.isPlayer)
            ? selectedEnemyTarget
            : GetFirstAliveEnemy();

        if (target == null)
        {
            yield return EndBattleAndReturn(true);
            yield break;
        }

        lockedTargetForThisAction[attacker] = target;
        lockedFxPrefabForThisAction[attacker] = attacker.attackFxPrefab;

        attacker.TriggerAttack();
        yield return WaitAttackFinish(attacker);

        var r = ComputeDamage(attacker, target);
        target.TakeDamage(r.dmg);
        SpawnDamagePopup(target, r.dmg, r.crit);

        attacker.AddEnergy(energyGainOnNormalAttack); 
        RefreshUI(); 

        if (target.IsDead())
        {
            // IMPORTANT: player deaths must never block the turn pipeline.
            // Schedule the death coroutine, but do not yield on it.
            if (target != null && target.isPlayer)
            {
                CleanupDeadTargetRefs(target);
                PurgeCutInsForUnit(target);
                StartCoroutine(PlayDeathAndRemove(target));
            }
            else
            {
                yield return PlayDeathAndRemove(target);
            }
        }

        if (AllEnemiesDead())
        {
            yield return EndBattleAndReturn(true);
            yield break;
        }
        if (token != turnToken)
        {
            Debug.LogWarning($"[STALE] PlayerAttackFlow ignored token={token} turnToken={turnToken}");
            yield break;
        }

        TickAllModifiersOneTurn();

        yield return ProcessPendingCutIns(token);

        // If the watchdog (or another system) already advanced the turn token while we were yielding,
        // do NOT advance again. This prevents "double turns" after a forced token advance.
        if (token != turnToken)
        {
            Debug.LogWarning($"[STALE] PlayerAttackFlow post-cutin ignored token={token} turnToken={turnToken}");
            yield break;
        }

        queueIndex++;
        AdvanceToNextActor();
    }

    IEnumerator PlayerUltimateFlow(BattleUnit attacker, int token)
    {
        _queueManuallyReorderedThisAction = false;
        if (!attacker.HasFullEnergy())
        {
            Debug.LogWarning($"[ULT] {attacker.name} energy not full: {attacker.energy}/{attacker.energyMax}");
            state = TurnState.WaitingInput;
            busyOwnerToken = token;
            RefreshUI();
            yield break;
        }
        state = TurnState.Busy;
        HideTargetArrow();
        HideActiveAura();
        RefreshUI();

        BattleUnit target =
            (IsAlive(selectedEnemyTarget) && !selectedEnemyTarget.isPlayer)
            ? selectedEnemyTarget
            : GetFirstAliveEnemy();

        if (target == null)
        {
            yield return EndBattleAndReturn(true);
            yield break;
        }

        lockedTargetForThisAction[attacker] = target;

        GameObject fx = attacker.ultimateFxPrefab != null ? attacker.ultimateFxPrefab : attacker.attackFxPrefab;
        lockedFxPrefabForThisAction[attacker] = fx;

        attacker.TriggerUltimate();
        yield return WaitUltimateFinish(attacker);

        attacker.SpendEnergy(energyCostUltimate);
        RefreshUI();
        if (attacker.ultimateHealsParty)
        {
            for (int i = 0; i < players.Count; i++)
            {
                var ally = players[i].unit;
                if (ally == null) continue;
                if (!IsAlive(ally)) continue;

                int heal = ComputePartyHealAmount(attacker, ally);
                ally.Heal(heal);

                SpawnHealPopup(ally, heal);

                if (attacker.ultimateHealFxPrefab != null)
                {
                    Transform hit = ally.hitPoint != null ? ally.hitPoint : ally.transform;
                    StartCoroutine(PlayFxAndWait(attacker.ultimateHealFxPrefab, hit));
                }
            }
            if (token != turnToken)
            {
                Debug.LogWarning($"[STALE] PlayerUltimateFlow ignored token={token} turnToken={turnToken}");
                yield break;
            }

            TickAllModifiersOneTurn();

            yield return ProcessPendingCutIns(token);

            if (token != turnToken)
            {
                Debug.LogWarning($"[STALE] PlayerUltimateFlow post-cutin ignored token={token} turnToken={turnToken}");
                yield break;
            }

            queueIndex++;
            AdvanceToNextActor();
            yield break;
        }


        var r = ComputeUltimateDamage(attacker, target);
        target.TakeDamage(r.dmg);
        SpawnDamagePopup(target, r.dmg, r.crit);

        if (target.IsDead())
        {
            yield return PlayDeathAndRemove(target);
        }

        if (AllEnemiesDead())
        {
            yield return EndBattleAndReturn(true);
            yield break;
        }

        TickAllModifiersOneTurn();

        yield return ProcessPendingCutIns(token);

        if (token != turnToken)
        {
            Debug.LogWarning($"[STALE] PlayerUltimateFlow post-cutin ignored token={token} turnToken={turnToken}");
            yield break;
        }

        queueIndex++;
        AdvanceToNextActor();
    }


    IEnumerator EnemyAutoAttackFlow(BattleUnit enemy, int token)
    {
        _queueManuallyReorderedThisAction = false;
        yield return new WaitForSecondsRealtime(enemyDelayBeforeAttack);

        if (enemy == null || !IsAlive(enemy))
        {
            // Defensive: avoid stalling if an enemy was destroyed/removed while still queued.
            if (token == turnToken)
            {
                AdvanceToNextActor();
            }
            yield break;
        }

        BattleUnit target = PickAlivePlayer();

        if (enemy != null && enemy.isBigBoss && enemy.bigBossUseSummonOnce && !bigBossSummonUsed.Contains(enemy))
        {
            yield return BigBossSummonFlow(enemy, token);

            if (token != turnToken)
            {
                Debug.LogWarning($"[STALE] BigBossSummonFlow ignored token={token} turnToken={turnToken}");
                yield break;
            }

            TickAllModifiersOneTurn();

            yield return ProcessPendingCutIns(token);

            if (token != turnToken)
            {
                Debug.LogWarning($"[STALE] BigBossSummonFlow post-cutin ignored token={token} turnToken={turnToken}");
                yield break;
            }

            queueIndex++;
            AdvanceToNextActor();
            yield break;
        }
        // Prefer data-driven skill for enemies if provided
        if (enemy != null && enemy.skillData != null)
        {
            yield return CastSkillAsTurn(enemy, enemy.skillData, token);
            yield break;
        }

        if (target == null)
        {
            yield return EndBattleAndReturn(false);
            yield break;
        }

        lockedTargetForThisAction[enemy] = target;

        enemy.TriggerAttack();
        yield return WaitAttackFinish(enemy);

        var r = ComputeDamage(enemy, target);
        target.TakeDamage(r.dmg);
        SpawnDamagePopup(target, r.dmg, r.crit);


        // Gain energy on normal attack (enables enemy ult cut-ins)
        enemy.AddEnergy(energyGainOnNormalAttack);
        if (target.IsDead())
        {
            // IMPORTANT: player deaths must never block the turn pipeline.
            // Schedule the death coroutine, but do not yield on it.
            if (target != null && target.isPlayer)
            {
                CleanupDeadTargetRefs(target);
                PurgeCutInsForUnit(target);
                StartCoroutine(PlayDeathAndRemove(target));
            }
            else
            {
                yield return PlayDeathAndRemove(target);
            }
        }

        if (AllPlayersDead())
        {
            yield return EndBattleAndReturn(false);
            yield break;
        }

        if (token != turnToken)
        {
            Debug.LogWarning($"[STALE] EnemyAutoAttackFlow ignored token={token} turnToken={turnToken}");
            yield break;
        }

        TickAllModifiersOneTurn();

        yield return ProcessPendingCutIns(token);

        if (token != turnToken)
        {
            Debug.LogWarning($"[STALE] EnemyAutoAttackFlow post-cutin ignored token={token} turnToken={turnToken}");
            yield break;
        }

        queueIndex++;
        AdvanceToNextActor();
    }

    IEnumerator BigBossSummonFlow(BattleUnit boss, int token)
{
    if (boss == null) yield break;
    if (boss.isSummonedClone) yield break;

    // Prevent duplicate concurrent summon coroutines.
    if (bigBossSummonInProgress) yield break;

    // Once per battle: big boss can summon only once.
    if (bigBossSummonUsedThisBattle)
    {
        bigBossSummonUsed.Add(boss);
        yield break;
    }

    bigBossSummonInProgress = true;

    RebuildAllUnits(); // for occupancy checks

    if (boss.summonFxPrefab != null)
    {
        Transform p = boss.GetSummonFxPoint();
        StartCoroutine(PlayFxAndWait(boss.summonFxPrefab, p));
    }

    boss.TriggerSummon();
    yield return WaitSummonFinish(boss);

    if (minionPrefab == null)
    {
        Debug.LogWarning("[SUMMON] minionPrefab is NULL, skip spawn.");
        bigBossSummonInProgress = false;
        yield break;
    }

    // BigBoss should summon at ALL configured spawn points in one cast.
    // (Users control the number by adding/removing spawn points.)
    int available = CountValidSpawnPoints();
    int desired = Mathf.Max(1, available);

    // SummonSpawnRule is a shared enum (see BattleEnums.cs). Do NOT reference it as SkillEffect.SummonSpawnRule.
    List<Vector3> spawnPositions = GetSummonPositions(boss, desired, SummonSpawnRule.UseManagerSpawnPoints);
    spawnPositions = EnsureUniqueAndFreeSummonPositions(spawnPositions, desired);

    // Spawn each minion (can be multiple) and then insert them into the current round queue.
    // This makes the summons immediately actable and visible in UI/turn order.
    var spawned = new List<BattleUnit>();

    int finished = 0;
    for (int i = 0; i < spawnPositions.Count; i++)
    {
        Vector3 p = spawnPositions[i];
        StartCoroutine(SpawnMinionAfterFx_WithResult(p, spawned, () => finished++));
    }

    float t = 0f;
    while (finished < spawnPositions.Count && t < (maxWaitSummonTotal + 2f))
    {
        t += Time.unscaledDeltaTime;
        yield return null;
    }

    // Mark used only after we attempted spawning.
    bigBossSummonUsedThisBattle = true;
    bigBossSummonUsed.Add(boss);

    // Insert spawned minions into current queue right after boss.
    if (speedQueue != null && speedQueue.Count > 0)
    {
        int insertPos = Mathf.Clamp(queueIndex + 1, 0, speedQueue.Count);
        for (int i = 0; i < spawned.Count; i++)
        {
            var m = spawned[i];
            if (m == null) continue;
            if (!IsAlive(m)) continue;
            if (!speedQueue.Contains(m))
            {
                speedQueue.Insert(insertPos, m);
                insertPos++;
            }
        }
    }

    RebuildAllUnits();
    RefreshUI();

    bigBossSummonInProgress = false;
}



void GetTwoSummonPositions(BattleUnit boss, out Vector3 p1, out Vector3 p2)
{
    // 优先用你在Inspector设置的多个出生点：每次召唤会轮换位置，避免永远固定
    if (minionSpawnPoints != null && minionSpawnPoints.Length > 0)
    {
        int n = minionSpawnPoints.Length;

        int i1 = -1;
        int i2 = -1;

        for (int k = 0; k < n; k++)
        {
            int i = (summonSpawnCursor + k) % n;
            if (minionSpawnPoints[i] == null) continue;
            i1 = i;
            break;
        }

        for (int k = 1; k <= n; k++)
        {
            int i = (summonSpawnCursor + k) % n;
            if (minionSpawnPoints[i] == null) continue;
            if (i == i1) continue;
            i2 = i;
            break;
        }

        if (i1 != -1 && i2 != -1)
        {
            p1 = JitterSummonPos(minionSpawnPoints[i1].position);
            p2 = JitterSummonPos(minionSpawnPoints[i2].position);

            summonSpawnCursor = (i2 + 1) % n;
            return;
        }
    }

    // 兜底：用boss位置两侧偏移
    Vector3 basePos = boss != null ? boss.transform.position : Vector3.zero;
    p1 = JitterSummonPos(basePos + fallbackSpawnOffset1);
    p2 = JitterSummonPos(basePos + fallbackSpawnOffset2);
}

// Small helper to slightly randomize spawn positions (prevents perfect overlap when points are close)
Vector3 JitterSummonPos(Vector3 pos)
{
    float jx = Mathf.Max(0f, summonPointJitterX);
    float jy = Mathf.Max(0f, summonPointJitterY);
    if (jx <= 0f && jy <= 0f) return pos;

    float dx = (jx > 0f) ? Random.Range(-jx, jx) : 0f;
    float dy = (jy > 0f) ? Random.Range(-jy, jy) : 0f;
    return new Vector3(pos.x + dx, pos.y + dy, pos.z);
}

int CountValidSpawnPoints()
{
    int count = 0;
    if (minionSpawnPoints == null) return 0;
    for (int i = 0; i < minionSpawnPoints.Length; i++)
    {
        if (minionSpawnPoints[i] != null) count++;
    }
    return count;
}

bool IsSpawnPosOccupied(Vector3 pos, float radius)
{
    if (radius <= 0f) return false;
    float r2 = radius * radius;
    for (int i = 0; i < allUnits.Count; i++)
    {
        var u = allUnits[i];
        if (u == null) continue;
        if (!u.gameObject.activeInHierarchy) continue;
        if (!IsAlive(u)) continue;
        Vector3 d = u.transform.position - pos;
        if (d.sqrMagnitude <= r2) return true;
    }
    return false;
}

Vector3 FindFreeSummonPos(Vector3 desired)
{
    if (summonOccupyRadius <= 0f) return desired;
    if (!IsSpawnPosOccupied(desired, summonOccupyRadius)) return desired;

    float step = summonOccupyRadius * 1.6f;
    Vector3[] offsets = new Vector3[]
    {
        new Vector3(step, 0f, 0f),
        new Vector3(-step, 0f, 0f),
        new Vector3(0f, step, 0f),
        new Vector3(0f, -step, 0f),
        new Vector3(step, step, 0f),
        new Vector3(-step, step, 0f),
        new Vector3(step, -step, 0f),
        new Vector3(-step, -step, 0f),
    };

    for (int i = 0; i < offsets.Length; i++)
    {
        Vector3 candidate = desired + offsets[i];
        if (!IsSpawnPosOccupied(candidate, summonOccupyRadius)) return candidate;
    }

    for (int k = 0; k < 8; k++)
    {
        Vector3 candidate = desired + new Vector3(Random.Range(-step, step), Random.Range(-step, step), 0f);
        if (!IsSpawnPosOccupied(candidate, summonOccupyRadius)) return candidate;
    }

    return desired;
}

List<Vector3> EnsureUniqueAndFreeSummonPositions(List<Vector3> raw, int desiredCount)
{
    List<Vector3> result = new List<Vector3>(desiredCount);

    if (raw != null)
    {
        for (int i = 0; i < raw.Count && result.Count < desiredCount; i++)
        {
            Vector3 p = FindFreeSummonPos(raw[i]);

            bool tooClose = false;
            for (int j = 0; j < result.Count; j++)
            {
                if ((result[j] - p).sqrMagnitude < 0.01f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) p += new Vector3(0.4f * (result.Count + 1), 0f, 0f);

            result.Add(p);
        }
    }

    while (result.Count < desiredCount)
    {
        Vector3 basePos = result.Count > 0 ? result[0] : Vector3.zero;
        Vector3 p = basePos + (result.Count == 0 ? fallbackSpawnOffset1 : fallbackSpawnOffset2);
        p = FindFreeSummonPos(JitterSummonPos(p));
        result.Add(p);
    }

    return result;
}

IEnumerator SpawnMinionAfterFx_WithDone(Vector3 spawnPos, System.Action done)
{
    yield return SpawnMinionAfterFx(spawnPos);
    done?.Invoke();
}

// Same as SpawnMinionAfterFx, but records the spawned unit and then signals done.
IEnumerator SpawnMinionAfterFx_WithResult(Vector3 spawnPos, List<BattleUnit> outSpawned, System.Action done)
{
    // 先播召唤点动画/特效（必须）
    if (minionSpawnFxPrefab != null)
    {
        GameObject fx = Instantiate(minionSpawnFxPrefab, spawnPos, Quaternion.identity);

        var sr = fx.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 999;

        var anim = fx.GetComponent<Animator>();
        if (anim != null)
        {
            yield return null;
            float t = 0f;
            while (anim != null && t < maxWaitFx)
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                if (!st.loop && st.normalizedTime >= 1f) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(fxFallbackTime);
        }

        Destroy(fx);
    }
    else
    {
        yield return new WaitForSecondsRealtime(0.15f);
    }

    // 再出小怪（BigBoss 召唤出的 SmallBoss clone）
    BattleUnit m = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
    if (m != null)
    {
        m.isPlayer = false;
        m.isSummonedClone = true;
        m.isSmallBoss = true;
        m.isBigBoss = false;
        // clone 死亡后必须直接消失，不能触发小Boss变身
        m.transformOnDeath = false;
        m.bigBossUseSummonOnce = false;
        // Make sure clone disappears on death.
        m.destroyOnDeath = true;

        // Lock hit/popup anchor to summon position so impact point stays at the spawn location.
        m.LockHitAndPopupPointToWorld(spawnPos, lockPopupPointToo: true);

        enemies.Add(m);
        if (outSpawned != null) outSpawned.Add(m);
        Debug.Log($"[SUMMON] spawned {m.name} at {spawnPos}");
    }

    done?.Invoke();
}




    IEnumerator SpawnMinionAfterFx(Vector3 spawnPos)
    {
        // 先播召唤点动画/特效（必须）
        if (minionSpawnFxPrefab != null)
        {
            // 复用现有 PlayFxAndWait（它会等特效播完再Destroy）
            GameObject fx = Instantiate(minionSpawnFxPrefab, spawnPos, Quaternion.identity);

            var sr = fx.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 999;

            var anim = fx.GetComponent<Animator>();
            if (anim != null)
            {
                yield return null;
                float t = 0f;
                while (anim != null && t < maxWaitFx)
                {
                    var st = anim.GetCurrentAnimatorStateInfo(0);
                    if (!st.loop && st.normalizedTime >= 1f) break;
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                // 没Animator就用兜底时间
                yield return new WaitForSecondsRealtime(fxFallbackTime);
            }

            Destroy(fx);
        }
        else
        {
            // 没有出生特效也不能“立刻出怪”——给个最短兜底
            yield return new WaitForSecondsRealtime(0.15f);
        }

        // 再出小怪（BigBoss 召唤出的 SmallBoss clone）
        BattleUnit m = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
        if (m != null)
        {
            m.isPlayer = false;
            m.isSummonedClone = true;
            m.isSmallBoss = true;
            m.isBigBoss = false;
            // clone 死亡后必须直接消失，不能触发小Boss变身
            m.transformOnDeath = false;
            m.bigBossUseSummonOnce = false;
            // Make sure clone disappears on death.
            m.destroyOnDeath = true;

            // Lock hit/popup anchor to summon position so impact point stays at the spawn location.
            m.LockHitAndPopupPointToWorld(spawnPos, lockPopupPointToo: true);

            enemies.Add(m);
        }
    }

    DamageResult ComputeDamage(BattleUnit attacker, BattleUnit target)
    {
        int baseDmg = attacker.atk - target.def;
        if (baseDmg < 1) baseDmg = 1;

        bool crit = (Random.value < Mathf.Clamp01(attacker.cr));
        float mul = crit ? Mathf.Max(1f, attacker.cd) : 1f;

        int dmg = Mathf.RoundToInt(baseDmg * mul);
        if (dmg < 1) dmg = 1;

        return new DamageResult { dmg = dmg, crit = crit };
    }

    DamageResult ComputeUltimateDamage(BattleUnit attacker, BattleUnit target)
    {
        var r = ComputeDamage(attacker, target);

        int dmg = Mathf.RoundToInt(r.dmg * Mathf.Max(1f, ultimateDamageMultiplier)) + ultimateFlatBonus;
        if (dmg < 1) dmg = 1;

        r.dmg = dmg;
        return r;
    }

    int ComputePartyHealAmount(BattleUnit healer, BattleUnit ally)
    {
        int heal = healer.ultimateHealFlat + Mathf.RoundToInt(healer.atk * healer.ultimateHealAtkRatio);
        if (heal < 1) heal = 1;
        return heal;
    }

    // ---------------------
    // Popup queue helpers
    // ---------------------
    void EnqueuePopup(PopupRequest req)
    {
        if (req == null || req.target == null) return;

        Queue<PopupRequest> q;
        if (!_popupQueues.TryGetValue(req.target, out q) || q == null)
        {
            q = new Queue<PopupRequest>();
            _popupQueues[req.target] = q;
        }
        q.Enqueue(req);

        if (!enablePopupQueue)
        {
            // If queue is disabled, spawn immediately without waiting.
            var r = q.Dequeue();
            SpawnPopupImmediate(r);
            return;
        }

        if (!_popupProcessing.Contains(req.target))
        {
            StartCoroutine(ProcessPopupQueue(req.target));
        }
    }

    IEnumerator ProcessPopupQueue(BattleUnit target)
    {
        if (target == null) yield break;
        _popupProcessing.Add(target);

        while (true)
        {
            if (target == null)
                break;

            Queue<PopupRequest> q;
            if (!_popupQueues.TryGetValue(target, out q) || q == null || q.Count == 0)
                break;

            var req = q.Dequeue();
            SpawnPopupImmediate(req);

            // Let the previous popup float up a bit before the next one spawns.
            if (popupQueueInterval > 0f)
                yield return new WaitForSecondsRealtime(popupQueueInterval);
            else
                yield return null;
        }

        _popupProcessing.Remove(target);
        if (target != null)
        {
            Queue<PopupRequest> q;
            if (_popupQueues.TryGetValue(target, out q) && (q == null || q.Count == 0))
                _popupQueues.Remove(target);
        }
    }

    void SpawnPopupImmediate(PopupRequest req)
    {
        if (req == null) return;
        if (damagePopupPrefab == null) { Debug.LogError("[POPUP] damagePopupPrefab = NULL"); return; }
        if (popupCanvas == null) { Debug.LogError("[POPUP] popupCanvas = NULL"); return; }
        if (req.target == null) { return; }

        // If the target was destroyed or hidden, skip.
        if (!req.target.gameObject) return;
        if (!req.target.gameObject.activeInHierarchy) return;

        if (worldCamera == null) worldCamera = Camera.main;
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) { Debug.LogError("[POPUP] worldCamera = NULL (MainCamera tag?)"); return; }

        Vector3 worldPos = req.target.transform.position + popupWorldOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f) return;

        DamagePopup popup = Instantiate(damagePopupPrefab, popupCanvas.transform);
        if (popup == null) return;
        popup.transform.SetAsLastSibling();

        RectTransform canvasRt = popupCanvas.GetComponent<RectTransform>();
        RectTransform popupRt = popup.GetComponent<RectTransform>();
        if (canvasRt == null || popupRt == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt,
            screenPos,
            popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
            out localPoint
        );
        popupRt.anchoredPosition = localPoint;

        switch (req.kind)
        {
            case PopupKind.Damage:
                popup.Setup(req.intValue, req.crit);
                break;
            case PopupKind.Heal:
                popup.SetupHeal(req.intValue);
                break;
            case PopupKind.Text:
                popup.SetupText(req.text);
                break;
        }
    }


    void SpawnDamagePopup(BattleUnit target, int damage, bool crit)
    {
        if (target == null) return;

        Debug.Log($"[POPUP] dmg={damage} crit={crit}");
        EnqueuePopup(new PopupRequest
        {
            kind = PopupKind.Damage,
            target = target,
            intValue = damage,
            crit = crit
        });
    }

    void SpawnHealPopup(BattleUnit target, int heal)
    {
        if (target == null) return;
        EnqueuePopup(new PopupRequest
        {
            kind = PopupKind.Heal,
            target = target,
            intValue = heal
        });
    }


    public void SpawnAttackFxNow(BattleUnit attacker)
    {
        if (attacker == null) return; 

        if (!lockedTargetForThisAction.TryGetValue(attacker, out var target) || target == null || !IsAlive(target))
        {
            target = attacker.isPlayer ? GetFirstAliveEnemy() : PickAlivePlayer();
        }
        if (target == null) return;

        GameObject fxPrefab = null;
        if (!lockedFxPrefabForThisAction.TryGetValue(attacker, out fxPrefab) || fxPrefab == null)
            fxPrefab = attacker.attackFxPrefab;

        // Decide FX spawn point.
        // Keep FX spawn point consistent with normal skills: always spawn at the target hit point.
        // (The prefab is still chosen by the current action/animation lock.)
        Transform fxPoint = target.hitPoint != null ? target.hitPoint : target.transform;

        StartCoroutine(PlayFxAndWait(fxPrefab, fxPoint));

        // Consume one-shot FX lock after spawning to avoid leaking into later actions.
        lockedFxPrefabForThisAction.Remove(attacker);

    }

    BattleUnit GetFirstAliveEnemy()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (IsAlive(e)) return e;
        }
        return null;
    }

    BattleUnit PickAlivePlayer()
    {
        List<BattleUnit> alive = new List<BattleUnit>();
        for (int i = 0; i < players.Count; i++)
        {
            var u = players[i].unit;
            if (IsAlive(u)) alive.Add(u);
        }
        if (alive.Count == 0) return null;
        return alive[Random.Range(0, alive.Count)];
    }

    bool AllEnemiesDead()
    {
        for (int i = 0; i < enemies.Count; i++)
            if (IsAlive(enemies[i])) return false;
        return true;
    }

    bool AllPlayersDead()
    {
        for (int i = 0; i < players.Count; i++)
            if (IsAlive(players[i].unit)) return false;
        return true;
    }

    IEnumerator EndBattleAndReturn(bool playerWin)
    {
        state = TurnState.End;
        RefreshButtons();
        yield return new WaitForSecondsRealtime(0.15f);
        if (GameSession.I != null)
        {
            GameSession.I.EnsurePartySize(players.Count);

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].unit != null)
                    GameSession.I.CaptureFromBattleUnit(players[i].unit, i);
            }

            if (playerWin) GameSession.I.EndBattle_PlayerWin();
            else GameSession.I.EndBattle_PlayerLose();

            if (playerWin)
            {
                int baseExp = GameSession.I != null ? GameSession.I.expPerEnemy : 5;
                int expGain = 10 + enemies.Count * baseExp;

                for (int i = 0; i < players.Count; i++)
                    GameSession.I.GrantWinExp(i, expGain);

                Debug.Log($"[BattleManager] Win -> partyExpGain={expGain} (players={players.Count})");
            }
        }
        SceneManager.LoadScene(worldSceneName);
    }

    IEnumerator WaitAttackFinish(BattleUnit unit)
    {
        if (unit == null || unit.animator == null) yield break;
        if (string.IsNullOrEmpty(unit.attackStateName)) yield break;

        yield return null;

        float t = 0f;
        while (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName) && t < maxWaitEnterAttack)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName))
            yield break;

        float t2 = 0f;
        while (unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName) && t2 < maxWaitAttackTotal)
        {
            t2 += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // Wait until the summon animation state finishes.
    // This mirrors WaitAttackFinish/WaitUltimateFinish but uses summonStateName and summon safety timers.
    IEnumerator WaitSummonFinish(BattleUnit unit)
    {
        if (unit == null || unit.animator == null) yield break;
        if (string.IsNullOrEmpty(unit.summonStateName))
        {
            // If no explicit summon state is configured, give one frame to let triggers process.
            yield return null;
            yield break;
        }

        yield return null;

        float tEnter = 0f;
        while (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.summonStateName) && tEnter < maxWaitEnterSummon)
        {
            tEnter += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.summonStateName))
            yield break;

        float t = 0f;
        while (unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.summonStateName) && t < maxWaitSummonTotal)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator WaitUltimateFinish(BattleUnit unit)
    {
        if (unit == null || unit.animator == null) yield break;

        if (string.IsNullOrEmpty(unit.ultimateStateName))
        {
            yield return WaitAttackFinish(unit);
            yield break;
        }

        yield return null;

        float t = 0f;
        while (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.ultimateStateName) && t < maxWaitEnterUltimate)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.ultimateStateName))
            yield break;

        float t2 = 0f;
        while (unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.ultimateStateName) && t2 < maxWaitUltimateTotal)
        {
            t2 += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator PlayFxAndWait(GameObject fxPrefab, Transform point)
    {
        if (fxPrefab == null || point == null)
        {
            yield return new WaitForSecondsRealtime(fxFallbackTime);
            yield break;
        }

        GameObject fx = Instantiate(fxPrefab, point.position, Quaternion.identity);

        var sr = fx.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 999;

        var anim = fx.GetComponent<Animator>();
        if (anim == null)
        {
            yield return new WaitForSecondsRealtime(fxFallbackTime);
            Destroy(fx);
            yield break;
        }

        yield return null;

        float t = 0f;
        while (anim != null && t < maxWaitFx)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (!st.loop && st.normalizedTime >= 1f) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Destroy(fx);
    }

    IEnumerator PlayDeathAndRemove(BattleUnit unit)
    {
        if (unit == null) yield break;
        // Prevent duplicate death handling (double coroutines can cause double spawns).
        if (deathInProgress.Contains(unit)) yield break;

        BattleUnit guardKey = unit;
        deathInProgress.Add(guardKey);

        // BigBoss: must NEVER go through the SmallBoss transform branch and must be destroyed.
        if (!unit.isPlayer && unit.isBigBoss)
        {
            EnsureBigBossRuntimeFlags(unit);
        }

        // SmallBoss clone: death should directly disappear, never transform into BigBoss.
        if (!unit.isPlayer && unit.isSummonedClone && unit.isSmallBoss)
        {
            CleanupDeadTargetRefs(unit);
            PurgeCutInsForUnit(unit);
            RemoveEnemyFromRuntimeLists(unit);
            unit.HideOrDestroy();
            deathInProgress.Remove(guardKey);
            yield break;
        }

        // Real SmallBoss: transform to BigBoss on death (if enabled).
        // Guard 1: never allow BigBoss to enter this branch.
        // Guard 2: once BigBoss has appeared in this battle, never spawn another BigBoss (prevents "revive").
        if (!unit.isPlayer && !unit.isBigBoss && unit.isSmallBoss && !unit.isSummonedClone
            && unit.transformOnDeath && unit.smallBossSkipDeathAnim
            && !bigBossSpawnedThisBattle)
        {
            PurgeCutInsForUnit(unit);
            yield return SmallBossTransformThenSpawnBigBoss(unit);
            deathInProgress.Remove(guardKey);
            yield break;
        }


        if (unit.animator == null || string.IsNullOrEmpty(unit.deathStateName))
        {
            CleanupDeadTargetRefs(unit);
            PurgeCutInsForUnit(unit);
            if (unit.isPlayer) RemoveUnitFromTurnQueue(unit);
            else RemoveEnemyFromRuntimeLists(unit);
            unit.HideOrDestroy();
            deathInProgress.Remove(guardKey);
            yield break;
        }

        unit.TriggerDie();
        yield return null;

        // 等进入死亡状态（最多0.5秒）
        float enter = 0f;
        while (enter < 0.5f)
        {
            if (unit == null) { deathInProgress.Remove(guardKey); yield break; }
            var st = unit.animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(unit.deathStateName)) break;
            enter += Time.unscaledDeltaTime;
            yield return null;
        }

        // 等死亡播完（最多 maxWaitDeath 秒），即使 loop/填错也会退出
        float t = 0f;
        while (t < maxWaitDeath)
        {
            if (unit == null) { deathInProgress.Remove(guardKey); yield break; }

            var st = unit.animator.GetCurrentAnimatorStateInfo(0);

            // 没在死亡状态就别卡着
            if (!st.IsName(unit.deathStateName))
                break;

            // 非循环播完就结束
            if (!st.loop && st.normalizedTime >= 1f)
                break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        CleanupDeadTargetRefs(unit);
        PurgeCutInsForUnit(unit);
        if (unit.isPlayer) RemoveUnitFromTurnQueue(unit);
        else RemoveEnemyFromRuntimeLists(unit);
        unit.HideOrDestroy();
        deathInProgress.Remove(guardKey);
    }

    IEnumerator SmallBossTransformThenSpawnBigBoss(BattleUnit smallBoss)
    {
        if (smallBoss == null) yield break;

        // If BigBoss has ever appeared in this battle, never spawn another.
        // This avoids an unintended "revive" when BigBoss is killed and a SmallBoss dies afterwards (including ...
        if (bigBossSpawnedThisBattle)
        {
            CleanupDeadTargetRefs(smallBoss);
            PurgeCutInsForUnit(smallBoss);
            RemoveEnemyFromRuntimeLists(smallBoss);
            smallBoss.HideOrDestroy();
            RebuildTurnQueuePreserveCurrentActor();
            yield break;
        }

        // Hard guard: prevent duplicate big boss spawns due to unexpected multiple death handlers.
        if (bigBossSpawnInProgress)
        {
            Debug.LogWarning("[BOSS] Big boss spawn already in progress, skip duplicate transform handling.");
            CleanupDeadTargetRefs(smallBoss);
            PurgeCutInsForUnit(smallBoss);
            RemoveEnemyFromRuntimeLists(smallBoss);
            smallBoss.HideOrDestroy();
            yield break;
        }
        bigBossSpawnInProgress = true;

        // 1) 变身特效（可选）
        if (smallBoss.transformFxPrefab != null)
        {
            Transform p = smallBoss.GetTransformFxPoint();
            StartCoroutine(PlayFxAndWait(smallBoss.transformFxPrefab, p));
        }

        // 2) 触发变身动画（不用 Die）
        if (smallBoss.animator != null && !string.IsNullOrEmpty(smallBoss.transformStateName))
        {
            smallBoss.TriggerTransform();
            yield return null;

            float enter = 0f;
            while (enter < maxWaitEnterTransform)
            {
                if (smallBoss == null) { bigBossSpawnInProgress = false; yield break; }
                if (smallBoss.animator == null) break;
                var st = smallBoss.animator.GetCurrentAnimatorStateInfo(0);
                if (st.IsName(smallBoss.transformStateName)) break;
                enter += Time.unscaledDeltaTime;
                yield return null;
            }

            float t = 0f;
            while (t < maxWaitTransformTotal)
            {
                if (smallBoss == null) { bigBossSpawnInProgress = false; yield break; }
                if (smallBoss.animator == null) break;
                var st = smallBoss.animator.GetCurrentAnimatorStateInfo(0);

                if (!st.IsName(smallBoss.transformStateName)) break;
                if (!st.loop && st.normalizedTime >= 1f) break;

                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        
        // If a big boss already exists in the scene, keep it and do not spawn another.
        BattleUnit existing = FindFirstAliveBigBossActive();
        if (existing != null)
        {
            cachedBigBoss = existing;
            bigBossSpawnedThisBattle = true;
            EnforceSingleBigBoss(cachedBigBoss);

            CleanupDeadTargetRefs(smallBoss);
            PurgeCutInsForUnit(smallBoss);
            RemoveEnemyFromRuntimeLists(smallBoss);
            smallBoss.HideOrDestroy();
            RebuildTurnQueuePreserveCurrentActor();
            bigBossSpawnInProgress = false;
            yield break;
        }

        if (bigBossPrefab == null)
        {
            Debug.LogWarning("[BOSS] bigBossPrefab is NULL, cannot spawn big boss.");
            CleanupDeadTargetRefs(smallBoss);
            PurgeCutInsForUnit(smallBoss);
            RemoveEnemyFromRuntimeLists(smallBoss);
            smallBoss.HideOrDestroy();
            RebuildTurnQueuePreserveCurrentActor();
            bigBossSpawnInProgress = false;
            yield break;
        }

        Vector3 pos = smallBoss.transform.position;
        if (bigBossSpawnPoint != null) pos = bigBossSpawnPoint.position;

        BattleUnit big = Instantiate(bigBossPrefab, pos, Quaternion.identity);
        if (big != null)
        {
            // Ensure BigBoss never shows the default Unity "(Clone)" suffix.
            big.gameObject.name = StripCloneSuffix(bigBossPrefab.name);

            big.isPlayer = false;
            big.isBigBoss = true;
            big.isSmallBoss = false;
            big.isSummonedClone = false;
            big.transformOnDeath = false;
            // Requirement: BigBoss should be destroyed on death.
            big.destroyOnDeath = true;

            enemies.Add(big);
            CleanupEnemiesList();

            cachedBigBoss = big;
            bigBossSpawnedThisBattle = true;
            EnforceSingleBigBoss(cachedBigBoss);

            // Final hardening for runtime flags.
            EnsureBigBossRuntimeFlags(cachedBigBoss);

            // In case other systems rename it later, normalize again.
            NormalizeBigBossRuntimeName(cachedBigBoss);
        }

        CleanupDeadTargetRefs(smallBoss);
        PurgeCutInsForUnit(smallBoss);
        RemoveEnemyFromRuntimeLists(smallBoss);
        smallBoss.HideOrDestroy();
        RebuildTurnQueuePreserveCurrentActor();
        bigBossSpawnInProgress = false;
    }


    void CleanupDeadTargetRefs(BattleUnit dead)
    {
        if (dead == null) return;

        if (selectedEnemyTarget == dead)
            selectedEnemyTarget = null;

        var keys = new List<BattleUnit>(lockedTargetForThisAction.Keys);
        foreach (var k in keys)
        {
            if (lockedTargetForThisAction.TryGetValue(k, out var tar) && tar == dead)
                lockedTargetForThisAction.Remove(k);
        }

        var fxKeys = new List<BattleUnit>(lockedFxPrefabForThisAction.Keys);
        foreach (var k in fxKeys)
        {
            if (lockedFxPrefabForThisAction.ContainsKey(k) && k == dead)
                lockedFxPrefabForThisAction.Remove(k);
        }
    }


    // =========================================================
    // (NEW) Data-driven Skill System
    // =========================================================

    bool IsHumanControllablePlayer(BattleUnit unit)
    {
        if (unit == null) return false;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null) continue;
            if (players[i].unit != unit) continue;

            if (players[i].attackButton != null) return true;
            if (players[i].ultimateButton != null) return true;
        }
        return false;
    }

    IEnumerator AutoPlayerUnitFlow(BattleUnit unit, int token)
    {
        if (unit == null || !IsAlive(unit))
        {
            // Defensive: if the queue scheduled an invalid unit, do not stall in Busy state.
            if (token == turnToken)
            {
                AdvanceToNextActor();
            }
            yield break;
        }

        // Prefer data-driven skill
        SkillData skill = unit.skillData != null ? unit.skillData : defaultPlayerSkill;
        if (skill != null)
        {
            yield return CastSkillAsTurn(unit, skill, token);
            yield break;
        }

        // Fallback to legacy normal attack
        yield return PlayerAttackFlow(unit, token);
    }

    
    // Automatically enqueue ultimates when energy is full (cut-in) for AI units only.
    // Human-controlled players must click the ultimate button; this auto-queue skips them.
    void AutoQueueCutInUltimates()
    {
        if (!allowCutInUltimate) return;

        RebuildAllUnits();
        for (int i = 0; i < allUnits.Count; i++)
        {
            var u = allUnits[i];
            if (u == null) continue;

            // Manual player ultimates: do NOT auto-enqueue. Players must click the ultimate button.
            if (u.isPlayer && IsHumanControllablePlayer(u))
                continue;

            if (!IsAlive(u)) continue;
            if (!u.HasFullEnergy()) continue;

            var ult = u.ultimateData;
            if (ult == null) continue;
            if (!ult.allowCutInWhenEnergyFull) continue;

            RequestCutInUltimate(u, ult);
        }
    }

void RequestCutInUltimate(BattleUnit caster, SkillData skill)
    {
        if (caster == null || skill == null) return;
        if (pendingCutInCasters.Contains(caster)) return;

        pendingCutIns.Enqueue(new PendingCutIn { caster = caster, skill = skill });
        pendingCutInCasters.Add(caster);
    }

    // Remove any queued cut-ins belonging to a specific unit.
    // NOTE: Use ReferenceEquals for the parameter check, because Unity overrides == for destroyed objects.
    void PurgeCutInsForUnit(BattleUnit unit)
    {
        if (object.ReferenceEquals(unit, null)) return;

        pendingCutInCasters.Remove(unit);
        // Clean up destroyed objects that compare as null.
        pendingCutInCasters.RemoveWhere(u => u == null);

        if (pendingCutIns == null || pendingCutIns.Count == 0) return;

        var newQ = new Queue<PendingCutIn>(pendingCutIns.Count);
        while (pendingCutIns.Count > 0)
        {
            var req = pendingCutIns.Dequeue();
            if (req == null) continue;
            if (req.caster == null) continue;
            if (req.caster == unit) continue;
            newQ.Enqueue(req);
        }
        pendingCutIns = newQ;
    }

    IEnumerator ProcessPendingCutIns(int token)
    {
        // Resolve queued cut-ins (clicked while busy) immediately after an action ends,
        // but BEFORE we advance to the next actor.
        AutoQueueCutInUltimates();

        // Safeguards: prevent turn freeze if a unit dies/destroys during cut-in scheduling,
        // or if cut-in enqueuing loops unexpectedly.
        float tStart = Time.realtimeSinceStartup;
        int processed = 0;

        while (pendingCutIns.Count > 0)
        {
            if (token != turnToken)
                yield break;

            // Hard limit by real time
            if (Time.realtimeSinceStartup - tStart > Mathf.Max(0.5f, cutInHardLimitSec))
            {
                Debug.LogWarning("[CUTIN] Hard limit reached -> clear pending cut-ins");
                pendingCutIns.Clear();
                pendingCutInCasters.Clear();
                yield break;
            }

            processed++;
            if (processed > Mathf.Max(1, cutInMaxPerWindow))
            {
                Debug.LogWarning("[CUTIN] Too many cut-ins in one window -> clear remaining");
                pendingCutIns.Clear();
                pendingCutInCasters.Clear();
                yield break;
            }

            var req = pendingCutIns.Dequeue();
            if (req == null) continue;

            if (req.caster != null) pendingCutInCasters.Remove(req.caster);
            pendingCutInCasters.RemoveWhere(u => u == null);

            if (req.caster == null || !IsAlive(req.caster)) continue;
            if (req.skill == null) continue;
            float reqCost = (req.skill != null && req.skill.isUltimate) ? Mathf.Max(0f, req.skill.energyCost) : req.caster.energyMax;
            if (!req.caster.HasEnoughEnergy(reqCost)) continue;

            // run cut-in, then resume the current actor (the action already ended)
            yield return CastSkillAsCutIn(req.caster, req.skill, token, resumeActor: currentActor);
        }
    }

    List<BattleUnit> GetAliveAlliesOf(BattleUnit caster)
    {
        var list = new List<BattleUnit>();
        if (caster == null) return list;

        RebuildAllUnits();
        for (int i = 0; i < allUnits.Count; i++)
        {
            var u = allUnits[i];
            if (u == null) continue;
            if (u.isPlayer != caster.isPlayer) continue;
            if (!IsAlive(u)) continue;
            list.Add(u);
        }
        return list;
    }

    List<BattleUnit> GetAliveOpponentsOf(BattleUnit caster)
    {
        var list = new List<BattleUnit>();
        if (caster == null) return list;

        RebuildAllUnits();
        for (int i = 0; i < allUnits.Count; i++)
        {
            var u = allUnits[i];
            if (u == null) continue;
            if (u.isPlayer == caster.isPlayer) continue;
            if (!IsAlive(u)) continue;
            list.Add(u);
        }
        return list;
    }

    List<BattleUnit> ResolveTargetsForSkill(BattleUnit caster, SkillData skill)
    {
        var targets = new List<BattleUnit>();
        if (caster == null || skill == null) return targets;

        List<BattleUnit> pool = (skill.targetSide == SkillData.TargetSide.Allies)
            ? GetAliveAlliesOf(caster)
            : GetAliveOpponentsOf(caster);

        if (pool.Count == 0) return targets;

        if (skill.targetScope == SkillData.TargetScope.All)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var t = pool[i];
                if (t == null) continue;
                if (!IsAlive(t)) continue;

                if (skill.targetSide == SkillData.TargetSide.Allies && !skill.includeSelfWhenAlliesAll && t == caster)
                    continue;

                targets.Add(t);
            }
            return targets;
        }

        // Single target
        BattleUnit picked = null;

        // Selection logic:
        // - Allies: always honor selectedAllyTarget when valid, so single-target ally skills can switch by clicking.
        // - Enemies: honor selectedEnemyTarget only when the skill explicitly requires manual selection.
        if (skill.targetSide == SkillData.TargetSide.Allies)
        {
            if (selectedAllyTarget != null && IsAlive(selectedAllyTarget) && selectedAllyTarget.isPlayer == caster.isPlayer)
                picked = selectedAllyTarget;
        }
        else if (skill.requireManualSelect)
        {
            if (selectedEnemyTarget != null && IsAlive(selectedEnemyTarget) && selectedEnemyTarget.isPlayer != caster.isPlayer)
                picked = selectedEnemyTarget;
        }

        if (picked == null)
        {
            switch (skill.defaultTargetRule)
            {
                case SkillData.DefaultTargetRule.Self:
                    // Defensive: a skill that targets enemies should never default to casting on self
                    // even if the data file is misconfigured.
                    if (skill.targetSide == SkillData.TargetSide.Enemies)
                    {
                        for (int i = 0; i < pool.Count; i++)
                        {
                            var t = pool[i];
                            if (t == null || !IsAlive(t)) continue;
                            picked = t;
                            break;
                        }
                    }
                    else
                    {
                        picked = caster;
                    }
                    break;

                case SkillData.DefaultTargetRule.LowestHpPercent:
                    float best = float.MaxValue;
                    for (int i = 0; i < pool.Count; i++)
                    {
                        var t = pool[i];
                        if (t == null || !IsAlive(t)) continue;
                        float p = (t.maxHp > 0) ? (t.hp / (float)t.maxHp) : 1f;
                        if (p < best)
                        {
                            best = p;
                            picked = t;
                        }
                    }
                    break;

                case SkillData.DefaultTargetRule.FirstAlive:
                default:
                    for (int i = 0; i < pool.Count; i++)
                    {
                        var t = pool[i];
                        if (t == null || !IsAlive(t)) continue;
                        picked = t;
                        break;
                    }
                    break;
            }
        }

        if (picked != null) targets.Add(picked);
        return targets;
    }

    IEnumerator CastSkillAsTurn(BattleUnit caster, SkillData skill, int token)
    {
        _queueManuallyReorderedThisAction = false;
        if (caster == null || skill == null || !IsAlive(caster))
        {
            // Defensive: avoid stalling the battle if skill/caster vanished.
            if (token == turnToken)
            {
                AdvanceToNextActor();
            }
            yield break;
        }

        if (state == TurnState.End) yield break;
        if (token != turnToken) yield break;

        HideTargetArrow();
        HideActiveAura();

        state = TurnState.Busy;
        busyTimer = 0f;
        busyOwnerToken = turnToken;

        // Resolve targets
        var targets = ResolveTargetsForSkill(caster, skill);

        // Lock the primary target for the animation-event spawned FX (AE_SpawnAttackFx).
        // Without this, SpawnAttackFxNow() will fall back to "first alive enemy/player",
        // causing FX to appear on the wrong unit (e.g., BigBoss instead of a summoned minion).
        BattleUnit primaryTargetForFx = (targets != null && targets.Count > 0) ? targets[0] : null;
        bool _setTargetLockThisAction = false;
        BattleUnit _prevTargetLock = null;
        bool _hadPrevTargetLock = lockedTargetForThisAction.TryGetValue(caster, out _prevTargetLock);

        if (skill.animType != SkillData.AnimType.None && primaryTargetForFx != null)
        {
            lockedTargetForThisAction[caster] = primaryTargetForFx;
            _setTargetLockThisAction = true;
        }

        // Spend energy if ultimate
        if (skill.isUltimate)
        {
            float cost = Mathf.Max(0f, skill.energyCost);
            if (cost <= 0f) caster.ConsumeAllEnergy();
            else caster.ConsumeEnergy(cost);
        }

        // Play animation
        // IMPORTANT: lock FX prefab for this action so the animation event uses the correct prefab.
        if (skill.animType == SkillData.AnimType.UseUltimateAnim)
        {
            if (caster != null) lockedFxPrefabForThisAction[caster] = caster.ultimateFxPrefab;
            caster.TriggerUltimate();
            yield return WaitUltimateFinish(caster);
            if (caster != null) lockedFxPrefabForThisAction.Remove(caster);
        }
        else if (skill.animType == SkillData.AnimType.UseAttackAnim)
        {
            if (caster != null) lockedFxPrefabForThisAction[caster] = caster.attackFxPrefab;
            caster.TriggerAttack();
            yield return WaitAttackFinish(caster);
            if (caster != null) lockedFxPrefabForThisAction.Remove(caster);
        }

        // Apply effects
        for (int ei = 0; ei < skill.effects.Count; ei++)
        {
            ApplyEffect(caster, skill, skill.effects[ei], targets);
        }

        RefreshUI();

        // Handle deaths / battle end
        yield return HandleDeathsAndBattleEnd(token);

        
        // Release the FX target lock after the action completes.
        if (_setTargetLockThisAction)
        {
            if (_hadPrevTargetLock && _prevTargetLock != null) lockedTargetForThisAction[caster] = _prevTargetLock;
            else lockedTargetForThisAction.Remove(caster);
        }

        if (token != turnToken) yield break;

        // Gain energy for non-ultimate actions
        if (!skill.isUltimate && caster != null && IsAlive(caster))
        {
            caster.AddEnergy(energyGainOnNormalAttack);
        }

        // After a normal skill action, we tick modifiers and allow queued cut-ins (same as other flows)
        TickAllModifiersOneTurn();
        yield return ProcessPendingCutIns(token);

        // Token may have been advanced by the watchdog while we were yielding (e.g., a stuck cut-in/death FX).
        // Never advance the turn order from a stale coroutine.
        if (token != turnToken)
        {
            Debug.LogWarning($"[STALE] CastSkillAsTurn post-cutin ignored token={token} turnToken={turnToken}");
            yield break;
        }

        queueIndex++;
        AdvanceToNextActor();
    }

    IEnumerator CastSkillAsCutIn(BattleUnit caster, SkillData skill, int token, BattleUnit resumeActor)
    {
        _queueManuallyReorderedThisAction = false;
        if (caster == null || skill == null || !IsAlive(caster))
        {
            // Defensive: if the caster/skill became invalid, just bail out.
            yield break;
        }
        if (token != turnToken) yield break;

        // Save
        var prevActor = currentActor;
        var prevState = state;

        // Temporarily set current actor to caster for aura/UI
        currentActor = caster;
        state = TurnState.Busy;
        busyTimer = 0f;
        busyOwnerToken = turnToken;

        HideTargetArrow();
        HideActiveAura();
        ShowActiveAuraOn(caster);
        RefreshUI();

        var targets = ResolveTargetsForSkill(caster, skill);

        // Lock the primary target for the animation-event spawned FX (AE_SpawnAttackFx).
        // Without this, SpawnAttackFxNow() will fall back to "first alive enemy/player",
        // causing FX to appear on the wrong unit (e.g., BigBoss instead of a summoned minion).
        BattleUnit primaryTargetForFx = (targets != null && targets.Count > 0) ? targets[0] : null;
        bool _setTargetLockThisAction = false;
        BattleUnit _prevTargetLock = null;
        bool _hadPrevTargetLock = lockedTargetForThisAction.TryGetValue(caster, out _prevTargetLock);

        if (skill.animType != SkillData.AnimType.None && primaryTargetForFx != null)
        {
            lockedTargetForThisAction[caster] = primaryTargetForFx;
            _setTargetLockThisAction = true;
        }

        // Spend energy
        float cost = Mathf.Max(0f, skill.energyCost);
        if (cost <= 0f) caster.ConsumeAllEnergy();
        else caster.ConsumeEnergy(cost);

        // Animation
        // Pre-lock FX prefab so the animation event spawns the correct FX (ultimate vs attack).
        if (skill.animType == SkillData.AnimType.UseUltimateAnim)
        {
            lockedFxPrefabForThisAction[caster] = caster.ultimateFxPrefab;
            caster.TriggerUltimate();
            yield return WaitUltimateFinish(caster);
            lockedFxPrefabForThisAction.Remove(caster);
        }
        else if (skill.animType == SkillData.AnimType.UseAttackAnim)
        {
            lockedFxPrefabForThisAction[caster] = caster.attackFxPrefab;
            caster.TriggerAttack();
            yield return WaitAttackFinish(caster);
            lockedFxPrefabForThisAction.Remove(caster);
        }

        // Effects
        for (int ei = 0; ei < skill.effects.Count; ei++)
        {
            ApplyEffect(caster, skill, skill.effects[ei], targets);
        }

        RefreshUI();
        yield return HandleDeathsAndBattleEnd(token);

        
        // Release the FX target lock after the action completes.
        if (_setTargetLockThisAction)
        {
            if (_hadPrevTargetLock && _prevTargetLock != null) lockedTargetForThisAction[caster] = _prevTargetLock;
            else lockedTargetForThisAction.Remove(caster);
        }

        if (token != turnToken) yield break;

        // Cut-in counts as an action tick
        TickAllModifiersOneTurn();

        // Restore actor and state (resume original current actor input)
        // Restore actor and state safely (avoid resuming input on dead/minion actors).

        BattleUnit restoreActor = null;

        if (resumeActor != null && IsAlive(resumeActor)) restoreActor = resumeActor;

        else if (prevActor != null && IsAlive(prevActor)) restoreActor = prevActor;


        currentActor = restoreActor;

        if (currentActor == null)
        {
            state = TurnState.Busy;
            AdvanceToNextActor();
            yield break;
        }


        if (prevState == TurnState.WaitingInput)

        {

            // Only return to input state if we have a valid human-controllable player actor.

            if (currentActor != null && currentActor.isPlayer && IsHumanControllablePlayer(currentActor))

            {

                state = TurnState.WaitingInput;

            }

            else

            {

                state = TurnState.Busy;

                AdvanceToNextActor();

                yield break;

            }

        }

        else

        {

            state = prevState;

        }
        HideActiveAura();
        if (currentActor != null) ShowActiveAuraOn(currentActor);
        RefreshUI();
    }

    void ApplyEffect(BattleUnit caster, SkillData skill, SkillEffect eff, List<BattleUnit> targets)
    {
        if (eff == null) return;

        switch (eff.type)
        {
            case SkillEffect.EffectType.Damage:
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (t == null || !IsAlive(t)) continue;

                    bool crit;
                    int dmg = ComputeEffectDamage(caster, t, eff, out crit);
                    t.TakeDamage(dmg);
                    SpawnDamagePopup(t, dmg, crit);
                }
                break;

            case SkillEffect.EffectType.Heal:
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (t == null || !IsAlive(t)) continue;

                    int heal = ComputeEffectHeal(caster, eff);
                    t.Heal(heal);
                    SpawnHealPopup(t, heal);
                }
                break;

            case SkillEffect.EffectType.BuffStat:
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (t == null || !IsAlive(t)) continue;

                    t.ApplyModifier(eff.stat, eff.flat, eff.percent, eff.durationTurns);
                    SpawnBuffPopup(t, eff.stat, eff.flat, eff.percent);
                }

                // If SPD changes mid-round, reorder upcoming turn order so the change is visible immediately.
                if (eff.stat == StatType.Spd && !_queueManuallyReorderedThisAction)
                {
                    ResortQueueKeepCurrentFirst();
                }
                break;

            case SkillEffect.EffectType.PullTurn:
                ApplyPullEffect(caster, eff, targets);
                break;

            case SkillEffect.EffectType.Summon:
                ApplySummonEffect(caster, eff);
                break;
        }
    }

    int ComputeEffectHeal(BattleUnit caster, SkillEffect eff)
    {
        int baseVal = Mathf.RoundToInt(eff.valueFlat + (caster != null ? caster.atk * eff.valueAtkRatio : 0f));
        return Mathf.Max(0, baseVal);
    }

    int ComputeEffectDamage(BattleUnit caster, BattleUnit target, SkillEffect eff, out bool crit)
    {
        crit = false;
        int baseVal = Mathf.RoundToInt(eff.valueFlat + (caster != null ? caster.atk * eff.valueAtkRatio : 0f));
        if (!eff.ignoreDef && target != null) baseVal = Mathf.Max(0, baseVal - target.def);

        if (eff.canCrit && caster != null && UnityEngine.Random.value < caster.cr)
        {
            crit = true;
            baseVal = Mathf.RoundToInt(baseVal * caster.cd);
        }

        return Mathf.Max(0, baseVal);
    }

    void ApplyPullEffect(BattleUnit caster, SkillEffect eff, List<BattleUnit> targets)
    {
        if (speedQueue == null || speedQueue.Count == 0) return;
        if (queueIndex < 0 || queueIndex >= speedQueue.Count) return;

        if (eff.pullMode == PullMode.TargetToActNext)
        {
            if (targets.Count > 0) PullUnitToActNext(targets[0]);
            return;
        }
        if (eff.pullMode == PullMode.AllTargetsToActNext)
        {
            PullUnitsToActNext(targets);
            return;
        }
        if (eff.pullMode == PullMode.AlliesAllToActNext)
        {
            PullAlliesToActNext(caster, includeCaster: eff.includeCasterInAlliesPull);
            return;
        }
        if (eff.pullMode == PullMode.SelfToActNext)
        {
            _queueManuallyReorderedThisAction = true;
        PullUnitToActNext(caster);
            return;
        }
    }

    void PullUnitToActNext(BattleUnit u)
    {
        if (u == null) return;
        if (!IsAlive(u)) return;

        int oldIndex = speedQueue.IndexOf(u);
        if (oldIndex < 0) return;
        if (oldIndex == queueIndex) return;

        speedQueue.RemoveAt(oldIndex);
        if (oldIndex < queueIndex) queueIndex--;

        int insertPos = Mathf.Min(queueIndex + 1, speedQueue.Count);
        speedQueue.Insert(insertPos, u);
        RefreshUI();
    }

    void PullUnitsToActNext(List<BattleUnit> units)
    {
        if (units == null || units.Count == 0) return;

        var ordered = new List<BattleUnit>();
        for (int i = 0; i < speedQueue.Count; i++)
        {
            var u = speedQueue[i];
            if (u == null) continue;
            if (units.Contains(u)) ordered.Add(u);
        }

        int insertPosBase = queueIndex + 1;
        int inserted = 0;

        for (int k = 0; k < ordered.Count; k++)
        {
            var u = ordered[k];
            if (u == null || !IsAlive(u)) continue;

            int oldIndex = speedQueue.IndexOf(u);
            if (oldIndex < 0) continue;
            if (oldIndex == queueIndex) continue;

            speedQueue.RemoveAt(oldIndex);
            if (oldIndex < queueIndex) queueIndex--;

            int insertPos = Mathf.Min(insertPosBase + inserted, speedQueue.Count);
            speedQueue.Insert(insertPos, u);
            inserted++;
        }

        RefreshUI();
    }

    void PullAlliesToActNext(BattleUnit caster, bool includeCaster)
    {
        if (caster == null) return;

        var allies = GetAliveAlliesOf(caster);
        if (!includeCaster) allies.Remove(caster);

        PullUnitsToActNext(allies);
    }

    void ApplySummonEffect(BattleUnit caster, SkillEffect eff)
    {
        if (eff.summonPrefab == null) return;

        // Summon logic is reserved for boss: only BigBoss can summon SmallBoss clones, and only once.
                if (caster == null || !caster.isBigBoss || caster.isSummonedClone) return;

        // Once per battle: big boss summon can only happen once.
        if (bigBossSummonUsedThisBattle) return;
        if (bigBossSummonUsed.Contains(caster)) return;

        bigBossSummonUsedThisBattle = true;
        bigBossSummonUsed.Add(caster);

        int n = Mathf.Max(1, eff.summonCount);

        // If using manager spawn points, try to spawn at all available points (capped).
        if (eff.spawnRule == SummonSpawnRule.UseManagerSpawnPoints)
        {
            int available = CountValidSpawnPoints();
            if (available > 0)
            {
                int desired = available;
                if (bigBossSummonMaxCount > 0) desired = Mathf.Min(desired, bigBossSummonMaxCount);
                n = Mathf.Max(n, desired);
            }
        }

        var spawnPositions = GetSummonPositions(caster, n, eff.spawnRule);
        spawnPositions = EnsureUniqueAndFreeSummonPositions(spawnPositions, n);

        for (int i = 0; i < n; i++)
        {
            Vector3 pos = (i < spawnPositions.Count) ? spawnPositions[i] : (caster != null ? caster.transform.position : Vector3.zero);
            GameObject go = Instantiate(eff.summonPrefab, pos, Quaternion.identity);
            if (go == null) continue;

            BattleUnit m = go.GetComponent<BattleUnit>();
            if (m == null)
            {
                Debug.LogWarning("[SUMMON] summonPrefab has no BattleUnit component, skip spawn.");
                Destroy(go);
                continue;
            }

            // BigBoss always summons enemy-side SmallBoss clones.
            m.isPlayer = false;
            m.isSmallBoss = true;
            m.isBigBoss = false;
            m.isSummonedClone = true;
            m.transformOnDeath = false;        // clone must NOT transform on death
            m.bigBossUseSummonOnce = false;    // safety: clone should never be treated as BigBoss
            m.destroyOnDeath = true;           // clone must disappear on death

            // Lock hit/popup anchor to summon position so impact point stays at the spawn location.
            m.LockHitAndPopupPointToWorld(pos, lockPopupPointToo: true);

            enemies.Add(m);

            if (eff.joinTurnQueue)
            {
                _queueManuallyReorderedThisAction = true;
                int insertPos = Mathf.Min(queueIndex + 1, speedQueue.Count);
                speedQueue.Insert(insertPos, m);
            }
        }

        RebuildAllUnits();
        RefreshUI();
    }

    
List<Vector3> GetSummonPositions(BattleUnit caster, int count, SummonSpawnRule rule)
{
    var list = new List<Vector3>();
    if (count <= 0) return list;

    if (rule == SummonSpawnRule.UseManagerSpawnPoints && minionSpawnPoints != null && minionSpawnPoints.Length > 0)
    {
        int n = minionSpawnPoints.Length;
        int idx = summonSpawnCursor;
        int guard = 0;

        while (list.Count < count && guard < n * 4)
        {
            int i = idx % n;
            idx++;
            guard++;

            if (minionSpawnPoints[i] == null) continue;
            list.Add(JitterSummonPos(minionSpawnPoints[i].position));
        }

        summonSpawnCursor = idx % n;

        if (list.Count > 0) return list;
        // 若全部为空则继续走兜底
    }

    // 兜底：围绕施法者生成（2D场景默认用XY平面）
    Vector3 basePos = caster != null ? caster.transform.position : Vector3.zero;
    float radius = 1.0f;

    for (int i = 0; i < count; i++)
    {
        float ang = (count <= 1) ? 0f : (i * (360f / count));
        float rad = ang * Mathf.Deg2Rad;
        Vector3 p = basePos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        list.Add(JitterSummonPos(p));
    }

    return list;
}


void SpawnBuffPopup(BattleUnit target, StatType stat, float flat, float percent)
{
    if (target == null) return;
    string content = FormatBuffPopupText(stat, flat, percent);
    if (string.IsNullOrEmpty(content)) return;

    EnqueuePopup(new PopupRequest
    {
        kind = PopupKind.Text,
        target = target,
        text = content
    });
}

string FormatBuffPopupText(StatType stat, float flat, float percent)
{
    string s = StatShortName(stat);

    string a = "";
    if (Mathf.Abs(flat) > 0.0001f)
    {
        a = (flat >= 0f ? "+" : "") + Mathf.RoundToInt(flat).ToString();
    }

    string b = "";
    if (Mathf.Abs(percent) > 0.0001f)
    {
        float p = percent * 100f;
        b = (p >= 0f ? "+" : "") + Mathf.RoundToInt(p).ToString() + "%";
    }

    if (a == "" && b == "") return "";

    if (a != "" && b != "") return s + a + " " + b;
    if (a != "") return s + a;
    return s + b;
}

string StatShortName(StatType stat)
{
    switch (stat)
    {
        case StatType.MaxHp: return "HP";
        case StatType.Atk: return "ATK";
        case StatType.Def: return "DEF";
        case StatType.Spd: return "SPD";
        case StatType.Cr: return "CR";
        case StatType.Cd: return "CD";
        default: return stat.ToString();
    }
}



    IEnumerator HandleDeathsAndBattleEnd(int token)
    {
        // Play deaths for any units that reached 0 HP during this action
        RebuildAllUnits();
        for (int i = allUnits.Count - 1; i >= 0; i--)
        {
            var u = allUnits[i];
            if (u == null) continue;
            if (u.IsDead())
            {
                // Player deaths must never block the action coroutine; otherwise the whole battle can
                // appear frozen until the watchdog forces a token advance.
                if (u.isPlayer)
                {
                    CleanupDeadTargetRefs(u);
                    PurgeCutInsForUnit(u);
                    StartCoroutine(PlayDeathAndRemove(u));
                }
                else
                {
                    yield return PlayDeathAndRemove(u);
                }
            }
        }

        if (AllPlayersDead())
        {
            yield return EndBattleAndReturn(false);
            yield break;
        }
        if (AllEnemiesDead())
        {
            yield return EndBattleAndReturn(true);
            yield break;
        }
    }

    void TickAllModifiersOneTurn()
    {
        RebuildAllUnits();
        for (int i = 0; i < allUnits.Count; i++)
        {
            var u = allUnits[i];
            if (u == null) continue;
            if (!IsAlive(u)) continue;
            u.TickModifiers();
        }

        // If we didn't explicitly reorder the queue via pull/summon this action,
        // re-sort the upcoming order so SPD buffs feel effective immediately.
        if (!_queueManuallyReorderedThisAction)
            ResortQueueKeepCurrentFirst();
        else
            RefreshUI();
    }

    List<BattleUnit> BuildDisplayQueue()
    {
        var list = new List<BattleUnit>();

        if (currentActor != null && IsAlive(currentActor))
            list.Add(currentActor);

        if (speedQueue == null || speedQueue.Count == 0)
            return list;

        int n = speedQueue.Count;
        int start = Mathf.Clamp(queueIndex, 0, n - 1);

        for (int k = 0; k < n; k++)
        {
            var u = speedQueue[(start + k) % n];
            if (u == null) continue;
            if (!IsAlive(u)) continue;
            if (u == currentActor) continue;
            list.Add(u);
        }
        return list;
    }


}