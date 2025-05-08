using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using System.Linq;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "HealSelfGoal", menuName = "Dokkaebi/AI/Goals/Heal Self")]
    public class HealSelfGoal : AIGoal
    {
        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            SmartLogger.Log($"[HealSelfGoal.IsGoalAchieved] ENTRY: agentUnitId={agentUnitId}, currentState null? {currentState == null}", LogCategory.AI, this);
            try
            {
                var unitState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
                SmartLogger.Log($"[HealSelfGoal.IsGoalAchieved] After GetUnitState. unitState null? {unitState == null}", LogCategory.AI, this);
                if (unitState == null)
                {
                    SmartLogger.Log($"[HealSelfGoal.IsGoalAchieved] Target unit not found (ID: {TargetUnitId})", LogCategory.AI, this);
                    return false;
                }
                bool healed = unitState.CurrentHP == unitState.MaxHP;
                SmartLogger.Log($"[HealSelfGoal.IsGoalAchieved] Unit {unitState.DisplayName} HP: {unitState.CurrentHP}/{unitState.MaxHP} | Healed: {healed}", LogCategory.AI, this);
                return healed;
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[HealSelfGoal.IsGoalAchieved] Exception: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
                return false;
            }
        }
    }
} 