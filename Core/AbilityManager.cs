    using UnityEngine;
    using System.Collections.Generic;
    using Dokkaebi.Core.Data;
    using Dokkaebi.Grid;
    using Dokkaebi.Units;
    using Dokkaebi.Zones;
    using System.Linq;
    using System.Collections;
    using Dokkaebi.Pathfinding;
    using Dokkaebi.Utilities;
    using Dokkaebi.Interfaces;
    using Dokkaebi.Common;
    using Dokkaebi.Core;

    namespace Dokkaebi.Core
    {
        /// <summary>
        /// Manager class that handles ability execution and validation
        /// </summary>
        public class AbilityManager : MonoBehaviour
        {
            public static AbilityManager Instance { get; private set; }

            [Header("References")]
            [SerializeField] private UnitManager unitManager;
            [SerializeField] private DokkaebiTurnSystemCore turnSystem;
            
            [Header("Settings")]
            [SerializeField] private bool enableAbilityVFX = true;
            [SerializeField] private LayerMask targetingLayerMask;
            
            [Header("Ability Settings")]
            [SerializeField] private int windZoneRangeBonus = 1; // Default range bonus for abilities in Wind Zones (except Gale Arrow)
            
            // Audio sources
            private AudioSource sfxPlayer;
            
            // VFX reference dicts
            private Dictionary<DamageType, GameObject> damageVFXs = new Dictionary<DamageType, GameObject>();
            private Dictionary<string, GameObject> specialVFXs = new Dictionary<string, GameObject>();
            
            private void Awake()
            {
                Instance = this;
                // Get component references
                if (unitManager == null)
                {
                    unitManager = FindObjectOfType<UnitManager>();
                }
                
                if (turnSystem == null)
                {
                    turnSystem = FindObjectOfType<DokkaebiTurnSystemCore>();
                }
                
                
                // Set up audio source for SFX
                sfxPlayer = gameObject.AddComponent<AudioSource>();
                sfxPlayer.playOnAwake = false;
                sfxPlayer.spatialBlend = 0f; // 2D sound
            }
            
            private void OnDestroy()
            {
                if (Instance == this) Instance = null;
            }
            
            /// <summary>
            /// Execute an ability from a unit to a target position/unit
            /// </summary>
            /// <param name="abilityData">The ability data to execute</param>
            /// <param name="sourceUnit">The unit using the ability</param>
            /// <param name="targetPosition">The target position</param>
            /// <param name="targetUnit">The target unit, if any</param>
            /// <param name="isOverload">Whether this is an overload cast</param>
            /// <param name="targetedZoneId">The ID of the targeted zone, if applicable (e.g., for Terrain Shift)</param>
            /// <param name="secondTargetUnitId">The ID of the second target unit for Karmic Tether, if applicable</param>
            /// <returns>True if ability was executed successfully</returns>
            public bool ExecuteAbility(AbilityData abilityData, DokkaebiUnit sourceUnit, GridPosition targetPosition, DokkaebiUnit targetUnit, bool isOverload, int? targetedZoneId = null, int? secondTargetUnitId = null)
            {
                // --- LOG: Trace movementType for FlameLunge at execution ---
                if (abilityData != null && abilityData.abilityId == "FlameLunge")
                {
                    SmartLogger.Log($"[AbilityManager.ExecuteAbility] FlameLunge ability received for execution. Movement Type: {abilityData.movementType}", LogCategory.Ability, this);
                }

                SmartLogger.Log($"[AbilityManager.ExecuteAbility] Attempting to execute ability: '{abilityData?.displayName ?? "NULL"}' (ID: '{abilityData?.abilityId ?? "NULL"}') by Unit: '{sourceUnit?.GetUnitName() ?? "NULL"}'", LogCategory.Ability, this);
                SmartLogger.Log($"[AbilityManager.ExecuteAbility] Targeted Zone ID: {targetedZoneId?.ToString() ?? "NULL"}", LogCategory.Ability, this);

                if (abilityData == null || sourceUnit == null)
                {
                    SmartLogger.LogError("[AbilityManager.ExecuteAbility] Invalid ability data or source unit", LogCategory.Ability);
                    return false;
                }

                // --- Karmic Tether Special Handling ---
                if (abilityData.abilityId == "KarmicTether" && secondTargetUnitId.HasValue)
                {
                    // --- KARMIC TETHER LOGGING ---
                    SmartLogger.Log($"[AbilityManager.ExecuteAbility] KarmicTether Block: Processing ability '{abilityData.displayName}' for unit {sourceUnit.GetUnitName()} (ID: {sourceUnit.UnitId}). SecondTargetUnitID: {secondTargetUnitId.Value}.", LogCategory.Ability, this);
                    DokkaebiUnit firstTarget = targetUnit;
                    DokkaebiUnit secondTarget = null;
                    if (firstTarget == null && targetPosition != GridPosition.invalid)
                    {
                        firstTarget = UnitManager.Instance?.GetUnitAtPosition(targetPosition) as DokkaebiUnit;
                    }
                    secondTarget = UnitManager.Instance?.GetUnitById(secondTargetUnitId.Value) as DokkaebiUnit;
                    if (firstTarget == null) SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] Karmic Tether Block: First target unit is NULL at target position {targetPosition}.", LogCategory.Ability, this);
                    if (secondTarget == null) SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] Karmic Tether Block: Second target unit is NULL with ID {secondTargetUnitId.Value}.", LogCategory.Ability, this);
                    var fateLinkedEffectData = DataManager.Instance.GetStatusEffectData("FateLinked");
                    if (fateLinkedEffectData == null) SmartLogger.LogError("[AbilityManager.ExecuteAbility] Karmic Tether Block: FateLinked StatusEffectData NOT found in DataManager!", LogCategory.Ability, this);
                    if (firstTarget != null && fateLinkedEffectData != null)
                    {
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility] Karmic Tether Block: About to call StatusEffectSystem.ApplyStatusEffect for first target {firstTarget.GetUnitName()} (ID: {firstTarget.UnitId}), linking to {secondTarget?.GetUnitName() ?? "NULL"} (ID: {secondTargetUnitId.Value}).", LogCategory.Ability, this);

                        // --- ADDED LOG FOR PARAMETERS ---
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility] DEBUG_PARAMS: Calling StatusEffectSystem.ApplyStatusEffect. Params: targetUnit={(firstTarget != null ? firstTarget.GetUnitName() : "NULL")}, effectData={(fateLinkedEffectData != null ? fateLinkedEffectData.displayName : "NULL")}, duration={fateLinkedEffectData?.duration ?? -1}, sourceUnit={(sourceUnit != null ? sourceUnit.GetUnitName() : "NULL")}, linkedUnitId={secondTargetUnitId?.ToString() ?? "NULL"}.", LogCategory.Ability, this);
                        // --- END ADDED ---

                        StatusEffectSystem.ApplyStatusEffect(firstTarget, fateLinkedEffectData, fateLinkedEffectData.duration, sourceUnit, secondTargetUnitId);
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility] Karmic Tether Block: Finished first StatusEffectSystem.ApplyStatusEffect call.", LogCategory.Ability, this);
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility] Applying FateLinked effect to {secondTarget.GetUnitName()} linked to {firstTarget.GetUnitName()}.", LogCategory.Ability);
                        StatusEffectSystem.ApplyStatusEffect(secondTarget, fateLinkedEffectData, fateLinkedEffectData.duration, sourceUnit, firstTarget?.UnitId);
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility] Karmic Tether Block: Finished second StatusEffectSystem.ApplyStatusEffect call.", LogCategory.Ability, this);

                        // --- ADDED LOGIC FOR SYNCHRONIZING EXISTING EFFECTS ---
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility] Karmic Tether Block: Synchronizing existing status effects between {firstTarget.GetUnitName()} and {secondTarget.GetUnitName()}.", LogCategory.Ability, this);
                        // Get effects from first target and apply to second target
                        var firstTargetEffects = firstTarget.GetStatusEffects()?.ToList();
                        if (firstTargetEffects != null)
                        {
                            SmartLogger.Log($"[AbilityManager.ExecuteAbility] Propagating effects from {firstTarget.GetUnitName()} to {secondTarget.GetUnitName()}. Found {firstTargetEffects.Count} effects on first target.", LogCategory.Ability, this);
                            foreach (var effectInstance in firstTargetEffects)
                            {
                                if (effectInstance != null && effectInstance.StatusEffectType != StatusEffectType.FateLinked)
                                {
                                    SmartLogger.Log($"[AbilityManager.ExecuteAbility] Propagating effect '{effectInstance.Effect?.displayName ?? "NULL"}' (Type: {effectInstance.StatusEffectType}) from {firstTarget.GetUnitName()} to {secondTarget.GetUnitName()}. Duration: {effectInstance.RemainingDuration}, Source: {effectInstance.SourceUnitId}.", LogCategory.Ability, this);
                                    // Apply the effect to the second target, preserving duration and source
                                    // Pass the other unit's ID as the linkedUnitId for this applied effect instance
                                    StatusEffectSystem.ApplyStatusEffect(secondTarget, effectInstance.Effect, effectInstance.RemainingDuration, UnitManager.Instance?.GetUnitById(effectInstance.SourceUnitId), firstTarget.UnitId);
                                }
                            }
                        }
                        // Get effects from second target and apply to first target
                        var secondTargetEffects = secondTarget.GetStatusEffects()?.ToList();
                        if (secondTargetEffects != null)
                        {
                            SmartLogger.Log($"[AbilityManager.ExecuteAbility] Propagating effects from {secondTarget.GetUnitName()} to {firstTarget.GetUnitName()}. Found {secondTargetEffects.Count} effects on second target.", LogCategory.Ability, this);
                            foreach (var effectInstance in secondTargetEffects)
                            {
                                if (effectInstance != null && effectInstance.StatusEffectType != StatusEffectType.FateLinked)
                                {
                                    SmartLogger.Log($"[AbilityManager.ExecuteAbility] Propagating effect '{effectInstance.Effect?.displayName ?? "NULL"}' (Type: {effectInstance.StatusEffectType}) from {secondTarget.GetUnitName()} to {firstTarget.GetUnitName()}. Duration: {effectInstance.RemainingDuration}, Source: {effectInstance.SourceUnitId}.", LogCategory.Ability, this);
                                    // Apply the effect to the first target, preserving duration and source
                                    // Pass the other unit's ID as the linkedUnitId for this applied effect instance
                                    StatusEffectSystem.ApplyStatusEffect(firstTarget, effectInstance.Effect, effectInstance.RemainingDuration, UnitManager.Instance?.GetUnitById(effectInstance.SourceUnitId), secondTarget.UnitId);
                                }
                            }
                        }
                        // --- END ADDED LOGIC FOR SYNCHRONIZING EXISTING EFFECTS ---
                    }
                    else
                    {
                        SmartLogger.LogError("[AbilityManager.ExecuteAbility] FateLinked StatusEffectData not found in DataManager! Cannot apply tether effect.", LogCategory.Ability);
                    }

                    SmartLogger.Log("[AbilityManager.ExecuteAbility] Karmic Tether Block: Playing VFX/SFX.", LogCategory.Ability, this);
                    PlayAbilityVFX(abilityData, sourceUnit, targetPosition, null);
                    PlayAbilitySound(abilityData.castSound);
                    SmartLogger.Log("[AbilityManager.ExecuteAbility] Karmic Tether Block: Execution successful, returning TRUE.", LogCategory.Ability, this);
                    return true;
                }
                // --- END Karmic Tether Special Handling ---

                // Validate target unit for unit-targeting abilities (Skip this check for Terrain Shift as it targets zones/ground)
                if (abilityData.abilityId != "TerrainShift" && !abilityData.targetsGround && targetUnit == null && abilityData.movementType != AbilityMovementType.TeleportToTarget)
                {
                    SmartLogger.LogError($"[AbilityManager.ExecuteAbility] Target unit required for non-ground-targeting ability {abilityData.displayName}", LogCategory.Ability);
                    return false;
                }

                // Check effective range (range validation should be handled by PlayerActionManager before this point)
                int effectiveRange = GetEffectiveRange(abilityData, sourceUnit);
                int distance = GridPosition.GetManhattanDistance(sourceUnit.GetGridPosition(), targetPosition);
                // Do NOT block execution here for movement abilities; let dash logic handle movement.

                // Apply ability cost and cooldown first
                sourceUnit.ModifyUnitAura(-abilityData.auraCost);
                sourceUnit.SetAbilityCooldown(abilityData.abilityId, abilityData.cooldownTurns);

                // --- ADDED: Handle Terrain Shift Execution ---
                if (abilityData.abilityId == "TerrainShift")
                {
                    SmartLogger.Log("[AbilityManager.ExecuteAbility] Handling Terrain Shift execution.", LogCategory.Ability, this);

                    if (!targetedZoneId.HasValue)
                    {
                        SmartLogger.LogError("[AbilityManager.ExecuteAbility] Terrain Shift requires a targetedZoneId, but it is null.", LogCategory.Ability, this);
                        return false;
                    }

                    var zoneManager = ZoneManager.Instance;
                    if (zoneManager == null)
                    {
                        SmartLogger.LogError("[AbilityManager.ExecuteAbility] ZoneManager.Instance is null! Cannot shift zone.", LogCategory.Ability, this);
                        return false;
                    }

                    // Find the zone instance by ID
                    IZoneInstance targetedZone = null;
                    var allActiveZones = zoneManager.GetAllActiveZoneInstances();
                    foreach(var z in allActiveZones)
                    {
                        if (z.Id == targetedZoneId.Value)
                        {
                            targetedZone = z;
                            break;
                        }
                    }

                    if (targetedZone == null || !targetedZone.IsActive)
                    {
                        SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] Targeted zone with ID {targetedZoneId.Value} not found or is not active. Cannot shift.", LogCategory.Ability, this);
                        return false;
                    }

                    // Cast to concrete ZoneInstance for ShiftZone method
                    if (!(targetedZone is Dokkaebi.Zones.ZoneInstance shiftZoneInstance))
                    {
                        SmartLogger.LogError($"[AbilityManager.ExecuteAbility] Targeted zone is not a concrete ZoneInstance. Type: {targetedZone.GetType().Name}. Cannot shift.", LogCategory.Ability, this);
                        return false;
                    }

                    // Shift the zone
                    bool shiftSuccess = zoneManager.ShiftZone(shiftZoneInstance, targetPosition);

                    if (shiftSuccess)
                    {
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility] Successfully shifted zone '{shiftZoneInstance.DisplayName}' from {shiftZoneInstance.Position} to {targetPosition}.", LogCategory.Ability, this);
                        // Play VFX/SFX if configured for the ability
                        PlayAbilityVFX(abilityData, sourceUnit, targetPosition, null); // VFX at destination
                        PlayAbilitySound(abilityData.castSound); // Cast sound
                    }
                    else
                    {
                         SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] ZoneManager.ShiftZone failed for zone '{shiftZoneInstance.DisplayName}' to {targetPosition}.", LogCategory.Ability, this);
                         // Log failure, but return true as the ability attempt was processed.
                    }

                    SmartLogger.Log($"[AbilityManager.ExecuteAbility END] Terrain Shift execution completed.", LogCategory.Ability, this);
                    return true; // Terrain Shift processed

                }
                // --- END ADDED ---

                // --- Handle Ability Movement Type ---
                SmartLogger.Log($"[AbilityManager.ExecuteAbility] Handling movement type: {abilityData.movementType}", LogCategory.Ability, this);
                switch (abilityData.movementType)
                {
                    case AbilityMovementType.None:
                        // No special movement, proceed to effects
                        break;
                    case AbilityMovementType.DashToTargetAdjacent:
                        // --- DEBUG LOG: Confirm entering DashToTargetAdjacent case ---
                        Debug.Log("[DEBUG] DashToTargetAdjacent case hit");
                        // Dash to the best adjacent tile to the target, then apply effects from the new position
                        if (targetUnit != null)
                        {
                            GridPosition dashPosition = FindBestDashPosition(sourceUnit, targetUnit);
                            SmartLogger.Log($"[AbilityManager.ExecuteAbility] FindBestDashPosition returned: {dashPosition}", LogCategory.Ability, this);
                            if (dashPosition != GridPosition.invalid)
                            {
                                SmartLogger.Log($"[AbilityManager.ExecuteAbility] Dashing {sourceUnit.GetUnitName()} (ID:{sourceUnit.UnitId}) from {sourceUnit.GetGridPosition()} to calculated dashPosition {dashPosition}", LogCategory.Ability);
                                System.Text.StringBuilder beforeLog = Dokkaebi.Utilities.StringBuilderPool.Get();
                                beforeLog.AppendLine($"[AbilityManager.ExecuteAbility] BEFORE SetGridPosition for {sourceUnit.GetUnitName()} (ID:{sourceUnit.UnitId}) to {dashPosition}. Current Grid State:");
                                var unitPositionsBefore = UnitManager.Instance.GetUnitPositionsReadOnly();
                                foreach(var kvp in unitPositionsBefore)
                                {
                                    beforeLog.AppendLine($"- Pos: {kvp.Key}, Unit: {(kvp.Value != null ? $"{kvp.Value.GetUnitName()} (ID:{kvp.Value.UnitId})" : "None")}");
                                }
                                SmartLogger.Log(Dokkaebi.Utilities.StringBuilderPool.GetStringAndReturn(beforeLog), LogCategory.Ability);
                                SmartLogger.Log($"[AbilityManager.ExecuteAbility] About to call SetGridPosition with position: {dashPosition}", LogCategory.Ability, this);
                                // --- DEBUG LOG: About to call SetGridPosition ---
                                Debug.Log($"[DEBUG] About to call SetGridPosition with position: {dashPosition}");
                                sourceUnit.SetGridPosition(dashPosition);
                                SmartLogger.Log($"[AbilityManager.ExecuteAbility] SetGridPosition called for {sourceUnit.GetUnitName()} to {dashPosition}", LogCategory.Ability);
                                System.Text.StringBuilder afterLog = Dokkaebi.Utilities.StringBuilderPool.Get();
                                afterLog.AppendLine($"[AbilityManager.ExecuteAbility] AFTER SetGridPosition for {sourceUnit.GetUnitName()}. New Logical Position: {sourceUnit.GetGridPosition()}. Current Grid State:");
                                var unitPositionsAfter = UnitManager.Instance.GetUnitPositionsReadOnly();
                                foreach(var kvp in unitPositionsAfter)
                                {
                                    afterLog.AppendLine($"- Pos: {kvp.Key}, Unit: {(kvp.Value != null ? $"{kvp.Value.GetUnitName()} (ID:{kvp.Value.UnitId})" : "None")}");
                                }
                                SmartLogger.Log(Dokkaebi.Utilities.StringBuilderPool.GetStringAndReturn(afterLog), LogCategory.Ability);
                                DokkaebiUnit finalTargetUnit = (abilityData.targetsGround || targetUnit == null) ? UnitManager.Instance.GetUnitAtPosition(targetPosition) as DokkaebiUnit : targetUnit;
                                ExecuteCoreAbilityEffects(abilityData, sourceUnit, targetPosition, finalTargetUnit, isOverload, 1.0f, secondTargetUnitId);
                                SmartLogger.Log($"[AbilityManager.ExecuteAbility END] Successfully executed '{abilityData?.displayName}' including dash", LogCategory.Ability);
                                return true;
                            }
                            else
                            {
                                Debug.Log("[DEBUG] No valid dash position found, ability fails");
                                SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] Could not find a valid dash position adjacent to target for {sourceUnit.GetUnitName()}. Ability fails.", LogCategory.Ability);
                                return false;
                            }
                        }
                        else
                        {
                            Debug.Log("[DEBUG] DashToTargetAdjacent: targetUnit is null, ability fails");
                            SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] DashToTargetAdjacent requires a target unit, but targetUnit is null. Ability fails.", LogCategory.Ability);
                            return false;
                        }
                    case AbilityMovementType.TeleportToTarget:
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility] Teleporting {sourceUnit.GetUnitName()} to {targetPosition}", LogCategory.Ability);
                        sourceUnit.SetGridPosition(targetPosition);
                        DokkaebiUnit teleportTargetUnit = (abilityData.targetsGround || targetUnit == null) ? UnitManager.Instance.GetUnitAtPosition(targetPosition) as DokkaebiUnit : targetUnit;
                        ExecuteCoreAbilityEffects(abilityData, sourceUnit, targetPosition, teleportTargetUnit, isOverload, 1.0f, secondTargetUnitId);
                        SmartLogger.Log($"[AbilityManager.ExecuteAbility END] Successfully executed '{abilityData?.displayName}' including teleport", LogCategory.Ability);
                        return true;
                    case AbilityMovementType.PullTargetTowardsCaster:
                        if (targetUnit != null)
                        {
                            SmartLogger.Log($"[AbilityManager.ExecuteAbility] Handling PullTargetTowardsCaster for {targetUnit.GetUnitName()}", LogCategory.Ability);
                            GridPosition pullDestination = FindPullDestination(sourceUnit.GetGridPosition(), targetUnit.GetGridPosition(), 1);
                            if (pullDestination != GridPosition.invalid)
                            {
                                targetUnit.SetGridPosition(pullDestination);
                                SmartLogger.Log($"[AbilityManager.ExecuteAbility] Pulled {targetUnit.GetUnitName()} to {pullDestination}", LogCategory.Ability);
                                ExecuteCoreAbilityEffects(abilityData, sourceUnit, pullDestination, targetUnit, isOverload, 1.0f, secondTargetUnitId);
                                SmartLogger.Log($"[AbilityManager.ExecuteAbility END] Successfully executed '{abilityData?.displayName}' including pull", LogCategory.Ability);
                                return true;
                            }
                            else
                            {
                                SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] Could not find a valid pull destination for {targetUnit.GetUnitName()}. Ability fails.", LogCategory.Ability);
                                ExecuteCoreAbilityEffects(abilityData, sourceUnit, targetPosition, targetUnit, isOverload, 1.0f, secondTargetUnitId);
                                SmartLogger.Log($"[AbilityManager.ExecuteAbility END] Executed '{abilityData?.displayName}' despite pull failure", LogCategory.Ability);
                                return false;
                            }
                        }
                        else
                        {
                            SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] PullTargetTowardsCaster requires a target unit, but targetUnit is null. Ability fails.", LogCategory.Ability);
                            return false;
                        }
                    case AbilityMovementType.PushTargetAway:
                        if (targetUnit != null)
                        {
                            SmartLogger.Log($"[AbilityManager.ExecuteAbility] Handling PushTargetAway for {targetUnit.GetUnitName()} with push distance {abilityData.pushDistance}", LogCategory.Ability);
                            HandlePushEffect(abilityData, sourceUnit, targetUnit);
                            SmartLogger.Log($"[AbilityManager.ExecuteAbility END] Successfully executed '{abilityData?.displayName}' including push", LogCategory.Ability);
                            return true;
                        }
                        else
                        {
                            SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] PushTargetAway requires a target unit, but targetUnit is null. Ability fails.", LogCategory.Ability);
                            return false;
                        }
                }

                if (abilityData.movementType == AbilityMovementType.None)
                {
                    SmartLogger.Log($"[AbilityManager.ExecuteAbility] Ability has movementType.None. Applying core effects to initial target.", LogCategory.Ability, this);

                    DokkaebiUnit initialTargetUnit = (abilityData.targetsGround || targetUnit == null) ? UnitManager.Instance.GetUnitAtPosition(targetPosition) as DokkaebiUnit : targetUnit;

                    // --- ADDED NULL CHECK AND LOG ---
                    SmartLogger.Log($"[AbilityManager.ExecuteAbility] Calculated initialTargetUnit. Is null: {initialTargetUnit == null}. Ability: {abilityData.displayName}, Target Position: {targetPosition}.", LogCategory.Ability, this);
                    if (initialTargetUnit == null && !abilityData.targetsGround)
                    {
                        SmartLogger.LogError($"[AbilityManager.ExecuteAbility] Calculated initialTargetUnit is NULL for non-ground ability '{abilityData.displayName}' at target position {targetPosition}. Aborting core effects execution.", LogCategory.Ability, this);
                        return false; // Abort execution if the primary target unit is null for a unit-targeting ability.
                    }
                    // --- END ADDED ---

                    ExecuteCoreAbilityEffects(abilityData, sourceUnit, targetPosition, initialTargetUnit, isOverload, 1.0f, secondTargetUnitId);
                    SmartLogger.Log($"[AbilityManager.ExecuteAbility END] Successfully executed '{abilityData?.displayName}' (movementType.None)", LogCategory.Ability);
                    return true;
                }
                else
                {
                    SmartLogger.LogWarning($"[AbilityManager.ExecuteAbility] Ability execution failed or completed within movement block. Movement type was {abilityData.movementType}.", LogCategory.Ability);
                    return false;
                }
            }
            
            /// <summary>
            /// Validate if the ability can be executed
            /// </summary>
            private bool ValidateAbility(AbilityData abilityData, DokkaebiUnit sourceUnit, GridPosition targetPosition, DokkaebiUnit targetUnit, bool isOverload)
            {
                // Check if source unit exists and is alive
                if (sourceUnit == null || !sourceUnit.IsAlive)
                {
                    SmartLogger.Log($"Invalid source unit for ability {abilityData.displayName}", LogCategory.Ability);
                    return false;
                }
                
                // Check if it's the unit's turn to use an ability
                if (turnSystem != null && !turnSystem.CanUnitUseAura(sourceUnit))
                {
                    SmartLogger.Log($"Unit {sourceUnit.GetUnitName()} cannot use aura at this time", LogCategory.Ability);
                    return false;
                }
                
                // Check cooldown using abilityId
                if (sourceUnit.IsOnCooldown(abilityData.abilityId))
                {
                    SmartLogger.Log($"Ability {abilityData.displayName} is on cooldown", LogCategory.Ability);
                    return false;
                }
                
                // Special validation for KarmicTether (requires two distinct targets)
                else if (abilityData.abilityId == "KarmicTether")
                {
                    // TODO: This only checks one target; true two-target validation requires updating the method signature and call chain.
                    // For now, always fail validation to prevent accidental use with only one target.
                    SmartLogger.LogWarning("[AbilityManager.ValidateAbility] Karmic Tether requires two distinct target units.", LogCategory.Ability);
                    return false;
                }
                
                // Check targeting
                bool isTargetValid = IsTargetValid(abilityData, sourceUnit, targetPosition, targetUnit);
                if (!isTargetValid)
                {
                    SmartLogger.Log($"Invalid target for ability {abilityData.displayName}", LogCategory.Ability);
                    return false;
                }
                
                return true;
            }
            
            /// <summary>
            /// Check if the target is valid for the ability
            /// </summary>
            private bool IsTargetValid(AbilityData abilityData, DokkaebiUnit sourceUnit, GridPosition targetPosition, DokkaebiUnit targetUnit)
            {
                SmartLogger.Log($"[AbilityManager.IsTargetValid] START - Ability: {abilityData.displayName}, Source: {sourceUnit?.GetUnitName()}, Target Position: {targetPosition}, Target Unit: {targetUnit?.GetUnitName()}", LogCategory.Ability);
                SmartLogger.Log($"[AbilityManager.IsTargetValid] Ability flags - Ground: {abilityData.targetsGround}, Self: {abilityData.targetsSelf}, Ally: {abilityData.targetsAlly}, Enemy: {abilityData.targetsEnemy}", LogCategory.Ability);

                // Calculate distance first
                GridPosition sourcePos = sourceUnit.GetGridPosition();
                int distance = GridPosition.GetManhattanDistance(sourcePos, targetPosition);
                
                // Check range first - this applies to ALL targeting types
                if (distance > abilityData.range)
                {
                    SmartLogger.Log($"[AbilityManager.IsTargetValid] FAILED: Target out of range. Distance: {distance}, Range: {abilityData.range}", LogCategory.Ability);
                    return false;
                }

                // For ground-targeting abilities, we only care about position validity
                if (abilityData.targetsGround)
                {
                    bool isValidPosition = GridManager.Instance.IsPositionValid(targetPosition);
                    SmartLogger.Log($"[AbilityManager.IsTargetValid] Ground-targeting ability check - Position valid: {isValidPosition}", LogCategory.Ability);
                    return isValidPosition;
                }

                // If we get here, this is a unit-targeting ability, so we need a valid target unit
                if (targetUnit == null)
                {
                    SmartLogger.Log("[AbilityManager.IsTargetValid] FAILED: Unit-targeting ability requires valid target unit", LogCategory.Ability);
                    return false;
                }

                if (!targetUnit.IsAlive)
                {
                    SmartLogger.Log("[AbilityManager.IsTargetValid] FAILED: Target unit is not alive", LogCategory.Ability);
                    return false;
                }

                // Check unit targeting rules
                bool canTargetSelf = abilityData.targetsSelf && targetUnit == sourceUnit;
                bool canTargetAlly = abilityData.targetsAlly && targetUnit != sourceUnit && targetUnit.IsPlayer() == sourceUnit.IsPlayer();
                bool canTargetEnemy = abilityData.targetsEnemy && targetUnit.IsPlayer() != sourceUnit.IsPlayer();

                bool isValidTarget = canTargetSelf || canTargetAlly || canTargetEnemy;

                SmartLogger.Log($"[AbilityManager.IsTargetValid] Unit targeting check - CanTargetSelf: {canTargetSelf}, CanTargetAlly: {canTargetAlly}, CanTargetEnemy: {canTargetEnemy}, Final result: {isValidTarget}", LogCategory.Ability);

                return isValidTarget;
            }
            
            /// <summary>
            /// Handle damage and healing from ability
            /// </summary>
            private void HandleDamageAndHealing(AbilityData abilityData, DokkaebiUnit sourceUnit, DokkaebiUnit targetUnit, bool isOverload)
            {
                if (abilityData == null || sourceUnit == null || targetUnit == null)
                {
                    SmartLogger.LogWarning("[AbilityManager.HandleDamageAndHealing] Invalid parameters", LogCategory.Ability);
                    return;
                }

                // Handle damage
                if (abilityData.damageAmount > 0)
                {
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Base damage from ability data: {abilityData.damageAmount}", LogCategory.Ability);
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Source unit: {sourceUnit.GetUnitName()}", LogCategory.Ability);
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Target unit: {targetUnit.GetUnitName()}", LogCategory.Ability);

                    // Calculate final damage using the service
                    // The hit/miss logic is now handled within CombatCalculationService.CalculateFinalDamage
                    // and returns 0 if it's a miss or dodge.
                    int actualDamage = CombatCalculationService.CalculateFinalDamage(abilityData, sourceUnit, targetUnit, isOverload, false); // Pass 'false' for isCriticalHit here as it's calculated internally

                    // Log target's state before damage
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Target state BEFORE damage - Health: {targetUnit.GetCurrentHealth()}/{targetUnit.GetMaxHealth()}, Status Effects: {string.Join(", ", targetUnit.GetStatusEffects().Select(e => e.StatusEffectType.ToString()))}", LogCategory.Ability);

                    // Apply damage ONLY if actualDamage is greater than 0
                    if (actualDamage > 0)
                    {
                        SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Applying {actualDamage} damage to {targetUnit.GetUnitName()} at {targetUnit.GetGridPosition()} (Base: {abilityData.damageAmount}, DamageType: {abilityData.damageType}, Overload: {isOverload})", LogCategory.Ability);
                        targetUnit.TakeDamage(actualDamage, abilityData.damageType, sourceUnit);
                        // Log target's state after damage
                        SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Target state AFTER damage - Health: {targetUnit.GetCurrentHealth()}/{targetUnit.GetMaxHealth()}, Damage Applied: {actualDamage}, DamageType: {abilityData.damageType}", LogCategory.Ability);

                        // Play hit sound if available
                        if (abilityData.hitSound != null)
                        {
                            PlayAbilitySound(abilityData.hitSound);
                            SmartLogger.Log("[AbilityManager.HandleDamageAndHealing] Hit sound played", LogCategory.Ability);
                        }
                    }
                    else
                    {
                        SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Attack from {sourceUnit.GetUnitName()} on {targetUnit.GetUnitName()} resulted in 0 actual damage (Miss or Dodge). No damage applied.", LogCategory.Ability);
                    }

                    // Removed the specific Paradox Bolt Temporal Echo application from here.
                    // It is now handled directly within CombatCalculationService.CalculateFinalDamage
                    // conditional on damage being dealt.

                }

                // Handle healing
                if (abilityData.healAmount > 0)
                {
                    // Calculate final healing using the service
                    int actualHealing = CombatCalculationService.CalculateFinalHealing(abilityData, sourceUnit, targetUnit, isOverload);
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Calculated healing amount: {actualHealing}", LogCategory.Ability);

                    // Log target's state before healing
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Target state BEFORE healing - Health: {targetUnit.GetCurrentHealth()}/{targetUnit.GetMaxHealth()}", LogCategory.Ability);

                    // Apply healing and log the result
                    targetUnit.ModifyHealth(actualHealing);

                    // Log target's state after healing
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealing] Target state AFTER healing - Health: {targetUnit.GetCurrentHealth()}/{targetUnit.GetMaxHealth()}, Healing Applied: {actualHealing}", LogCategory.Ability);
                }
            }
            
            /// <summary>
            /// Get the effective range of an ability, considering environmental effects
            /// </summary>
            public int GetEffectiveRange(AbilityData abilityData, DokkaebiUnit sourceUnit)
            {
                if (abilityData == null || sourceUnit == null)
                {
                    SmartLogger.LogWarning("[AbilityManager.GetEffectiveRange] Invalid parameters", LogCategory.Ability);
                    return 0;
                }

                SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Source Unit: {sourceUnit.GetUnitName()} (ID: {sourceUnit.UnitId}) at position: {sourceUnit.GetGridPosition()}", LogCategory.Ability, this);

                int baseRange = abilityData.range;
                int totalBonus = 0;
                
                SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Checking for ZoneManager instance...", LogCategory.Ability, this);
                ZoneManager zoneManager = ZoneManager.Instance;
                if (zoneManager != null)
                {
                    SmartLogger.Log($"[AbilityManager.GetEffectiveRange] ZoneManager instance found. Checking if unit is within any active zone at {sourceUnit.GetGridPosition()}...", LogCategory.Ability, this);
                    // Find the first active zone instance that contains the unit's position
                    var zoneAtUnitPosition = zoneManager.FindActiveZoneInstanceAtPosition(sourceUnit.GetGridPosition());

                    if (zoneAtUnitPosition != null && zoneAtUnitPosition.IsActive)
                    {
                        SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Unit found within active zone: '{zoneAtUnitPosition.DisplayName}' (ID: {zoneAtUnitPosition.Id})", LogCategory.Ability, this);
                        // Check if the found zone is a Wind Zone (assuming DisplayName containing "Storm" indicates a Wind Zone)
                        bool isInWindZone = zoneAtUnitPosition.DisplayName.Contains("Storm");
                        SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Found zone '{zoneAtUnitPosition.DisplayName}' is a Wind Zone: {isInWindZone}", LogCategory.Ability, this);

                        if (isInWindZone)
                        {
                            SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Unit detected in Wind Zone (isInWindZone is true).", LogCategory.Ability, this);
                            // Check if the ability is Gale Arrow to apply the specific +2 bonus
                            if (abilityData.abilityId == "GaleArrow") // Assuming abilityId for Gale Arrow is "GaleArrow"
                            {
                                totalBonus += 2; // Apply the +2 range bonus for Gale Arrow in Wind Zones
                                SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Ability ID '{abilityData.abilityId}' matched 'GaleArrow'. Applying +2 bonus.", LogCategory.Ability, this);
                            }
                            else
                            {
                                // Keep the base +1 bonus for other abilities in Wind Zones if applicable (based on original logic)
                                totalBonus += windZoneRangeBonus;
                                SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Unit is in Wind Zone with non-Gale Arrow ability. Applying +1 bonus.", LogCategory.Ability, this);
                            }
                        }
                        else
                        {
                            SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Unit is in a zone, but it is NOT a Wind Zone.", LogCategory.Ability, this);
                        }
                    }
                    else
                    {
                        SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Unit is not within any active zone at {sourceUnit.GetGridPosition()}.", LogCategory.Ability, this);
                    }
                }
                else
                {
                    SmartLogger.LogWarning("[AbilityManager.GetEffectiveRange] ZoneManager.Instance is null! Cannot check for zones.", LogCategory.Ability, this);
                }
                
                // Check for status effect range modifiers
                float rangeModifier = StatusEffectSystem.GetStatModifier(sourceUnit, UnitAttributeType.AbilityRange);
                if (rangeModifier != 1.0f)
                {
                    int rangeBonus = Mathf.RoundToInt(baseRange * (rangeModifier - 1.0f));
                    totalBonus += rangeBonus;
                    SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Unit {sourceUnit.GetUnitName()} has range modifier {rangeModifier:P0} - Range bonus: {rangeBonus}", LogCategory.Ability);
                }

                int effectiveRange = baseRange + totalBonus;
                SmartLogger.Log($"[AbilityManager.GetEffectiveRange] Final range for {abilityData.displayName}: {effectiveRange} (Base: {baseRange}, Bonus: {totalBonus})", LogCategory.Ability);
                
                return effectiveRange;
            }

            /// <summary>
            /// Handle area of effect damage for ground-targeted abilities
            /// </summary>
            private void HandleAreaDamage(AbilityData abilityData, DokkaebiUnit sourceUnit, GridPosition centerPosition, bool isOverload)
            {
                if (abilityData == null || sourceUnit == null || !GridManager.Instance.IsValidGridPosition(centerPosition))
                {
                    SmartLogger.LogWarning("[AbilityManager.HandleAreaDamage] Invalid parameters", LogCategory.Ability);
                    return;
                }

                SmartLogger.Log($"[AbilityManager.HandleAreaDamage] Processing AoE for {abilityData.displayName} at {centerPosition} with radius {abilityData.areaOfEffect}", LogCategory.Ability);

                // Get all positions in range
                var affectedPositions = GridManager.Instance.GetGridPositionsInRange(centerPosition, abilityData.areaOfEffect);
                
                // Track units we've already hit to prevent double-hits
                HashSet<DokkaebiUnit> affectedUnits = new HashSet<DokkaebiUnit>();

                // Get source crit chance once for efficiency
                float criticalChance = StatusEffectSystem.GetStatModifier(sourceUnit, UnitAttributeType.CriticalChance);

                foreach (var pos in affectedPositions)
                {
                    // Get unit at position
                    var unit = UnitManager.Instance.GetUnitAtPosition(pos) as DokkaebiUnit;
                    
                    if (unit == null || !unit.IsAlive || affectedUnits.Contains(unit))
                    {
                        continue;
                    }

                    // Check targeting rules
                    bool canTargetUnit = (abilityData.targetsEnemy && unit.IsPlayer() != sourceUnit.IsPlayer()) ||
                                    (abilityData.targetsAlly && unit.IsPlayer() == sourceUnit.IsPlayer()) ||
                                    (abilityData.targetsSelf && unit == sourceUnit);

                    if (!canTargetUnit)
                    {
                        SmartLogger.Log($"[AbilityManager.HandleAreaDamage] Unit {unit.GetUnitName()} at {pos} is not a valid target due to targeting rules", LogCategory.Ability);
                        continue;
                    }

                    // Critical hit calculation for this unit
                    float randomValue = UnityEngine.Random.value;
                    bool isCriticalHit = randomValue <= criticalChance;

                    // Calculate and apply damage
                    int damage = CombatCalculationService.CalculateFinalDamage(abilityData, sourceUnit, unit, isOverload, false);
                    
                    if (damage > 0)
                    {
                        SmartLogger.Log($"[AbilityManager.HandleAreaDamage] Applying {damage} damage to {unit.GetUnitName()} at {pos}", LogCategory.Ability);
                        unit.TakeDamage(damage, abilityData.damageType, sourceUnit);
                        affectedUnits.Add(unit);

                        // Play VFX/SFX for each hit
                        PlayAbilityVFX(abilityData, sourceUnit, pos, unit);
                        PlayAbilitySound(abilityData.hitSound);
                    }
                }

                SmartLogger.Log($"[AbilityManager.HandleAreaDamage] AoE complete. Affected {affectedUnits.Count} units", LogCategory.Ability);
            }
            
            /// <summary>
            /// Apply status effects from ability
            /// </summary>
            private void ApplyStatusEffects(AbilityData abilityData, DokkaebiUnit sourceUnit, DokkaebiUnit targetUnit, bool isOverload, int? secondTargetUnitId = null)
            {
                if (targetUnit == null || !targetUnit.IsAlive || abilityData.appliedEffects == null || abilityData.appliedEffects.Count == 0)
                {
                    SmartLogger.Log("[AbilityManager.ApplyStatusEffects] Invalid parameters or no effects to apply", LogCategory.Ability);
                    return;
                }

                SmartLogger.Log($"[AbilityManager.ApplyStatusEffects] Applying {abilityData.appliedEffects.Count} effects from {abilityData.displayName} to {targetUnit.GetUnitName()}", LogCategory.Ability);

                foreach (var effectData in abilityData.appliedEffects)
                {
                    if (effectData != null)
                    {
                        int actualDuration = effectData.duration;
                        if (isOverload && abilityData.hasOverloadVariant)
                        {
                            actualDuration = Mathf.RoundToInt(actualDuration * abilityData.overloadEffectDurationMultiplier);
                            SmartLogger.Log($"[AbilityManager.ApplyStatusEffects] Overload active: Effect {effectData.displayName} duration increased from {effectData.duration} to {actualDuration}", LogCategory.Ability);
                        }

                        // Determine the linked unit ID to pass, only for the FateLinked effect when secondTargetUnitId is available
                        int? idToPassAsLinked = null;
                        if (effectData.effectId == "FateLinked" && secondTargetUnitId.HasValue)
                        {
                            idToPassAsLinked = secondTargetUnitId.Value;
                            SmartLogger.Log($"[AbilityManager.ApplyStatusEffects] Identified FateLinked effect. Will pass second target unit ID {idToPassAsLinked} as linkedUnitId.", LogCategory.Ability);
                        }
                        else
                        {
                            SmartLogger.Log($"[AbilityManager.ApplyStatusEffects] Identified non-FateLinked effect '{effectData.displayName}'. No linkedUnitId to pass.", LogCategory.Ability);
                        }

                        // Call the StatusEffectSystem.ApplyStatusEffect method.
                        StatusEffectSystem.ApplyStatusEffect(targetUnit, effectData, actualDuration, sourceUnit, idToPassAsLinked);

                        var currentEffects = targetUnit.GetStatusEffects();
                        SmartLogger.Log($"[AbilityManager.ApplyStatusEffects] {targetUnit.GetUnitName()} now has {currentEffects.Count} active effects: {string.Join(", ", currentEffects.Select(e => e.StatusEffectType))}", LogCategory.Ability);
                    }
                }
            }
            
            /// <summary>
            /// Create a zone from ability
            /// </summary>
            private void CreateZone(AbilityData abilityData, GridPosition position, DokkaebiUnit sourceUnit, bool isOverload)
            {
                if (!abilityData.createsZone || abilityData.zoneToCreate == null)
                {
                    SmartLogger.Log("No zone to create or invalid zone data", LogCategory.Ability);
                    return;
                }

                var zoneManager = ZoneManager.Instance;
                if (zoneManager == null)
                {
                    SmartLogger.LogError("ZoneManager instance not found", LogCategory.Ability);
                    return;
                }

                // Determine base duration
                int zoneDuration = abilityData.zoneDuration > 0 ? abilityData.zoneDuration : abilityData.zoneToCreate.defaultDuration;
                
                // Apply overload multiplier if applicable
                if (isOverload && abilityData.hasOverloadVariant)
                {
                    zoneDuration = Mathf.RoundToInt(zoneDuration * abilityData.overloadZoneEffectMultiplier);
                    SmartLogger.Log($"Overload active: Zone duration increased to {zoneDuration} turns", LogCategory.Ability);
                }
                
                zoneManager.CreateZone(position, abilityData.zoneToCreate, sourceUnit, abilityData, zoneDuration);
                SmartLogger.Log($"Created {abilityData.zoneToCreate.displayName} zone at {position} lasting {zoneDuration} turns", LogCategory.Ability);
            }
            
            /// <summary>
            /// Play ability visual effects
            /// </summary>
            private void PlayAbilityVFX(AbilityData abilityData, DokkaebiUnit sourceUnit, GridPosition targetPosition, DokkaebiUnit targetUnit)
            {
                if (abilityData == null)
                {
                    SmartLogger.LogWarning("[AbilityManager.PlayAbilityVFX] Cannot play VFX: abilityData is null", LogCategory.Ability);
                    return;
                }

                if (abilityData.abilityVFXPrefab == null)
                {
                    SmartLogger.LogWarning($"[AbilityManager.PlayAbilityVFX] VFX prefab missing for ability '{abilityData.displayName}'. Assign it in the Unity Inspector.", LogCategory.Ability);
                    return;
                }

                Vector3 worldPosition = targetPosition != null 
                    ? GridManager.Instance.GridToWorldPosition(targetPosition)
                    : sourceUnit.transform.position;
                
                // Instantiate the VFX prefab
                GameObject vfxInstance = Instantiate(abilityData.abilityVFXPrefab, worldPosition, Quaternion.identity);
                
                // Destroy after some time
                Destroy(vfxInstance, 3f);
            }
            
            /// <summary>
            /// Play ability sound effects
            /// </summary>
            private void PlayAbilitySound(AudioClip clip)
            {
                if (clip != null && sfxPlayer != null)
                {
                    sfxPlayer.clip = clip;
                    sfxPlayer.Play();
                }
            }
            
            /// <summary>
            /// Execute tactical repositioning for a unit
            /// </summary>
            public bool ExecuteTacticalRepositioning(DokkaebiUnit unit, GridPosition targetPosition)
            {
                if (unit == null)
                {
                    SmartLogger.LogError("Invalid unit for tactical repositioning", LogCategory.Ability);
                    return false;
                }
                
                // Check if the target position is valid
                GridPosition currentPosition = unit.GetGridPosition();
                
                // Calculate Manhattan distance
                int distance = Mathf.Abs(targetPosition.x - currentPosition.x) + 
                            Mathf.Abs(targetPosition.z - currentPosition.z);
                
                // Ensure the distance is within the tactical repositioning range (1-2 tiles)
                if (distance < 1 || distance > 2)
                {
                    SmartLogger.LogWarning($"Invalid tactical repositioning distance ({distance})", LogCategory.Ability);
                    return false;
                }
                
                // Check if the target position is passable
                // This would typically check with GridManager if the tile is passable
                
                // Move the unit to the target position
                unit.SetGridPosition(targetPosition);
                
                SmartLogger.Log($"Unit {unit.GetUnitName()} repositioned to {targetPosition}", LogCategory.Ability);
                return true;
            }

            /// <summary>
            /// Handle pushing a target unit away from the source unit
            /// </summary>
            private void HandlePushEffect(AbilityData abilityData, DokkaebiUnit sourceUnit, DokkaebiUnit targetUnit)
            {
                if (targetUnit == null || sourceUnit == null)
                {
                    SmartLogger.LogWarning("[AbilityManager.HandlePushEffect] Cannot push: source or target unit is null", LogCategory.Ability);
                    return;
                }

                // Get push direction from source to target
                GridPosition sourcePos = sourceUnit.GetGridPosition();
                GridPosition targetPos = targetUnit.GetGridPosition();
                
                // Calculate normalized direction vector
                int dx = targetPos.x - sourcePos.x;
                int dz = targetPos.z - sourcePos.z;
                Vector2Int pushDir = new Vector2Int(System.Math.Sign(dx), System.Math.Sign(dz));

                SmartLogger.Log($"[AbilityManager.HandlePushEffect] Attempting to push {targetUnit.GetUnitName()} from {targetPos} in direction {pushDir}", LogCategory.Ability);

                // Find furthest valid landing spot
                GridPosition landingSpot = targetPos;
                bool foundValidSpot = false;

                for (int i = 1; i <= abilityData.pushDistance; i++)
                {
                    GridPosition testPos = new GridPosition(
                        targetPos.x + (pushDir.x * i),
                        targetPos.z + (pushDir.y * i)
                    );

                    // Validate the test position
                    if (!GridManager.Instance.IsValidGridPosition(testPos))
                    {
                        SmartLogger.Log($"[AbilityManager.HandlePushEffect] Position {testPos} is invalid - stopping push search", LogCategory.Ability);
                        break;
                    }

                    if (GridManager.Instance.IsTileOccupied(testPos))
                    {
                        SmartLogger.Log($"[AbilityManager.HandlePushEffect] Position {testPos} is occupied - stopping push search", LogCategory.Ability);
                        break;
                    }

                    // Valid spot found
                    landingSpot = testPos;
                    foundValidSpot = true;
                    SmartLogger.Log($"[AbilityManager.HandlePushEffect] Found valid push position {testPos} at distance {i}", LogCategory.Ability);
                }

                // Only push if we found a different spot
                if (foundValidSpot && landingSpot != targetPos)
                {
                    SmartLogger.Log($"[AbilityManager.HandlePushEffect] Pushing {targetUnit.GetUnitName()} from {targetPos} to {landingSpot}", LogCategory.Ability);
                    targetUnit.SetGridPosition(landingSpot);
                    PlayAbilityVFX(abilityData, sourceUnit, landingSpot, targetUnit);
                    PlayAbilitySound(abilityData.hitSound);
                }
                else
                {
                    SmartLogger.Log($"[AbilityManager.HandlePushEffect] No valid push position found for {targetUnit.GetUnitName()}", LogCategory.Ability);
                }
            }

            /// <summary>
            /// Handle pushing a target unit exactly one tile away from the source unit
            /// </summary>
            private void HandleSingleTilePush(AbilityData abilityData, DokkaebiUnit sourceUnit, DokkaebiUnit targetUnit)
            {
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] ENTRY - Attempting to push {targetUnit?.GetUnitName()} exactly one tile away from {sourceUnit?.GetUnitName()}", LogCategory.Ability);

                if (targetUnit == null || sourceUnit == null)
                {
                    SmartLogger.LogWarning("[AbilityManager.HandleSingleTilePush] Cannot push: source or target unit is null", LogCategory.Ability);
                    return;
                }

                // Get push direction from source to target
                GridPosition sourcePos = sourceUnit.GetGridPosition();
                GridPosition targetPos = targetUnit.GetGridPosition();
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Source Position: {sourcePos}, Target Position: {targetPos}", LogCategory.Ability);
                
                // Calculate normalized direction vector
                int dx = targetPos.x - sourcePos.x;
                int dz = targetPos.z - sourcePos.z;
                Vector2Int pushDir = new Vector2Int(System.Math.Sign(dx), System.Math.Sign(dz));
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Calculated Push Direction - dx: {dx}, dz: {dz}, Normalized Direction: {pushDir}", LogCategory.Ability);

                // Calculate exact push destination (exactly 1 tile away)
                GridPosition pushDestination = new GridPosition(
                    targetPos.x + pushDir.x,
                    targetPos.z + pushDir.y
                );
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Calculated Push Destination: {pushDestination} (Direction: {pushDir})", LogCategory.Ability);

                // Validate the destination tile
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Checking if destination {pushDestination} is a valid grid position...", LogCategory.Ability);
                bool isValidPosition = GridManager.Instance.IsValidGridPosition(pushDestination);
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Position validity check result: {isValidPosition}", LogCategory.Ability);

                if (!isValidPosition)
                {
                    SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Push failed: Destination {pushDestination} is not a valid grid position", LogCategory.Ability);
                    return;
                }

                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Checking if destination {pushDestination} is occupied...", LogCategory.Ability);
                bool isOccupied = GridManager.Instance.IsTileOccupied(pushDestination);
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Tile occupancy check result: {isOccupied}", LogCategory.Ability);

                if (isOccupied)
                {
                    SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Push failed: Destination {pushDestination} is occupied", LogCategory.Ability);
                    return;
                }

                // Execute the push
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] All checks passed. Executing push of {targetUnit.GetUnitName()} from {targetPos} to {pushDestination}", LogCategory.Ability);
                targetUnit.SetGridPosition(pushDestination);
                PlayAbilityVFX(abilityData, sourceUnit, pushDestination, targetUnit);
                PlayAbilitySound(abilityData.hitSound);
                SmartLogger.Log($"[AbilityManager.HandleSingleTilePush] Push completed successfully. Unit {targetUnit.GetUnitName()} moved to {pushDestination}", LogCategory.Ability);
            }

            /// <summary>
            /// Find the best adjacent position to dash to near a target
            /// </summary>
            private GridPosition FindBestDashPosition(DokkaebiUnit sourceUnit, DokkaebiUnit targetUnit)
            {
                // --- DEBUG LOG: Confirm entering FindBestDashPosition ---
                Debug.Log("[DEBUG] FindBestDashPosition CALLED");
                SmartLogger.Log($"[AbilityManager.FindBestDashPosition] ENTRY - Source: {sourceUnit?.GetUnitName()} (ID: {sourceUnit?.UnitId}, Pos: {sourceUnit?.GetGridPosition()}), Target: {targetUnit?.GetUnitName()} (ID: {targetUnit?.UnitId}, Pos: {targetUnit?.GetGridPosition()})", LogCategory.Ability);

                if (sourceUnit == null || targetUnit == null)
                {
                    SmartLogger.LogError("[AbilityManager.FindBestDashPosition] FAILED: Source or target unit is null", LogCategory.Ability);
                    return GridPosition.invalid;
                }

                GridPosition sourcePos = sourceUnit.GetGridPosition();
                GridPosition targetPos = targetUnit.GetGridPosition();
                SmartLogger.Log($"[AbilityManager.FindBestDashPosition] Source Position: {sourcePos}, Target Position: {targetPos}", LogCategory.Ability);

                // Get all adjacent positions (including diagonals) around the target
                var adjacentPositions = GridManager.Instance.GetNeighborPositions(targetUnit.GetGridPosition(), includeDiagonals: true);
                SmartLogger.Log($"[AbilityManager.FindBestDashPosition] Found {adjacentPositions.Count} potential adjacent positions to check around target at {targetPos}: {string.Join(", ", adjacentPositions)}", LogCategory.Ability);

                GridPosition bestDashPosition = GridPosition.invalid;
                int shortestDistance = int.MaxValue;
                bool foundValidPosition = false; // Flag to track if ANY valid position was found

                foreach (var pos in adjacentPositions)
                {
                    // --- MODIFIED: Explicitly skip the target's own tile ---
                    if (pos == targetPos)
                    {
                        SmartLogger.LogWarning($"[AbilityManager.FindBestDashPosition] - Checking pos {pos}: WARNING - This is the target unit's position!", LogCategory.Ability, this);
                        SmartLogger.Log($"  REJECTED: Position {pos} is the target unit's own tile (cannot dash onto target)", LogCategory.Ability);
                        continue;
                    }
                    // --- END MODIFIED ---

                    SmartLogger.Log($"\n[AbilityManager.FindBestDashPosition] === Checking Position {pos} ===", LogCategory.Ability);

                    bool isValidGridPosition = GridManager.Instance.IsValidGridPosition(pos);
                    SmartLogger.Log($"  1. IsValidGridPosition({pos}): {isValidGridPosition}", LogCategory.Ability);
                    if (!isValidGridPosition)
                    {
                        SmartLogger.Log($"  REJECTED: Position {pos} is not a valid grid position", LogCategory.Ability);
                        continue;
                    }

                    bool isWalkable = GridManager.Instance.IsWalkable(pos, sourceUnit);
                    SmartLogger.Log($"  2. IsWalkable({pos}, ignoring {sourceUnit.GetUnitName()}): {isWalkable}", LogCategory.Ability);
                    if (!isWalkable)
                    {
                        SmartLogger.Log($"  REJECTED: Position {pos} is not walkable for source unit", LogCategory.Ability);
                        continue;
                    }

                    var occupyingUnit = UnitManager.Instance.GetUnitAtPosition(pos);
                    bool isOccupied = occupyingUnit != null && occupyingUnit.UnitId != sourceUnit.UnitId;
                    // --- ADDED LOG: Show occupancy and isOccupied status ---
                    SmartLogger.Log($"[AbilityManager.FindBestDashPosition] - Checking pos {pos}: Occupying unit: {(occupyingUnit != null ? occupyingUnit.GetUnitName() : "None")}, IsOccupied (excluding source): {isOccupied}", LogCategory.Ability, this);
                    if (isOccupied)
                    {
                        SmartLogger.Log($"  REJECTED: Position {pos} is occupied by another unit ({occupyingUnit.GetUnitName()})", LogCategory.Ability);
                        continue;
                    }

                    // If the source unit is already at this adjacent position, it's not a position to dash *to*.
                    if (pos == sourcePos)
                    {
                        SmartLogger.Log($"  REJECTED: Position {pos} is the source unit's current position", LogCategory.Ability);
                        continue;
                    }

                    // Valid position found
                    foundValidPosition = true; // Mark that at least one valid position was found

                    int distance = GridPosition.GetManhattanDistance(sourcePos, pos);
                    SmartLogger.Log($"  4. Manhattan distance from source {sourcePos} to candidate {pos}: {distance} tiles (Current best: {shortestDistance})", LogCategory.Ability);

                    if (distance < shortestDistance)
                    {
                        shortestDistance = distance;
                        bestDashPosition = pos;
                        // --- ADDED LOG: New best dash position found ---
                        SmartLogger.Log($"[AbilityManager.FindBestDashPosition] - New best dash position found: {bestDashPosition} at distance {distance}", LogCategory.Ability, this);
                    }
                    else if (distance == shortestDistance)
                    {
                        SmartLogger.Log($"  Position {pos} is at the same distance {distance} as current best {bestDashPosition}. Keeping existing best.", LogCategory.Ability);
                    }
                    else
                    {
                        SmartLogger.Log($"  Position {pos} is farther than current best ({distance} > {shortestDistance})", LogCategory.Ability);
                    }
                }

                if (bestDashPosition == GridPosition.invalid)
                {
                    SmartLogger.LogWarning("[AbilityManager.FindBestDashPosition] FAILED: No valid dash position found among adjacent tiles.", LogCategory.Ability);
                    SmartLogger.Log($"[AbilityManager.FindBestDashPosition] Summary of checked positions:", LogCategory.Ability);
                    foreach (var pos in adjacentPositions)
                    {
                        bool isValid = GridManager.Instance.IsValidGridPosition(pos);
                        bool isWalkable = isValid && GridManager.Instance.IsWalkable(pos, sourceUnit);
                        var occupyingUnitCheck = isValid ? UnitManager.Instance.GetUnitAtPosition(pos) : null;
                        bool isOccupiedExcludingSource = isValid && occupyingUnitCheck != null && occupyingUnitCheck.UnitId != sourceUnit.UnitId;
                        bool isSourcePos = pos == sourcePos;
                        bool isTargetPos = pos == targetPos;
                        SmartLogger.Log($"  Position {pos}: Valid Grid={isValid}, Walkable(for source)={isWalkable}, Occupied(excl. source)={isOccupiedExcludingSource}, Is Source Pos={isSourcePos}, Is Target Pos={isTargetPos}", LogCategory.Ability);
                    }
                }
                else
                {
                    SmartLogger.Log($"[AbilityManager.FindBestDashPosition] SUCCESS: Best dash position found at {bestDashPosition} (Closest distance to source: {shortestDistance})", LogCategory.Ability);
                }

                // --- ADDED LOG: Final return value ---
                SmartLogger.Log($"[AbilityManager.FindBestDashPosition] END - Returning best dash position: {bestDashPosition}", LogCategory.Ability, this);
                return bestDashPosition;
            }

            /// <summary>
            /// Execute the Heatwave Counter ability
            /// </summary>
            private void ExecuteHeatwaveCounter(AbilityData abilityData, DokkaebiUnit sourceUnit, bool isOverload)
            {
                if (abilityData == null || sourceUnit == null)
                {
                    SmartLogger.LogWarning("[AbilityManager.ExecuteHeatwaveCounter] Invalid parameters", LogCategory.Ability);
                    return;
                }

                // Get the Heatwave Counter effect data
                if (abilityData.appliedEffects == null || !abilityData.appliedEffects.Any())
                {
                    SmartLogger.LogError("[AbilityManager.ExecuteHeatwaveCounter] No status effects found for Heatwave Counter", LogCategory.Ability);
                    return;
                }

                // Apply both the Armor and ReactiveDamage effects
                SmartLogger.Log($"[AbilityManager.ExecuteHeatwaveCounter] Applying Heatwave Counter effects to {sourceUnit.DisplayName}", LogCategory.Ability);
                
                // Apply all effects to the source unit
                foreach (var effectData in abilityData.appliedEffects)
                {
                    StatusEffectSystem.ApplyStatusEffect(sourceUnit, effectData, effectData.duration, sourceUnit);
                }

                // Play VFX/SFX
                PlayAbilityVFX(abilityData, sourceUnit, sourceUnit.CurrentGridPosition, sourceUnit);
                PlayAbilitySound(abilityData.castSound);

                // Consume aura cost
                sourceUnit.ModifyUnitAura(-abilityData.auraCost);

                // Set cooldown
                sourceUnit.SetAbilityCooldown(abilityData.abilityId, abilityData.cooldownTurns);

                SmartLogger.Log($"[AbilityManager.ExecuteHeatwaveCounter] Successfully applied Heatwave Counter to {sourceUnit.DisplayName}", LogCategory.Ability);
            }

            /// <summary>
            /// Find the closest valid adjacent tile to a target unit
            /// </summary>
            private GridPosition FindClosestAdjacentTile(DokkaebiUnit sourceUnit, DokkaebiUnit targetUnit)
            {
                if (sourceUnit == null || targetUnit == null || GridManager.Instance == null)
                {
                    SmartLogger.LogError("[AbilityManager.FindClosestAdjacentTile] Invalid parameters", LogCategory.Ability);
                    return GridPosition.invalid;
                }

                GridPosition targetPos = targetUnit.GetGridPosition();
                GridPosition sourcePos = sourceUnit.GetGridPosition();
                GridPosition bestPosition = GridPosition.invalid;
                int shortestDistance = int.MaxValue;

                // Get all adjacent positions (including diagonals)
                var adjacentPositions = GridManager.Instance.GetNeighborPositions(targetPos, includeDiagonals: true);
                SmartLogger.Log($"[AbilityManager.FindClosestAdjacentTile] Checking {adjacentPositions.Count} adjacent positions around target at {targetPos}", LogCategory.Ability);

                int validPositions = 0;
                int invalidGridPositions = 0;
                int unwalkablePositions = 0;
                int occupiedPositions = 0;

                foreach (var pos in adjacentPositions)
                {
                    SmartLogger.Log($"\n[AbilityManager.FindClosestAdjacentTile] === Checking position {pos} ===", LogCategory.Ability);

                    // Check if position is valid
                    if (!GridManager.Instance.IsValidGridPosition(pos))
                    {
                        invalidGridPositions++;
                        SmartLogger.Log($"[AbilityManager.FindClosestAdjacentTile] Position {pos} is not a valid grid position (out of bounds)", LogCategory.Ability);
                        continue;
                    }

                    // Check if walkable
                    if (!GridManager.Instance.IsWalkable(pos, null))
                    {
                        unwalkablePositions++;
                        SmartLogger.Log($"[AbilityManager.FindClosestAdjacentTile] Position {pos} is not walkable (obstacle or terrain)", LogCategory.Ability);
                        continue;
                    }

                    // Check if occupied
                    var occupyingUnit = UnitManager.Instance.GetUnitAtPosition(pos);
                    if (occupyingUnit != null)
                    {
                        occupiedPositions++;
                        SmartLogger.Log($"[AbilityManager.FindClosestAdjacentTile] Position {pos} is occupied by {occupyingUnit.GetUnitName()}", LogCategory.Ability);
                        continue;
                    }

                    // Valid position found
                    validPositions++;
                    
                    // Calculate distance from source unit to this position
                    int distance = GridPosition.GetManhattanDistance(sourcePos, pos);
                    SmartLogger.Log($"[AbilityManager.FindClosestAdjacentTile] Valid position {pos} found. Distance from source: {distance} (Current best: {shortestDistance})", LogCategory.Ability);

                    if (distance < shortestDistance)
                    {
                        shortestDistance = distance;
                        bestPosition = pos;
                        SmartLogger.Log($"[AbilityManager.FindClosestAdjacentTile] New best position found: {pos} with distance {distance}", LogCategory.Ability);
                    }
                }

                // Log summary
                SmartLogger.Log($"[AbilityManager.FindClosestAdjacentTile] Position check summary:", LogCategory.Ability);
                SmartLogger.Log($"  Total adjacent positions: {adjacentPositions.Count}", LogCategory.Ability);
                SmartLogger.Log($"  Valid positions: {validPositions}", LogCategory.Ability);
                SmartLogger.Log($"  Invalid grid positions: {invalidGridPositions}", LogCategory.Ability);
                SmartLogger.Log($"  Unwalkable positions: {unwalkablePositions}", LogCategory.Ability);
                SmartLogger.Log($"  Occupied positions: {occupiedPositions}", LogCategory.Ability);
                SmartLogger.Log($"  Best position found: {(bestPosition != GridPosition.invalid ? bestPosition.ToString() : "None")}", LogCategory.Ability);

                return bestPosition;
            }

            /// <summary>
            /// Executes a reactive ability triggered by an event (e.g., being attacked).
            /// </summary>
            /// <param name="attackedUnit">The unit that was attacked and is reacting.</param>
            /// <param name="attacker">The unit that triggered the reaction.</param>
            /// <param name="reactiveAbilityData">The ability data for the reactive ability.</param>
            public void ExecuteReactiveAbility(DokkaebiUnit attackedUnit, IDokkaebiUnit attacker, AbilityData reactiveAbilityData)
            {
                // 1. Verification
                if (reactiveAbilityData == null || reactiveAbilityData.abilityId != "HeatwaveCounter")
                {
                    SmartLogger.LogWarning("[AbilityManager.ExecuteReactiveAbility] Called for non-HeatwaveCounter ability or null data.", LogCategory.Ability);
                    return;
                }

                // 2. Log Trigger
                SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] REACTION: {attackedUnit?.GetUnitName() ?? "NULL Unit"} is reacting to attack from {attacker?.GetUnitName() ?? "NULL Attacker"}", LogCategory.Ability);

                // 3. Remove Buff (expected to be handled by removeOnHit on the StatusEffectData)
                SmartLogger.Log("[AbilityManager.ExecuteReactiveAbility] Heatwave Counter Ready buff should be removed automatically by removeOnHit.", LogCategory.Ability);

                // 4. Find Dash Position
                if (!(attacker is DokkaebiUnit attackerUnit))
                {
                    SmartLogger.LogWarning("[AbilityManager.ExecuteReactiveAbility] Attacker is not a DokkaebiUnit. Cannot dash.", LogCategory.Ability);
                    return;
                }
                GridPosition attackerPos = attackerUnit.CurrentGridPosition;
                GridPosition attackedUnitOriginalPos = attackedUnit.CurrentGridPosition;
                var adjacentPositions = GridManager.Instance.GetNeighborPositions(attackerPos, includeDiagonals: true);

                GridPosition dashPosition = GridPosition.invalid;
                int shortestDistance = int.MaxValue;
                SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Evaluating dash positions around attacker at {attackerPos}. Attacked unit original pos: {attackedUnitOriginalPos}", LogCategory.Ability);
                foreach (var pos in adjacentPositions)
                {
                    if (!GridManager.Instance.IsValidGridPosition(pos))
                    {
                        SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Skipping {pos}: Not a valid grid position.", LogCategory.Ability);
                        continue;
                    }
                    if (!GridManager.Instance.IsWalkable(pos, attackedUnit))
                    {
                        SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Skipping {pos}: Not walkable for attacked unit.", LogCategory.Ability);
                        continue;
                    }
                    if (GridManager.Instance.IsTileOccupied(pos))
                    {
                        var occupyingUnit = UnitManager.Instance.GetUnitAtPosition(pos);
                        if (occupyingUnit != null && occupyingUnit != attackedUnit)
                        {
                            SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Skipping {pos}: Occupied by {occupyingUnit.GetUnitName()}.", LogCategory.Ability);
                            continue;
                        }
                    }
                    int distance = GridPosition.GetManhattanDistance(attackedUnitOriginalPos, pos);
                    SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Candidate dash pos: {pos}, distance from original: {distance}", LogCategory.Ability);
                    if (distance < shortestDistance)
                    {
                        shortestDistance = distance;
                        dashPosition = pos;
                        SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] New best dash position: {dashPosition} (distance: {shortestDistance})", LogCategory.Ability);
                    }
                }
                SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Final dash position selected: {dashPosition}", LogCategory.Ability);

                // 5. Execute Dash
                if (dashPosition != GridPosition.invalid)
                {
                    attackedUnit.SetGridPosition(dashPosition);
                    SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Dashed {attackedUnit.GetUnitName()} to {dashPosition} (adjacent to attacker)", LogCategory.Ability);

                    // Play VFX/SFX if configured
                    if (reactiveAbilityData.abilityVFXPrefab != null)
                    {
                        Vector3 worldPos = GridManager.Instance.GridToWorldPosition(dashPosition);
                        GameObject vfxInstance = GameObject.Instantiate(reactiveAbilityData.abilityVFXPrefab, worldPos, Quaternion.identity);
                        GameObject.Destroy(vfxInstance, 3f);
                        SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Played VFX at {worldPos}", LogCategory.Ability);
                    }
                    if (reactiveAbilityData.castSound != null)
                    {
                        AudioSource.PlayClipAtPoint(reactiveAbilityData.castSound, attackedUnit.transform.position);
                        SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] Played cast sound at {attackedUnit.transform.position}", LogCategory.Ability);
                    }
                }
                else
                {
                    SmartLogger.LogWarning("[AbilityManager.ExecuteReactiveAbility] No valid dash position found. Dash will not be performed.", LogCategory.Ability);
                }

                // 6. Deal Damage to Attacker
                if (attacker is DokkaebiUnit attackerDokkaebi)
                {
                    attackerDokkaebi.TakeDamage(reactiveAbilityData.damageAmount, reactiveAbilityData.damageType, attackedUnit);
                    SmartLogger.Log($"[AbilityManager.ExecuteReactiveAbility] {attackedUnit.GetUnitName()} dealt {reactiveAbilityData.damageAmount} {reactiveAbilityData.damageType} damage to {attacker.GetUnitName()}", LogCategory.Ability);
                }
                else
                {
                    SmartLogger.LogWarning("[AbilityManager.ExecuteReactiveAbility] Attacker is not a DokkaebiUnit. Cannot deal damage.", LogCategory.Ability);
                }
                // 7. No cooldown or aura consumption here. Buff removal is handled by removeOnHit.
            }

            // --- New Helper: ExecuteCoreAbilityEffects ---
            private void ExecuteCoreAbilityEffects(AbilityData abilityData, DokkaebiUnit sourceUnit, GridPosition targetPosition, DokkaebiUnit targetUnit, bool isOverload, float damageMultiplier, int? secondTargetUnitId = null)
            {
                // --- LOGGING FOR EXECOREABILITYEFFECTS ENTRY ---
                SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] ENTRY. Ability: {abilityData?.displayName ?? "NULL"}, Source: {sourceUnit?.GetUnitName() ?? "NULL"}.", LogCategory.Ability, this);
                // --- END LOGGING ---
                SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Executing core effects for '{abilityData?.displayName}' with damage multiplier {damageMultiplier:F2}", LogCategory.Ability, this);
                if (abilityData.targetsGround && abilityData.areaOfEffect > 0)
                {
                    SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Handling ground-targeted AoE with multiplier {damageMultiplier:F2}", LogCategory.Ability);
                    HandleAreaDamageWithMultiplier(abilityData, sourceUnit, targetPosition, isOverload, damageMultiplier);
                }
                else if (targetUnit != null)
                {
                    SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Handling single-target effects with multiplier {damageMultiplier:F2}", LogCategory.Ability);
                    HandleDamageAndHealingWithMultiplier(abilityData, sourceUnit, targetUnit, isOverload, damageMultiplier);
                    // Atemporal Echo: Apply Temporal Echo status effect after damage/healing
                    if (abilityData.abilityId == "AtemporalEcho" && targetUnit != null)
                    {
                        SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Handling Atemporal Echo specific effects on target {targetUnit.GetUnitName()}", LogCategory.Ability);
                        var temporalEchoEffectData = DataManager.Instance.GetStatusEffectData("TemporalEcho");
                        if (temporalEchoEffectData != null)
                        {
                            StatusEffectSystem.ApplyStatusEffect(targetUnit, temporalEchoEffectData, 1, sourceUnit);
                            SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Applied Temporal Echo status effect to {targetUnit.GetUnitName()}", LogCategory.Ability);
                            SmartLogger.Log("[AbilityManager.ExecuteCoreAbilityEffects] Temporal Echo bonus damage and cooldown reduction logic handled in CombatCalculationService.", LogCategory.Ability);
                        }
                        else
                        {
                            SmartLogger.LogError("[AbilityManager.ExecuteCoreAbilityEffects] TemporalEcho StatusEffectData not found in DataManager!", LogCategory.Ability);
                        }
                    }
                    // Push effect only on first hit (damageMultiplier == 1.0f)
                    if (damageMultiplier == 1.0f && abilityData.pushDistance > 0)
                    {
                        SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Handling push effect (first hit only)", LogCategory.Ability);
                        if (abilityData.pushDistance == 1)
                        {
                            HandleSingleTilePush(abilityData, sourceUnit, targetUnit);
                        }
                        else
                        {
                            HandlePushEffect(abilityData, sourceUnit, targetUnit);
                        }
                    }
                }
                // Status effects (apply on both hits)
                if (abilityData.appliedEffects != null && abilityData.appliedEffects.Count > 0 && abilityData.abilityId != "KarmicTether")
                {
                    SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Applying general status effects for non-Karmic Tether ability with multiplier {damageMultiplier:F2}", LogCategory.Ability, this);
                    ApplyStatusEffects(abilityData, sourceUnit, targetUnit, isOverload, secondTargetUnitId);
                }
                else if (abilityData.abilityId == "KarmicTether")
                {
                    SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Skipping general status effect application for Karmic Tether. FateLinked handling occurs in ExecuteAbility.", LogCategory.Ability, this);
                }
                // Zone creation only on first hit
                if (damageMultiplier == 1.0f && abilityData.createsZone && abilityData.zoneToCreate != null)
                {
                    SmartLogger.Log($"[AbilityManager.ExecuteCoreAbilityEffects] Creating zone (first hit only)", LogCategory.Ability);
                    CreateZone(abilityData, targetPosition, sourceUnit, isOverload);
                }
            }

            // --- New Helper: HandleDamageAndHealingWithMultiplier ---
            private void HandleDamageAndHealingWithMultiplier(AbilityData abilityData, DokkaebiUnit sourceUnit, DokkaebiUnit targetUnit, bool isOverload, float damageMultiplier)
            {
                SmartLogger.Log($"[AbilityManager.HandleDamageAndHealingWithMultiplier] START. Ability: {abilityData?.displayName}, Target: {targetUnit?.GetUnitName()}, Multiplier: {damageMultiplier:F2}", LogCategory.Ability);
                if (abilityData == null || sourceUnit == null || targetUnit == null)
                {
                    SmartLogger.LogWarning("[AbilityManager.HandleDamageAndHealingWithMultiplier] Invalid parameters", LogCategory.Ability);
                    return;
                }
                if (abilityData.damageAmount > 0)
                {
                    int actualDamage = CombatCalculationService.CalculateFinalDamage(abilityData, sourceUnit, targetUnit, isOverload, false, damageMultiplier);
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealingWithMultiplier] Applying {actualDamage} damage to {targetUnit.GetUnitName()} (Type: {abilityData.damageType})", LogCategory.Ability);
                    targetUnit.TakeDamage(actualDamage, abilityData.damageType, sourceUnit);
                    if (abilityData.hitSound != null)
                    {
                        PlayAbilitySound(abilityData.hitSound);
                    }
                }
                if (abilityData.healAmount > 0)
                {
                    int actualHealing = CombatCalculationService.CalculateFinalHealing(abilityData, sourceUnit, targetUnit, isOverload);
                    SmartLogger.Log($"[AbilityManager.HandleDamageAndHealingWithMultiplier] Applying {actualHealing} healing to {targetUnit.GetUnitName()}", LogCategory.Ability);
                    targetUnit.ModifyHealth(actualHealing);
                }
                SmartLogger.Log($"[AbilityManager.HandleDamageAndHealingWithMultiplier] END", LogCategory.Ability);
            }

            // --- New Helper: HandleAreaDamageWithMultiplier ---
            private void HandleAreaDamageWithMultiplier(AbilityData abilityData, DokkaebiUnit sourceUnit, GridPosition centerPosition, bool isOverload, float damageMultiplier)
            {
                SmartLogger.Log($"[AbilityManager.HandleAreaDamageWithMultiplier] START. Ability: {abilityData?.displayName}, Center: {centerPosition}, Multiplier: {damageMultiplier:F2}", LogCategory.Ability);
                if (abilityData == null || sourceUnit == null || !GridManager.Instance.IsValidGridPosition(centerPosition))
                {
                    SmartLogger.LogWarning("[AbilityManager.HandleAreaDamageWithMultiplier] Invalid parameters", LogCategory.Ability);
                    return;
                }
                var affectedPositions = GridManager.Instance.GetGridPositionsInRange(centerPosition, abilityData.areaOfEffect);
                HashSet<DokkaebiUnit> affectedUnits = new HashSet<DokkaebiUnit>();
                float criticalChance = StatusEffectSystem.GetStatModifier(sourceUnit, UnitAttributeType.CriticalChance);
                foreach (var pos in affectedPositions)
                {
                    var unit = UnitManager.Instance.GetUnitAtPosition(pos) as DokkaebiUnit;
                    if (unit == null || !unit.IsAlive || affectedUnits.Contains(unit))
                    {
                        continue;
                    }
                    bool canTargetUnit = (abilityData.targetsEnemy && unit.IsPlayer() != sourceUnit.IsPlayer()) ||
                                    (abilityData.targetsAlly && unit.IsPlayer() == sourceUnit.IsPlayer()) ||
                                    (abilityData.targetsSelf && unit == sourceUnit);
                    if (!canTargetUnit)
                    {
                        continue;
                    }
                    int actualDamage = CombatCalculationService.CalculateFinalDamage(abilityData, sourceUnit, unit, isOverload, false, damageMultiplier);
                    if (actualDamage > 0)
                    {
                        SmartLogger.Log($"[AbilityManager.HandleAreaDamageWithMultiplier] Applying {actualDamage} damage to {unit.GetUnitName()} at {pos} (Type: {abilityData.damageType})", LogCategory.Ability);
                        unit.TakeDamage(actualDamage, abilityData.damageType, sourceUnit);
                        affectedUnits.Add(unit);
                        PlayAbilityVFX(abilityData, sourceUnit, pos, unit);
                        if (abilityData.hitSound != null)
                        {
                            PlayAbilitySound(abilityData.hitSound);
                        }
                    }
                }
                SmartLogger.Log($"[AbilityManager.HandleAreaDamageWithMultiplier] END. Affected {affectedUnits.Count} units.", LogCategory.Ability);
            }

            /// <summary>
            /// Finds a valid position for a pull effect.
            /// Attempts to find a tile N tiles closer to the caster along the line between caster and target.
            /// </summary>
            /// <param name="casterPos">Caster's grid position.</param>
            /// <param name="targetPos">Target's current grid position.</param>
            /// <param name="pullDistance">How many tiles closer to pull.</param>
            /// <returns>The valid grid position to pull to, or GridPosition.invalid if no valid spot found.</returns>
            private GridPosition FindPullDestination(GridPosition casterPos, GridPosition targetPos, int pullDistance)
            {
                SmartLogger.Log($"[AbilityManager.FindPullDestination] ENTRY - Caster: {casterPos}, Target: {targetPos}, Pull Distance: {pullDistance}", LogCategory.Ability);

                if (pullDistance <= 0)
                {
                    SmartLogger.LogWarning("[AbilityManager.FindPullDestination] Pull distance must be positive.", LogCategory.Ability);
                    return GridPosition.invalid;
                }

                // Calculate the direction from target to caster
                int dx = casterPos.x - targetPos.x;
                int dz = casterPos.z - targetPos.z;

                // Normalize the direction (get -1, 0, or 1 in each component)
                int dirX = System.Math.Sign(dx);
                int dirZ = System.Math.Sign(dz);

                GridPosition potentialDestination = targetPos;
                for (int i = 0; i < pullDistance; i++)
                {
                    // Move one step closer
                    potentialDestination = new GridPosition(potentialDestination.x + dirX, potentialDestination.z + dirZ);

                    // Intermediate step check: is this tile valid and unoccupied?
                    if (!GridManager.Instance.IsValidGridPosition(potentialDestination) || GridManager.Instance.IsTileOccupied(potentialDestination))
                    {
                        SmartLogger.Log($"[AbilityManager.FindPullDestination] Intermediate tile {potentialDestination} is invalid or occupied. Pull blocked.", LogCategory.Ability);
                        return GridPosition.invalid; // Path blocked
                    }
                }

                // Final check: is the final destination valid and unoccupied? (redundant if intermediate checks pass, but safety)
                if (!GridManager.Instance.IsValidGridPosition(potentialDestination) || GridManager.Instance.IsTileOccupied(potentialDestination))
                {
                    SmartLogger.Log($"[AbilityManager.FindPullDestination] Final destination {potentialDestination} is invalid or occupied.", LogCategory.Ability);
                    return GridPosition.invalid; // Final destination blocked
                }

                SmartLogger.Log($"[AbilityManager.FindPullDestination] Valid pull destination found at {potentialDestination}.", LogCategory.Ability);
                return potentialDestination;
            }
        }
    } 