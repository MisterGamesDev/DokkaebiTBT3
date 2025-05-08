using UnityEngine;
using System.Collections.Generic;
using Dokkaebi.Interfaces; // Ensure DokkaebiUnit is accessible here
using Dokkaebi.Common;

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Base class for all AI Action ScriptableObjects.
    /// Defines an action the AI can perform to change the world state.
    /// </summary>
    public abstract class AIAction : ScriptableObject
    {
        [Tooltip("A descriptive name for this action.")]
        public string ActionName;

        [Tooltip("The cost of performing this action. Used by the planner to find the cheapest plan.")]
        public float Cost; // Cost of performing this action (e.g., Aura cost, movement points, time)

        // TODO: Define preconditions (partial world state) required to perform this action.
        // This could be represented similarly to goal conditions. For now, we rely on
        // the abstract CheckPreconditions method.

        // TODO: Define effects (how this action changes the world state).
        // This could also be represented in various ways. For now, we rely on
        // the abstract ApplyEffects method.

        /// <summary>
        /// Checks if the preconditions for performing this action are met in the current world state.
        /// Concrete action implementations must define this logic.
        /// </summary>
        /// <param name="currentState">The current AIWorldState.</param>
        /// <param name="agentUnit">The DokkaebiUnit performing the action.</param>
        /// <returns>True if preconditions are met, false otherwise.</returns>
        public abstract bool CheckPreconditions(AIWorldState currentState, IDokkaebiUnit agentUnit);

        /// <summary>
        /// Applies the effects of this action to a simulated world state.
        /// This method is used by the planner to predict future states.
        /// Concrete action implementations must define this logic.
        /// </summary>
        /// <param name="currentState">The AIWorldState to apply effects to (should be a clone).</param>
        /// <param name="agentUnit">The DokkaebiUnit performing the action.</param>
        /// <returns>The modified AIWorldState after applying effects.</returns>
        public abstract AIWorldState ApplyEffects(AIWorldState currentState, IDokkaebiUnit agentUnit);

        /// <summary>
        /// Executes the action in the actual game world by generating and submitting a Command.
        /// Concrete action implementations must define this logic.
        /// </summary>
        /// <param name="agentUnit">The DokkaebiUnit performing the action.</param>
        /// <param name="currentState">The current AIWorldState (can be used to determine command parameters).</param>
        public abstract void Execute(IDokkaebiUnit agentUnit, AIWorldState currentState);
    }
} 