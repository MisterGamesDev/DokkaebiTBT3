using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using System.Linq;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "ApplyBuffToSelfGoal", menuName = "Dokkaebi/AI/Goals/Apply Buff To Self")]
    public class ApplyBuffToSelfGoal : AIGoal
    {
        [Tooltip("The effectId of the buff to check for.")]
        public string BuffEffectId;

        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            SmartLogger.Log($"[ApplyBuffToSelfGoal.IsGoalAchieved] ENTRY: agentUnitId={agentUnitId}, currentState null? {currentState == null}, BuffEffectId={BuffEffectId}", LogCategory.AI, this);
            try
            {
                var unitState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == TargetUnitId);
                SmartLogger.Log($"[ApplyBuffToSelfGoal.IsGoalAchieved] After GetUnitState. unitState null? {unitState == null}", LogCategory.AI, this);
                if (unitState == null)
                {
                    SmartLogger.Log($"[ApplyBuffToSelfGoal.IsGoalAchieved] Target unit not found (ID: {TargetUnitId})", LogCategory.AI, this);
                    return false;
                }
                bool hasBuff = unitState.ActiveStatusEffects.Contains(BuffEffectId);
                SmartLogger.Log($"[ApplyBuffToSelfGoal.IsGoalAchieved] Unit {unitState.DisplayName} has buff '{BuffEffectId}': {hasBuff}", LogCategory.AI, this);
                return hasBuff;
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[ApplyBuffToSelfGoal.IsGoalAchieved] Exception: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
                return false;
            }
        }
    }
} 