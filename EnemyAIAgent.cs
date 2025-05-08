using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for LINQ operations like OrderByDescending
using Dokkaebi.Core; // Assuming access to core managers like UnitManager, TurnSystemCore, PlayerActionManager
using Dokkaebi.Units; // Required for DokkaebiUnit
using Dokkaebi.AI.Data; // Required for AIWorldState, AIGoal, AIAction, EnemyAIProfile
using Dokkaebi.Common; // Required for TurnPhase
using Dokkaebi.Interfaces; // Required for ICommand (assuming it's in Interfaces)
using Dokkaebi.Grid; // Required for GridManager (assuming it's in Grid)
using Dokkaebi.Zones; // Required for ZoneManager (assuming it's in Zones)
using Dokkaebi.Core.Data; // Required for DataManager (assuming it's in Core.Data)
using Dokkaebi.Utilities; // Required for SmartLogger
using Dokkaebi.AI.Data.Goals; // Required for UseAnyAbilityGoal

namespace Dokkaebi.AI
{
    /// <summary>
    /// Component attached to enemy units to drive their GOAP-based AI behavior.
    /// Responsible for building world state, selecting goals, finding plans, and executing actions.
    /// </summary>
    public class EnemyAIAgent : MonoBehaviour
    {
        [Tooltip("Reference to the DokkaebiUnit component this AI agent controls.")]
        [SerializeField] private DokkaebiUnit controlledUnit;

        [Tooltip("The AI Profile ScriptableObject that defines this agent's goals and actions.")]
        [SerializeField] private EnemyAIProfile aiProfile;

        // References to core managers (will be obtained from a central AI Manager or Game Controller)
        // Accessing singletons via Instance property
        private EnemyAIManager AiManager => EnemyAIManager.Instance;
        private AIPlanner AiPlanner => AIPlanner.Instance; // Reference to the singleton AIPlanner

        // SmartLogger is static, no instance needed.

        private bool _hasMadeDecisionThisPhase = false;
        private MoveAction _pendingMoveAction;

        private void Awake()
        {
            // Get reference to the controlled unit (should be on the same GameObject)
            if (controlledUnit == null)
            {
                controlledUnit = GetComponent<DokkaebiUnit>();
            }

            // Manager references are now accessed via static Instance properties.
            // No need for FindObjectOfType calls here anymore.

            // SmartLogger is static, no instance needed.
        }

        /// <summary>
        /// Checks if this unit is currently able to act during the AI turn phase.
        /// </summary>
        /// <returns>True if the unit can act, false otherwise.</returns>
        public bool CanActThisTurn()
        {
            // Check if the unit is alive and has not already acted this turn phase.
            // TODO: Add check if unit has already acted this turn phase using TurnSystemCore/UnitStateManager.
            // This might involve checking a flag on the unit or querying a UnitStateManager.
            if (controlledUnit == null || !controlledUnit.IsAlive)
            {
                // Corrected SmartLogger call: combined prefix and message, added context
                SmartLogger.Log($"AI Agent ({controlledUnit?.GetUnitName() ?? "Unknown"}) Cannot act: Controlled unit is null or not alive.", LogCategory.AI, this);
                return false;
            }

            // Placeholder: Assuming units can act if alive. Implement proper turn action tracking.
            return true;
        }

        /// <summary>
        /// Triggers the AI agent to make a decision based on the current world state and its goals.
        /// </summary>
        public void MakeDecision()
        {
            AIWorldState currentState = BuildWorldState();
            var phase = currentState?.TurnState.CurrentPhase ?? TurnPhase.Opening;
            var activePlayer = currentState?.TurnState.ActivePlayerId ?? -1;
            if ((phase == TurnPhase.MovementPhase && activePlayer != 2) ||
                ((phase == TurnPhase.AuraPhase1B || phase == TurnPhase.AuraPhase2B) && activePlayer != 2))
            {
                SmartLogger.Log($"[EnemyAIAgent.MakeDecision] Skipping: Not AI's turn or wrong phase. Phase={phase}, ActivePlayer={activePlayer}", LogCategory.AI, this);
                return;
            }
            SmartLogger.Log($"[AI] [EnemyAIAgent] MakeDecision ENTRY for Unit {controlledUnit?.UnitId}.", LogCategory.AI, this);

            if (phase == TurnPhase.MovementPhase)
            {
                HandleMovementPhase(currentState);
            }
            else if (phase == TurnPhase.AuraPhase1B || phase == TurnPhase.AuraPhase2B)
            {
                HandleAuraPhase(currentState);
            }
            else
            {
                // Existing logic for other phases, if any
            }
        }

        private void HandleMovementPhase(AIWorldState currentState)
        {
            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] ========== MOVEMENT PHASE START ==========", LogCategory.AI, this);
            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] Processing unit {controlledUnit.UnitId} ({controlledUnit.GetUnitName()})", LogCategory.AI, this);
            
            // Log unit's current state
            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] Unit state:", LogCategory.AI, this);
            SmartLogger.Log($"- Current Position: {controlledUnit.GetGridPosition()}", LogCategory.AI, this);
            SmartLogger.Log($"- Has Moved This Turn: {controlledUnit.HasMovedThisTurn}", LogCategory.AI, this);
            SmartLogger.Log($"- Has Pending Movement: {controlledUnit.HasPendingMovement}", LogCategory.AI, this);
            SmartLogger.Log($"- Movement Range: {controlledUnit.MovementRange}", LogCategory.AI, this);
            
            if (controlledUnit.HasMovedThisTurn)
            {
                SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] Unit has already moved this turn. Skipping movement planning.", LogCategory.AI, this);
                return;
            }

            // Get all goals and log them before filtering
            var allGoals = SelectPotentialGoals(currentState);
            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] All available goals before filtering:", LogCategory.AI, this);
            foreach (var goal in allGoals)
            {
                if (goal != null)
                {
                    SmartLogger.Log($"- Goal: {goal.GoalName} (Type: {goal.GetType().Name}, Priority: {goal.Priority})", LogCategory.AI, this);
                }
            }
            
            // Filter for movement-specific goals
            var potentialGoals = allGoals.Where(g => 
                g != null && (
                    g.GetType().Name.Contains("ReachPosition") ||
                    g.GetType().Name.Contains("MoveToward") ||
                    g.GetType().Name.Contains("FlankEnemy") ||
                    g.GetType().Name.Contains("RetreatFrom")
                )).ToList();
            
            // Log filtered goals
            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] Movement-specific goals after filtering:", LogCategory.AI, this);
            foreach (var goal in potentialGoals)
            {
                SmartLogger.Log($"- Goal: {goal.GoalName} (Type: {goal.GetType().Name}, Priority: {goal.Priority})", LogCategory.AI, this);
            }
            
            // Get all actions and log them before filtering
            var allActions = GetAvailableActions();
            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] All available actions before filtering:", LogCategory.AI, this);
            foreach (var action in allActions)
            {
                if (action != null)
                {
                    SmartLogger.Log($"- Action: {action.ActionName} (Type: {action.GetType().Name})", LogCategory.AI, this);
                }
            }
            
            // Filter for movement-specific actions
            List<AIAction> availableActions = allActions.Where(a => a is MoveAction).ToList();
            
            // Log filtered actions
            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] Movement-specific actions after filtering:", LogCategory.AI, this);
            foreach (var action in availableActions)
            {
                SmartLogger.Log($"- Action: {action.ActionName} (Type: {action.GetType().Name})", LogCategory.AI, this);
            }

            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] Final counts - Goals: {potentialGoals.Count}, Actions: {availableActions.Count}", LogCategory.AI, this);

            // Process goals and find best plan
            ProcessGoalsAndPlan(currentState, potentialGoals, availableActions);
            
            SmartLogger.Log($"[EnemyAIAgent.HandleMovementPhase] ========== MOVEMENT PHASE END ==========", LogCategory.AI, this);
        }

        private void HandleAuraPhase(AIWorldState currentState)
        {
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] ========== AURA PHASE START ==========", LogCategory.AI, this);
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Processing unit {controlledUnit.UnitId} ({controlledUnit.GetUnitName()})", LogCategory.AI, this);
            
            // Log unit's current state
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Unit state:", LogCategory.AI, this);
            SmartLogger.Log($"- Current Position: {controlledUnit.GetGridPosition()}", LogCategory.AI, this);
            SmartLogger.Log($"- Current Aura: {controlledUnit.GetCurrentAura()}", LogCategory.AI, this);
            SmartLogger.Log($"- Has Acted This Phase: {DokkaebiTurnSystemCore.Instance?.HasUnitActedThisPhase(controlledUnit)}", LogCategory.AI, this);
            SmartLogger.Log($"- Team ID: {controlledUnit.TeamId}", LogCategory.AI, this);
            SmartLogger.Log($"- Current Phase: {currentState.TurnState.CurrentPhase}", LogCategory.AI, this);
            SmartLogger.Log($"- Active Player: {currentState.TurnState.ActivePlayerId}", LogCategory.AI, this);
            
            // Verify we're in a valid phase for AI ability usage
            if (currentState.TurnState.CurrentPhase != TurnPhase.AuraPhase1B && currentState.TurnState.CurrentPhase != TurnPhase.AuraPhase2B)
            {
                SmartLogger.LogWarning($"[EnemyAIAgent.HandleAuraPhase] Invalid phase {currentState.TurnState.CurrentPhase} for AI ability usage", LogCategory.AI, this);
                return;
            }

            // Verify it's the AI's turn
            if (currentState.TurnState.ActivePlayerId != 2)
            {
                SmartLogger.LogWarning($"[EnemyAIAgent.HandleAuraPhase] Not AI's turn. Active Player: {currentState.TurnState.ActivePlayerId}", LogCategory.AI, this);
                return;
            }

            // Verify unit hasn't acted yet
            if (DokkaebiTurnSystemCore.Instance?.HasUnitActedThisPhase(controlledUnit) == true)
            {
                SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Unit has already acted this phase. Skipping ability planning.", LogCategory.AI, this);
                return;
            }

            // Get all goals and log them before filtering
            var allGoals = SelectPotentialGoals(currentState);
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] All available goals before filtering:", LogCategory.AI, this);
            foreach (var goal in allGoals)
            {
                if (goal != null)
                {
                    SmartLogger.Log($"- Goal: {goal.GoalName} (Type: {goal.GetType().Name}, Priority: {goal.Priority})", LogCategory.AI, this);
                }
            }
            
            // Filter for ability-specific goals
            var potentialGoals = allGoals.Where(g => 
                g != null && (
                    g.GetType().Name.Contains("Attack") ||
                    g.GetType().Name.Contains("Heal") ||
                    g.GetType().Name.Contains("Buff") ||
                    g.GetType().Name.Contains("Debuff") ||
                    g.GetType().Name.Contains("UseAbility")
                )).ToList();
            
            // Log filtered goals
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Ability-specific goals after filtering:", LogCategory.AI, this);
            foreach (var goal in potentialGoals)
            {
                SmartLogger.Log($"- Goal: {goal.GoalName} (Type: {goal.GetType().Name}, Priority: {goal.Priority})", LogCategory.AI, this);
            }
            
            // Get all actions and log them before filtering
            var allActions = GetAvailableActions();
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] All available actions before filtering:", LogCategory.AI, this);
            foreach (var action in allActions)
            {
                if (action != null)
                {
                    SmartLogger.Log($"- Action: {action.ActionName} (Type: {action.GetType().Name})", LogCategory.AI, this);
                    if (action is UseAbilityAction abilityAction)
                    {
                        SmartLogger.Log($"  * Ability: {abilityAction.AbilityToUse?.displayName ?? "NULL"}", LogCategory.AI, this);
                        SmartLogger.Log($"  * Aura Cost: {abilityAction.AbilityToUse?.auraCost ?? 0}", LogCategory.AI, this);
                    }
                }
            }
            
            // Filter for ability-specific actions and verify they're usable
            List<AIAction> availableActions = allActions.Where(a => {
                if (a is UseAbilityAction abilityAction)
                {
                    // Check if we have enough aura
                    bool hasEnoughAura = controlledUnit.GetCurrentAura() >= (abilityAction.AbilityToUse?.auraCost ?? 0);
                    // Check if ability is not on cooldown
                    bool notOnCooldown = !controlledUnit.IsOnCooldown(abilityAction.AbilityToUse?.abilityId ?? "");
                    SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Checking ability {abilityAction.AbilityToUse?.displayName ?? "NULL"}:", LogCategory.AI, this);
                    SmartLogger.Log($"- Has Enough Aura: {hasEnoughAura} (Current: {controlledUnit.GetCurrentAura()}, Cost: {abilityAction.AbilityToUse?.auraCost ?? 0})", LogCategory.AI, this);
                    SmartLogger.Log($"- Not On Cooldown: {notOnCooldown}", LogCategory.AI, this);
                    return hasEnoughAura && notOnCooldown;
                }
                return false;
            }).ToList();
            
            // Log filtered actions
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Ability-specific actions after filtering:", LogCategory.AI, this);
            foreach (var action in availableActions)
            {
                SmartLogger.Log($"- Action: {action.ActionName} (Type: {action.GetType().Name})", LogCategory.AI, this);
                if (action is UseAbilityAction abilityAction)
                {
                    SmartLogger.Log($"  * Ability: {abilityAction.AbilityToUse?.displayName ?? "NULL"}", LogCategory.AI, this);
                    SmartLogger.Log($"  * Aura Cost: {abilityAction.AbilityToUse?.auraCost ?? 0}", LogCategory.AI, this);
                }
            }

            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Final counts - Goals: {potentialGoals.Count}, Actions: {availableActions.Count}", LogCategory.AI, this);

            // --- NEW LOGIC: Use fallback goal to guarantee a plan ---
            // Create fallback UseAnyAbilityGoal instance
            var fallbackGoal = ScriptableObject.CreateInstance<Dokkaebi.AI.Data.Goals.UseAnyAbilityGoal>();
            fallbackGoal.GoalName = "Use Any Ability (Fallback)";
            fallbackGoal.Priority = 1f;
            var fallbackGoals = new List<AIGoal> { fallbackGoal };
            // Only pass UseAbilityActions
            var usableAbilityActions = availableActions.Where(a => a is UseAbilityAction).ToList();
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Fallback: Passing {usableAbilityActions.Count} UseAbilityActions to planner with UseAnyAbilityGoal.", LogCategory.AI, this);
            var plan = AiPlanner.FindPlan(currentState, controlledUnit.UnitId, fallbackGoals, usableAbilityActions);
            if (plan != null && plan.Count > 0)
            {
                SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Fallback: Executing first action in plan: {plan[0].ActionName}", LogCategory.AI, this);
                plan[0].Execute(controlledUnit, currentState);
            }
            else
            {
                SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] Fallback: No usable ability found. Performing default action.", LogCategory.AI, this);
                PerformDefaultAction();
            }
            // --- END NEW LOGIC ---
            
            SmartLogger.Log($"[EnemyAIAgent.HandleAuraPhase] ========== AURA PHASE END ==========", LogCategory.AI, this);
        }

        private void ProcessGoalsAndPlan(AIWorldState currentState, List<AIGoal> potentialGoals, List<AIAction> availableActions)
        {
            SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId} ({controlledUnit?.GetUnitName()}) processing for phase: {currentState.TurnState.CurrentPhase}", LogCategory.AI, this);
            var sortedGoals = potentialGoals.OrderByDescending(goal => goal.Priority).ToList();
            if (sortedGoals.Count > 0)
            {
                SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: {sortedGoals.Count} potential goals. Highest priority: {sortedGoals[0].GoalName} (Priority: {sortedGoals[0].Priority})", LogCategory.AI, this);
            }
            else
            {
                SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: 0 potential goals.", LogCategory.AI, this);
            }
            AIGoal selectedGoal = null;
            foreach (var goal in sortedGoals)
            {
                if (goal == null) {
                    SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: Skipping null goal in sortedGoals.", LogCategory.AI, this);
                    continue;
                }
                SmartLogger.Log($"[EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit.UnitId}: Checking goal '{goal.GoalName}' (Priority: {goal.Priority}) for achievement.", LogCategory.AI, this);
                if (!goal.IsGoalAchieved(currentState, controlledUnit.UnitId))
                {
                    selectedGoal = goal;
                    SmartLogger.Log($"[EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit.UnitId}: Goal '{goal.GoalName}' is NOT achieved. Selecting this goal.", LogCategory.AI, this);
                    break;
                }
                else
                {
                    SmartLogger.Log($"[EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit.UnitId}: Goal '{goal.GoalName}' IS achieved. Skipping.", LogCategory.AI, this);
                }
            }
            if (selectedGoal != null)
            {
                SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: PRE-PLANNER CALL.", LogCategory.AI, this);
                SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: Calling AIPlanner.FindPlan for goal '{selectedGoal.GoalName}' with {availableActions.Count} actions.", LogCategory.AI, this);
                List<AIAction> plan = null;
                try
                {
                    plan = AiPlanner.FindPlan(currentState.Clone(), controlledUnit.UnitId, new List<AIGoal> { selectedGoal }, availableActions);
                    if (plan == null)
                    {
                        SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: AIPlanner.FindPlan returned NULL plan.", LogCategory.AI, this);
                    }
                    else if (plan.Count == 0)
                    {
                        SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: AIPlanner.FindPlan returned an EMPTY plan.", LogCategory.AI, this);
                    }
                    else
                    {
                        SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: AIPlanner.FindPlan found a plan with {plan.Count} actions.", LogCategory.AI, this);
                        var firstAction = plan[0];
                        SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: --- Attempting to execute first action: {firstAction.ActionName} ---", LogCategory.AI, this);
                        if (firstAction != null)
                        {
                            firstAction.Execute(controlledUnit, currentState);
                        }
                        else
                        {
                            SmartLogger.LogWarning($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: First action in plan is NULL!", LogCategory.AI, this);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: Exception during planning or plan handling: {ex.Message}\n{ex.StackTrace}", LogCategory.AI, this);
                    plan = null;
                }
            }
            else
            {
                // --- ADDED LOG 15 ---
                SmartLogger.Log($"[AI] [EnemyAIAgent.ProcessGoalsAndPlan] Unit {controlledUnit?.UnitId}: No plan found. Performing default action.", LogCategory.AI, this);
                // --- END ADDED LOG 15 ---
                PerformDefaultAction();
            }
        }

        /// <summary>
        /// Builds a snapshot of the current game world state relevant to this AI agent.
        /// Queries relevant managers (UnitManager, GridManager, ZoneManager, TurnSystemCore).
        /// </summary>
        /// <returns>An AIWorldState object representing the current state.</returns>
        private AIWorldState BuildWorldState()
        {
            SmartLogger.Log("[EnemyAIAgent.BuildWorldState] ENTRY", LogCategory.AI, this);

            SmartLogger.Log("[EnemyAIAgent.BuildWorldState] --- LOG BEFORE PERF SCOPE ---", LogCategory.AI, this);

            AIWorldState worldState = new AIWorldState();

            // Get managers (These lines were inside the using block)
            UnitManager unitManager = UnitManager.Instance;
            GridManager gridManager = GridManager.Instance;
            ZoneManager zoneManager = ZoneManager.Instance;
            DokkaebiTurnSystemCore turnSystem = DokkaebiTurnSystemCore.Instance;

            // Unit State Population (This was inside the using block)
            SmartLogger.Log("[BuildWorldState] Attempting to get all units from UnitManager.", LogCategory.AI, this);
            var allUnits = unitManager.GetAllUnits();
            SmartLogger.Log($"[BuildWorldState] Found {allUnits.Count} total units from UnitManager.", LogCategory.AI, this);

            worldState.UnitStates = new List<AIUnitState>();
            foreach (var unit in allUnits)
            {
                SmartLogger.Log($"[BuildWorldState] Processing unit from UnitManager: ID={unit.UnitId}, Name={unit.DisplayName}, TeamId={unit.TeamId}", LogCategory.AI, this);
                var unitState = new AIUnitState
                {
                    UnitId = unit.UnitId,
                    DisplayName = unit.DisplayName,
                    TeamId = unit.TeamId,
                    Position = unit.GetGridPosition(),
                    CurrentHP = unit.GetCurrentHealth(),
                    MaxHP = unit.GetMaxHealth(),
                    CurrentAura = unit.GetCurrentAura(),
                    MaxAura = unit.GetMaxAura(),
                    IsAlive = unit.IsAlive,
                    MovementRange = unit.MovementRange,
                    IsPlayerControlled = unit.IsPlayerControlled,
                    UnitTypeId = unit.GetUnitDefinitionData()?.name ?? string.Empty
                };
                // Status Effects
                var statusEffects = unit.GetStatusEffects();
                if (statusEffects != null)
                {
                    foreach (var effect in statusEffects)
                    {
                        if (effect?.Effect != null)
                            unitState.ActiveStatusEffects.Add(effect.Effect.effectId);
                    }
                }
                // Abilities
                var abilities = unit.GetAbilities();
                if (abilities != null)
                {
                    foreach (var ability in abilities)
                    {
                        if (ability != null)
                        {
                            unitState.AvailableAbilityIds.Add(ability.abilityId);
                            unitState.AbilityCooldowns[ability.abilityId] = unit.GetRemainingCooldown(ability.abilityId);
                        }
                    }
                }
                // --- ADDED LOGGING ---
                SmartLogger.Log($"[BuildWorldState] Unit {unit.UnitId} aura: {unit.GetCurrentAura()}, cooldowns: {string.Join(",", unitState.AbilityCooldowns.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}", LogCategory.AI, this);
                // --- END ADDED LOGGING ---
                worldState.UnitStates.Add(unitState);
                SmartLogger.Log($"[BuildWorldState] Added UnitState for ID={unitState.UnitId}, TeamId={unitState.TeamId} to worldState.", LogCategory.AI, this);
            }
            SmartLogger.Log($"[BuildWorldState] Finished populating worldState.UnitStates. Total AIUnitStates added: {worldState.UnitStates.Count}", LogCategory.AI, this);
            // --- END ENHANCED LOGGING ---
            // Log a few sample units for verification
            foreach (var sample in worldState.UnitStates.Take(2))
            {
                SmartLogger.Log($"[AIWorldState] Sample Unit: {sample.DisplayName} (ID: {sample.UnitId}) | HP: {sample.CurrentHP}/{sample.MaxHP} | Aura: {sample.CurrentAura}/{sample.MaxAura} | Abilities: {string.Join(",", sample.AvailableAbilityIds)} | StatusEffects: {string.Join(",", sample.ActiveStatusEffects)}", LogCategory.AI, this);
            }
            // --- BEGIN: Player Unit Position Logging ---
            SmartLogger.Log("[BuildWorldState] UnitStates populated. Checking player unit positions:", LogCategory.AI, this);
            foreach (var unitState in worldState.UnitStates)
            {
                if (unitState.TeamId == 1)
                {
                    SmartLogger.Log($"[BuildWorldState] Player Unit Found: Unit ID {unitState.UnitId}, Display Name: {unitState.DisplayName}, Position: {unitState.Position}", LogCategory.AI, this);
                }
            }
            SmartLogger.Log("[BuildWorldState] Finished checking player unit positions in populated state.", LogCategory.AI, this);
            // --- END: Player Unit Position Logging ---
            SmartLogger.Log($"[AIWorldState] Unit data population complete.", LogCategory.AI, this);

            // --- ADDED LOGS FOR GRID STATE CONSTRUCTION ---
            if (gridManager == null)
            {
                SmartLogger.LogError("[BuildWorldState] GridManager.Instance is NULL! Cannot build grid state.", LogCategory.AI, this);
            }
            else
            {
                SmartLogger.Log("[BuildWorldState] Building AIGridState from GridManager.", LogCategory.AI, this);
                int width = gridManager.GetGridWidth();
                int height = gridManager.GetGridHeight();
                SmartLogger.Log($"[BuildWorldState] Grid dimensions: {width}x{height}", LogCategory.AI, this);
                var aiGridState = gridManager.GetGridState(); // Call the method directly

                // Execution previously halted within GetGridState() right after its nested loops.
                // Now, if GetGridState() returns successfully, the following lines in BuildWorldState()
                // that were originally after the using block will execute:

                SmartLogger.Log("[BuildWorldState] Returned from gridManager.GetGridState().", LogCategory.AI, this); // Add this log AFTER the call to GetGridState()

                worldState.GridState = aiGridState; // This line will now be reached if GetGridState() returns

                SmartLogger.Log("[BuildWorldState] Assigned GridState to worldState.", LogCategory.AI, this); // Add this log after assignment
            }
            // --- END ADDED LOGS FOR GRID STATE CONSTRUCTION ---
            // Set the current phase and active player in the AI world state
            if (turnSystem != null)
            {
                worldState.TurnState.CurrentPhase = turnSystem.CurrentPhase;
                worldState.TurnState.ActivePlayerId = turnSystem.ActivePlayerId;
                SmartLogger.Log($"[BuildWorldState] Set TurnState: Phase={worldState.TurnState.CurrentPhase}, ActivePlayer={worldState.TurnState.ActivePlayerId}", LogCategory.AI, this);
            }
            else
            {
                SmartLogger.LogWarning("[BuildWorldState] TurnSystemCore is null! Cannot set phase or active player.", LogCategory.AI, this);
            }
            SmartLogger.Log("[EnemyAIAgent.BuildWorldState] EXIT", LogCategory.AI, this);
            return worldState;
        }

        /// <summary>
        /// Selects potential goals for this agent based on its AI profile and the current world state.
        /// Can include filtering out irrelevant goals or dynamically adjusting priorities.
        /// </summary>
        /// <param name="currentState">The current AIWorldState.</param>
        /// <returns>A list of potential AIGoal ScriptableObjects.</returns>
        private List<AIGoal> SelectPotentialGoals(AIWorldState currentState)
        {
            List<AIGoal> potentialGoals = new List<AIGoal>();
            var phase = currentState.TurnState.CurrentPhase;
            SmartLogger.Log($"[SelectPotentialGoals] Current Phase: {phase}", LogCategory.AI, this);

            if (aiProfile == null || aiProfile.PossibleGoals == null)
                return potentialGoals;

            // Log all possible goals with their priorities before filtering
            SmartLogger.Log($"[SelectPotentialGoals] All PossibleGoals from profile:", LogCategory.AI, this);
            foreach (var goal in aiProfile.PossibleGoals)
            {
                if (goal != null)
                {
                    SmartLogger.Log($"  - {goal.GoalName} (Type: {goal.GetType().Name}, Priority: {goal.Priority})", LogCategory.AI, this);
                }
            }

            var selfState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == controlledUnit.UnitId);
            if (selfState == null || !selfState.IsAlive)
                return potentialGoals;

            // --- DYNAMIC GOAL GENERATION ---
            // 1. HealSelfGoal if HP is low
            float hpPercent = (float)selfState.CurrentHP / Mathf.Max(1, selfState.MaxHP);
            if (hpPercent < 0.5f) // Threshold can be tuned
            {
                var healSelfGoalPrefab = aiProfile.PossibleGoals.FirstOrDefault(g => g is Dokkaebi.AI.Data.Goals.HealSelfGoal);
                if (healSelfGoalPrefab != null)
                {
                    var healSelfGoal = ScriptableObject.Instantiate(healSelfGoalPrefab);
                    healSelfGoal.TargetUnitId = controlledUnit.UnitId;
                    healSelfGoal.Priority = 100f - (hpPercent * 50f); // Higher priority if lower HP
                    healSelfGoal.GoalName = $"Heal Self (Dynamic)";
                    potentialGoals.Add(healSelfGoal);
                    SmartLogger.Log($"[SelectPotentialGoals] Dynamic HealSelfGoal added. HP%: {hpPercent:F2}, Priority: {healSelfGoal.Priority}", LogCategory.AI, this);
                }
            }

            // 2. UseAbilityGoal for powerful abilities off cooldown
            foreach (var abilityId in selfState.AvailableAbilityIds)
            {
                var abilityData = Dokkaebi.Core.Data.DataManager.Instance.GetAbilityData(abilityId);
                if (abilityData == null) continue;
                if (selfState.AbilityCooldowns.TryGetValue(abilityId, out int cooldown) && cooldown == 0 && selfState.CurrentAura >= abilityData.auraCost)
                {
                    // Only consider high-impact abilities (damage, heal, or strong effect)
                    if (abilityData.damageAmount > 10 || abilityData.healAmount > 10 || (abilityData.appliedEffects != null && abilityData.appliedEffects.Count > 0))
                    {
                        var useAbilityGoalPrefab = aiProfile.PossibleGoals.FirstOrDefault(g => g is Dokkaebi.AI.Data.UseAbilityGoal);
                        if (useAbilityGoalPrefab != null)
                        {
                            var useAbilityGoal = ScriptableObject.Instantiate(useAbilityGoalPrefab) as Dokkaebi.AI.Data.UseAbilityGoal;
                            useAbilityGoal.TargetUnitId = controlledUnit.UnitId;
                            useAbilityGoal.DesiredAbility = abilityData;
                            useAbilityGoal.Priority = 90f + (abilityData.damageAmount + abilityData.healAmount) * 0.5f;
                            useAbilityGoal.GoalName = $"Use {abilityData.displayName} (Dynamic)";
                            potentialGoals.Add(useAbilityGoal);
                            SmartLogger.Log($"[SelectPotentialGoals] Dynamic UseAbilityGoal added for {abilityData.displayName}. Priority: {useAbilityGoal.Priority}", LogCategory.AI, this);
                        }
                    }
                }
            }

            // 3. AttackNearestEnemyGoal if enemy in range
            int attackRange = 1;
            string usedAbilityId = null;
            foreach (var abilityId in selfState.AvailableAbilityIds)
            {
                var abilityData = Dokkaebi.Core.Data.DataManager.Instance.GetAbilityData(abilityId);
                if (abilityData != null && abilityData.targetsEnemy && abilityData.damageAmount > 0)
                {
                    attackRange = abilityData.range;
                    usedAbilityId = abilityId;
                    break;
                }
            }
            bool enemyInRange = currentState.UnitStates.Any(u => u.IsPlayerControlled && u.IsAlive && Dokkaebi.Interfaces.GridPosition.GetManhattanDistance(selfState.Position, u.Position) <= attackRange);
            if (enemyInRange)
            {
                var attackGoalPrefab = aiProfile.PossibleGoals.FirstOrDefault(g => g is Dokkaebi.AI.Data.Goals.AttackNearestEnemyGoal);
                if (attackGoalPrefab != null)
                {
                    var attackGoal = ScriptableObject.Instantiate(attackGoalPrefab);
                    attackGoal.TargetUnitId = controlledUnit.UnitId;
                    attackGoal.Priority = 95f;
                    attackGoal.GoalName = $"Attack Nearest Enemy (Dynamic)";
                    potentialGoals.Add(attackGoal);
                    SmartLogger.Log($"[SelectPotentialGoals] Dynamic AttackNearestEnemyGoal added. Priority: {attackGoal.Priority}", LogCategory.AI, this);
                }
            }

            // --- END DYNAMIC GOAL GENERATION ---

            // --- STATIC PROFILE GOALS (avoid duplicates) ---
            foreach (var goal in aiProfile.PossibleGoals)
            {
                if (goal == null) continue;
                // Avoid adding duplicate dynamic goals (by type and TargetUnitId)
                bool alreadyAdded = potentialGoals.Any(g => g.GetType() == goal.GetType() && g.TargetUnitId == controlledUnit.UnitId);
                if (!alreadyAdded)
                {
                    var clonedGoal = ScriptableObject.Instantiate(goal);
                    clonedGoal.TargetUnitId = controlledUnit.UnitId;
                    potentialGoals.Add(clonedGoal);
                    SmartLogger.Log($"[SelectPotentialGoals] Static goal '{goal.GoalName}' added.", LogCategory.AI, this);
                }
            }

            // Sort by Priority (descending)
            potentialGoals.Sort((g1, g2) => g2.Priority.CompareTo(g1.Priority));
            // Log the final filtered/prioritized list
            SmartLogger.Log($"[SelectPotentialGoals] Final potential goals after filtering/prioritization:", LogCategory.AI, this);
            foreach (var goal in potentialGoals)
            {
                SmartLogger.Log($"  - {goal.GoalName} (Type: {goal.GetType().Name}, Priority: {goal.Priority})", LogCategory.AI, this);
            }
            SmartLogger.Log($"[SelectPotentialGoals] Final potential goals: {string.Join(", ", potentialGoals.Select(g => g.GoalName))}", LogCategory.AI, this);
            return potentialGoals;
        }

        /// <summary>
        /// Determines which actions are currently available to the controlled unit.
        /// Filters actions based on unit's abilities, cooldowns, aura cost, etc.
        /// </summary>
        /// <returns>A list of available AIAction ScriptableObjects.</returns>
        private List<AIAction> GetAvailableActions()
        {
            List<AIAction> availableActions = new List<AIAction>();

            if (aiProfile != null && aiProfile.AvailableActions != null)
            {
                // TODO: Implement logic to filter actions based on the current unit's state.
                // This will involve checking ability cooldowns, sufficient aura, movement range, etc.
                // For now, just add all available actions from the profile.
                availableActions.AddRange(aiProfile.AvailableActions);
            }
             else
            {
                 // Corrected SmartLogger call
                 SmartLogger.LogWarning($"AI Agent ({controlledUnit?.GetUnitName() ?? "Unknown"}) AI Profile or Available Actions list is null.", LogCategory.AI, this);
            }

            foreach (var action in aiProfile.AvailableActions)
            {
                if (string.IsNullOrEmpty(action.ActionName))
                {
                    SmartLogger.LogWarning($"[AI] [EnemyAIAgent.GetAvailableActions] Action asset missing ActionName! Asset: {action.name}", LogCategory.AI, this);
                }
                if (action is UseAbilityAction abilityAction && abilityAction.AbilityToUse == null)
                {
                    SmartLogger.LogWarning($"[AI] [EnemyAIAgent.GetAvailableActions] UseAbilityAction asset missing AbilityToUse! Asset: {action.name}", LogCategory.AI, this);
                }
            }

            return availableActions;
        }

        /// <summary>
        /// Performs a fallback action if the AI planner cannot find a plan for any goal.
        /// </summary>
        private void PerformDefaultAction()
        {
            // --- ADDED LOG 16 ---
            SmartLogger.Log($"[AI] [EnemyAIAgent.PerformDefaultAction] Unit {controlledUnit?.UnitId}: ENTERED PerformDefaultAction.", LogCategory.AI, this);
            // --- END ADDED LOG 16 ---
            // For now, the default action is to end the unit's turn.
            SmartLogger.Log($"[AI] [EnemyAIAgent.PerformDefaultAction] Unit {controlledUnit?.UnitId}: Defaulting to EndTurn.", LogCategory.AI, this);
            var endTurnCommand = new Dokkaebi.Core.Networking.Commands.EndTurnCommand();
            // --- ADDED LOG 17 ---
            SmartLogger.Log($"[AI] [EnemyAIAgent.PerformDefaultAction] Unit {controlledUnit?.UnitId}: Submitting EndTurnCommand.", LogCategory.AI, this);
            // --- END ADDED LOG 17 ---
            EnemyAIManager.Instance.SubmitCommand(endTurnCommand);
        }

        public void ResetDecisionStateForPhase()
        {
            _hasMadeDecisionThisPhase = false;
            SmartLogger.Log($"[EnemyAIAgent.ResetDecisionStateForPhase] Unit {controlledUnit?.UnitId} ({controlledUnit?.GetUnitName()}): Resetting decision state for new phase.", LogCategory.AI, this);
        }

        public void ExecutePendingMove(AIWorldState currentState)
        {
            SmartLogger.Log($"[EnemyAIAgent] ExecutePendingMove called for unit {controlledUnit.UnitId}. _pendingMoveAction: {(_pendingMoveAction != null ? "SET" : "NULL")}", LogCategory.AI, this);
            if (_pendingMoveAction != null && !controlledUnit.HasMovedThisTurn)
            {
                SmartLogger.Log($"[EnemyAIAgent] Executing stored MoveAction for unit {controlledUnit.UnitId} to {_pendingMoveAction.TargetPosition}", LogCategory.AI, this);
                _pendingMoveAction.Execute(controlledUnit, currentState);
                _pendingMoveAction = null;
            }
            else if (_pendingMoveAction != null && controlledUnit.HasMovedThisTurn)
            {
                SmartLogger.Log($"[EnemyAIAgent] Unit {controlledUnit.UnitId} already moved this turn. Skipping pending move execution.", LogCategory.AI, this);
                _pendingMoveAction = null;
            }
            else
            {
                SmartLogger.Log($"[EnemyAIAgent] No pending MoveAction to execute for unit {controlledUnit.UnitId}.", LogCategory.AI, this);
            }
        }

        // TODO: Add a method to initialize the agent with manager references if not using FindObjectOfType
        // public void Initialize(EnemyAIManager manager, AIPlanner planner) { ... }
    }
} 