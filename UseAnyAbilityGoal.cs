using Dokkaebi.AI.Data;
using UnityEngine;

namespace Dokkaebi.AI.Data.Goals
{
    [CreateAssetMenu(fileName = "UseAnyAbilityGoal", menuName = "Dokkaebi/AI/Goals/Use Any Ability Goal")]
    public class UseAnyAbilityGoal : AIGoal
    {
        public override bool IsGoalAchieved(AIWorldState state, int unitId)
        {
            // Never achieved, so the planner will always try to act
            return false;
        }
    }
} 