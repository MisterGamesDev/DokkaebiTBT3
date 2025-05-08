using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;
using Dokkaebi.Core;
using System.Linq;

namespace Dokkaebi.AI.Data.Actions
{
    [CreateAssetMenu(fileName = "HealAction", menuName = "Dokkaebi/AI/Actions/Heal")]
    public class HealAction : UseAbilityAction
    {
        private void OnValidate()
        {
            ActionName = "Heal";
            if (AbilityToUse != null)
                Cost = AbilityToUse.auraCost;
        }

        public override bool CheckPreconditions(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[HealAction] CheckPreconditions ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            if (!base.CheckPreconditions(currentState, agentUnit))
                return false;
            if (TargetUnitId == -1)
            {
                SmartLogger.Log("[HealAction] TargetUnitId is not set.", LogCategory.AI, this);
                return false;
            }
            var targetState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
            if (targetState == null || !targetState.IsAlive)
            {
                SmartLogger.Log("[HealAction] Target unit not found or not alive.", LogCategory.AI, this);
                return false;
            }
            var agentState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == agentUnit.UnitId);
            if (agentState == null)
                return false;
            if (targetState.TeamId != agentUnit.TeamId && targetState.UnitId != agentUnit.UnitId)
            {
                SmartLogger.Log("[HealAction] Target is not an ally or self.", LogCategory.AI, this);
                return false;
            }
            if (targetState.CurrentHP >= targetState.MaxHP)
            {
                SmartLogger.Log("[HealAction] Target is already at full HP.", LogCategory.AI, this);
                return false;
            }
            int dist = GridPosition.GetManhattanDistance(agentState.Position, targetState.Position);
            if (AbilityToUse != null && dist > AbilityToUse.range)
            {
                SmartLogger.Log($"[HealAction] Target out of range. Distance: {dist}, Range: {AbilityToUse.range}", LogCategory.AI, this);
                return false;
            }
            SmartLogger.Log("[HealAction] Preconditions met.", LogCategory.AI, this);
            return true;
        }

        public override AIWorldState ApplyEffects(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[HealAction] ApplyEffects ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            var simulatedState = base.ApplyEffects(currentState, agentUnit);
            var targetState = simulatedState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
            if (targetState != null && targetState.IsAlive && AbilityToUse != null)
            {
                int oldHP = targetState.CurrentHP;
                targetState.CurrentHP = Mathf.Min(targetState.MaxHP, targetState.CurrentHP + AbilityToUse.healAmount);
                SmartLogger.Log($"[HealAction] Simulated healing: {AbilityToUse.healAmount} to unit {targetState.UnitId}. HP: {oldHP} -> {targetState.CurrentHP}", LogCategory.AI, this);
                // Simulate status effects
                if (AbilityToUse.appliedEffects != null)
                {
                    foreach (var effect in AbilityToUse.appliedEffects)
                    {
                        if (!targetState.ActiveStatusEffects.Contains(effect.effectId))
                            targetState.ActiveStatusEffects.Add(effect.effectId);
                    }
                }
            }
            return simulatedState;
        }

        public override void Execute(IDokkaebiUnit agentUnit, AIWorldState currentState)
        {
            SmartLogger.Log($"[HealAction] Execute ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            var dokkaebiUnit = agentUnit as Dokkaebi.Units.DokkaebiUnit;
            if (dokkaebiUnit == null)
            {
                SmartLogger.LogError("[HealAction] agentUnit is not a DokkaebiUnit.", LogCategory.AI, this);
                return;
            }
            var abilities = dokkaebiUnit.GetAbilities();
            int abilityIndex = abilities.FindIndex(a => a != null && a.abilityId == AbilityToUse.abilityId);
            if (abilityIndex == -1)
            {
                SmartLogger.LogError($"[HealAction] AbilityToUse not found on agent unit.", LogCategory.AI, this);
                return;
            }
            var targetState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
            var targetPos = targetState != null ? targetState.Position : agentUnit.CurrentGridPosition;
            PlayerActionManager.Instance.ExecuteAbilityCommand(agentUnit.UnitId, abilityIndex, targetPos.ToVector2Int(), null, TargetUnitId);
            SmartLogger.Log($"[HealAction] ExecuteAbilityCommand called for unit {agentUnit.UnitId}, ability {AbilityToUse.displayName}, target {TargetUnitId}", LogCategory.AI, this);
        }
    }
} 