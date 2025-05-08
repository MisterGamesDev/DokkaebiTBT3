using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using System.Linq;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "DefeatPlayerUnitGoal", menuName = "Dokkaebi/AI/Goals/Defeat Player Unit")]
    public class DefeatPlayerUnitGoal : AIGoal
    {
        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            SmartLogger.Log($"[DefeatPlayerUnitGoal.IsGoalAchieved] ENTRY: agentUnitId={agentUnitId}, currentState null? {currentState == null}", LogCategory.AI, this);
            bool allPlayersDefeated = false;
            try
            {
                allPlayersDefeated = currentState.UnitStates
                    .Where(u => u.IsPlayerControlled)
                    .All(u => !u.IsAlive);
                SmartLogger.Log($"[DefeatPlayerUnitGoal.IsGoalAchieved] All player units defeated: {allPlayersDefeated}", LogCategory.AI, this);
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[DefeatPlayerUnitGoal.IsGoalAchieved] Exception: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
            }
            SmartLogger.Log($"[DefeatPlayerUnitGoal.IsGoalAchieved] Returning: {allPlayersDefeated}", LogCategory.AI, this);
            return allPlayersDefeated;
        }
    }
} 