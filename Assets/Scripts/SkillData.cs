using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Skill Data", fileName = "NewSkillData")]
public class SkillData : ScriptableObject
{
    public enum TargetSide
    {
        Enemies,
        Allies
    }

    public enum TargetScope
    {
        Single,
        All
    }

    public enum DefaultTargetRule
    {
        FirstAlive,
        LowestHpPercent,
        Self
    }

    public enum AnimType
    {
        None,
        UseAttackAnim,
        UseUltimateAnim
    }

    [Header("Basic")]
    public string displayName = "Skill";
    [TextArea(2, 4)]
    public string description;

    [Header("Flags")]
    public bool isUltimate = false;
    public bool requireManualSelect = true;

    // For Ally-All skills: whether to include self in the target list.
    public bool includeSelfWhenAlliesAll = true;

    // If true and energy is full, this ultimate will be automatically enqueued to cut in.
    public bool allowCutInWhenEnergyFull = true;

    [Header("Targeting")]
    public TargetSide targetSide = TargetSide.Enemies;
    public TargetScope targetScope = TargetScope.Single;
    public DefaultTargetRule defaultTargetRule = DefaultTargetRule.FirstAlive;

    [Header("Cost / Animation")]
    public float energyCost = 0f; // for non-ultimate skills; ultimates typically consume all energy in BattleManager
    public AnimType animType = AnimType.UseAttackAnim;

    [Header("Effects (executed in order)")]
    public List<SkillEffect> effects = new List<SkillEffect>();
}
