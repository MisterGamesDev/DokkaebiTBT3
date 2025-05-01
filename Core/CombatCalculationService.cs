using UnityEngine;
using Dokkaebi.Core.Data;
using Dokkaebi.Units;
using Dokkaebi.Common;
using Dokkaebi.Utilities;
using System.Linq;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Static service class that handles combat-related calculations
    /// </summary>
    public static class CombatCalculationService
    {
        /// <summary>
        /// Calculate the final damage amount for an ability
        /// </summary>
        /// <param name="ability">The ability being used</param>
        /// <param name="source">The unit using the ability</param>
        /// <param name="target">The unit being targeted</param>
        /// <param name="isOverload">Whether this is an overload cast</param>
        /// <param name="isCriticalHit">Whether this is a critical hit</param>
        /// <param name="damageMultiplier">The damage multiplier to apply</param>
        /// <returns>The final calculated damage amount</returns>
        public static int CalculateFinalDamage(AbilityData ability, DokkaebiUnit source, DokkaebiUnit target, bool isOverload, bool isCriticalHit, float damageMultiplier = 1.0f)
        {
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage START] Ability: {ability?.displayName}, Source: {source?.GetUnitName()}, Target: {target?.GetUnitName()}, Overload: {isOverload}", LogCategory.Game);

            if (ability == null || source == null || target == null)
            {
                SmartLogger.LogError("[CombatCalculationService.CalculateFinalDamage] Invalid parameters provided", LogCategory.Game);
                return 0;
            }

            // Log initial state
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Initial base damage: {ability.damageAmount}", LogCategory.Game);
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Target status effects: {string.Join(", ", target.GetStatusEffects().Select(e => $"{e.StatusEffectType}"))}", LogCategory.Game);

            float finalDamage = ability.damageAmount;
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Starting damage calculation with base damage: {finalDamage}", LogCategory.Game);
            
            // Check accuracy first
            float accuracyModifier = StatusEffectSystem.GetStatModifier(source, UnitAttributeType.Accuracy);
            float baseHitChance = 0.95f; // 95% base hit chance
            float finalHitChance = baseHitChance * accuracyModifier;
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Source {source.GetUnitName()} accuracy modifier: {accuracyModifier:P0}, Final hit chance: {finalHitChance:P0}", LogCategory.Game);

            if (Random.value > finalHitChance)
            {
                SmartLogger.Log($"{source.GetUnitName()} missed the attack! (Hit chance: {finalHitChance:P0})", LogCategory.Game);
                return 0;
            }
            
            // Check for dodge
            float dodgeChance = StatusEffectSystem.GetStatModifier(target, UnitAttributeType.DodgeChance);
            dodgeChance = Mathf.Clamp01(dodgeChance); // Clamps the value between 0 and 1
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Target {target.GetUnitName()} dodge chance: {dodgeChance:P0}", LogCategory.Game);

            if (dodgeChance > 0f && Random.value < dodgeChance)
            {
                SmartLogger.Log($"{target.GetUnitName()} dodged the attack! (Dodge chance: {dodgeChance:P0})", LogCategory.Game);
                return 0;
            }

            // Apply overload multiplier if applicable
            if (isOverload && ability.hasOverloadVariant)
            {
                int preOverloadDamage = (int)finalDamage;
                finalDamage = Mathf.RoundToInt(finalDamage * ability.overloadDamageMultiplier);
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Applied overload multiplier: {ability.overloadDamageMultiplier:F2} to damage: {preOverloadDamage} → {finalDamage}", LogCategory.Game);
            }

            // Apply armor reduction (Stone Skin)
            float armorBonus = StatusEffectSystem.GetStatModifier(target, UnitAttributeType.Armor);
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Raw armor modifier from status effects: {armorBonus:F2}x (Base: 1.0x)", LogCategory.Game);
            
            if (armorBonus > 1.0f) // Only apply if there's a bonus (base is 1.0)
            {
                int preArmorDamage = (int)finalDamage;
                float armorBonusPercent = armorBonus - 1.0f; // Convert to bonus percentage (e.g., 2.0 -> 100% bonus)
                float damageReductionPercent = armorBonusPercent / 2.0f; // 100% armor = 50% reduction
                int armorReduction = Mathf.RoundToInt(finalDamage * damageReductionPercent);
                finalDamage = Mathf.Max(1, finalDamage - armorReduction); // Ensure minimum 1 damage
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Armor calculation details:", LogCategory.Game);
                SmartLogger.Log($"  - Base damage: {preArmorDamage}", LogCategory.Game);
                SmartLogger.Log($"  - Armor bonus: +{armorBonusPercent:P0} ({armorBonus:F2}x)", LogCategory.Game);
                SmartLogger.Log($"  - Damage reduction: {damageReductionPercent:P0}", LogCategory.Game);
                SmartLogger.Log($"  - Damage reduced by: {armorReduction}", LogCategory.Game);
                SmartLogger.Log($"  - Final damage: {finalDamage} (Min: 1)", LogCategory.Game);
            }
            else
            {
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] No armor reduction applied (modifier <= 1.0)", LogCategory.Game);
            }

            // Apply percentage damage reduction (Temporal Shift)
            float damageReduction = StatusEffectSystem.GetStatModifier(target, UnitAttributeType.DamageTaken);
            if (damageReduction != 1.0f)
            {
                int preDamageReduction = (int)finalDamage;
                finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * damageReduction));
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Applied % damage reduction: {damageReduction:P0}: {preDamageReduction} → {finalDamage}", LogCategory.Game);

                // Remove Temporal Shift if it's configured to be removed on hit
                var temporalEffect = target.GetStatusEffects().FirstOrDefault(e => e.StatusEffectType == StatusEffectType.DamageReduction);
                if (temporalEffect != null && (temporalEffect.Effect as StatusEffectData)?.removeOnHit == true)
                {
                    StatusEffectSystem.RemoveStatusEffect(target, StatusEffectType.DamageReduction);
                    SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Removed Temporal Shift effect from {target.GetUnitName()} after damage reduction", LogCategory.Game);
                }
            }

            // Apply flat damage reduction (Weave Barrier)
            var weaveEffect = target.GetStatusEffects().FirstOrDefault(e => e.StatusEffectType == StatusEffectType.DamageReduction);
            if (weaveEffect != null)
            {
                var weaveData = weaveEffect.Effect as StatusEffectData;
                if (weaveData != null && weaveData.flatDamageReduction > 0)
                {
                    int preWeaveBarrier = (int)finalDamage;
                    int flatReduction = weaveData.flatDamageReduction;
                    finalDamage = Mathf.Max(0, finalDamage - flatReduction);
                    SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Applied Weave Barrier flat reduction: {flatReduction}: {preWeaveBarrier} → {finalDamage}", LogCategory.Game);

                    // Remove Weave Barrier if configured to be removed on hit
                    if (weaveData.removeOnHit)
                    {
                        StatusEffectSystem.RemoveStatusEffect(target, StatusEffectType.DamageReduction);
                        SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Removed Weave Barrier effect from {target.GetUnitName()} after damage reduction", LogCategory.Game);
                    }
                }
            }

            // Apply damage absorption (Water Shield)
            var shieldEffect = target.GetStatusEffects().FirstOrDefault(e => e.StatusEffectType == StatusEffectType.Shield);
            if (shieldEffect != null)
            {
                var shieldData = shieldEffect.Effect as StatusEffectData;
                if (shieldData != null && shieldData.damageAbsorptionAmount > 0)
                {
                    SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Shield effect '{shieldData.displayName}' detected with {shieldData.damageAbsorptionAmount} absorption potential", LogCategory.Game);
                    
                    int preShieldDamage = (int)finalDamage;
                    int absorption = shieldData.damageAbsorptionAmount;
                    finalDamage = Mathf.Max(0, finalDamage - absorption);
                    int absorbed = preShieldDamage - (int)finalDamage;
                    
                    SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Shield absorption calculation:", LogCategory.Game);
                    SmartLogger.Log($"  - Incoming damage: {preShieldDamage}", LogCategory.Game);
                    SmartLogger.Log($"  - Shield absorption: {absorption}", LogCategory.Game);
                    SmartLogger.Log($"  - Damage absorbed: {absorbed}", LogCategory.Game);
                    SmartLogger.Log($"  - Remaining damage: {finalDamage}", LogCategory.Game);

                    // Remove Water Shield if configured to be removed on hit
                    if (shieldData.removeOnHit && absorbed > 0)
                    {
                        SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Removing shield '{shieldData.displayName}' after absorbing {absorbed} damage", LogCategory.Game);
                        StatusEffectSystem.RemoveStatusEffect(target, StatusEffectType.Shield);
                    }
                }
            }

            // Log the initial ability damage amount
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Initial base ability damage: {ability.damageAmount}", LogCategory.Game);
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Target status effects: {string.Join(", ", target.GetStatusEffects().Select(e => $"{e.StatusEffectType}"))}", LogCategory.Game);
            float finalDamageBeforeCrit = finalDamage;
            
            // Get source unit's critical chance
            float criticalChance = StatusEffectSystem.GetStatModifier(source, UnitAttributeType.CriticalChance);
            // --- ADD LOGGING BEFORE CRIT ROLL ---
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Crit Chance Check - Source: {source.GetUnitName()}, Calculated Critical Chance: {criticalChance:P2}", LogCategory.Game);
            // --- END LOGGING BEFORE CRIT ROLL ---
            float randomValue = UnityEngine.Random.value;
            bool isCriticalHitResult = randomValue <= criticalChance;
            // --- ADD LOGGING AFTER CRIT ROLL ---
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Critical Hit Roll: {randomValue:F2} vs Chance {criticalChance:F2}. Critical Hit Result: {isCriticalHitResult}", LogCategory.Game);
            // --- END LOGGING AFTER CRIT ROLL ---
            // --- CRITICAL DAMAGE CALCULATION LOGIC ---
            if (isCriticalHitResult)
            {
                // Get the base critical damage multiplier from the source unit's definition
                float baseCriticalDamageMultiplier = 1.0f; // Default to 1.0 if definition is missing
                if (source.GetUnitDefinitionData() != null)
                {
                    baseCriticalDamageMultiplier = source.GetUnitDefinitionData().baseCriticalDamageMultiplier;
                }
                else
                {
                    SmartLogger.LogWarning($"[CombatCalculationService.CalculateFinalDamage] Source unit '{source.GetUnitName()}' is missing UnitDefinitionData. Cannot get base critical damage multiplier. Defaulting to 1.0f.", LogCategory.Game);
                }
                // Get the source unit's critical damage modifier from status effects
                float criticalDamageModifierFromEffects = StatusEffectSystem.GetStatModifier(source, UnitAttributeType.CriticalDamage);
                // The total multiplier is base multiplier + additive modifier from effects
                float totalCriticalDamageMultiplier = baseCriticalDamageMultiplier + criticalDamageModifierFromEffects;
                // --- ADD LOGGING BEFORE CRIT DAMAGE MULTIPLY ---
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] CRITICAL HIT! Base Crit Multiplier: {baseCriticalDamageMultiplier:F2}, Effect Modifier: {criticalDamageModifierFromEffects:F2}, Total Multiplier: {totalCriticalDamageMultiplier:F2}", LogCategory.Game);
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Damage BEFORE Critical Multiplication: {finalDamage}", LogCategory.Game);
                // --- END LOGGING BEFORE CRIT DAMAGE MULTIPLY ---
                int preCritDamage = (int)finalDamage;
                finalDamage = Mathf.RoundToInt(finalDamage * totalCriticalDamageMultiplier);
                // --- ADD LOGGING AFTER CRIT DAMAGE MULTIPLY ---
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Damage AFTER Critical Multiplication: {finalDamage}", LogCategory.Game);
                // --- END LOGGING AFTER CRIT DAMAGE MULTIPLY ---
            }
            else
            {
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] No Critical Hit occurred. Damage remains: {finalDamage}", LogCategory.Game);
            }
            // --- END CRITICAL DAMAGE CALCULATION LOGIC ---

            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage END] Final damage: {finalDamage}", LogCategory.Game);

            // Remove effects with removeOnCritHit if this was a critical hit
            if (isCriticalHit && source != null)
            {
                var statusEffects = source.GetStatusEffects();
                if (statusEffects != null)
                {
                    foreach (var effect in statusEffects.ToList())
                    {
                        var effectData = effect.Effect as Dokkaebi.Core.Data.StatusEffectData;
                        if (effectData != null && effectData.removeOnCritHit)
                        {
                            StatusEffectSystem.RemoveStatusEffect(source, effect.StatusEffectType);
                        }
                    }
                }
            }

            // Apply the damage multiplier (used for second hits from Fractured Moment)
            finalDamage = Mathf.RoundToInt(finalDamage * damageMultiplier);
            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Applied damage multiplier ({damageMultiplier:F2}). Final damage: {finalDamage}", LogCategory.Game);

            // --- ParadoxBolt + TemporalEcho/AtemporalEchoed bonus logic (Moved AFTER all damage calculations) ---
            // FIX: Add condition to check if actual damage was dealt (i.e., not a miss or fully dodged/absorbed)
            if ((ability.abilityId == "ParadoxBolt" || ability.abilityId == "AtemporalEcho") && finalDamage > 0)
            {
                // Find the Temporal Echo or Atemporal Echoed status effect instance on the target
                var echoEffectInstance = target.GetStatusEffects().FirstOrDefault(e =>
                    (e.StatusEffectType == StatusEffectType.TemporalEcho) && e is Dokkaebi.Units.StatusEffectInstance
                ) as Dokkaebi.Units.StatusEffectInstance;

                if (echoEffectInstance != null && echoEffectInstance.Effect != null)
                {
                    SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Hit on Echoed target detected ({finalDamage} > 0) by {ability.displayName}. Target has {echoEffectInstance.Effect.displayName}.", LogCategory.Game);

                    // Distinguish between Chronomage's Temporal Echo and Fatebinder's Atemporal Echoed by effectId
                    if (ability.abilityId == "ParadoxBolt" && echoEffectInstance.Effect.effectId == "TemporalEcho")
                    {
                        // Chronomage: Apply bonus damage and remove effect
                        int preBonusDamage = (int)finalDamage;
                        int bonusDamage = echoEffectInstance.Effect.bonusDamageAmount;
                        finalDamage += bonusDamage;
                        SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Added Paradox Bolt bonus damage: {bonusDamage}. Damage changed from {preBonusDamage} → {finalDamage}", LogCategory.Game);
                        StatusEffectSystem.RemoveStatusEffect(target, StatusEffectType.TemporalEcho);
                        SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Removed Temporal Echo effect from {target.GetUnitName()}", LogCategory.Game);
                    }
                    else if (ability.abilityId == "AtemporalEcho" && echoEffectInstance.Effect.effectId == "AtemporalEchoed")
                    {
                        // Fatebinder: Reset ALL cooldowns for the caster
                        if (source is DokkaebiUnit sourceDokkaebiUnit)
                        {
                            sourceDokkaebiUnit.ResetAllCooldowns(); // Call the new method to reset all cooldowns
                            SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Reset ALL cooldowns for caster '{sourceDokkaebiUnit.GetUnitName()}'.", LogCategory.Game);
                        }
                        else
                        {
                            SmartLogger.LogWarning("[CombatCalculationService.CalculateFinalDamage] Source unit is not a DokkaebiUnit. Cannot reset all cooldowns.", LogCategory.Game);
                        }
                        StatusEffectSystem.RemoveStatusEffect(target, StatusEffectType.TemporalEcho);
                        SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Removed Atemporal Echoed effect from {target.GetUnitName()}", LogCategory.Game);
                    }
                }
                else if (ability.abilityId == "ParadoxBolt" || ability.abilityId == "AtemporalEcho")
                {
                    SmartLogger.Log($"[CombatCalculationService.CalculateFinalDamage] Paradox Bolt or Atemporal Echo hit target, but target does not have {StatusEffectType.TemporalEcho} effect or effect data is invalid.", LogCategory.Game);
                }
            }
            // --- End ParadoxBolt + Temporal Echo/Atemporal Echoed bonus logic ---

            return (int)finalDamage;
        }

        /// <summary>
        /// Calculate the final healing amount for an ability
        /// </summary>
        /// <param name="ability">The ability being used</param>
        /// <param name="source">The unit using the ability</param>
        /// <param name="target">The unit being targeted</param>
        /// <param name="isOverload">Whether this is an overload cast</param>
        /// <returns>The final calculated healing amount</returns>
        public static int CalculateFinalHealing(AbilityData ability, DokkaebiUnit source, DokkaebiUnit target, bool isOverload)
        {
            if (ability == null || source == null || target == null)
            {
                SmartLogger.LogError("[CombatCalculationService.CalculateFinalHealing] Invalid parameters provided", LogCategory.Game);
                return 0;
            }

            int baseHealing = ability.healAmount;
            
            // Apply overload multiplier if applicable
            if (isOverload && ability.hasOverloadVariant)
            {
                baseHealing = Mathf.RoundToInt(baseHealing * ability.overloadHealMultiplier);
                SmartLogger.Log($"[CombatCalculationService.CalculateFinalHealing] Applied overload multiplier: {ability.overloadHealMultiplier} to base healing: {ability.healAmount}, result: {baseHealing}", LogCategory.Game);
            }

            return baseHealing;
        }
    }
} 