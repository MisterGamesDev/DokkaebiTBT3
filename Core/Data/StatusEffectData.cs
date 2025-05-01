using System;
using UnityEngine;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using System.Collections.Generic;
using Dokkaebi.Utilities;

namespace Dokkaebi.Core.Data
{
    /// <summary>
    /// Defines a status effect that can be applied to units
    /// </summary>
    [CreateAssetMenu(fileName = "New Status Effect", menuName = "Dokkaebi/Data/Status Effect")]
    public class StatusEffectData : ScriptableObject, IStatusEffect
    {
        public string effectId;
        public string effectName;
        public string displayName;
        public string description;
        public Sprite icon;
        
        public StatusEffectType effectType;
        public bool isStackable = false;
        public int maxStacks = 1;
        public bool isPermanent = false;
        public int duration;
        public int potency;
        
        // Visual effect prefab to show on affected unit
        public GameObject visualEffect;
        
        // Audio
        public AudioClip applySound;
        public AudioClip tickSound;
        public AudioClip removeSound;
        
        public Color effectColor = Color.white;
        
        // Stat modifiers
        [Header("Stat Modifiers")]
        [SerializeField]
        private Dictionary<UnitAttributeType, float> statModifiers = new Dictionary<UnitAttributeType, float>();

        [Header("Stat Modifiers (Specific)")]
        [Tooltip("Flat value added to Armor")]
        public int armorModifier = 0;
        [Tooltip("Flat value added to Movement Range")]
        public int movementRangeModifier = 0;
        [Tooltip("Multiplier applied to incoming damage (e.g., 0.5 for 50% reduction)")]
        public float damageReductionMultiplier = 1.0f;
        [Tooltip("Chance to dodge an attack (0.0 to 1.0)")]
        public float dodgeChance = 0f;
        [Tooltip("Modifier applied to Accuracy (e.g., -0.1 for -10%)")]
        public float accuracyModifier = 0f;
        [Tooltip("Flat damage reduction applied to incoming damage")]
        public int flatDamageReduction = 0;
        [Tooltip("Absorption amount for shield effects")]
        public int damageAbsorptionAmount = 0;
        [Tooltip("Flat value added to Critical Chance (e.g., 0.0 to 1.0)")]
        public float criticalChanceModifier = 0f;
        [Tooltip("Flat value added to Critical Damage (e.g., 0.0 to 1.0)")]
        public float criticalDamageModifier = 0f;
        [Tooltip("Bonus damage amount for effects like Temporal Echo")]
        public int bonusDamageAmount = 0;

        [Tooltip("Damage multiplier for the second hit of a Fractured Moment ability")]
        public float secondHitDamageMultiplier = 1.0f;

        [Header("Effect Behavior")]
        [Tooltip("Should this effect be removed after the unit takes a hit? (Used for dodge/shields)")]
        public bool removeOnHit = false;
        [Tooltip("Should this effect be removed after the unit deals a hit? (Future use?)")]
        public bool removeOnDealHit = false;
        [Tooltip("ID of another unit this effect is linked to (e.g., Fate Link)")]
        public int linkedUnitId = -1;
        [Tooltip("Should this effect be removed after the unit scores a critical hit?")]
        public bool removeOnCritHit = false;
        
        // Periodic Effects
        [Header("Periodic Effects")]
        public bool hasDamageOverTime = false;
        public int damageOverTimeAmount = 0;
        public DamageType damageOverTimeType = DamageType.Normal;

        public bool hasHealingOverTime = false;
        public int healingOverTimeAmount = 0;

        // Immediate Effects
        [Header("Immediate Effects")]
        public bool hasImmediateDamage = false;
        public int immediateDamageAmount = 0;
        public DamageType immediateDamageType = DamageType.Normal;

        public bool hasImmediateHealing = false;
        public int immediateHealingAmount = 0;
        
        // IStatusEffect implementation
        string IStatusEffect.Id => effectId;
        string IStatusEffect.DisplayName => displayName;
        StatusEffectType IStatusEffect.EffectType => effectType;
        int IStatusEffect.DefaultDuration => duration;
        int IStatusEffect.Potency => potency;
        bool IStatusEffect.IsPermanent => isPermanent;
        Color IStatusEffect.EffectColor => effectColor;
        
        /// <summary>
        /// Check if this effect modifies a specific stat
        /// </summary>
        public bool HasStatModifier(UnitAttributeType statType)
        {
            return statModifiers.ContainsKey(statType);
        }
        
        /// <summary>
        /// Get the modifier value for a specific stat
        /// </summary>
        public float GetStatModifier(UnitAttributeType statType)
        {
            // --- ADD LOG START ---
            SmartLogger.Log($"[StatusEffectData.GetStatModifier] UnitAttributeType requested: {statType} for effect: {displayName}", LogCategory.Unit);
            // --- ADD LOG END ---

            if (!statModifiers.TryGetValue(statType, out float value))
            {
                // --- ADD LOG START ---
                SmartLogger.Log($"[StatusEffectData.GetStatModifier] Stat type {statType} not found in statModifiers dictionary for effect {displayName}. Returning default 1.0f.", LogCategory.Unit);
                // --- ADD LOG END ---
                return 1.0f; // Default multiplier is 1.0 (no change)
            }

            // For dodge chance, return the raw value
            if (statType == UnitAttributeType.DodgeChance)
            {
                // --- ADD LOG START ---
                SmartLogger.Log($"[StatusEffectData.GetStatModifier] Handling DodgeChance for {displayName}. Returning raw value: {value}", LogCategory.Unit);
                // --- ADD LOG END ---
                return value;
            }
            // For other stats, return the multiplier
            // --- ADD LOG START ---
            SmartLogger.Log($"[StatusEffectData.GetStatModifier] Returning value {value} for stat type {statType} for effect {displayName}.", LogCategory.Unit);
            // --- ADD LOG END ---
            return value;
        }
        
        /// <summary>
        /// Set a stat modifier
        /// </summary>
        public void SetStatModifier(UnitAttributeType statType, float value)
        {
            // For dodge chance, store the raw value (0.0 to 1.0)
            if (statType == UnitAttributeType.DodgeChance)
            {
                statModifiers[statType] = value;
            }
            // For other stats, store as multiplier (1.0 + modifier)
            else
            {
                statModifiers[statType] = value;
            }
        }
        
        protected virtual void OnValidate()
        {
            // Auto-generate effectId if empty
            if (string.IsNullOrEmpty(effectId))
            {
                effectId = name;
            }
            
            // Auto-generate displayName if empty
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = effectName;
            }

            // Define Atemporal Echoed status effect properties if this is the Atemporal Echoed effect
            if (effectId == "AtemporalEchoed")
            {
                displayName = "Atemporal Echoed";
                description = "Target takes bonus damage and caster's next ability cooldown is reduced when hit again. Removed on hit.";
                effectType = StatusEffectType.TemporalEcho;
                isStackable = false;
                maxStacks = 1;
                isPermanent = false;
                duration = 1;
                potency = 0;
                removeOnHit = true;
                effectColor = Color.blue;
                bonusDamageAmount = 2;
                hasDamageOverTime = false;
                damageOverTimeAmount = 0;
                hasHealingOverTime = false;
                healingOverTimeAmount = 0;
                hasImmediateDamage = false;
                immediateDamageAmount = 0;
                hasImmediateHealing = false;
                immediateHealingAmount = 0;
                linkedUnitId = -1;
                removeOnDealHit = false;
                removeOnCritHit = false;
            }
            // Add a check for the Chronomage's Temporal Echo if it's defined in the same OnValidate
            else if (effectId == "TemporalEcho")
            {
                displayName = "Temporal Echo (Chronomage)";
                description = "Target is marked for bonus damage from Paradox Bolt.";
                effectType = StatusEffectType.TemporalEcho;
                isStackable = false;
                maxStacks = 1;
                isPermanent = false;
                duration = 1;
                potency = 0;
                removeOnHit = true;
                effectColor = Color.blue;
                bonusDamageAmount = 2;
            }
            // Define Temporal Echo status effect properties if this is the Temporal Echo effect
            else if (effectId == "TemporalEcho")
            {
                // Ensure basic properties are consistent for Temporal Echo
                displayName = "Temporal Echo";
                description = "Target takes bonus damage and caster's next ability cooldown is reduced when hit again. Removed on hit.";
                effectType = StatusEffectType.TemporalEcho;
                isStackable = false;
                maxStacks = 1;
                isPermanent = false;
                duration = 1; // Lasts for 1 turn
                potency = 0; // Potency not directly used for this effect's main purpose
                removeOnHit = true; // Removed when the target is hit again
                effectColor = Color.blue; // Example color

                // Set the bonus damage amount for Temporal Echo
                // Assume bonusDamageAmount field is already declared in the class
                bonusDamageAmount = 2;

                // Ensure other relevant fields are default or explicitly set if needed
                hasDamageOverTime = false;
                damageOverTimeAmount = 0;
                hasHealingOverTime = false;
                healingOverTimeAmount = 0;
                hasImmediateDamage = false;
                immediateDamageAmount = 0;
                hasImmediateHealing = false;
                immediateHealingAmount = 0;
                linkedUnitId = -1; // Default
                removeOnDealHit = false; // Default
                removeOnCritHit = false; // Default
                // statModifiers.Clear(); // Uncomment if you want to clear stat modifiers for Temporal Echo
            }
            // Destiny Shifted status effect configuration
            else if (effectId == "DestinyShifted")
            {
                displayName = "Destiny Shifted";
                description = "Ability range reduced for 1 turn.";
                effectType = StatusEffectType.AbilityStatDebuff;
                isStackable = false;
                maxStacks = 1;
                isPermanent = false;
                duration = 1;
                potency = 0;
                removeOnHit = false;
                effectColor = new Color(0.6f, 0.2f, 0.8f, 1f); // Example: purple
            }
        }
    }

    // --- Probability Field Crit Buff Effect Data ---

    [CreateAssetMenu(fileName = "ProbabilityFieldCritBuffEffect", menuName = "Dokkaebi/Data/Status Effect/Probability Field Crit Buff")]
    public class ProbabilityFieldCritBuffEffectData : Dokkaebi.Core.Data.StatusEffectData
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            if (string.IsNullOrEmpty(effectId))
            {
                effectId = "ProbabilityFieldCritBuff"; // Unique ID for this specific effect
            }
            displayName = "Probability Field Buff";
            description = "Increased critical chance and damage from Probability Field.";
            effectType = StatusEffectType.DamageBoost; // Or create a new StatusEffectType if needed
            isStackable = false;
            maxStacks = 1;
            isPermanent = false;
            duration = 1; // Applied each turn the unit is in the zone
            potency = 0; // Potency not directly used here
            removeOnHit = false;
            removeOnDealHit = false;
            removeOnCritHit = false;
            
            // Configure the modifiers
            criticalChanceModifier = 0.5f; // 50% increase to critical chance (additive)
            criticalDamageModifier = 0.5f; // 50% increase to critical damage (additive)

            // Ensure other relevant fields are default or explicitly set if needed
            hasDamageOverTime = false;
            damageOverTimeAmount = 0;
            hasHealingOverTime = false;
            healingOverTimeAmount = 0;
            hasImmediateDamage = false;
            immediateDamageAmount = 0;
            hasImmediateHealing = false;
            immediateHealingAmount = 0;
            linkedUnitId = -1;
            // ... set other fields to default or appropriate values
        }
    }
}