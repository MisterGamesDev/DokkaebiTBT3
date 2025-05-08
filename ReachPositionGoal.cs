using UnityEngine;
using Dokkaebi.Interfaces; // Required for GridPosition
using Dokkaebi.AI.Data; // Required for AIWorldState, AIUnitState
using Dokkaebi.Utilities; // Required for SmartLogger

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Concrete AIGoal representing the desire for a unit to reach a specific grid position.
    /// </summary>
    [CreateAssetMenu(fileName = "ReachPositionGoal", menuName = "Dokkaebi/AI/Goals/Reach Position")]
    public class ReachPositionGoal : AIGoal
    {
        [Tooltip("The target grid position the AI unit wants to reach.")]
        public GridPosition DesiredPosition;

        [Tooltip("The ID of the unit that should reach the desired position.")]
        // Note: This could be the AI agent's own unit ID, or potentially another unit's ID for escort/rendezvous goals.
        public int TargetUnitId;

        // TODO: Consider adding parameters like acceptance radius, duration goal must be met, etc.

        /// <summary>
        /// Checks if the goal (unit reaching the desired position) is achieved in the given world state.
        /// </summary>
        /// <param name="currentState">The current AIWorldState.</param>
        /// <param name="agentUnitId">The ID of the agent unit.</param>
        /// <returns>True if the target unit is at the desired position, false otherwise.</returns>
        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            // Find the target unit's state in the current world state.
            AIUnitState targetUnitState = currentState.UnitStates.Find(u => u.UnitId == TargetUnitId);

            if (targetUnitState != null)
            {
                // TODO: Implement actual goal check.
                // Check if targetUnitState.Position is equal to DesiredPosition.
                bool goalAchieved = targetUnitState.Position.Equals(DesiredPosition); // Assuming GridPosition has Equals

                SmartLogger.Log($"[ReachPositionGoal] Checking if goal achieved for unit {TargetUnitId} at {targetUnitState.Position} vs desired {DesiredPosition}: {goalAchieved}", LogCategory.AI, this);
                return goalAchieved;
            }
            else
            {
                // If the target unit doesn't exist in the world state, the goal cannot be achieved by this unit.
                SmartLogger.LogWarning($"[ReachPositionGoal] Target unit {TargetUnitId} not found in world state. Goal cannot be achieved.", LogCategory.AI, this);
                return false;
            }
        }
    }
} 