using UnityEngine;
using Dokkaebi.Common;
using System.Collections.Generic;
using Dokkaebi.Core;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;
using System.Linq; // Required for LINQ methods like ToList()
using Dokkaebi.Core.Networking.Commands; // For ICommand
using Dokkaebi.Units; // Ensure this using directive is present and correct
using System.Collections;
using Dokkaebi.AI.Data;

namespace Dokkaebi.AI
{
    /// <summary>
    /// Manages the overall AI turn process, coordinating EnemyAIAgents.
    /// </summary>
    public class EnemyAIManager : MonoBehaviour
    {
        public static EnemyAIManager Instance { get; private set; }

        private UnitManager unitManager; // Reference to the UnitManager
        private DokkaebiTurnSystemCore turnSystemCore; // Reference to the TurnSystemCore
        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Get references
            unitManager = UnitManager.Instance;
            turnSystemCore = DokkaebiTurnSystemCore.Instance;

            if (unitManager == null)
            {
                SmartLogger.LogError("[EnemyAIManager] Could not find UnitManager instance!", LogCategory.AI, this);
                return;
            }

            if (turnSystemCore == null)
            {
                SmartLogger.LogError("[EnemyAIManager] Could not find DokkaebiTurnSystemCore instance!", LogCategory.AI, this);
                return;
            }

            // Subscribe to turn system events
            turnSystemCore.OnPhaseChanged += OnTurnPhaseChanged;
            isInitialized = true;
            SmartLogger.Log("[EnemyAIManager] Initialized successfully.", LogCategory.AI, this);
        }

        private void OnDestroy()
        {
            if (turnSystemCore != null)
            {
                turnSystemCore.OnPhaseChanged -= OnTurnPhaseChanged;
            }
        }

        /// <summary>
        /// Handles changes in the turn phase. Triggers AI planning at the start of AI phases.
        /// </summary>
        /// <param name="newPhase">The new turn phase.</param>
        public void OnTurnPhaseChanged(TurnPhase newPhase)
        {
            SmartLogger.Log($"[AI] [EnemyAIManager.OnTurnPhaseChanged] Phase changed to: {newPhase}", LogCategory.AI, this);
            SmartLogger.Log($"[EnemyAIManager.OnTurnPhaseChanged] Received phase: {newPhase}", LogCategory.AI, this);
            
            if (!isInitialized)
            {
                SmartLogger.LogWarning("[EnemyAIManager.OnTurnPhaseChanged] Skipping: EnemyAIManager not fully initialized.", LogCategory.AI, this);
                return;
            }

            if (unitManager == null || turnSystemCore == null)
            {
                SmartLogger.LogWarning("[EnemyAIManager.OnTurnPhaseChanged] Skipping: unitManager or turnSystemCore is null.", LogCategory.AI, this);
                return;
            }

            // Log turn system state
            var activePlayer = turnSystemCore.GetActivePlayer();
            SmartLogger.Log($"[EnemyAIManager.OnTurnPhaseChanged] Turn System State:", LogCategory.AI, this);
            SmartLogger.Log($"- Current Phase: {newPhase}", LogCategory.AI, this);
            SmartLogger.Log($"- Active Player: {activePlayer}", LogCategory.AI, this);
            SmartLogger.Log($"- Turn Number: {turnSystemCore.GetCurrentTurn()}", LogCategory.AI, this);

            // Reset decision state for all AI agents at the start of each phase
            var enemyUnits = unitManager.GetAliveUnits().Where(u => !u.IsPlayer()).ToList();
            SmartLogger.Log($"[AI] [EnemyAIManager.OnTurnPhaseChanged] Found {enemyUnits.Count} enemy units for MovementPhase.", LogCategory.AI, this);
            
            foreach (var enemyUnit in enemyUnits)
            {
                if (enemyUnit != null)
                {
                    var aiAgent = enemyUnit.GetComponent<EnemyAIAgent>();
                    if (aiAgent != null)
                    {
                        SmartLogger.Log($"[EnemyAIManager.OnTurnPhaseChanged] Resetting decision state for unit {enemyUnit.UnitId} ({enemyUnit.GetUnitName()})", LogCategory.AI, this);
                        aiAgent.ResetDecisionStateForPhase();
                    }
                }
            }

            // Handle different phases
            switch (newPhase)
            {
                case TurnPhase.MovementPhase:
                    SmartLogger.Log($"[AI] [EnemyAIManager.OnTurnPhaseChanged] Starting ProcessAIAgentsSequentially coroutine. (ActivePlayer={activePlayer})", LogCategory.AI, this);
                    SmartLogger.Log("[EnemyAIManager.ProcessAIAgentsSequentially ENTRY - Movement Phase]", LogCategory.AI, this);
                    SmartLogger.Log("[EnemyAIManager.OnTurnPhaseChanged] Starting sequential AI movement processing.", LogCategory.AI, this);
                    StartCoroutine(ProcessAIAgentsSequentially());
                    break;

                case TurnPhase.AuraPhase1B:
                case TurnPhase.AuraPhase2B:
                    if (activePlayer == 2) // AI's turn
                    {
                        SmartLogger.Log($"[EnemyAIManager.OnTurnPhaseChanged] ========== AURA PHASE {newPhase} START ==========", LogCategory.AI, this);
                        SmartLogger.Log($"[EnemyAIManager.StartEnemyDecisionMaking ENTRY - Aura Phase {newPhase}]", LogCategory.AI, this);
                        SmartLogger.Log($"[EnemyAIManager.OnTurnPhaseChanged] Starting AI ability decision making.", LogCategory.AI, this);
                        StartEnemyDecisionMaking();
                    }
                    else
                    {
                        SmartLogger.Log($"[EnemyAIManager.OnTurnPhaseChanged] Skipping {newPhase}: Not AI's turn (Active Player: {activePlayer})", LogCategory.AI, this);
                    }
                    break;

                // Explicitly skip all other phases
                case TurnPhase.Opening:
                case TurnPhase.BufferPhase:
                case TurnPhase.AuraPhase1A:
                case TurnPhase.AuraPhase2A:
                case TurnPhase.Resolution:
                case TurnPhase.EndTurn:
                case TurnPhase.GameOver:
                default:
                    SmartLogger.Log($"[EnemyAIManager.OnTurnPhaseChanged] Phase {newPhase} is not valid for AI actions. Skipping.", LogCategory.AI, this);
                    break;
            }

            SmartLogger.Log($"[EnemyAIManager.OnTurnPhaseChanged] ========== PHASE CHANGE END ==========", LogCategory.AI, this);
        }

        /// <summary>
        /// Processes AI agents one at a time with a delay between each.
        /// </summary>
        private IEnumerator ProcessAIAgentsSequentially()
        {
            SmartLogger.Log("[EnemyAIManager.ProcessAIAgentsSequentially] ========== MOVEMENT PROCESSING START ==========", LogCategory.AI, this);
            
            // Get all enemy units from UnitManager
            var enemyUnits = unitManager.GetAliveUnits().Where(u => !u.IsPlayer()).ToList();
            SmartLogger.Log($"[EnemyAIManager.ProcessAIAgentsSequentially] Found {enemyUnits.Count} enemy units to process.", LogCategory.AI, this);

            foreach (var enemyUnit in enemyUnits)
            {
                // --- ADDED LOG 1 ---
                SmartLogger.Log($"[EnemyAIManager.ProcessAIAgentsSequentially] Processing unit in loop: {enemyUnit?.UnitId} ({enemyUnit?.GetUnitName()})", LogCategory.AI, this);
                // --- END ADDED LOG 1 ---
                if (enemyUnit != null)
                {
                    var aiAgent = enemyUnit.GetComponent<EnemyAIAgent>();
                    if (aiAgent != null)
                    {
                        SmartLogger.Log($"[EnemyAIManager.ProcessAIAgentsSequentially] Processing movement for unit {enemyUnit.UnitId} ({enemyUnit.GetUnitName()})", LogCategory.AI, this);
                        SmartLogger.Log($"[EnemyAIAIManager.ProcessAIAgentsSequentially] Unit state - HasMovedThisTurn: {enemyUnit.HasMovedThisTurn}, HasPendingMovement: {enemyUnit.HasPendingMovement}", LogCategory.AI, this);
                        // --- ADDED LOG 2 ---
                        SmartLogger.Log($"[EnemyAIManager.ProcessAIAgentsSequentially] Calling aiAgent.MakeDecision() for unit {enemyUnit.UnitId}", LogCategory.AI, this);
                        // --- END ADDED LOG 2 ---
                        aiAgent.MakeDecision();
                        yield return new WaitForSeconds(0.2f); // Short delay after decision
                        
                        SmartLogger.Log($"[EnemyAIManager.ProcessAIAgentsSequentially] Executing pending move for unit {enemyUnit.UnitId}", LogCategory.AI, this);
                        aiAgent.ExecutePendingMove(null);
                    }
                    else
                    {
                        SmartLogger.LogWarning($"[EnemyAIManager] No EnemyAIAgent component found on enemy unit {enemyUnit.UnitId} ({enemyUnit.GetUnitName()})", LogCategory.AI, enemyUnit.GameObject);
                    }
                }
                else
                {
                    SmartLogger.LogWarning("[EnemyAIManager] Encountered null unit while processing sequential AI.", LogCategory.AI, this);
                }
                yield return new WaitForSeconds(0.5f); // Throttle: wait before next AI acts
                SmartLogger.Log("[EnemyAIManager.ProcessAIAgentsSequentially] After movement processing yield", LogCategory.AI, this);
            }

            SmartLogger.Log("[EnemyAIManager.ProcessAIAgentsSequentially] ========== MOVEMENT PROCESSING END ==========", LogCategory.AI, this);
        }

        /// <summary>
        /// Submits an AI command to the PlayerActionManager for execution.
        /// Called by AI Actions after planning.
        /// </summary>
        /// <param name="command">The command to submit.</param>
        public void SubmitCommand(ICommand command)
        {
            SmartLogger.Log($"[AI] [EnemyAIManager.SubmitCommand] Received command of type: {command?.GetType().Name}", LogCategory.AI, this);
            if (command == null)
            {
                SmartLogger.LogError("[EnemyAIManager.SubmitCommand] Cannot submit null command.", LogCategory.AI, this);
                return;
            }

            SmartLogger.Log($"[EnemyAIManager.SubmitCommand] Submitting command of type {command.GetType().Name}", LogCategory.AI, this);
            PlayerActionManager.Instance.SubmitAICommand(command);
        }

        /// <summary>
        /// Starts the decision-making process for all enemy units.
        /// </summary>
        private void StartEnemyDecisionMaking()
        {
            SmartLogger.Log("[EnemyAIManager.StartEnemyDecisionMaking] ========== ABILITY DECISION START ==========", LogCategory.AI, this);
            
            // Log turn system state
            var turnSystemCore = DokkaebiTurnSystemCore.Instance;
            if (turnSystemCore != null)
            {
                SmartLogger.Log($"[EnemyAIManager.StartEnemyDecisionMaking] Turn System State:", LogCategory.AI, this);
                SmartLogger.Log($"- Current Phase: {turnSystemCore.CurrentPhase}", LogCategory.AI, this);
                SmartLogger.Log($"- Active Player: {turnSystemCore.GetActivePlayer()}", LogCategory.AI, this);
                SmartLogger.Log($"- Turn Number: {turnSystemCore.GetCurrentTurn()}", LogCategory.AI, this);
            }
            
            // Get all enemy units from UnitManager
            var enemyUnits = unitManager.GetAliveUnits().Where(u => !u.IsPlayer()).ToList();
            SmartLogger.Log($"[EnemyAIManager.StartEnemyDecisionMaking] Found {enemyUnits.Count} enemy units to process.", LogCategory.AI, this);

            foreach (var enemyUnit in enemyUnits)
            {
                if (enemyUnit != null)
                {
                    var aiAgent = enemyUnit.GetComponent<EnemyAIAgent>();
                    if (aiAgent != null)
                    {
                        SmartLogger.Log($"[EnemyAIManager.StartEnemyDecisionMaking] Processing ability decision for unit {enemyUnit.UnitId} ({enemyUnit.GetUnitName()})", LogCategory.AI, this);
                        SmartLogger.Log($"[EnemyAIManager.StartEnemyDecisionMaking] Unit state - IsAlive: {enemyUnit.IsAlive}, TeamId: {enemyUnit.TeamId}, HasActedThisPhase: {turnSystemCore.HasUnitActedThisPhase(enemyUnit)}", LogCategory.AI, this);
                        aiAgent.MakeDecision();
                    }
                    else
                    {
                        SmartLogger.LogWarning($"[EnemyAIManager] No EnemyAIAgent component found on enemy unit {enemyUnit.UnitId} ({enemyUnit.GetUnitName()})", LogCategory.AI, enemyUnit.GameObject);
                    }
                }
                else
                {
                    SmartLogger.LogWarning("[EnemyAIManager] Encountered null unit while processing enemy decision making.", LogCategory.AI, this);
                }
            }
            
            SmartLogger.Log("[EnemyAIManager.StartEnemyDecisionMaking] ========== ABILITY DECISION END ==========", LogCategory.AI, this);
        }

        /// <summary>
        /// Gets all AI agents in the scene.
        /// </summary>
        /// <returns>A list of all EnemyAIAgent components.</returns>
        public List<EnemyAIAgent> GetAllAgents()
        {
            SmartLogger.Log("[EnemyAIManager.GetAllAgents] Finding all AI agents in scene", LogCategory.AI, this);
            var agents = FindObjectsOfType<EnemyAIAgent>().ToList();
            SmartLogger.Log($"[EnemyAIManager.GetAllAgents] Found {agents.Count} AI agents", LogCategory.AI, this);
            return agents;
        }

        /// <summary>
        /// Executes any queued actions for AI units.
        /// This is now a no-op since we're executing actions immediately.
        /// Kept for backward compatibility with existing code.
        /// </summary>
        public void ExecuteQueuedActions()
        {
            SmartLogger.Log("[EnemyAIManager.ExecuteQueuedActions] Method called but is now a no-op since actions are executed immediately", LogCategory.AI, this);
            // No-op since we now execute actions immediately in EnemyAIAgent.MakeDecision
        }
    }
} 