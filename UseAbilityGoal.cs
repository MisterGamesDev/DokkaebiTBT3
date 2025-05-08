using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Interfaces;
using Dokkaebi.Core.Data;
using Dokkaebi.Utilities; // For SmartLogger
using System.Linq; // For LINQ methods like Any

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// A generic AI Goal representing the desire to use an ability.
    /// Concrete goals will inherit from this or directly from AIGoal
    /// with more specific conditions.
    /// </summary>
    [CreateAssetMenu(fileName = "UseAbilityGoal", menuName = "Dokkaebi/AI/Goals/Use Ability")]
    public class UseAbilityGoal : AIGoal
    {
        [Tooltip("The specific AbilityData this goal is related to. Optional if this is a very general goal.")]
        public AbilityData DesiredAbility;

        // Add parameters here that define when this goal is desired.
        // Examples:
        // public bool NeedsHealing;
        // public float MinEnemyHealthToTarget;
        // public AbilityType DesiredAbilityType; // e.g., Primary, Healing, etc.
        // public float MinUnitsInAreaForAoE; // For AoE ability goals

        protected void OnValidate()
        {
            // Set a default goal name if not provided
            if (string.IsNullOrEmpty(GoalName))
            {
                GoalName = DesiredAbility != null ? $"Use {DesiredAbility.displayName}" : "Use Any Ability";
            }
        }

        /// <summary>
        /// Checks if this goal is achieved in the given world state for the specified agent unit.
        /// </summary>
        /// <param name="currentState">The current AIWorldState.</param>
        /// <param name="agentUnitId">The unit ID of the agent for which to check goal achievement.</param>
        /// <returns>True if the goal is achieved (or the conditions make it relevant to pursue), false otherwise.</returns>
        public override bool IsGoalAchieved(AIWorldState currentState, int agentUnitId)
        {
            var agentState = currentState.GetUnitState(agentUnitId);
            if (agentState == null)
            {
                SmartLogger.LogWarning($"[UseAbilityGoal - {GoalName}] IsGoalAchieved: Agent unit {agentUnitId} not found in world state.", LogCategory.AI, this);
                return false;
            }

            if (DesiredAbility != null)
            {
                // Check if the agent has the desired ability, it's off cooldown, agent has enough aura, and a valid target is in range
                if (agentState.AbilityCooldowns.TryGetValue(DesiredAbility.abilityId, out int cooldown))
                {
                    if (cooldown > 0)
                    {
                        SmartLogger.Log($"[UseAbilityGoal - {GoalName}] IsGoalAchieved: DesiredAbility '{DesiredAbility.displayName}' is on cooldown ({cooldown}). Not achieved.", LogCategory.AI, this);
                        return false;
                    }
                    if (agentState.CurrentAura < DesiredAbility.auraCost)
                    {
                        SmartLogger.Log($"[UseAbilityGoal - {GoalName}] IsGoalAchieved: Not enough aura for '{DesiredAbility.displayName}'. Current: {agentState.CurrentAura}, Cost: {DesiredAbility.auraCost}", LogCategory.AI, this);
                        return false;
                    }
                    // Check for a valid target in range
                    bool foundTarget = false;
                    if (DesiredAbility.targetsEnemy)
                    {
                        foreach (var unit in currentState.UnitStates)
                        {
                            if (unit.IsAlive && unit.TeamId != agentState.TeamId)
                            {
                                int dist = GridPosition.GetManhattanDistance(agentState.Position, unit.Position);
                                if (dist <= DesiredAbility.range)
                                {
                                    foundTarget = true;
                                    break;
                                }
                            }
                        }
                    }
                    // Add checks for other target types as needed (ally, ground, etc.)
                    SmartLogger.Log($"[UseAbilityGoal - {GoalName}] IsGoalAchieved: Found valid target for '{DesiredAbility.displayName}': {foundTarget}", LogCategory.AI, this);
                    return foundTarget;
                }
                else
                {
                    SmartLogger.Log($"[UseAbilityGoal - {GoalName}] IsGoalAchieved: Agent does not have DesiredAbility '{DesiredAbility.displayName}' (ID: {DesiredAbility.abilityId}).", LogCategory.AI, this);
                    return false;
                }
            }
            else
            {
                // Generic: Check if any ability is off cooldown, affordable, and has a valid target
                foreach (var abilityId in agentState.AvailableAbilityIds)
                {
                    if (agentState.AbilityCooldowns.TryGetValue(abilityId, out int cooldown) && cooldown == 0)
                    {
                        var abilityData = Dokkaebi.Core.Data.DataManager.Instance.GetAbilityData(abilityId);
                        if (abilityData == null) continue;
                        if (agentState.CurrentAura < abilityData.auraCost) continue;
                        bool foundTarget = false;
                        if (abilityData.targetsEnemy)
                        {
                            foreach (var unit in currentState.UnitStates)
                            {
                                if (unit.IsAlive && unit.TeamId != agentState.TeamId)
                                {
                                    int dist = GridPosition.GetManhattanDistance(agentState.Position, unit.Position);
                                    if (dist <= abilityData.range)
                                    {
                                        foundTarget = true;
                                        break;
                                    }
                                }
                            }
                        }
                        // Add checks for other target types as needed
                        SmartLogger.Log($"[UseAbilityGoal - {GoalName}] IsGoalAchieved: Found valid target for ability '{abilityData.displayName}': {foundTarget}", LogCategory.AI, this);
                        if (foundTarget) return true;
                    }
                }
                SmartLogger.Log($"[UseAbilityGoal - {GoalName}] IsGoalAchieved: No usable ability with valid target found.", LogCategory.AI, this);
                return false;
            }
        }

        // Note: Priority is inherited from AIGoal
    }
} 