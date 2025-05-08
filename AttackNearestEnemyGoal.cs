using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using System.Linq;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "AttackNearestEnemyGoal", menuName = "Dokkaebi/AI/Goals/Attack Nearest Enemy")]
    public class AttackNearestEnemyGoal : AIGoal
    {
        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            SmartLogger.Log($"[AttackNearestEnemyGoal.IsGoalAchieved] ENTRY: agentUnitId={agentUnitId}, currentState null? {currentState == null}", LogCategory.AI, this);
            try
            {
                var agentState = currentState.GetUnitState(agentUnitId);
                SmartLogger.Log($"[AttackNearestEnemyGoal.IsGoalAchieved] After GetUnitState. agentState null? {agentState == null}", LogCategory.AI, this);
                if (agentState == null || !agentState.IsAlive)
                {
                    SmartLogger.LogWarning($"[AttackNearestEnemyGoal.IsGoalAchieved] Agent unit {agentUnitId} not found or not alive.", LogCategory.AI, this);
                    return false;
                }
                // Find the agent's first offensive ability (targetsEnemy && damageAmount > 0)
                int attackRange = 1; // Default melee range
                string usedAbilityId = null;
                if (agentState.AvailableAbilityIds != null && agentState.AvailableAbilityIds.Count > 0)
                {
                    foreach (var abilityId in agentState.AvailableAbilityIds)
                    {
                        var abilityData = Dokkaebi.Core.Data.DataManager.Instance.GetAbilityData(abilityId);
                        if (abilityData != null && abilityData.targetsEnemy && abilityData.damageAmount > 0)
                        {
                            attackRange = abilityData.range;
                            usedAbilityId = abilityId;
                            break;
                        }
                    }
                }
                SmartLogger.Log($"[AttackNearestEnemyGoal.IsGoalAchieved] Using attack range {attackRange} from abilityId '{usedAbilityId ?? "(default)"}' for agent {agentUnitId}.", LogCategory.AI, this);
                // Check if any player-controlled unit is alive and within attack range
                foreach (var unit in currentState.UnitStates)
                {
                    if (unit.IsPlayerControlled && unit.IsAlive)
                    {
                        int dist = Dokkaebi.Interfaces.GridPosition.GetManhattanDistance(agentState.Position, unit.Position);
                        if (dist <= attackRange)
                        {
                            SmartLogger.Log($"[AttackNearestEnemyGoal.IsGoalAchieved] Player unit {unit.UnitId} is within attack range ({dist} <= {attackRange}) of agent {agentUnitId}.", LogCategory.AI, this);
                            return true;
                        }
                    }
                }
                SmartLogger.Log($"[AttackNearestEnemyGoal.IsGoalAchieved] No player unit is within attack range of agent {agentUnitId}.", LogCategory.AI, this);
                return false;
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[AttackNearestEnemyGoal.IsGoalAchieved] Exception: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
                return false;
            }
        }
    }
} 