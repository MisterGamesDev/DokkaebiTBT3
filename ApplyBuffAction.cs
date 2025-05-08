using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;
using Dokkaebi.Core;
using System.Linq;

namespace Dokkaebi.AI.Data.Actions
{
    [CreateAssetMenu(fileName = "ApplyBuffAction", menuName = "Dokkaebi/AI/Actions/Apply Buff")]
    public class ApplyBuffAction : UseAbilityAction
    {
        private void OnValidate()
        {
            ActionName = "Apply Buff";
            if (AbilityToUse != null)
                Cost = AbilityToUse.auraCost;
        }

        public override bool CheckPreconditions(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[ApplyBuffAction] CheckPreconditions ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            if (!base.CheckPreconditions(currentState, agentUnit))
                return false;
            if (TargetUnitId == -1)
            {
                SmartLogger.Log("[ApplyBuffAction] TargetUnitId is not set.", LogCategory.AI, this);
                return false;
            }
            var targetState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
            if (targetState == null || !targetState.IsAlive)
            {
                SmartLogger.Log("[ApplyBuffAction] Target unit not found or not alive.", LogCategory.AI, this);
                return false;
            }
            var agentState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == agentUnit.UnitId);
            if (agentState == null)
                return false;
            // Check valid target type
            bool validTarget = false;
            if (AbilityToUse.targetsSelf && targetState.UnitId == agentUnit.UnitId)
                validTarget = true;
            if (AbilityToUse.targetsAlly && targetState.TeamId == agentUnit.TeamId && targetState.UnitId != agentUnit.UnitId)
                validTarget = true;
            if (AbilityToUse.targetsEnemy && targetState.TeamId != agentUnit.TeamId)
                validTarget = true;
            if (!validTarget)
            {
                SmartLogger.Log("[ApplyBuffAction] Target is not a valid type for this buff.", LogCategory.AI, this);
                return false;
            }
            // Check for redundant buff
            if (AbilityToUse.appliedEffects != null && AbilityToUse.appliedEffects.Any())
            {
                var effectId = AbilityToUse.appliedEffects[0].effectId;
                if (targetState.ActiveStatusEffects.Contains(effectId))
                {
                    SmartLogger.Log($"[ApplyBuffAction] Target already has buff {effectId}.", LogCategory.AI, this);
                    return false;
                }
            }
            int dist = GridPosition.GetManhattanDistance(agentState.Position, targetState.Position);
            if (AbilityToUse != null && dist > AbilityToUse.range)
            {
                SmartLogger.Log($"[ApplyBuffAction] Target out of range. Distance: {dist}, Range: {AbilityToUse.range}", LogCategory.AI, this);
                return false;
            }
            SmartLogger.Log("[ApplyBuffAction] Preconditions met.", LogCategory.AI, this);
            return true;
        }

        public override AIWorldState ApplyEffects(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[ApplyBuffAction] ApplyEffects ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            var simulatedState = base.ApplyEffects(currentState, agentUnit);
            var targetState = simulatedState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
            if (targetState != null && targetState.IsAlive && AbilityToUse != null && AbilityToUse.appliedEffects != null)
            {
                foreach (var effect in AbilityToUse.appliedEffects)
                {
                    if (!targetState.ActiveStatusEffects.Contains(effect.effectId))
                        targetState.ActiveStatusEffects.Add(effect.effectId);
                }
                SmartLogger.Log($"[ApplyBuffAction] Simulated buff(s) applied to unit {targetState.UnitId}", LogCategory.AI, this);
            }
            return simulatedState;
        }

        public override void Execute(IDokkaebiUnit agentUnit, AIWorldState currentState)
        {
            SmartLogger.Log($"[ApplyBuffAction] Execute ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            var dokkaebiUnit = agentUnit as Dokkaebi.Units.DokkaebiUnit;
            if (dokkaebiUnit == null)
            {
                SmartLogger.LogError("[ApplyBuffAction] agentUnit is not a DokkaebiUnit.", LogCategory.AI, this);
                return;
            }
            var abilities = dokkaebiUnit.GetAbilities();
            int abilityIndex = abilities.FindIndex(a => a != null && a.abilityId == AbilityToUse.abilityId);
            if (abilityIndex == -1)
            {
                SmartLogger.LogError($"[ApplyBuffAction] AbilityToUse not found on agent unit.", LogCategory.AI, this);
                return;
            }
            var targetState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
            var targetPos = targetState != null ? targetState.Position : agentUnit.CurrentGridPosition;
            PlayerActionManager.Instance.ExecuteAbilityCommand(agentUnit.UnitId, abilityIndex, targetPos.ToVector2Int(), null, TargetUnitId);
            SmartLogger.Log($"[ApplyBuffAction] ExecuteAbilityCommand called for unit {agentUnit.UnitId}, ability {AbilityToUse.displayName}, target {TargetUnitId}", LogCategory.AI, this);
        }
    }
} 