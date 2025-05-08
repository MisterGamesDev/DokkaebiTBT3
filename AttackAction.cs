using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;
using Dokkaebi.Core;
using System.Linq;

namespace Dokkaebi.AI.Data.Actions
{
    [CreateAssetMenu(fileName = "AttackAction", menuName = "Dokkaebi/AI/Actions/Attack")]
    public class AttackAction : UseAbilityAction
    {
        private void OnValidate()
        {
            ActionName = "Attack";
            if (AbilityToUse != null)
                Cost = AbilityToUse.auraCost;
        }

        public override bool CheckPreconditions(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[AttackAction] CheckPreconditions ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            
            // Log ability details if available
            if (AbilityToUse != null)
            {
                SmartLogger.Log($"[AttackAction] Checking ability: {AbilityToUse.displayName}, Damage: {AbilityToUse.damageAmount}, Range: {AbilityToUse.range}, Cost: {AbilityToUse.auraCost}", LogCategory.AI, this);
            }
            
            if (!base.CheckPreconditions(currentState, agentUnit))
            {
                SmartLogger.Log("[AttackAction] Base preconditions not met.", LogCategory.AI, this);
                return false;
            }
            
            if (TargetPosition == null || TargetPosition.Equals(GridPosition.invalid))
            {
                SmartLogger.Log("[AttackAction] TargetPosition is invalid.", LogCategory.AI, this);
                return false;
            }
            
            if (TargetUnitId == -1)
            {
                SmartLogger.Log("[AttackAction] TargetUnitId is not set.", LogCategory.AI, this);
                return false;
            }
            
            var targetState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
            if (targetState == null || !targetState.IsAlive)
            {
                SmartLogger.Log($"[AttackAction] Target unit {TargetUnitId} not found or not alive. Found: {targetState != null}, Alive: {targetState?.IsAlive ?? false}", LogCategory.AI, this);
                return false;
            }
            
            var agentState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == agentUnit.UnitId);
            if (agentState == null)
            {
                SmartLogger.Log($"[AttackAction] Agent unit state not found for unit {agentUnit.UnitId}", LogCategory.AI, this);
                return false;
            }
            
            if (targetState.IsPlayerControlled == agentUnit.IsPlayerControlled)
            {
                SmartLogger.Log($"[AttackAction] Target {TargetUnitId} is not an enemy. Target IsPlayerControlled: {targetState.IsPlayerControlled}, Agent IsPlayerControlled: {agentUnit.IsPlayerControlled}", LogCategory.AI, this);
                return false;
            }
            
            int dist = GridPosition.GetManhattanDistance(agentState.Position, TargetPosition);
            if (AbilityToUse != null && dist > AbilityToUse.range)
            {
                SmartLogger.Log($"[AttackAction] Target out of range. Distance: {dist}, Range: {AbilityToUse.range}, Agent Pos: {agentState.Position}, Target Pos: {TargetPosition}", LogCategory.AI, this);
                return false;
            }
            
            // Check if agent has enough aura
            if (AbilityToUse != null && agentState.CurrentAura < AbilityToUse.auraCost)
            {
                SmartLogger.Log($"[AttackAction] Not enough aura. Current: {agentState.CurrentAura}, Required: {AbilityToUse.auraCost}", LogCategory.AI, this);
                return false;
            }
            
            SmartLogger.Log($"[AttackAction] All preconditions met for unit {agentUnit.UnitId} targeting unit {TargetUnitId}. Distance: {dist}, Has Aura: {agentState.CurrentAura}/{AbilityToUse?.auraCost ?? 0}", LogCategory.AI, this);
            return true;
        }

        public override AIWorldState ApplyEffects(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[AttackAction] ApplyEffects ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            var simulatedState = base.ApplyEffects(currentState, agentUnit);
            var targetState = simulatedState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
            if (targetState != null && targetState.IsAlive && AbilityToUse != null)
            {
                int oldHP = targetState.CurrentHP;
                targetState.CurrentHP = Mathf.Max(0, targetState.CurrentHP - AbilityToUse.damageAmount);
                SmartLogger.Log($"[AttackAction] Simulated damage: {AbilityToUse.damageAmount} to unit {targetState.UnitId}. HP: {oldHP} -> {targetState.CurrentHP}", LogCategory.AI, this);
                // Simulate status effects
                if (AbilityToUse.appliedEffects != null)
                {
                    foreach (var effect in AbilityToUse.appliedEffects)
                    {
                        if (!targetState.ActiveStatusEffects.Contains(effect.effectId))
                            targetState.ActiveStatusEffects.Add(effect.effectId);
                    }
                }
                // Simulate zone creation
                if (AbilityToUse.createsZone)
                {
                    // Add a new AIZoneState (simplified, real implementation may need more data)
                    simulatedState.ZoneStates.Add(new AIZoneState
                    {
                        ZoneId = System.Guid.NewGuid().ToString(),
                        Position = TargetPosition,
                        ZoneType = AbilityToUse.displayName,
                        IsActive = true,
                        RemainingDuration = AbilityToUse.zoneDuration,
                        OwnerTeamId = agentUnit.TeamId
                    });
                    SmartLogger.Log($"[AttackAction] Simulated zone creation at {TargetPosition}", LogCategory.AI, this);
                }
            }
            return simulatedState;
        }

        public override void Execute(IDokkaebiUnit agentUnit, AIWorldState currentState)
        {
            SmartLogger.Log($"[AttackAction] Execute ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            
            // Log ability details
            if (AbilityToUse != null)
            {
                SmartLogger.Log($"[AttackAction] Executing ability: {AbilityToUse.displayName} on target {TargetUnitId} at position {TargetPosition}", LogCategory.AI, this);
                SmartLogger.Log($"[AttackAction] Ability details - Damage: {AbilityToUse.damageAmount}, Range: {AbilityToUse.range}, Cost: {AbilityToUse.auraCost}", LogCategory.AI, this);
            }
            
            var dokkaebiUnit = agentUnit as Dokkaebi.Units.DokkaebiUnit;
            if (dokkaebiUnit == null)
            {
                SmartLogger.LogError("[AttackAction] agentUnit is not a DokkaebiUnit.", LogCategory.AI, this);
                return;
            }
            
            var abilities = dokkaebiUnit.GetAbilities();
            int abilityIndex = abilities.FindIndex(a => a != null && a.abilityId == AbilityToUse.abilityId);
            if (abilityIndex == -1)
            {
                SmartLogger.LogError($"[AttackAction] AbilityToUse {AbilityToUse?.abilityId ?? "NULL"} not found on agent unit {agentUnit.UnitId}.", LogCategory.AI, this);
                return;
            }
            
            SmartLogger.Log($"[AttackAction] About to submit command for unit {agentUnit.UnitId} using ability {AbilityToUse.displayName} (index: {abilityIndex})", LogCategory.AI, this);
            SmartLogger.Log($"[AttackAction] Target details - Unit ID: {TargetUnitId}, Position: {TargetPosition}", LogCategory.AI, this);
            PlayerActionManager.Instance.ExecuteAbilityCommand(agentUnit.UnitId, abilityIndex, TargetPosition.ToVector2Int(), null, TargetUnitId);
            SmartLogger.Log($"[AttackAction] ExecuteAbilityCommand called for unit {agentUnit.UnitId}, ability {AbilityToUse.displayName}, target {TargetUnitId}", LogCategory.AI, this);
            SmartLogger.Log($"[AttackAction] Execute EXIT for unit {agentUnit.UnitId}", LogCategory.AI, this);
        }
    }
} 