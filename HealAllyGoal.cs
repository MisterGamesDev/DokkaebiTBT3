using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using System.Linq;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "HealAllyGoal", menuName = "Dokkaebi/AI/Goals/Heal Ally")]
    public class HealAllyGoal : AIGoal
    {
        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            SmartLogger.Log($"[HealAllyGoal.IsGoalAchieved] ENTRY: agentUnitId={agentUnitId}, currentState null? {currentState == null}", LogCategory.AI, this);
            try
            {
                var self = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
                SmartLogger.Log($"[HealAllyGoal.IsGoalAchieved] After GetUnitState. self null? {self == null}", LogCategory.AI, this);
                if (self == null)
                {
                    SmartLogger.Log($"[HealAllyGoal.IsGoalAchieved] Target unit not found (ID: {TargetUnitId})", LogCategory.AI, this);
                    return false;
                }
                var allies = currentState.UnitStates.Where(u => u.TeamId == self.TeamId && u.UnitId != self.UnitId);
                bool allHealed = allies.All(a => a.CurrentHP == a.MaxHP);
                SmartLogger.Log($"[HealAllyGoal.IsGoalAchieved] All allies healed: {allHealed}", LogCategory.AI, this);
                return allHealed;
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[HealAllyGoal.IsGoalAchieved] Exception: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
                return false;
            }
        }
    }
} 