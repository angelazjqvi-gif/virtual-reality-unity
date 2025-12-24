using UnityEngine;

// Shared enums for the data-driven battle system.
public enum StatType
{
    MaxHp,
    Atk,
    Def,
    Spd,
    Cr,
    Cd
}

public enum PullMode
{
    // Alias-friendly names (keep both old and new spellings to avoid breaking existing assets)
    TargetToActNext = 0,
    TargetsToActNext = 0,

    AllTargetsToActNext = 1,

    AlliesAllToActNext = 2,

    SelfToActNext = 3,
    SelfToActNextImmediate = 3
}

public enum SummonSpawnRule
{
    UseManagerSpawnPoints,
    AroundCaster
}
