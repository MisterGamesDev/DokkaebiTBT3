using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;
using System.Linq;
using System.Collections.Generic;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "ReachCoverGoal", menuName = "Dokkaebi/AI/Goals/Reach Cover")]
    public class ReachCoverGoal : AIGoal
    {
        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            SmartLogger.Log($"[ReachCoverGoal.IsGoalAchieved] ENTRY: agentUnitId={agentUnitId}, currentState null? {currentState == null}", LogCategory.AI, this);
            try
            {
                var unitState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
                SmartLogger.Log($"[ReachCoverGoal.IsGoalAchieved] After GetUnitState. unitState null? {unitState == null}", LogCategory.AI, this);
                if (unitState == null)
                {
                    SmartLogger.Log($"[ReachCoverGoal.IsGoalAchieved] Target unit not found (ID: {TargetUnitId})", LogCategory.AI, this);
                    return false;
                }
                GridPosition pos = unitState.Position;
                var neighborOffsets = new List<GridPosition> {
                    new GridPosition(1,0), new GridPosition(-1,0), new GridPosition(0,1), new GridPosition(0,-1)
                };
                foreach (var offset in neighborOffsets)
                {
                    var neighbor = new GridPosition(pos.x + offset.x, pos.z + offset.z);
                    if (currentState.GridState.IsWalkable.TryGetValue(neighbor, out bool walkable) && walkable &&
                        (!currentState.GridState.OccupyingUnitId.TryGetValue(neighbor, out int occ) || occ == -1))
                    {
                        SmartLogger.Log($"[ReachCoverGoal.IsGoalAchieved] Found adjacent walkable, unoccupied tile at {neighbor}", LogCategory.AI, this);
                        return true;
                    }
                }
                SmartLogger.Log($"[ReachCoverGoal.IsGoalAchieved] No adjacent cover (walkable, unoccupied) found for unit {unitState.DisplayName} at {pos}", LogCategory.AI, this);
                return false;
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[ReachCoverGoal.IsGoalAchieved] Exception: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
                return false;
            }
        }
    }
} 