using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;
using Dokkaebi.Units;
using Dokkaebi.Utilities;
using Dokkaebi.Common;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Static system responsible for managing and updating status effects on units.
    /// </summary>
    public static class StatusEffectSystem
    {
        // Add a static constructor here
        static StatusEffectSystem()
        {
            // Add a log at the very beginning of the static constructor
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] --- ENTER StatusEffectSystem Static Constructor: {Time.frameCount} ---");

            // Add logging for any static field initializations if you have them
            // Example:
            // UnityEngine.Debug.LogError($"[DEBUG_FREEZE] StatusEffectSystem Static Constructor: Initializing static field X.");
            // public static int staticFieldX = InitializeStaticFieldX();

            // Add a log at the very end of the static constructor
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] --- EXIT StatusEffectSystem Static Constructor: {Time.frameCount} ---");
        }

        /// <summary>
        /// Apply a status effect to a unit
        /// </summary>
        public static void ApplyStatusEffect(IDokkaebiUnit targetUnit, StatusEffectData effectData, int duration = -1, IDokkaebiUnit sourceUnit = null, int? linkedUnitId = null)
        {
            SmartLogger.Log($"[StatusEffectSystem.ApplyStatusEffect ENTRY] Effect: {effectData?.displayName ?? "NULL"}, Target: {targetUnit?.DisplayName ?? "NULL"}", LogCategory.Ability, null);
            // --- This should be the very first line ---
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] --- ENTER ApplyStatusEffect: {Time.frameCount} ---");
            // --- ADDED GRANULAR LOGS ---
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Line 1: Checking targetUnit and effectData for null. targetUnit null: {targetUnit == null}, effectData null: {effectData == null}.");
            if (targetUnit == null || effectData == null)
            {
                UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Line 2: targetUnit or effectData is null. Aborting. targetUnit null: {targetUnit == null}, effectData null: {effectData == null}.");
                return; // Abort if null
            }

            // Add log just before creating the new StatusEffectInstance
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Line 3a: About to create new StatusEffectInstance for effect '{effectData.displayName}'.");
            var newEffect = new StatusEffectInstance(
                effectData,
                duration >= 0 ? duration : effectData.duration,
                sourceUnit?.UnitId ?? -1
            );
            // Add log just after creating the new StatusEffectInstance
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Line 3b: New StatusEffectInstance created. Duration: {newEffect.RemainingDuration}, Source ID: {newEffect.SourceUnitId}.");

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Line 4: New StatusEffectInstance created. Duration: {newEffect.RemainingDuration}, Source ID: {newEffect.SourceUnitId}.");
            if (linkedUnitId.HasValue)
            {
                UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Line 5: linkedUnitId has value {linkedUnitId.Value}. Setting on new effect.");
                newEffect.linkedUnitId = linkedUnitId.Value;
                UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Line 6: linkedUnitId set on new effect instance: {newEffect.linkedUnitId}.");
            }
            // --- END ADDED ---

            // ENTRY LOG
            SmartLogger.Log($"[ApplyStatusEffect ENTRY] Effect: {effectData?.displayName ?? "<null>"}, Target: {targetUnit?.DisplayName ?? "<null>"}, Source: {sourceUnit?.DisplayName ?? "<null>"}, Duration: {duration}\nStackTrace:\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}", LogCategory.Ability);

            // DEBUG LOG: Trace BurningStrideMovementBuff applications
            string effectName = effectData?.name ?? "<null>";
            string targetName = targetUnit?.DisplayName ?? targetUnit?.GetType().Name ?? "<null>";
            string sourceName = sourceUnit?.DisplayName ?? sourceUnit?.GetType().Name ?? "<null>";
            string stack = UnityEngine.StackTraceUtility.ExtractStackTrace();
            if (effectData?.effectType == StatusEffectType.Movement)
            {
                SmartLogger.Log($"[DEBUG][ApplyStatusEffect] Movement Effect: Applying '{effectName}' to '{targetName}' from '{sourceName}'.\nStackTrace:\n{stack}", LogCategory.Ability);
            }

            SmartLogger.Log($"[StatusEffectSystem.ApplyStatusEffect] BEGIN: Applying '{effectData?.displayName ?? "<null>"}' to '{targetUnit?.DisplayName ?? "<null>"}' for duration {duration}", LogCategory.Ability);
            if (targetUnit == null || effectData == null)
            {
                SmartLogger.LogWarning("Cannot apply null status effect or apply to null unit", LogCategory.General);
                return;
            }

            // Get all existing effects of the same type
            var existingEffects = targetUnit.GetStatusEffects()?
                .Where(e => e.StatusEffectType == effectData.effectType)
                .ToList() ?? new List<IStatusEffectInstance>();
            
            // 1. Log before the isStackable check
            if (effectData?.effectType == StatusEffectType.Movement)
            {
                SmartLogger.Log($"[DEBUG][StatusEffectSystem] Effect {effectData.displayName} (Type: {effectData.effectType}) - isStackable: {effectData.isStackable}, existingEffects count: {existingEffects.Count}", LogCategory.Ability);
            }

            // Handle stacking logic
            if (effectData.isStackable)
            {
                SmartLogger.Log($"Effect '{effectData.displayName}' is stackable.", LogCategory.Ability, null);
                // 2. Log inside the stacking logic block
                if (effectData?.effectType == StatusEffectType.Movement)
                {
                    SmartLogger.Log($"[DEBUG][StatusEffectSystem] Effect {effectData.displayName} (Type: {effectData.effectType}) - Entered stacking logic block.", LogCategory.Ability);
                }
                // Check if we've reached max stacks
                if (existingEffects.Count >= effectData.maxStacks)
                {
                    // Find the oldest effect to potentially refresh
                    var oldestEffect = existingEffects
                        .OrderBy(e => e.RemainingDuration)
                        .FirstOrDefault();
                        
                    if (oldestEffect != null)
                    {
                        // Refresh the duration of the oldest stack
                        oldestEffect.RemainingDuration = duration >= 0 ? duration : effectData.duration;
                        SmartLogger.Log($"Refreshed duration of oldest stack for effect '{effectData.displayName}' on {targetUnit.DisplayName}.", LogCategory.Ability, null);
                        SmartLogger.Log($"Max stacks reached for {effectData.displayName} on {targetUnit.DisplayName}, refreshed oldest stack duration", LogCategory.General);
                    }
                }
                else
                {
                    // ADDING LOG (stackable)
                    SmartLogger.Log($"[ApplyStatusEffect ADDING] Effect: {effectData?.displayName ?? "<null>"}, Target: {targetUnit?.DisplayName ?? "<null>"}\nStackTrace:\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}", LogCategory.Ability);
                    SmartLogger.Log($"About to call AddStatusEffect for stackable effect '{effectData.displayName}' on {targetUnit.DisplayName}.", LogCategory.Ability, null);
                    // Add log just before calling targetUnit.AddStatusEffect
                    UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Stacking block. About to call targetUnit.AddStatusEffect for unit '{targetUnit?.DisplayName ?? "NULL"}' for effect '{newEffect?.StatusEffectType ?? StatusEffectType.None}'.");
                    // Add new stack
                    targetUnit.AddStatusEffect(newEffect);
                    SmartLogger.Log($"Returned from AddStatusEffect for stackable effect '{effectData.displayName}' on {targetUnit.DisplayName}.", LogCategory.Ability, null);
                    // Add log just after calling targetUnit.AddStatusEffect
                    UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Stacking block. Returned from targetUnit.AddStatusEffect for unit '{targetUnit?.DisplayName ?? "NULL"}'.");

                    if (targetUnit is DokkaebiUnit concreteUnit)
                    {
                        // Add log just before calling RaiseStatusEffectApplied
                        UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Stacking block. About to call RaiseStatusEffectApplied for unit '{concreteUnit.DisplayName}' for effect '{newEffect?.StatusEffectType ?? StatusEffectType.None}'.");
                        concreteUnit.RaiseStatusEffectApplied(newEffect);
                        // Add log just after calling RaiseStatusEffectApplied
                        UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Stacking block. Returned from RaiseStatusEffectApplied for unit '{concreteUnit.DisplayName}'.");
                    }
                    SmartLogger.Log($"Added new stack of {effectData.displayName} to {targetUnit.DisplayName} ({existingEffects.Count + 1}/{effectData.maxStacks} stacks)", LogCategory.General);
                }
            }
            else
            {
                SmartLogger.Log($"Effect '{effectData.displayName}' is NOT stackable.", LogCategory.Ability, null);
                // 3. Log inside the non-stackable logic block
                if (effectData?.effectType == StatusEffectType.Movement)
                {
                    SmartLogger.Log($"[DEBUG][StatusEffectSystem] Effect {effectData.displayName} (Type: {effectData.effectType}) - Entered non-stackable logic block.", LogCategory.Ability);
                }
                // For non-stackable effects, refresh or apply
                var existingEffect = existingEffects.FirstOrDefault();
                if (effectData?.effectType == StatusEffectType.Movement)
                {
                    SmartLogger.Log($"[DEBUG][StatusEffectSystem] Effect {effectData.displayName} (Type: {effectData.effectType}) - existingEffect found: {existingEffect != null}", LogCategory.Ability);
                }
                if (existingEffect != null)
                {
                    // Refresh duration of existing effect
                    existingEffect.RemainingDuration = duration >= 0 ? duration : effectData.duration;
                    SmartLogger.Log($"Refreshed duration of non-stackable effect '{effectData.displayName}' on {targetUnit.DisplayName}.", LogCategory.Ability, null);
                    SmartLogger.Log($"Refreshed duration of {effectData.displayName} on {targetUnit.DisplayName}", LogCategory.General);
                }
                else
                {
                    // ADDING LOG (non-stackable)
                    SmartLogger.Log($"[ApplyStatusEffect ADDING] Effect: {effectData?.displayName ?? "<null>"}, Target: {targetUnit?.DisplayName ?? "<null>"}\nStackTrace:\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}", LogCategory.Ability);
                    SmartLogger.Log($"About to call AddStatusEffect for non-stackable effect '{effectData.displayName}' on {targetUnit.DisplayName}.", LogCategory.Ability, null);
                    // Add log just before calling targetUnit.AddStatusEffect
                    UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Non-stacking block. About to call targetUnit.AddStatusEffect for unit '{targetUnit?.DisplayName ?? "NULL"}' for effect '{newEffect?.StatusEffectType ?? StatusEffectType.None}'.");
                    // Apply new effect
                    targetUnit.AddStatusEffect(newEffect);
                    SmartLogger.Log($"Returned from AddStatusEffect for non-stackable effect '{effectData.displayName}' on {targetUnit.DisplayName}.", LogCategory.Ability, null);
                    // Add log just after calling targetUnit.AddStatusEffect
                    UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Non-stacking block. Returned from targetUnit.AddStatusEffect for unit '{targetUnit?.DisplayName ?? "NULL"}'.");

                    if (targetUnit is DokkaebiUnit concreteUnit)
                    {
                        // Add log just before calling RaiseStatusEffectApplied
                        UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Non-stacking block. About to call RaiseStatusEffectApplied for unit '{concreteUnit.DisplayName}' for effect '{newEffect?.StatusEffectType ?? StatusEffectType.None}'.");
                        concreteUnit.RaiseStatusEffectApplied(newEffect);
                        // Add log just after calling RaiseStatusEffectApplied
                        UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Non-stacking block. Returned from RaiseStatusEffectApplied for unit '{concreteUnit.DisplayName}'.");
                    }
                    SmartLogger.Log($"Applied new effect {effectData.displayName} to {targetUnit.DisplayName}", LogCategory.General);
                }
            }

            // Add log just before calling ApplyEffectImmediateImpact
            SmartLogger.Log($"About to call ApplyEffectImmediateImpact for effect '{effectData.displayName}' on {targetUnit.DisplayName}.", LogCategory.Ability, null);
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - About to call ApplyEffectImmediateImpact for unit '{targetUnit?.DisplayName ?? "NULL"}' for effect '{effectData?.displayName ?? "NULL"}'.");
            // Apply immediate effects if any
            ApplyEffectImmediateImpact(targetUnit, effectData);
            SmartLogger.Log($"Returned from ApplyEffectImmediateImpact for effect '{effectData.displayName}' on {targetUnit.DisplayName}.", LogCategory.Ability, null);
            // Add log just after calling ApplyEffectImmediateImpact
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] ApplyStatusEffect - Returned from ApplyEffectImmediateImpact for unit '{targetUnit?.DisplayName ?? "NULL"}'.");

            // Add at the very end of the method
            SmartLogger.Log($"[StatusEffectSystem.ApplyStatusEffect EXIT] Effect: {effectData?.displayName ?? "NULL"}, Target: {targetUnit?.DisplayName ?? "NULL"}", LogCategory.Ability, null);
        }

        /// <summary>
        /// Remove a specific status effect from a unit
        /// </summary>
        public static void RemoveStatusEffect(IDokkaebiUnit targetUnit, StatusEffectType effectType)
        {
            if (targetUnit == null) return;

            var effectToRemove = targetUnit.GetStatusEffects()?.FirstOrDefault(e => e.StatusEffectType == effectType);
            if (effectToRemove != null)
            {
                targetUnit.RemoveStatusEffect(effectToRemove);
                if (targetUnit is DokkaebiUnit concreteUnit)
                {
                    concreteUnit.RaiseStatusEffectRemoved(effectToRemove);
                }
            }
        }

        /// <summary>
        /// Process status effects at the end of a unit's turn
        /// </summary>
        public static void ProcessTurnEndForUnit(IDokkaebiUnit unit)
        {
            // --- ADD LOG START ---
            SmartLogger.Log($"[StatusEffectSystem.ProcessTurnEndForUnit] BEGIN: Processing turn end for unit: {unit?.DisplayName ?? "<null unit>"}. Total effects to process: {unit?.GetStatusEffects()?.Count ?? 0}", LogCategory.Unit);
            // --- ADD LOG END ---

            if (unit == null)
            {
                SmartLogger.LogWarning("[StatusEffectSystem.ProcessTurnEndForUnit] Attempted to process turn end for null unit", LogCategory.Unit);
                return;
            }

            var effects = unit.GetStatusEffects();
            if (effects == null)
            {
                SmartLogger.LogWarning("[StatusEffectSystem.ProcessTurnEndForUnit] Unit has null status effects list", LogCategory.Unit);
                return;
            }

            SmartLogger.Log($"[StatusEffectSystem.ProcessTurnEndForUnit] Processing turn end for unit: {unit.DisplayName}. Total effects: {effects.Count}", LogCategory.Unit);
            
            // Create a copy of the list to avoid modification during iteration
            var effectsToProcess = effects.ToList();
            
            foreach (var effect in effectsToProcess)
            {
                if (effect == null)
                {
                    SmartLogger.LogWarning("[StatusEffectSystem.ProcessTurnEndForUnit] Encountered null effect in effects list", LogCategory.Unit);
                    continue;
                }

                // --- ADD LOG START ---
                SmartLogger.Log($"[StatusEffectSystem.ProcessTurnEndForUnit] Processing effect: {effect.StatusEffectType} (Instance ID: {effect.GetHashCode()}) on unit: {unit.DisplayName}. Current Remaining Duration: {effect.RemainingDuration}, Is Permanent: {effect.Effect?.isPermanent ?? false}", LogCategory.Unit);
                // --- ADD LOG END ---

                if (effect is StatusEffectInstance instance && instance.Effect != null)
                {
                    // Apply turn-end impacts
                    ApplyEffectTurnEndImpact(unit, instance.Effect);
                    SmartLogger.Log($"[StatusEffectSystem.ProcessTurnEndForUnit] Applied turn-end impact for effect: {effect.StatusEffectType}", LogCategory.Unit);
                    
                    // Reduce duration if not permanent
                    if (!instance.Effect.isPermanent)
                    {
                        // --- ADD LOG START ---
                        SmartLogger.Log($"[StatusEffectSystem.ProcessTurnEndForUnit] Decrementing duration for effect: {effect.StatusEffectType}. Before decrement: {instance.RemainingDuration}", LogCategory.Unit);
                        // --- ADD LOG END ---
                        instance.RemainingDuration--;
                        // --- ADD LOG START ---
                        SmartLogger.Log($"[StatusEffectSystem.ProcessTurnEndForUnit] Decremented duration for effect: {effect.StatusEffectType}. After decrement: {instance.RemainingDuration}", LogCategory.Unit);
                        // --- ADD LOG END ---
                        
                        // Remove if expired
                        if (instance.RemainingDuration <= 0)
                        {
                            // --- ADD LOG START ---
                            SmartLogger.Log($"[StatusEffectSystem.ProcessTurnEndForUnit] Effect {effect.StatusEffectType} duration is <= 0 ({instance.RemainingDuration}). About to remove effect from unit {unit.DisplayName}.", LogCategory.Unit);
                            // --- ADD LOG END ---
                            unit.RemoveStatusEffect(effect);
                        }
                    }
                }
            }
            
            var remainingEffects = unit.GetStatusEffects();
            SmartLogger.Log($"[StatusEffectSystem.ProcessTurnEndForUnit] Completed turn end processing for unit: {unit.DisplayName}. Remaining effects: {remainingEffects.Count}", LogCategory.Unit);
        }

        /// <summary>
        /// Remove all status effects from a unit.
        /// </summary>
        public static void ClearAllStatusEffects(IDokkaebiUnit target)
        {
            if (target == null) return;

            var unitEffects = target.GetStatusEffects()?.ToList();
            if (unitEffects == null) return;

            // Remove impacts of all effects
            foreach (var effect in unitEffects)
            {
                if (effect is StatusEffectInstance instance)
                {
                    RemoveEffectImpacts(target, instance.Effect);
                }
            }

            if (target is DokkaebiUnit dokkaebiUnit)
            {
                foreach (var effect in unitEffects)
                {
                    dokkaebiUnit.RaiseStatusEffectRemoved(effect);
                }
                dokkaebiUnit.GetStatusEffects().Clear();
            }

            SmartLogger.Log($"Cleared all status effects from {target.DisplayName}", LogCategory.General);
        }

        /// <summary>
        /// Check if a unit has a specific status effect.
        /// </summary>
        public static bool HasStatusEffect(IDokkaebiUnit target, StatusEffectType effectType)
        {
            if (target == null) return false;

            var unitEffects = target.GetStatusEffects();
            return unitEffects?.Any(e => e.StatusEffectType == effectType) ?? false;
        }

        /// <summary>
        /// Aggregates the stat modifier value for a specific attribute from all active status effects on a unit.
        /// </summary>
        public static float GetStatModifier(IDokkaebiUnit unit, UnitAttributeType statType)
        {
            // --- ADD LOG START ---
            SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit?.DisplayName ?? "<null>"}, Querying Stat: {statType}", LogCategory.Ability, unit as MonoBehaviour);
            // --- END ADD LOG ---
            if (unit == null) return 1.0f;
            var unitEffects = unit.GetStatusEffects()?.ToList();
            if (unitEffects == null)
            {
                SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, No active effects. Returning base multiplier 1.0 for {statType}.", LogCategory.Ability, unit as MonoBehaviour);
                return 1.0f;
            }

            // --- ADD LOG START ---
            SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Found {unitEffects.Count} active effects. Effects: {string.Join(", ", unitEffects.Select(e => e.StatusEffectType))}", LogCategory.Ability, unit as MonoBehaviour);
            // --- END ADD LOG ---
            // Special handling for dodge chance - we add the modifiers instead of multiplying
            if (statType == UnitAttributeType.DodgeChance)
            {
                float totalDodgeChance = 0f;
                foreach (var effect in unitEffects)
                {
                    if (effect is StatusEffectInstance instance && instance.Effect != null)
                    {
                        if (instance.Effect.HasStatModifier(statType))
                        {
                            totalDodgeChance += instance.Effect.GetStatModifier(statType) - 1.0f; // Subtract base 1.0 since modifiers are stored as 1.0 + actual_value
                        }
                    }
                }
                // --- ADD LOG START ---
                SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Stat: {statType}. Calculated total dodge chance: {totalDodgeChance}. Returning raw value.", LogCategory.Ability, unit as MonoBehaviour);
                // --- END ADD LOG ---
                return totalDodgeChance;
            }

            // Special handling for crit chance and crit damage - additive aggregation
            if (statType == UnitAttributeType.CriticalChance)
            {
                float totalCritChance = 0f;
                foreach (var effect in unitEffects)
                {
                    if (effect is StatusEffectInstance instance && instance.Effect != null)
                    {
                        var data = instance.Effect as StatusEffectData;
                        if (data != null && data.criticalChanceModifier != 0f)
                        {
                            totalCritChance += data.criticalChanceModifier;
                        }
                    }
                }
                // --- ADD LOG START ---
                SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Stat: {statType}. Calculated total crit chance: {totalCritChance}.", LogCategory.Ability, unit as MonoBehaviour);
                // --- END ADD LOG ---
                return totalCritChance;
            }
            if (statType == UnitAttributeType.CriticalDamage)
            {
                float totalCritDamage = 0f;
                foreach (var effect in unitEffects)
                {
                    if (effect is StatusEffectInstance instance && instance.Effect != null)
                    {
                        var data = instance.Effect as StatusEffectData;
                        if (data != null && data.criticalDamageModifier != 0f)
                        {
                            totalCritDamage += data.criticalDamageModifier;
                        }
                    }
                }
                // --- ADD LOG START ---
                SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Stat: {statType}. Calculated total crit damage: {totalCritDamage}.", LogCategory.Ability, unit as MonoBehaviour);
                // --- END ADD LOG ---
                return totalCritDamage;
            }

            // For all other stats, multiply modifiers as before
            float multiplier = 1.0f;
            bool foundDestinyShifted = false; // Flag to check for DestinyShifted effect

            foreach (var effect in unitEffects)
            {
                if (effect is StatusEffectInstance instance && instance.Effect != null)
                {
                    // --- ADD LOG START ---
                    SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Checking Effect: {instance.Effect.displayName} (ID: {instance.Effect.effectId}, Type: {instance.Effect.effectType}) for Stat: {statType}", LogCategory.Ability, unit as MonoBehaviour);
                    // --- END ADD LOG ---
                    if (instance.Effect.HasStatModifier(statType))
                    {
                        multiplier *= instance.Effect.GetStatModifier(statType);
                        // --- ADD LOG START ---
                        SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Effect {instance.Effect.displayName} modifies {statType}. Multiplier updated to: {multiplier}", LogCategory.Ability, unit as MonoBehaviour);
                        // --- END ADD LOG ---
                    }

                    // DestinyShifted special logic (still keep this for now)
                    if (instance.Effect.effectId == "DestinyShifted")
                    {
                        foundDestinyShifted = true; // Mark that DestinyShifted is present
                        SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Detected DestinyShifted effect. Checking stat type for special handling...", LogCategory.Ability, unit as MonoBehaviour);
                        if (statType == UnitAttributeType.AbilityRange)
                        {
                            SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Stat {statType} is AbilityRange. DestinyShifted special logic applies.", LogCategory.Ability, unit as MonoBehaviour);
                            // We will apply the flat reduction *after* the loop.
                        }
                        else if (statType == UnitAttributeType.AbilityCooldown)
                        {
                            SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Stat {statType} is AbilityCooldown. DestinyShifted special logic applies.", LogCategory.Ability, unit as MonoBehaviour);
                            // We will handle cooldown increase separately.
                        }
                    }
                }
            }
            float finalValue = multiplier;
            // Apply flat modifiers *after* multiplicative ones
            if (foundDestinyShifted && statType == UnitAttributeType.AbilityRange)
            {
                // Apply the flat range reduction for DestinyShifted
                finalValue = multiplier - 2.0f; // Assuming a flat -2 range reduction
                SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Stat: {statType}. Applying flat -2 reduction from DestinyShifted. Multiplier: {multiplier} -> Final Value: {finalValue}", LogCategory.Ability, unit as MonoBehaviour);
            }
            // Add similar logic here later for flat cooldown increase if implementing

            // --- ADD LOG START ---
            SmartLogger.Log($"[StatusEffectSystem.GetStatModifier] UNIT: {unit.DisplayName}, Stat: {statType}. Returning final calculated value: {finalValue}", LogCategory.Ability, unit as MonoBehaviour);
            // --- END ADD LOG ---

            return finalValue;
        }

        /// <summary>
        /// Checks if the unit is prevented from acting by status effects (e.g., Stun).
        /// </summary>
        public static bool CanUnitAct(IDokkaebiUnit unit)
        {
            if (unit == null) return true;

            var unitEffects = unit.GetStatusEffects()?.ToList();
            if (unitEffects == null) return true;

            // Check for effects that prevent acting (e.g., Stun)
            return !unitEffects.Any(effect => 
                effect.StatusEffectType == StatusEffectType.Stun || 
                effect.StatusEffectType == StatusEffectType.Frozen);
        }

        /// <summary>
        /// Checks if the unit is prevented from moving by status effects (e.g., Root).
        /// </summary>
        public static bool CanUnitMove(IDokkaebiUnit unit)
        {
            if (unit == null) return true;

            var unitEffects = unit.GetStatusEffects()?.ToList();
            if (unitEffects == null) return true;

            // Check for effects that prevent movement (e.g., Root)
            return !unitEffects.Any(effect => 
                effect.StatusEffectType == StatusEffectType.Root || 
                effect.StatusEffectType == StatusEffectType.Stun || 
                effect.StatusEffectType == StatusEffectType.Frozen);
        }

        /// <summary>
        /// Apply the turn end impact of a status effect.
        /// </summary>
        private static void ApplyEffectTurnEndImpact(IDokkaebiUnit target, StatusEffectData effectData)
        {
            if (target == null || effectData == null) return;

            // Apply damage over time
            if (effectData.hasDamageOverTime)
            {
                int damage = effectData.damageOverTimeAmount;
                if (target is DokkaebiUnit dokkaebiUnit)
                {
                    dokkaebiUnit.TakeDamage(damage, DamageType.Normal);
                }
            }

            // Apply healing over time
            if (effectData.hasHealingOverTime)
            {
                int healing = effectData.healingOverTimeAmount;
                if (target is DokkaebiUnit dokkaebiUnit)
                {
                    dokkaebiUnit.Heal(healing);
                }
            }
        }

        /// <summary>
        /// Apply the immediate impact of a status effect.
        /// </summary>
        private static void ApplyEffectImmediateImpact(IDokkaebiUnit target, StatusEffectData effectData)
        {
            if (target == null || effectData == null) return;

            // Apply immediate damage
            if (effectData.hasImmediateDamage)
            {
                int damage = effectData.immediateDamageAmount;
                if (target is DokkaebiUnit dokkaebiUnit)
                {
                    dokkaebiUnit.TakeDamage(damage, DamageType.Normal);
                }
            }

            // Apply immediate healing
            if (effectData.hasImmediateHealing)
            {
                int healing = effectData.immediateHealingAmount;
                if (target is DokkaebiUnit dokkaebiUnit)
                {
                    dokkaebiUnit.Heal(healing);
                }
            }
        }

        /// <summary>
        /// Remove the impacts of a status effect.
        /// </summary>
        private static void RemoveEffectImpacts(IDokkaebiUnit target, StatusEffectData effectData)
        {
            if (target == null || effectData == null) return;

            // Remove stat modifiers
            // Note: This is handled automatically through the GetStatModifier method
            // which only considers active effects
        }

        // Add a new static method to handle effect removal propagation
        public static void HandleStatusEffectRemovedPropagation(IDokkaebiUnit targetUnit, IStatusEffectInstance removedEffectInstance)
        {
            SmartLogger.Log($"[StatusEffectSystem.HandleStatusEffectRemovedPropagation] BEGIN: Unit {targetUnit?.DisplayName ?? "<null>"}. Removed Effect: {removedEffectInstance?.StatusEffectType ?? StatusEffectType.None}.", LogCategory.Ability);
            if (targetUnit == null || removedEffectInstance == null) return;

            // Check if the unit that had the effect removed also has the FateLinked effect
            var fateLinkedEffectOnTarget = targetUnit.GetStatusEffects()?.FirstOrDefault(e => e.StatusEffectType == StatusEffectType.FateLinked) as StatusEffectInstance;
            if (fateLinkedEffectOnTarget != null && fateLinkedEffectOnTarget.linkedUnitId != -1)
            {
                SmartLogger.Log($"[StatusEffectSystem.HandleStatusEffectRemovedPropagation] Unit {targetUnit.GetUnitName()} has FateLinked, propagating removal of '{removedEffectInstance.StatusEffectType}' to linked unit {fateLinkedEffectOnTarget.linkedUnitId}.", LogCategory.Ability);
                var linkedUnit = UnitManager.Instance?.GetUnitById(fateLinkedEffectOnTarget.linkedUnitId);
                if (linkedUnit != null && linkedUnit != targetUnit)
                {
                    SmartLogger.Log($"[StatusEffectSystem.HandleStatusEffectRemovedPropagation] Propagating removal of effect type '{removedEffectInstance.StatusEffectType}' to linked unit {linkedUnit.GetUnitName()}.", LogCategory.Ability);
                    RemoveStatusEffect(linkedUnit, removedEffectInstance.StatusEffectType);
                    SmartLogger.Log($"[StatusEffectSystem.HandleStatusEffectRemovedPropagation] Effect removal propagation complete for '{removedEffectInstance.StatusEffectType}' to {linkedUnit.GetUnitName()}.", LogCategory.Ability);
                }
                else
                {
                    SmartLogger.LogWarning($"[StatusEffectSystem.HandleStatusEffectRemovedPropagation] FateLinked removal propagation failed for '{removedEffectInstance.StatusEffectType}': Linked unit is null or is the same as the target unit.", LogCategory.Ability);
                }
            }
            SmartLogger.Log($"[StatusEffectSystem.HandleStatusEffectRemovedPropagation] END.", LogCategory.Ability);
        }
    }
} 
