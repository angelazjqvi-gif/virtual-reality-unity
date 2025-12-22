using UnityEngine;
using System;
using System.Collections.Generic;

public class GameSession : MonoBehaviour
{
    public static GameSession I;

    [Header("Battle Link")]
    public int currentEnemyId = -1;   
    public bool playerWon = false;
    public bool playerLost = false;

    [Header("Battle Reward From World")]
    public int expPerEnemy = 5;


    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BeginBattle(int enemyId)
    {
        currentEnemyId = enemyId;
        playerWon = false;
        playerLost = false;
        Debug.Log("[GameSession] BeginBattle enemyId=" + enemyId);
    }

    public void EndBattle_PlayerWin()
    {
        playerWon = true;
        playerLost = false;
        Debug.Log("[GameSession] EndBattle_PlayerWin enemyId=" + currentEnemyId);
    }

    public void EndBattle_PlayerLose()
    {
        playerWon = false;
        playerLost = true;
        Debug.Log("[GameSession] EndBattle_PlayerLose enemyId=" + currentEnemyId);
    }

    public void ClearBattleLink()
    {
        Debug.Log("[GameSession] ClearBattleLink");
        currentEnemyId = -1;
        playerWon = false;
        playerLost = false;
    }

    [Serializable]
    public class PlayerData
    {
        [Header("Identity")]
        public string playerName = "Player";

        [Header("Level/Exp")]
        public int level = 1;
        public int exp = 0;

        [Header("Base Stats (level up grows these)")]
        public int baseMaxHp = 30;
        public int baseAtk = 10;
        public int baseDef = 3;
        public int baseSpd = 10;

        [Header("Crit")]
        public float baseCr = 0.10f;
        public float baseCd = 1.50f;

        [Header("Runtime")]
        public int currentHp = 30;
        public bool inited = false;
    }

    [Header("Party (Multi Players)")]
    public List<PlayerData> party = new List<PlayerData>();

    [Header("Active Player Index ")]
    public int activePlayerIndex = 0;

    public Action<int> OnActivePlayerChanged;

    [Header("World Transfer")]
    public bool worldTransferPending = false;
    public string targetWorldScene = "";
    public string targetSpawnId = "";
    public bool transferMoveWholeParty = true;

    public void BeginWorldTransfer(string toScene, string spawnId, bool moveWholeParty = true)
    {
        worldTransferPending = true;
        targetWorldScene = toScene;
        targetSpawnId = spawnId;
        transferMoveWholeParty = moveWholeParty;

        Debug.Log($"[GameSession] BeginWorldTransfer to={toScene}, spawn={spawnId}, allParty={moveWholeParty}");
    }

    public void ClearWorldTransfer()
    {
        worldTransferPending = false;
        targetWorldScene = "";
        targetSpawnId = "";
        transferMoveWholeParty = true;
    }


    public void EnsurePartySize(int n)
    {
        if (n < 1) n = 1;
        if (party == null) party = new List<PlayerData>();
        while (party.Count < n)
        {
            var d = new PlayerData();
            d.playerName = "Player" + party.Count;
            party.Add(d);
        }
        if (activePlayerIndex < 0) activePlayerIndex = 0;
        if (activePlayerIndex >= party.Count) activePlayerIndex = party.Count - 1;
    }

    public void SetActivePlayerIndex(int idx)
    {
        EnsurePartySize(1);
        idx = Mathf.Clamp(idx, 0, party.Count - 1);
        if (activePlayerIndex == idx) return;
        activePlayerIndex = idx;
        OnActivePlayerChanged?.Invoke(activePlayerIndex);
    }

    public PlayerData GetPlayerDataByIndex(int idx)
    {
        EnsurePartySize(1);
        idx = Mathf.Clamp(idx, 0, party.Count - 1);
        return party[idx];
    }

    public PlayerData GetActivePlayerData()
    {
        return GetPlayerDataByIndex(activePlayerIndex);
    }

    // ========= Level/Exp =========
    public int ExpToNextLevel(int lv)
    {
        return 20 + lv * 10;
    }

    public void GrantWinExp(int idx, int expGain)
    {
        var pd = GetPlayerDataByIndex(idx);
        if (expGain < 0) expGain = 0;

        pd.exp += expGain;
        while (pd.exp >= ExpToNextLevel(pd.level))
        {
            pd.exp -= ExpToNextLevel(pd.level);
            pd.level += 1;
            ApplyLevelGains(pd);
            Debug.Log($"[GameSession] LevelUp idx={idx} => Lv.{pd.level}");
        }
    }

    void ApplyLevelGains(PlayerData pd)
    {
        pd.baseMaxHp += 5;
        pd.baseAtk += 2;
        pd.baseDef += 1;
        if (pd.level % 2 == 0) pd.baseSpd += 1;

        pd.baseCr = Mathf.Clamp01(pd.baseCr + 0.01f);
        pd.baseCd = Mathf.Clamp(pd.baseCd + 0.05f, 1f, 3f);

        pd.currentHp = Mathf.Clamp(pd.currentHp, 0, pd.baseMaxHp);
    }

    // ========= Sync with BattleUnit =========
    public void ApplyToBattleUnit(BattleUnit u, int idx)
    {
        if (u == null) return;

        var pd = GetPlayerDataByIndex(idx);

        if (!pd.inited)
        {
            pd.inited = true;

            pd.baseMaxHp = SafeGetInt(u, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" }, pd.baseMaxHp);
            pd.currentHp = SafeGetInt(u, new string[] { "hp", "HP", "curHp", "currentHp", "CurrentHp" }, pd.currentHp);

            pd.baseAtk = u.atk;
            pd.baseDef = u.def;
            pd.baseSpd = u.spd;
            pd.baseCr = u.cr;
            pd.baseCd = u.cd;

            if (pd.baseMaxHp <= 0) pd.baseMaxHp = 30;
            if (pd.currentHp <= 0) pd.currentHp = pd.baseMaxHp;
        }

        SafeSetInt(u, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" }, pd.baseMaxHp);
        SafeSetInt(u, new string[] { "hp", "HP", "curHp", "currentHp", "CurrentHp" }, Mathf.Clamp(pd.currentHp, 0, pd.baseMaxHp));

        u.atk = pd.baseAtk;
        u.def = pd.baseDef;
        u.spd = pd.baseSpd;
        u.cr = pd.baseCr;
        u.cd = pd.baseCd;
    }

    public void CaptureFromBattleUnit(BattleUnit u, int idx)
    {
        if (u == null) return;
        var pd = GetPlayerDataByIndex(idx);

        int hp = SafeGetInt(u, new string[] { "hp", "HP", "curHp", "currentHp", "CurrentHp" }, pd.currentHp);
        pd.currentHp = Mathf.Clamp(hp, 0, pd.baseMaxHp);
    }

    // ========= Sync to World Player GameObject (reflection) =========
    public void ApplyToWorldPlayer(GameObject playerObj, int idx)
    {
        if (playerObj == null) return;
        var pd = GetPlayerDataByIndex(idx);

        var behaviours = playerObj.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            var b = behaviours[i];
            if (b == null) continue;

            SafeSetInt(b, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" }, pd.baseMaxHp);
            SafeSetInt(b, new string[] { "hp", "HP", "curHp", "currentHp", "CurrentHp" }, Mathf.Clamp(pd.currentHp, 0, pd.baseMaxHp));

            SafeSetInt(b, new string[] { "atk", "Atk", "attack", "Attack" }, pd.baseAtk);
            SafeSetInt(b, new string[] { "def", "Def", "defense", "Defense" }, pd.baseDef);
            SafeSetInt(b, new string[] { "spd", "Spd", "speed", "Speed" }, pd.baseSpd);

            SafeSetFloat(b, new string[] { "cr", "CR", "crit", "Crit", "critRate", "CritRate" }, pd.baseCr);
            SafeSetFloat(b, new string[] { "cd", "CD", "critDamage", "CritDamage" }, pd.baseCd);
        }
    }

    // ========= Reflection helpers (ONLY ADD) =========
    int SafeGetInt(object obj, string[] names, int fallback)
    {
        if (obj == null) return fallback;
        var t = obj.GetType();
        for (int i = 0; i < names.Length; i++)
        {
            var f = t.GetField(names[i]);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);

            var p = t.GetProperty(names[i]);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead) return (int)p.GetValue(obj);
        }
        return fallback;
    }

    void SafeSetInt(object obj, string[] names, int value)
    {
        if (obj == null) return;
        var t = obj.GetType();
        for (int i = 0; i < names.Length; i++)
        {
            var f = t.GetField(names[i]);
            if (f != null && f.FieldType == typeof(int)) { f.SetValue(obj, value); return; }

            var p = t.GetProperty(names[i]);
            if (p != null && p.PropertyType == typeof(int) && p.CanWrite) { p.SetValue(obj, value); return; }
        }
    }

    void SafeSetFloat(object obj, string[] names, float value)
    {
        if (obj == null) return;
        var t = obj.GetType();
        for (int i = 0; i < names.Length; i++)
        {
            var f = t.GetField(names[i]);
            if (f != null && f.FieldType == typeof(float)) { f.SetValue(obj, value); return; }

            var p = t.GetProperty(names[i]);
            if (p != null && p.PropertyType == typeof(float) && p.CanWrite) { p.SetValue(obj, value); return; }
        }
    }
}
