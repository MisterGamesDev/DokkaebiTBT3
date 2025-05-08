using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;
using Dokkaebi.Core.Data;
using System.Linq;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "UseOffensiveAbilityGoal", menuName = "Dokkaebi/AI/Goals/Use Offensive Ability")]
    public class UseOffensiveAbilityGoal : AIGoal
    {
        private void OnEnable()
        {
            Priority = 95f;
            GoalName = "Use Offensive Ability";
        }

        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            SmartLogger.Log($"[UseOffensiveAbilityGoal.IsGoalAchieved] ENTRY: agentUnitId={agentUnitId}, currentState null? {currentState == null}", LogCategory.AI, this);
            try
            {
                var agentState = currentState.GetUnitState(agentUnitId);
                SmartLogger.Log($"[UseOffensiveAbilityGoal.IsGoalAchieved] After GetUnitState. agentState null? {agentState == null}", LogCategory.AI, this);
                if (agentState == null || !agentState.IsAlive)
                {
                    SmartLogger.LogWarning($"[UseOffensiveAbilityGoal.IsGoalAchieved] Agent unit {agentUnitId} not found or not alive.", LogCategory.AI, this);
                    return false;
                }
                foreach (var abilityId in agentState.AvailableAbilityIds)
                {
                    var abilityData = DataManager.Instance.GetAbilityData(abilityId);
                    if (abilityData == null || !abilityData.targetsEnemy || abilityData.damageAmount <= 0)
                        continue;
                    // Check cooldown
                    if (agentState.AbilityCooldowns.TryGetValue(abilityId, out int cooldown) && cooldown > 0)
                        continue;
                    // Check aura
                    if (agentState.CurrentAura < abilityData.auraCost)
                        continue;
                    // Check if any player unit is in range
                    foreach (var playerUnit in currentState.UnitStates.Where(u => u.IsPlayerControlled && u.IsAlive))
                    {
                        int dist = GridPosition.GetManhattanDistance(agentState.Position, playerUnit.Position);
                        if (dist <= abilityData.range)
                        {
                            SmartLogger.Log($"[UseOffensiveAbilityGoal.IsGoalAchieved] Agent {agentUnitId} can use ability '{abilityData.displayName}' (ID: {abilityId}) on player unit {playerUnit.UnitId}.", LogCategory.AI, this);
                            return true;
                        }
                    }
                }
                SmartLogger.Log($"[UseOffensiveAbilityGoal.IsGoalAchieved] Agent {agentUnitId} cannot use any offensive ability on a player unit.", LogCategory.AI, this);
                return false;
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[UseOffensiveAbilityGoal.IsGoalAchieved] Exception: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
                return false;
            }
        }
    }
} 