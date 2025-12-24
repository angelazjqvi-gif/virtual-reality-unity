using System;
using UnityEngine;

// Skill effects are plain serializable data objects referenced by SkillData.
// Keep fields explicit and editor-friendly (no hidden magic).
[Serializable]
public class SkillEffect
{
    public enum EffectType
    {
        Damage,
        Heal,
        BuffStat,
        PullTurn,
        Summon
    }

    [Header("Core")]
    public EffectType type = EffectType.Damage;

    // -------------------------
    // Damage / Heal
    // -------------------------
    [Header("Damage / Heal")]
    public int valueFlat = 0;
    public float valueAtkRatio = 0f;
    public bool canCrit = true;
    public bool ignoreDef = false;

    // -------------------------
    // Buff (temporary modifier)
    // -------------------------
    [Header("Buff Stat")]
    public StatType stat = StatType.Atk;
    public float flat = 0f;
    public float percent = 0f;           // 0.5 = +50%
    public int durationTurns = 2;

    // -------------------------
    // Pull (turn order manipulation)
    // -------------------------
    [Header("Pull Turn")]
    public PullMode pullMode = PullMode.TargetToActNext;
    public bool includeCasterInAlliesPull = false;

    // -------------------------
    // Summon
    // -------------------------
    [Header("Summon")]
    public GameObject summonPrefab;
    public int summonCount = 1;
    public SummonSpawnRule spawnRule = SummonSpawnRule.UseManagerSpawnPoints;

    // If true, summoned units are inserted right after the current actor.
    public bool joinTurnQueue = true;
}
