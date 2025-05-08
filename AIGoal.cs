using UnityEngine;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Base class for all AI Goal ScriptableObjects.
    /// Defines a desired state the AI wants to achieve.
    /// </summary>
    public abstract class AIGoal : ScriptableObject
    {
        [Tooltip("A descriptive name for this goal.")]
        public string GoalName;

        [Tooltip("Priority of this goal. Higher values mean the AI will consider this goal first.")]
        public float Priority; // Higher priority goals are considered first

        [Tooltip("The ID of the unit this goal is for. -1 if not unit-specific.")]
        public int TargetUnitId = -1;

        // TODO: Define the desired state (partial world state) or conditions
        // This could be represented in various ways, e.g., a list of conditions,
        // or a partial AIWorldState instance. For now, we'll rely on the abstract
        // IsGoalAchieved method to check against the current state.

        /// <summary>
        /// Checks if this goal is achieved in the given world state for the specified agent unit.
        /// </summary>
        /// <param name="currentState">The current AIWorldState.</param>
        /// <param name="agentUnitId">The unit ID of the agent for which to check goal achievement.</param>
        /// <returns>True if the goal is achieved, false otherwise.</returns>
        public abstract bool IsGoalAchieved(AIWorldState currentState, int agentUnitId);
    }
} 