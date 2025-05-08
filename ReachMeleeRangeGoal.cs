using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;
using System.Linq;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "ReachMeleeRangeGoal", menuName = "Dokkaebi/AI/Goals/Reach Melee Range")]
    public class ReachMeleeRangeGoal : AIGoal
    {
        private void OnEnable()
        {
            Priority = 90f;
            GoalName = "Reach Melee Range";
        }

        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            SmartLogger.Log($"[ReachMeleeRangeGoal.IsGoalAchieved] ENTRY: agentUnitId={agentUnitId}, currentState null? {currentState == null}", LogCategory.AI, this);
            try
            {
                var agentState = currentState.GetUnitState(agentUnitId);
                SmartLogger.Log($"[ReachMeleeRangeGoal.IsGoalAchieved] After GetUnitState. agentState null? {agentState == null}", LogCategory.AI, this);
                if (agentState == null || !agentState.IsAlive)
                {
                    SmartLogger.LogWarning($"[ReachMeleeRangeGoal.IsGoalAchieved] Agent unit {agentUnitId} not found or not alive.", LogCategory.AI, this);
                    return false;
                }
                int moveRange = agentState.MovementRange;
                var agentPos = agentState.Position;
                var walkableTiles = currentState.GridState.IsWalkable
                    .Where(kv => kv.Value)
                    .Select(kv => kv.Key)
                    .Where(pos => !currentState.IsTileOccupied(pos))
                    .Where(pos => GridPosition.GetManhattanDistance(agentPos, pos) <= moveRange)
                    .ToList();
                SmartLogger.Log($"[ReachMeleeRangeGoal.IsGoalAchieved] Computed walkableTiles count: {walkableTiles.Count}", LogCategory.AI, this);
                foreach (var playerUnit in currentState.UnitStates.Where(u => u.IsPlayerControlled && u.IsAlive))
                {
                    foreach (var tile in walkableTiles)
                    {
                        if (GridPosition.GetManhattanDistance(tile, playerUnit.Position) == 1)
                        {
                            SmartLogger.Log($"[ReachMeleeRangeGoal.IsGoalAchieved] Agent {agentUnitId} can reach melee range of player unit {playerUnit.UnitId} by moving to {tile}.", LogCategory.AI, this);
                            return true;
                        }
                    }
                }
                SmartLogger.Log($"[ReachMeleeRangeGoal.IsGoalAchieved] Agent {agentUnitId} cannot reach melee range of any player unit this turn.", LogCategory.AI, this);
                return false;
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[ReachMeleeRangeGoal.IsGoalAchieved] Exception: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
                return false;
            }
        }
    }
} 