namespace Dokkaebi.Common
{
    /// <summary>
    /// Types of status effects that can be applied to units
    /// </summary>
    public enum StatusEffectType
    {
        None,
        AccuracyDebuff, // For Storm Surge
        Armor,          // For Stone Skin, Heatwave Counter
        AuraDelayed,    // For Thread Snap
        Blind,
        Burn,
        Charm,
        Confusion,
        CooldownLock,   // For Time Lock
        Curse,
        DamageBoost,
        DamageReduction,// For Temporal Shift, Weave Barrier
        DefenseBoost,
        Dodge,          // For Wind Barrier
        FateLinked,     // For Fate Link
        Frozen,
        Heal,
        Invulnerable,
        Movement,       // For Burning Stride, Storm Surge, Quake Zone
        MovementImmunity, // For Storm Surge push immunity
        Poison,
        ReactiveDamage, // For Heatwave Counter
        Root,
        Shield,
        Silence,
        Sleep,
        SpeedBoost,
        Stealth,
        Stun,          // Already exists, used for Time Warp
        Taunt,
        TemporalEcho,  // For Paradox Bolt's mark effect
        FracturedMoment, // For Chronomage Temporal Rift
        HeatwaveCounterReady, // For Heatwave Counter self-buff
        ForesightBuff,   // For Foresight status effect
        AbilityStatDebuff // For effects that modify ability stats like range or cooldown
    }
} 