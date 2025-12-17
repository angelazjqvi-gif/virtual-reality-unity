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

    [Header("Damage Popup (TMP UI)")]
    public DamagePopup damagePopupPrefab;
    public Canvas popupCanvas;
    public Camera worldCamera;
    public Vector3 popupWorldOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Watchdog (Anti-freeze)")]
    public float busyTimeout = 5f;   
    private float busyTimer = 0f;

    [Header("Wait Safety - Ultimate")]
    public float maxWaitEnterUltimate = 0.5f;
    public float maxWaitUltimateTotal = 2.5f;

    [Header("Target Arrow")]
    public GameObject targetArrowPrefab;          
    public Vector3 targetArrowWorldOffset = new Vector3(0f, 1.2f, 0f);
    public bool allowClickToSelectEnemy = true;

    private GameObject targetArrowInstance = null;
    private BattleUnit arrowTarget = null;

    [Header("Active Player Aura")]
    public GameObject activePlayerAuraPrefab;        
    public Vector3 activePlayerAuraOffset = new Vector3(0f, -0.45f, 0f); 
    public bool auraFollowPlayer = true;

    private GameObject activePlayerAuraInstance = null;
    private BattleUnit auraOwner = null;


    private TurnState state = TurnState.WaitingInput;

    private BattleUnit selectedEnemyTarget = null;

    private readonly List<BattleUnit> allUnits = new List<BattleUnit>();
    private readonly List<BattleUnit> speedQueue = new List<BattleUnit>();
    private int queueIndex = 0;
    private BattleUnit currentActor = null;

    private readonly Dictionary<BattleUnit, BattleUnit> lockedTargetForThisAction = new Dictionary<BattleUnit, BattleUnit>();
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
        BindPlayerButtons();
        RebuildAllUnits();
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
        BuildSpeedQueueNewRound();
        AdvanceToNextActor();
    }

    void Update()
    {
        if (state == TurnState.Busy)
        {
            busyTimer += Time.deltaTime;
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

        TryMouseSelectEnemy();
        UpdateTargetArrowDuringPlayerInput();
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

    void BuildSpeedQueueNewRound()
    {
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
            return Random.Range(-1, 2);
        });

        queueIndex = 0;
        RefreshUI();
    }

    void RefreshUI()
    {
        if (uiOverlay != null)
            uiOverlay.Render(speedQueue, queueIndex, allUnits);

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
            bool canUltimate = isMyTurn && players[i].unit != null && players[i].unit.HasFullEnergy();

            if (players[i].ultimateButton != null)
                players[i].ultimateButton.interactable = canUltimate;

        }
    }

    public void SelectEnemyTarget(BattleUnit enemy)
    {
        if (enemy == null) return;
        if (!IsAlive(enemy)) return;
        if (enemy.isPlayer) return;

        selectedEnemyTarget = enemy;
        Debug.Log($"Selected Target => {enemy.name}");
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

    void UpdateTargetArrowDuringPlayerInput()
    {
        if (state != TurnState.WaitingInput || currentActor == null || !currentActor.isPlayer)
        {
            HideTargetArrow();
            return;
        }

        if (selectedEnemyTarget != null && !IsAlive(selectedEnemyTarget))
            selectedEnemyTarget = null;

        BattleUnit target =
            (IsAlive(selectedEnemyTarget) && !selectedEnemyTarget.isPlayer)
            ? selectedEnemyTarget
            : GetFirstAliveEnemy();

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

        if (!Input.GetMouseButtonDown(0)) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        Vector2 worldPos = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider == null) return;

        BattleUnit u = hit.collider.GetComponentInParent<BattleUnit>();
        if (u == null) return;

        if (u.isPlayer) return;
        if (!IsAlive(u)) return;

        SelectEnemyTarget(u); 
        ShowTargetArrowOn(u);
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

        if (queueIndex >= speedQueue.Count)
            BuildSpeedQueueNewRound();

        while (queueIndex < speedQueue.Count && !IsAlive(speedQueue[queueIndex]))
            queueIndex++;

        if (queueIndex >= speedQueue.Count)
        {
            BuildSpeedQueueNewRound();
            return;
        }

        currentActor = speedQueue[queueIndex];
        turnToken++;
        Debug.Log($"[TURN] idx={queueIndex}/{speedQueue.Count} next={currentActor.name} isPlayer={currentActor.isPlayer}");

        if (currentActor.isPlayer)
        {
            state = TurnState.WaitingInput;

            if (selectedEnemyTarget != null && !IsAlive(selectedEnemyTarget))
                selectedEnemyTarget = null;

            RefreshUI();
            return;
        }

        state = TurnState.Busy;
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

        StartCoroutine(PlayerAttackFlow(attacker, turnToken));
    }

    void OnClickUltimate(int playerIndex)
    {
        if (state != TurnState.WaitingInput) return;
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        BattleUnit attacker = players[playerIndex].unit;
        if (attacker == null || attacker != currentActor) return;

        StartCoroutine(PlayerUltimateFlow(attacker, turnToken));
    }

    IEnumerator PlayerAttackFlow(BattleUnit attacker, int token)
    {
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
            yield return PlayDeathAndRemove(target);
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

        queueIndex++;
        AdvanceToNextActor();
    }

    IEnumerator PlayerUltimateFlow(BattleUnit attacker, int token)
    {
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

        queueIndex++;
        AdvanceToNextActor();
    }


    IEnumerator EnemyAutoAttackFlow(BattleUnit enemy, int token)
    {
        yield return new WaitForSeconds(enemyDelayBeforeAttack);

        BattleUnit target = PickAlivePlayer();
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

        if (target.IsDead())
        {
            yield return PlayDeathAndRemove(target);
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

        queueIndex++;
        AdvanceToNextActor();
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


    void SpawnDamagePopup(BattleUnit target, int damage, bool crit)
    {
        if (damagePopupPrefab == null) { Debug.LogError("[POPUP] damagePopupPrefab = NULL"); return; }
        if (popupCanvas == null) { Debug.LogError("[POPUP] popupCanvas = NULL"); return; }
        if (target == null) { Debug.LogError("[POPUP] target = NULL"); return; }

        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) { Debug.LogError("[POPUP] worldCamera = NULL (MainCamera tag?)"); return; }

        // 世界->屏幕
        Vector3 worldPos = target.transform.position + popupWorldOffset;
        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
        Debug.Log($"[POPUP] screenPos={screenPos} dmg={damage} crit={crit}");
        if (screenPos.z < 0f) { Debug.LogError("[POPUP] behind camera"); return; }

        // 生成到Canvas
        DamagePopup popup = Instantiate(damagePopupPrefab, popupCanvas.transform);
        popup.transform.SetAsLastSibling(); // 保证在最上层

        // 屏幕->Canvas局部坐标（关键）
        RectTransform canvasRt = popupCanvas.GetComponent<RectTransform>();
        RectTransform popupRt = popup.GetComponent<RectTransform>();

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt,
            screenPos,
            popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera,
            out localPoint
        );
        popupRt.anchoredPosition = localPoint;

        popup.Setup(damage, crit); 
        Debug.Log("[POPUP] spawned OK");
    }

    void SpawnHealPopup(BattleUnit target, int heal)
    {
        if (damagePopupPrefab == null) { Debug.LogError("[POPUP] damagePopupPrefab = NULL"); return; }
        if (popupCanvas == null) { Debug.LogError("[POPUP] popupCanvas = NULL"); return; }
        if (target == null) { Debug.LogError("[POPUP] target = NULL"); return; }

        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) { Debug.LogError("[POPUP] worldCamera = NULL (MainCamera tag?)"); return; }

        Vector3 worldPos = target.transform.position + popupWorldOffset;
        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f) return;

        DamagePopup popup = Instantiate(damagePopupPrefab, popupCanvas.transform);
        popup.transform.SetAsLastSibling();

        RectTransform canvasRt = popupCanvas.GetComponent<RectTransform>();
        RectTransform popupRt = popup.GetComponent<RectTransform>();

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt,
            screenPos,
            popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera,
            out localPoint
        );
        popupRt.anchoredPosition = localPoint;

        popup.SetupHeal(heal);
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

        Transform hit = target.hitPoint != null ? target.hitPoint : target.transform;
        StartCoroutine(PlayFxAndWait(fxPrefab, hit));

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
        yield return new WaitForSeconds(0.15f);
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
            t += Time.deltaTime;
            yield return null;
        }

        if (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName))
            yield break;

        float t2 = 0f;
        while (unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName) && t2 < maxWaitAttackTotal)
        {
            t2 += Time.deltaTime;
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
            t += Time.deltaTime;
            yield return null;
        }

        if (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.ultimateStateName))
            yield break;

        float t2 = 0f;
        while (unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.ultimateStateName) && t2 < maxWaitUltimateTotal)
        {
            t2 += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator PlayFxAndWait(GameObject fxPrefab, Transform point)
    {
        if (fxPrefab == null || point == null)
        {
            yield return new WaitForSeconds(fxFallbackTime);
            yield break;
        }

        GameObject fx = Instantiate(fxPrefab, point.position, Quaternion.identity);

        var sr = fx.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 999;

        var anim = fx.GetComponent<Animator>();
        if (anim == null)
        {
            yield return new WaitForSeconds(fxFallbackTime);
            Destroy(fx);
            yield break;
        }

        yield return null;

        float t = 0f;
        while (anim != null && t < maxWaitFx)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (!st.loop && st.normalizedTime >= 1f) break;
            t += Time.deltaTime;
            yield return null;
        }

        Destroy(fx);
    }

    IEnumerator PlayDeathAndRemove(BattleUnit unit)
    {
        if (unit == null) yield break;

        if (unit.animator == null || string.IsNullOrEmpty(unit.deathStateName))
        {
            CleanupDeadTargetRefs(unit);
            unit.HideOrDestroy();
            yield break;
        }

        unit.TriggerDie();
        yield return null;

        // 等进入死亡状态（最多0.5秒）
        float enter = 0f;
        while (enter < 0.5f)
        {
            if (unit == null) yield break;
            var st = unit.animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(unit.deathStateName)) break;
            enter += Time.deltaTime;
            yield return null;
        }

        // 等死亡播完（最多 maxWaitDeath 秒），即使 loop/填错也会退出
        float t = 0f;
        while (t < maxWaitDeath)
        {
            if (unit == null) yield break;

            var st = unit.animator.GetCurrentAnimatorStateInfo(0);

            // 没在死亡状态就别卡着
            if (!st.IsName(unit.deathStateName))
                break;

            // 非循环播完就结束
            if (!st.loop && st.normalizedTime >= 1f)
                break;

            t += Time.deltaTime;
            yield return null;
        }

        CleanupDeadTargetRefs(unit);
        unit.HideOrDestroy();
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
}
