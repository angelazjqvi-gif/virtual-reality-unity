using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    public enum TurnState { WaitingInput, Busy, End }

    [System.Serializable]
    public class PlayerSlot
    {
        public BattleUnit unit;
        public Button attackButton;
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
    public float busyTimeout = 5f;   // ✅ Busy 超时自动推进，彻底杜绝卡死
    private float busyTimer = 0f;

    private TurnState state = TurnState.WaitingInput;

    private BattleUnit selectedEnemyTarget = null;

    private readonly List<BattleUnit> allUnits = new List<BattleUnit>();
    private readonly List<BattleUnit> speedQueue = new List<BattleUnit>();
    private int queueIndex = 0;
    private BattleUnit currentActor = null;

    // 当前行动者锁定的目标（动画事件/特效用）
    private readonly Dictionary<BattleUnit, BattleUnit> lockedTargetForThisAction = new Dictionary<BattleUnit, BattleUnit>();

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
        // ✅ Busy 看门狗：任何原因导致 Busy 不退出，都强制推进
        if (state == TurnState.Busy)
        {
            busyTimer += Time.deltaTime;
            if (busyTimer > busyTimeout)
            {
                Debug.LogWarning("[WATCHDOG] Busy timeout -> force next turn");
                busyTimer = 0f;

                // 强制推进：跳过当前卡住的动作
                queueIndex++;
                AdvanceToNextActor();
            }
        }
        else
        {
            busyTimer = 0f;
        }
    }

    void BindPlayerButtons()
    {
        for (int i = 0; i < players.Count; i++)
        {
            int idx = i;
            if (players[i].attackButton == null) continue;

            players[i].attackButton.onClick.RemoveAllListeners();
            players[i].attackButton.onClick.AddListener(() => OnClickAttack(idx));
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

        // SPD 降序；SPD 相同则随机打散一点
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

        // 新一轮
        if (queueIndex >= speedQueue.Count)
            BuildSpeedQueueNewRound();

        // 跳过已死
        while (queueIndex < speedQueue.Count && !IsAlive(speedQueue[queueIndex]))
            queueIndex++;

        if (queueIndex >= speedQueue.Count)
        {
            BuildSpeedQueueNewRound();
            return;
        }

        currentActor = speedQueue[queueIndex];
        Debug.Log($"[TURN] idx={queueIndex}/{speedQueue.Count} next={currentActor.name} isPlayer={currentActor.isPlayer}");

        // ✅ 核心修复：轮到玩家时强制回 WaitingInput（杜绝 boss 死后 Busy 残留）
        if (currentActor.isPlayer)
        {
            state = TurnState.WaitingInput;

            // 选中的敌人如果死了，清掉
            if (selectedEnemyTarget != null && !IsAlive(selectedEnemyTarget))
                selectedEnemyTarget = null;

            RefreshUI();
            return;
        }

        // 敌人自动行动
        state = TurnState.Busy;
        RefreshUI();
        StartCoroutine(EnemyAutoAttackFlow(currentActor));
    }

    void OnClickAttack(int playerIndex)
    {
        if (state != TurnState.WaitingInput) return;
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        BattleUnit attacker = players[playerIndex].unit;
        if (attacker == null || attacker != currentActor) return;

        StartCoroutine(PlayerAttackFlow(attacker));
    }

    IEnumerator PlayerAttackFlow(BattleUnit attacker)
    {
        state = TurnState.Busy;
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

        attacker.TriggerAttack();
        yield return WaitAttackFinish(attacker);

        var r = ComputeDamage(attacker, target);
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

    IEnumerator EnemyAutoAttackFlow(BattleUnit enemy)
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



    // Animation Event 调用：攻击某一帧生成特效
    public void SpawnAttackFxNow(BattleUnit attacker)
    {
        if (attacker == null) return; 

        if (!lockedTargetForThisAction.TryGetValue(attacker, out var target) || target == null || !IsAlive(target))
        {
            target = attacker.isPlayer ? GetFirstAliveEnemy() : PickAlivePlayer();
        }
        if (target == null) return;

        Transform hit = target.hitPoint != null ? target.hitPoint : target.transform;
        StartCoroutine(PlayFxAndWait(attacker.attackFxPrefab, hit));
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

            // 1) 回写每个玩家的当前血量（战斗扣血 -> 主世界同步用）
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].unit != null)
                    GameSession.I.CaptureFromBattleUnit(players[i].unit, i);
            }

            // 2) 记录胜负
            if (playerWin) GameSession.I.EndBattle_PlayerWin();
            else GameSession.I.EndBattle_PlayerLose();

            // 3) 经验/升级：默认“全队都加经验”
            if (playerWin)
            {
                int expGain = 10 + enemies.Count * 5;
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

        // 进入攻击状态
        float t = 0f;
        while (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName) && t < maxWaitEnterAttack)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName))
            yield break;

        // 等攻击状态结束（兜底超时）
        float t2 = 0f;
        while (unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName) && t2 < maxWaitAttackTotal)
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

    // ✅ 关键：死亡流程永不卡死（boss loop / stateName 不匹配 / Animator 异常都兜底）
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
    }
}
