using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Interfaces;
using Dokkaebi.Core.Data;
using Dokkaebi.Core.Networking.Commands; // Assuming AbilityCommand is here
using Dokkaebi.Utilities; // For SmartLogger
using Dokkaebi.Core; // For AbilityManager access
using System.Linq; // For LINQ methods like Any, FirstOrDefault
using Dokkaebi.Units; // Required for DokkaebiUnit cast
using Dokkaebi.Common;

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// A generic AI Action representing the use of a specific ability.
    /// Concrete actions for individual abilities may inherit from this.
    /// </summary>
    [CreateAssetMenu(fileName = "UseAbilityAction", menuName = "Dokkaebi/AI/Actions/Use Ability")]
    public class UseAbilityAction : AIAction
    {
        [Tooltip("The AbilityData ScriptableObject this action will use.")]
        public AbilityData AbilityToUse;

        // Add parameters here that define the specific instance of using the ability,
        // like the target position or target unit ID, which would be set by the planner.
        public GridPosition TargetPosition; // Target grid position for the ability
        public int TargetUnitId = -1; // Target unit ID if the ability targets a unit (-1 if not applicable)

        // Add support for two-target abilities if needed (e.g., Karmic Tether)
        public int SecondTargetUnitId = -1;

        protected void OnValidate()
        {
            // Set a default action name and cost if not provided
            if (AbilityToUse != null)
            {
                ActionName = $"Use {AbilityToUse.displayName}";
                Cost = AbilityToUse.auraCost; // Use Aura cost as the default action cost
            }
            else if (string.IsNullOrEmpty(ActionName))
            {
                ActionName = "Use Generic Ability";
                Cost = 1; // Default cost if no ability is specified
            }
        }

        /// <summary>
        /// Checks if the preconditions for using this ability are met in the current world state.
        /// Preconditions: Unit is alive, has enough aura, ability is not on cooldown, target is valid.
        /// </summary>
        /// <param name="currentState">The current AIWorldState.</param>
        /// <param name="agentUnit">The IDokkaebiUnit performing the action.</param>
        /// <returns>True if preconditions are met, false otherwise.</returns>
        public override bool CheckPreconditions(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[UseAbilityAction.CheckPreconditions] Base Precondition Check for {ActionName}", LogCategory.AI, this);

            // Get agent AI state
            var agentState = currentState.GetUnitState(agentUnit.UnitId);
            if (agentState == null)
            {
                SmartLogger.LogWarning($"[UseAbilityAction.CheckPreconditions] FAIL: Agent state not found for unit {agentUnit.UnitId}", LogCategory.AI, this);
                return false;
            }

            // Check if agent is alive
            if (!agentState.IsAlive)
            {
                SmartLogger.LogWarning($"[UseAbilityAction.CheckPreconditions] FAIL: Agent unit {agentUnit.UnitId} is not alive.", LogCategory.AI, this);
                return false;
            }

            // Check if AbilityToUse is assigned
            if (AbilityToUse == null)
            {
                SmartLogger.LogWarning($"[UseAbilityAction.CheckPreconditions] FAIL: AbilityToUse not assigned for action {ActionName}.", LogCategory.AI, this);
                return false;
            }

            // Check for enough aura
            if (agentState.CurrentAura < AbilityToUse.auraCost)
            {
                SmartLogger.LogWarning($"[UseAbilityAction.CheckPreconditions] FAIL: Not enough aura for {ActionName}. Current: {agentState.CurrentAura}, Required: {AbilityToUse.auraCost}", LogCategory.AI, this);
                return false;
            }

            // Check cooldown
            if (agentState.AbilityCooldowns.TryGetValue(AbilityToUse.abilityId, out int cooldown) && cooldown > 0)
            {
                SmartLogger.LogWarning($"[UseAbilityAction.CheckPreconditions] FAIL: Ability is on cooldown ({cooldown} turns remaining)", LogCategory.AI, this);
                return false;
            }

            // --- ADDED LOGGING ---
            if (agentState != null && AbilityToUse != null)
            {
                int logCooldown = agentState.AbilityCooldowns.ContainsKey(AbilityToUse.abilityId) ? agentState.AbilityCooldowns[AbilityToUse.abilityId] : -1;
                SmartLogger.Log($"[UseAbilityAction.CheckPreconditions] Logging: Unit {agentUnit.UnitId}, Aura: {agentState.CurrentAura}, Ability: {AbilityToUse.abilityId}, Cooldown: {logCooldown}", LogCategory.AI, this);
            }
            // --- END ADDED LOGGING ---

            SmartLogger.Log($"[UseAbilityAction.CheckPreconditions] Base preconditions passed for {ActionName}.", LogCategory.AI, this);
            return true;
        }

        /// <summary>
        /// Applies the effects of using this ability to a simulated world state.
        /// Effects: Deduct aura cost, put ability on cooldown, potentially change unit/grid state based on ability type.
        /// </summary>
        /// <param name="currentState">The AIWorldState to apply effects to (should be a clone).</param>
        /// <param name="agentUnit">The IDokkaebiUnit performing the action.</param>
        /// <returns>The modified AIWorldState after applying effects.</returns>
        public override AIWorldState ApplyEffects(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] ApplyEffects ENTRY for unit {agentUnit.UnitId} ({agentUnit.GetUnitName()})", LogCategory.AI, this);
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] Ability details: {AbilityToUse.displayName}, Damage: {AbilityToUse.damageAmount}, Heal: {AbilityToUse.healAmount}, Range: {AbilityToUse.range}, Cost: {AbilityToUse.auraCost}", LogCategory.AI, this);
            
            var simulatedState = currentState.Clone();
            var agentState = simulatedState.GetUnitState(agentUnit.UnitId);

            if (agentState != null)
            {
                // Log pre-effect state
                SmartLogger.Log($"[UseAbilityAction - {ActionName}] Pre-effect state - Agent Aura: {agentState.CurrentAura}, Target Unit: {TargetUnitId}, AbilityCooldowns: [{string.Join(", ", agentState.AbilityCooldowns.Select(kvp => kvp.Key + ":" + kvp.Value))}]", LogCategory.AI, this);

                // Apply aura cost
                int oldAura = agentState.CurrentAura;
                agentState.CurrentAura = Mathf.Max(0, agentState.CurrentAura - AbilityToUse.auraCost);
                SmartLogger.Log($"[UseAbilityAction - {ActionName}] Aura deduction: OldAura={oldAura}, Cost={AbilityToUse.auraCost}, NewAura={agentState.CurrentAura}", LogCategory.AI, this);

                // Apply cooldown
                int oldCooldown = agentState.AbilityCooldowns.ContainsKey(AbilityToUse.abilityId) ? agentState.AbilityCooldowns[AbilityToUse.abilityId] : -1;
                if (!agentState.AbilityCooldowns.ContainsKey(AbilityToUse.abilityId))
                    agentState.AbilityCooldowns.Add(AbilityToUse.abilityId, AbilityToUse.cooldownTurns);
                else
                    agentState.AbilityCooldowns[AbilityToUse.abilityId] = AbilityToUse.cooldownTurns;
                SmartLogger.Log($"[UseAbilityAction - {ActionName}] Cooldown set: abilityId={AbilityToUse.abilityId}, OldCooldown={oldCooldown}, NewCooldown={AbilityToUse.cooldownTurns}", LogCategory.AI, this);

                // Log post-effect state
                SmartLogger.Log($"[UseAbilityAction - {ActionName}] Post-effect state - Agent Aura: {agentState.CurrentAura}, Cooldowns: [{string.Join(", ", agentState.AbilityCooldowns.Select(kvp => kvp.Key + ":" + kvp.Value))}]", LogCategory.AI, this);
            }
            else
            {
                SmartLogger.LogWarning($"[UseAbilityAction - {ActionName}] Agent state not found for unit {agentUnit.UnitId}", LogCategory.AI, this);
            }

            // Log target state if applicable
            if (TargetUnitId != -1)
            {
                var targetState = simulatedState.GetUnitState(TargetUnitId);
                if (targetState != null)
                {
                    SmartLogger.Log($"[UseAbilityAction - {ActionName}] Target state - Unit: {TargetUnitId}, HP: {targetState.CurrentHP}/{targetState.MaxHP}, Position: {targetState.Position}", LogCategory.AI, this);
                }
            }

            SmartLogger.Log($"[UseAbilityAction - {ActionName}] ApplyEffects EXIT for unit {agentUnit.UnitId}", LogCategory.AI, this);
            return simulatedState;
        }

        /// <summary>
        /// Executes the action in the actual game world by generating and submitting an AbilityCommand.
        /// </summary>
        /// <param name="agentUnit">The IDokkaebiUnit performing the action.</param>
        /// <param name="currentState">The current AIWorldState (can be used to determine command parameters if not set by planner).</param>
        public override void Execute(IDokkaebiUnit agentUnit, AIWorldState currentState)
        {
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] ========== EXECUTE START ==========", LogCategory.AI, this);
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] Execute ENTRY for unit {agentUnit.UnitId} ({agentUnit.GetUnitName()}) in phase {currentState.TurnState.CurrentPhase}", LogCategory.AI, this);
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] Ability Details:", LogCategory.AI, this);
            SmartLogger.Log($"- Name: {AbilityToUse?.displayName ?? "NULL"}", LogCategory.AI, this);
            SmartLogger.Log($"- ID: {AbilityToUse?.abilityId ?? "NULL"}", LogCategory.AI, this);
            SmartLogger.Log($"- Target Position: {TargetPosition}", LogCategory.AI, this);
            SmartLogger.Log($"- Target Unit ID: {TargetUnitId}", LogCategory.AI, this);
            SmartLogger.Log($"- Second Target Unit ID: {SecondTargetUnitId}", LogCategory.AI, this);

            if (AbilityToUse == null)
            {
                SmartLogger.LogError($"[UseAbilityAction - {ActionName}] FAILED: No AbilityToUse assigned", LogCategory.AI, this);
                return;
            }

            // Cast to DokkaebiUnit for ability manager
            var dokkaebiUnit = agentUnit as DokkaebiUnit;
            if (dokkaebiUnit == null)
            {
                SmartLogger.LogError($"[UseAbilityAction - {ActionName}] FAILED: Agent unit is not a DokkaebiUnit", LogCategory.AI, this);
                return;
            }

            // Log unit state before ability execution
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] Unit State Before Execution:", LogCategory.AI, this);
            SmartLogger.Log($"- Current HP: {agentUnit.CurrentHealth}", LogCategory.AI, this);
            SmartLogger.Log($"- Position: {agentUnit.CurrentGridPosition}", LogCategory.AI, this);

            // Get the ability index from the unit's ability list
            var abilities = dokkaebiUnit.GetAbilities();
            int abilityIndex = abilities.FindIndex(a => a != null && a.abilityId == AbilityToUse.abilityId);
            if (abilityIndex == -1)
            {
                SmartLogger.LogError($"[UseAbilityAction - {ActionName}] FAILED: Could not find ability {AbilityToUse.abilityId} on unit {agentUnit.UnitId}", LogCategory.AI, this);
                return;
            }

            // Create and validate the ability command
            AbilityCommand abilityCommand;
            if (AbilityToUse.abilityId == "KarmicTether")
            {
                SmartLogger.Log($"[UseAbilityAction - {ActionName}] Creating KarmicTether command with two targets: {TargetUnitId} and {SecondTargetUnitId}", LogCategory.AI, this);
                abilityCommand = new AbilityCommand(agentUnit.UnitId, abilityIndex, TargetPosition.ToVector2Int(), null, SecondTargetUnitId);
            }
            else if (TargetUnitId != -1)
            {
                SmartLogger.Log($"[UseAbilityAction - {ActionName}] Creating unit-targeted ability command. Target Unit: {TargetUnitId}", LogCategory.AI, this);
                abilityCommand = new AbilityCommand(agentUnit.UnitId, abilityIndex, TargetPosition.ToVector2Int(), null, TargetUnitId);
            }
            else
            {
                SmartLogger.Log($"[UseAbilityAction - {ActionName}] Creating position-targeted ability command. Target Position: {TargetPosition}", LogCategory.AI, this);
                abilityCommand = new AbilityCommand(agentUnit.UnitId, abilityIndex, TargetPosition.ToVector2Int());
            }

            // Log command details before submission
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] Submitting Command:", LogCategory.AI, this);
            SmartLogger.Log($"- Command Type: {abilityCommand.GetType().Name}", LogCategory.AI, this);
            SmartLogger.Log($"- Caster Unit ID: {abilityCommand.UnitId}", LogCategory.AI, this);
            SmartLogger.Log($"- Ability Index: {abilityCommand.AbilityIndex}", LogCategory.AI, this);
            SmartLogger.Log($"- Target Position: {abilityCommand.TargetPosition}", LogCategory.AI, this);
            SmartLogger.Log($"- Second Target Unit ID: {abilityCommand.SecondTargetUnitId}", LogCategory.AI, this);

            // Submit the command
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] Submitting command to EnemyAIManager...", LogCategory.AI, this);
            EnemyAIManager.Instance.SubmitCommand(abilityCommand);
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] Command submitted successfully", LogCategory.AI, this);

            // Log completion
            SmartLogger.Log($"[UseAbilityAction - {ActionName}] ========== EXECUTE COMPLETE ==========", LogCategory.AI, this);
        }
    }
} 