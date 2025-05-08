using System.Collections.Generic;
using UnityEngine;
using Dokkaebi.Grid;
using Dokkaebi.Units;
using Dokkaebi.Interfaces;
using Dokkaebi.Core.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Core;
using Dokkaebi.Common;

namespace Dokkaebi.Core.Networking.Commands
{
    /// <summary>
    /// Command for planning a unit's ability use
    /// </summary>
    public class AbilityCommand : CommandBase
    {
        public int UnitId { get; private set; }
        public int AbilityIndex { get; private set; }
        public Vector2Int TargetPosition { get; private set; }

        // Optional field for the targeted zone ID (for abilities like Terrain Shift)
        public int? TargetZoneId { get; private set; } = null;

        // Add support for a second target unit (nullable int)
        public int? SecondTargetUnitId { get; private set; } = null;

        // Required for deserialization
        public AbilityCommand() : base() { }

        public AbilityCommand(int unitId, int abilityIndex, Vector2Int targetPosition, int? targetZoneId = null, int? secondTargetUnitId = null) : base()
        {
            UnitId = unitId;
            AbilityIndex = abilityIndex;
            TargetPosition = targetPosition;
            TargetZoneId = targetZoneId;
            SecondTargetUnitId = secondTargetUnitId;
        }

        public override string CommandType => "ability";

        public override Dictionary<string, object> Serialize()
        {
            var data = base.Serialize();
            data["unitId"] = UnitId;
            data["abilityIndex"] = AbilityIndex;
            data["targetX"] = TargetPosition.x;
            data["targetY"] = TargetPosition.y;
            // Include TargetZoneId in serialization if it has a value
            if (TargetZoneId.HasValue)
            {
                data["targetZoneId"] = TargetZoneId.Value;
            }
            // Add serialization for SecondTargetUnitId
            if (SecondTargetUnitId.HasValue)
            {
                data["secondTargetUnitId"] = SecondTargetUnitId.Value;
            }
            return data;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            base.Deserialize(data);

            if (data.TryGetValue("unitId", out object unitIdObj))
            {
                if (unitIdObj is long unitIdLong)
                {
                    UnitId = (int)unitIdLong;
                }
                else if (unitIdObj is int unitIdInt)
                {
                    UnitId = unitIdInt;
                }
            }

            if (data.TryGetValue("abilityIndex", out object abilityIndexObj))
            {
                if (abilityIndexObj is long abilityIndexLong)
                {
                    AbilityIndex = (int)abilityIndexLong;
                }
                else if (abilityIndexObj is int abilityIndexInt)
                {
                    AbilityIndex = abilityIndexInt;
                }
            }

            int x = 0, y = 0;
            if (data.TryGetValue("targetX", out object xObj))
            {
                if (xObj is long xLong)
                {
                    x = (int)xLong;
                }
                else if (xObj is int xInt)
                {
                    x = xInt;
                }
            }

            if (data.TryGetValue("targetY", out object yObj))
            {
                if (yObj is long yLong)
                {
                    y = (int)yLong;
                }
                else if (yObj is int yInt)
                {
                    y = yInt;
                }
            }

            TargetPosition = new Vector2Int(x, y);

            // Deserialize TargetZoneId if it exists
            if (data.TryGetValue("targetZoneId", out object targetZoneIdObj))
            {
                if (targetZoneIdObj is long targetZoneIdLong)
                {
                    TargetZoneId = (int)targetZoneIdLong;
                }
                else if (targetZoneIdObj is int targetZoneIdInt)
                {
                    TargetZoneId = targetZoneIdInt;
                }
                else
                {
                    // Handle potential deserialization issues
                    SmartLogger.LogWarning($"[AbilityCommand.Deserialize] Could not deserialize targetZoneId. Expected int or long, got {targetZoneIdObj?.GetType().Name ?? "NULL"}.", LogCategory.Networking);
                    TargetZoneId = null;
                }
            }
            else
            {
                TargetZoneId = null;
            }
            // Add deserialization for secondTargetUnitId
            if (data.TryGetValue("secondTargetUnitId", out object secondTargetUnitIdObj))
            {
                if (secondTargetUnitIdObj is long secondTargetUnitIdLong)
                {
                    SecondTargetUnitId = (int)secondTargetUnitIdLong;
                }
                else if (secondTargetUnitIdObj is int secondTargetUnitIdInt)
                {
                    SecondTargetUnitId = secondTargetUnitIdInt;
                }
                else
                {
                    // Handle potential deserialization issues
                    SmartLogger.LogWarning($"[AbilityCommand.Deserialize] Could not deserialize secondTargetUnitId. Expected int or long, got {secondTargetUnitIdObj?.GetType().Name ?? "NULL"}.", LogCategory.Networking);
                    SecondTargetUnitId = null;
                }
            }
            else
            {
                SecondTargetUnitId = null;
            }
        }

        public override bool Validate()
        {
            SmartLogger.Log($"[AbilityCommand.Validate] --- Start Validation --- Unit: {UnitId}, AbilityIdx: {AbilityIndex}, Target: {TargetPosition}", LogCategory.Ability);
            
            var unitManager = UnitManager.Instance;
            SmartLogger.Log($"[AbilityCommand.Validate] Checking UnitManager... Found: {unitManager != null}", LogCategory.Ability);
            
            if (unitManager == null)
            {
                SmartLogger.LogWarning("[AbilityCommand.Validate] FAILED: UnitManager not found", LogCategory.Ability);
                return false;
            }

            var unit = unitManager.GetUnitById(UnitId);
            SmartLogger.Log($"[AbilityCommand.Validate] Checking Unit... Found: {(unit != null ? unit.GetUnitName() : "NULL")}, IsAI: {(unit != null ? !unit.IsPlayer() : false)}", LogCategory.Ability);
            
            if (unit == null)
            {
                SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Unit {UnitId} not found", LogCategory.Ability);
                return false;
            }

            // Get turn system core for phase and turn validation
            var turnSystemCore = DokkaebiTurnSystemCore.Instance;
            if (turnSystemCore == null)
            {
                SmartLogger.LogWarning("[AbilityCommand.Validate] FAILED: TurnSystemCore not found", LogCategory.Ability);
                return false;
            }

            // Check if we're in a valid phase for ability usage
            var currentPhase = turnSystemCore.CurrentPhase;
            bool isValidPhase = currentPhase == TurnPhase.AuraPhase1A || 
                               currentPhase == TurnPhase.AuraPhase1B || 
                               currentPhase == TurnPhase.AuraPhase2A || 
                               currentPhase == TurnPhase.AuraPhase2B;
            
            SmartLogger.Log($"[AbilityCommand.Validate] Phase Check - Current: {currentPhase}, IsValidPhase: {isValidPhase}", LogCategory.Ability);
            
            if (!isValidPhase)
            {
                SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Invalid phase {currentPhase} for ability usage", LogCategory.Ability);
                return false;
            }

            // For AI units, check if they can act in the current phase
            bool isAI = !unit.IsPlayer();
            if (isAI)
            {
                bool canActInPhase = (currentPhase == TurnPhase.AuraPhase1B && turnSystemCore.GetActivePlayer() == 1) ||
                                    (currentPhase == TurnPhase.AuraPhase2B && turnSystemCore.GetActivePlayer() == 2);
                
                SmartLogger.Log($"[AbilityCommand.Validate] AI Phase Check - Phase: {currentPhase}, ActivePlayer: {turnSystemCore.GetActivePlayer()}, CanActInPhase: {canActInPhase}", LogCategory.Ability);
                
                if (!canActInPhase)
                {
                    SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: AI unit cannot act in phase {currentPhase}", LogCategory.Ability);
                    return false;
                }
            }
            else
            {
                // For player units, check if they can act in the current phase
                bool canActInPhase = (currentPhase == TurnPhase.AuraPhase1A && turnSystemCore.GetActivePlayer() == 1) ||
                                    (currentPhase == TurnPhase.AuraPhase2A && turnSystemCore.GetActivePlayer() == 2);
                
                SmartLogger.Log($"[AbilityCommand.Validate] Player Phase Check - Phase: {currentPhase}, ActivePlayer: {turnSystemCore.GetActivePlayer()}, CanActInPhase: {canActInPhase}", LogCategory.Ability);
                
                if (!canActInPhase)
                {
                    SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Player unit cannot act in phase {currentPhase}", LogCategory.Ability);
                    return false;
                }
            }

            // Check if the unit can use aura abilities
            bool canUseAura = turnSystemCore.CanUnitUseAura(unit);
            SmartLogger.Log($"[AbilityCommand.Validate] Checking CanUseAura... Result: {canUseAura}", LogCategory.Ability);
            
            if (!canUseAura)
            {
                SmartLogger.LogWarning("[AbilityCommand.Validate] FAILED: Unit cannot use aura abilities", LogCategory.Ability);
                return false;
            }

            var abilities = unit.GetAbilities();
            var abilityIndexValid = AbilityIndex >= 0 && AbilityIndex < abilities.Count;
            SmartLogger.Log($"[AbilityCommand.Validate] Checking Ability Index... Index: {AbilityIndex}, Count: {abilities.Count}, Valid: {abilityIndexValid}", LogCategory.Ability);
            
            if (!abilityIndexValid)
            {
                SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Invalid ability index {AbilityIndex}", LogCategory.Ability);
                return false;
            }

            var ability = abilities[AbilityIndex];
            var isOnCooldownCheck = unit.IsOnCooldown(ability.abilityId);
            SmartLogger.Log($"[AbilityCommand.Validate] Checking Cooldown... AbilityId: {ability.abilityId}, IsOnCooldown: {isOnCooldownCheck}", LogCategory.Ability);
            
            if (isOnCooldownCheck)
            {
                SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Ability {ability.displayName} is on cooldown", LogCategory.Ability);
                return false;
            }

            // Check if unit can use ability (aura cost and cooldown)
            SmartLogger.Log($"[AbilityCommand.Validate] LOG_CHECK: About to call CanUseAbility for Ability: {ability?.displayName}", LogCategory.Ability);
            bool canUseResult = unit.CanUseAbility(ability);
            SmartLogger.Log($"[AbilityCommand.Validate] LOG_CHECK: CanUseAbility returned {canUseResult} for Ability: {ability?.displayName}", LogCategory.Ability);
            if (!canUseResult)
            {
                SmartLogger.LogWarning($"[AbilityCommand.Validate] LOG_CHECK: Validation returning FALSE due to CanUseAbility result.", LogCategory.Ability);
                return false;
            }

            // Convert positions and calculate grid-based distance
            GridPosition unitPos = unit.GetGridPosition();
            GridPosition targetGridPos = GridPosition.FromVector2Int(TargetPosition);
            int distance = GridPosition.GetManhattanDistance(unitPos, targetGridPos);
            // Get the effective range (this includes the Wind Zone bonus for Gale Arrow)
            int effectiveRange = AbilityManager.Instance != null
                ? AbilityManager.Instance.GetEffectiveRange(ability, unit)
                : ability.range; // Fallback to base range if AbilityManager instance is null
            bool rangeCheck = distance <= effectiveRange;
            SmartLogger.Log($"[AbilityCommand.Validate] Checking Range... Distance: {distance}, Effective Range: {effectiveRange}, InRange: {rangeCheck}", LogCategory.Ability);
            if (!rangeCheck)
            {
                SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Target position {TargetPosition} is out of effective range (effective range: {effectiveRange})", LogCategory.Ability);
                return false;
            }

            bool targetCheck = ValidateTarget(unit, ability, targetGridPos);
            SmartLogger.Log($"[AbilityCommand.Validate] Checking Target Validity... Valid: {targetCheck}", LogCategory.Ability);
            
            if (!targetCheck)
            {
                SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Invalid target for ability {ability.displayName}", LogCategory.Ability);
                return false;
            }

            // Add validation for the second target for Karmic Tether
            if (ability.abilityId == "KarmicTether" && SecondTargetUnitId.HasValue)
            {
                SmartLogger.Log($"[AbilityCommand.Validate] Validating second target for Karmic Tether. Second Target Unit ID: {SecondTargetUnitId.Value}", LogCategory.Ability);
                var secondTargetUnit = unitManager.GetUnitById(SecondTargetUnitId.Value);

                if (secondTargetUnit == null || !secondTargetUnit.IsAlive)
                {
                    SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Second target unit {SecondTargetUnitId.Value} not found or not alive for Karmic Tether.", LogCategory.Ability);
                    return false;
                }

                // Validate the second target unit type based on ability flags (targetsAlly, targetsEnemy)
                bool isSecondTargetAlly = secondTargetUnit.TeamId == unit.TeamId;
                bool secondTargetTypeIsValid = (isSecondTargetAlly && ability.targetsAlly) || (!isSecondTargetAlly && ability.targetsEnemy);

                if (!secondTargetTypeIsValid)
                {
                    SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: Second target unit {secondTargetUnit.GetUnitName()} type is invalid for Karmic Tether (IsAlly: {isSecondTargetAlly}, TargetsAlly: {ability.targetsAlly}, TargetsEnemy: {ability.targetsEnemy}).", LogCategory.Ability);
                    return false;
                }

                // Add a check to ensure the first and second targets are distinct units
                var firstTargetUnitAtPos = unitManager.GetUnitAtPosition(GridPosition.FromVector2Int(TargetPosition));
                if (firstTargetUnitAtPos != null && firstTargetUnitAtPos.UnitId == SecondTargetUnitId.Value)
                {
                    SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: First and second target units are the same for Karmic Tether.", LogCategory.Ability);
                    return false;
                }

                SmartLogger.Log("[AbilityCommand.Validate] Second target validation PASSED for Karmic Tether.", LogCategory.Ability);
            }
            // Ensure the first target is also a unit since Karmic Tether targets units.
            if (ability.abilityId == "KarmicTether" && !ability.targetsGround)
            {
                var firstTargetUnit = unitManager.GetUnitAtPosition(GridPosition.FromVector2Int(TargetPosition));
                if (firstTargetUnit == null || !firstTargetUnit.IsAlive)
                {
                    SmartLogger.LogWarning($"[AbilityCommand.Validate] FAILED: First target position {TargetPosition} does not contain a valid unit for Karmic Tether.", LogCategory.Ability);
                    return false;
                }
            }

            SmartLogger.Log("[AbilityCommand.Validate] --- Validation PASSED ---", LogCategory.Ability);
            return true;
        }

        private bool ValidateTarget(DokkaebiUnit unit, AbilityData ability, GridPosition targetPos)
        {
            SmartLogger.Log($"[AbilityCommand.ValidateTarget] Starting target validation for ability {ability.displayName}", LogCategory.Ability);
            
            var unitManager = UnitManager.Instance;
            SmartLogger.Log($"[AbilityCommand.ValidateTarget] Checking UnitManager... Found: {unitManager != null}", LogCategory.Ability);
            
            if (unitManager == null)
            {
                SmartLogger.LogWarning("[AbilityCommand.ValidateTarget] FAILED: UnitManager not found", LogCategory.Ability);
                return false;
            }

            // Check if the ability ONLY targets ground
            bool isGroundOnlyAbility = ability.targetsGround &&
                                      !ability.targetsSelf &&
                                      !ability.targetsAlly &&
                                      !ability.targetsEnemy;

            if (isGroundOnlyAbility)
            {
                // For ground-only abilities, we only need to check if the position is valid
                // Range check is handled in the main Validate() method
                bool isValidPosition = GridManager.Instance?.IsValidGridPosition(targetPos) ?? false;
                SmartLogger.Log($"[AbilityCommand.ValidateTarget] Ground-only ability check. IsValidPosition: {isValidPosition}", LogCategory.Ability);
                return isValidPosition;
            }

            var targetUnit = unitManager.GetUnitAtPosition(targetPos);
            SmartLogger.Log($"[AbilityCommand.ValidateTarget] Target unit at position {targetPos}: {(targetUnit != null ? targetUnit.GetUnitName() : "NULL")}", LogCategory.Ability);

            var isSelfTarget = targetUnit == unit;
            var canTargetSelf = ability.targetsSelf;
            SmartLogger.Log($"[AbilityCommand.ValidateTarget] Self-targeting check - Is self: {isSelfTarget}, Can target self: {canTargetSelf}, Unit IDs match: {targetUnit?.GetUnitId() == unit?.GetUnitId()}", LogCategory.Ability);
            
            if (isSelfTarget)
            {
                if (!canTargetSelf)
                {
                    SmartLogger.LogWarning("[AbilityCommand.ValidateTarget] FAILED: Cannot target self with this ability", LogCategory.Ability);
                    return false;
                }
                // If it's a valid self-target, we can return true immediately
                SmartLogger.Log("[AbilityCommand.ValidateTarget] Target validation PASSED (Self-target)", LogCategory.Ability);
                return true;
            }

            var isGroundTarget = targetUnit == null;
            var canTargetGround = ability.targetsGround;
            SmartLogger.Log($"[AbilityCommand.ValidateTarget] Ground-targeting check - Is ground: {isGroundTarget}, Can target ground: {canTargetGround}, Grid position valid: {GridManager.Instance?.IsValidGridPosition(targetPos) ?? false}", LogCategory.Ability);
            
            if (isGroundTarget && !canTargetGround)
            {
                SmartLogger.LogWarning("[AbilityCommand.ValidateTarget] FAILED: Cannot target ground with this ability", LogCategory.Ability);
                return false;
            }

            if (targetUnit != null)
            {
                var isAlly = targetUnit.IsPlayer() == unit.IsPlayer();
                SmartLogger.Log($"[AbilityCommand.ValidateTarget] Ally/Enemy check - Is ally: {isAlly}, Can target ally: {ability.targetsAlly}, Can target enemy: {ability.targetsEnemy}, Source IsPlayer: {unit.IsPlayer()}, Target IsPlayer: {targetUnit.IsPlayer()}", LogCategory.Ability);
                
                if (isAlly && !ability.targetsAlly)
                {
                    SmartLogger.LogWarning("[AbilityCommand.ValidateTarget] FAILED: Cannot target allies with this ability", LogCategory.Ability);
                    return false;
                }
                
                if (!isAlly && !ability.targetsEnemy)
                {
                    SmartLogger.LogWarning("[AbilityCommand.ValidateTarget] FAILED: Cannot target enemies with this ability", LogCategory.Ability);
                    return false;
                }
            }

            SmartLogger.Log("[AbilityCommand.ValidateTarget] Target validation PASSED", LogCategory.Ability);
            return true;
        }

        public override void Execute()
        {
            SmartLogger.Log($"[AbilityCommand.Execute] ENTRY. UnitId: {UnitId}, AbilityIndex: {AbilityIndex}, TargetPosition: {TargetPosition}", LogCategory.Ability);
            
            var unitManager = UnitManager.Instance;
            var abilityManager = UnityEngine.Object.FindObjectOfType<AbilityManager>();
            
            if (unitManager == null || abilityManager == null)
            {
                SmartLogger.LogError("[AbilityCommand.Execute] UnitManager or AbilityManager not found", LogCategory.Ability);
                return;
            }

            var unit = unitManager.GetUnitById(UnitId) as DokkaebiUnit;
            if (unit == null)
            {
                SmartLogger.LogError($"[AbilityCommand.Execute] Unit with ID {UnitId} not found", LogCategory.Ability);
                return;
            }

            var abilities = unit.GetAbilities();
            if (AbilityIndex < 0 || AbilityIndex >= abilities.Count)
            {
                SmartLogger.LogError($"[AbilityCommand.Execute] Invalid ability index {AbilityIndex} for unit {unit.GetUnitName()}", LogCategory.Ability);
                return;
            }

            var ability = abilities[AbilityIndex];
            if (ability == null)
            {
                SmartLogger.LogError($"[AbilityCommand.Execute] Ability at index {AbilityIndex} is null for unit {unit.GetUnitName()}", LogCategory.Ability);
                return;
            }

            SmartLogger.Log($"[AbilityCommand.Execute] Found ability: {ability.displayName} for unit {unit.GetUnitName()}", LogCategory.Ability);

            // Get target unit if any
            DokkaebiUnit targetUnit = null;
            if (TargetZoneId.HasValue)
            {
                targetUnit = unitManager.GetUnitById(TargetZoneId.Value) as DokkaebiUnit;
                SmartLogger.Log($"[AbilityCommand.Execute] Target unit found: {targetUnit?.GetUnitName() ?? "NULL"}", LogCategory.Ability);
            }
            else if (!ability.targetsGround)
            {
                // If no explicit target unit ID but ability requires a unit target, try to find unit at position
                targetUnit = unitManager.GetUnitAtPosition(GridPosition.FromVector2Int(TargetPosition)) as DokkaebiUnit;
                SmartLogger.Log($"[AbilityCommand.Execute] Target unit found at position: {targetUnit?.GetUnitName() ?? "NULL"}", LogCategory.Ability);
            }

            // Get second target unit if any
            DokkaebiUnit secondTargetUnit = null;
            if (SecondTargetUnitId.HasValue)
            {
                secondTargetUnit = unitManager.GetUnitById(SecondTargetUnitId.Value) as DokkaebiUnit;
                SmartLogger.Log($"[AbilityCommand.Execute] Second target unit found: {secondTargetUnit?.GetUnitName() ?? "NULL"}", LogCategory.Ability);
            }

            // Log ability execution details
            SmartLogger.Log($"[AbilityCommand.Execute] Executing ability with details:", LogCategory.Ability);
            SmartLogger.Log($"- Source Unit: {unit.GetUnitName()} (ID: {UnitId})", LogCategory.Ability);
            SmartLogger.Log($"- Ability: {ability.displayName} (Index: {AbilityIndex})", LogCategory.Ability);
            SmartLogger.Log($"- Target Position: {TargetPosition}", LogCategory.Ability);
            SmartLogger.Log($"- Target Unit: {targetUnit?.GetUnitName() ?? "None"} (ID: {TargetZoneId?.ToString() ?? "None"})", LogCategory.Ability);
            SmartLogger.Log($"- Second Target Unit: {secondTargetUnit?.GetUnitName() ?? "None"} (ID: {SecondTargetUnitId?.ToString() ?? "None"})", LogCategory.Ability);
            SmartLogger.Log($"- Target Zone ID: {TargetZoneId?.ToString() ?? "None"}", LogCategory.Ability);

            // Execute the ability
            SmartLogger.Log($"[AbilityCommand.Execute] Calling AbilityManager.ExecuteAbility...", LogCategory.Ability);
            bool success = abilityManager.ExecuteAbility(ability, unit, new GridPosition(TargetPosition.x, TargetPosition.y), targetUnit, false, TargetZoneId, SecondTargetUnitId);
            SmartLogger.Log($"[AbilityCommand.Execute] AbilityManager.ExecuteAbility result: {success}", LogCategory.Ability);
            SmartLogger.Log($"[AbilityCommand.Execute] EXIT", LogCategory.Ability);
        }
    }
} 