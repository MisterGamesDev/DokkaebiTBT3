using System;
using System.Collections.Generic;
using UnityEngine;
using Dokkaebi.Grid;
using Dokkaebi.Units;
using Dokkaebi.Utilities;
using Dokkaebi.Core.TurnStates;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using Dokkaebi.Pathfinding;
using Dokkaebi.AI;
using Dokkaebi.AI.Data;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Core implementation of the Dokkaebi Turn Flow System (DTFS).
    /// Handles turn progression, movement, and Aura activation.
    /// </summary>
    public class DokkaebiTurnSystemCore : MonoBehaviour, IUpdateObserver, ITurnSystem
    {
        // Singleton pattern
        public static DokkaebiTurnSystemCore Instance { get; private set; }
        
        // Core components
        [SerializeField] private UnitStateManager unitStateManager;
        private UnitManager unitManager;
        
        // Turn state management
        private TurnStateContext turnStateContext;
        
        // Turn settings
        [Header("Turn Settings")]
        [SerializeField] private float phaseTransitionDelay = 0.3f;
        
        [Header("Phase Durations")]
        [SerializeField] private float openingPhaseDuration = 3.0f;
        [SerializeField] private float movementPhaseDuration = 15.0f;
        [SerializeField] private float auraChargingPhaseDuration = 5.0f;
        [SerializeField] private float bufferPhaseDuration = 2.0f;
        
        // Events
        public event Action<TurnPhase> OnPhaseChanged;
        public event Action<int> OnTurnChanged;
        public event Action<int> OnActivePlayerChanged;
        public event Action OnMovementPhaseStart;
        public event Action OnMovementPhaseEnd;
        public event Action OnTurnResolutionEnd;
        
        // Unit management
        private Dictionary<DokkaebiUnit, GridPosition> pendingMoves = new Dictionary<DokkaebiUnit, GridPosition>();
        private HashSet<DokkaebiUnit> unitsActedThisPhase = new HashSet<DokkaebiUnit>();
        private List<DokkaebiUnit> registeredUnits = new List<DokkaebiUnit>();
        
        // Movement tracking
        private bool isExecutingMoves = false;
        private int totalMovesMade = 0;
        private int requiredMoves = 4; // Default value
        
        // Debug flags
        [Header("Debug")]
        [SerializeField] private bool debugLogTurns = false;
        
        // Properties for phase durations
        public float OpeningPhaseDuration => openingPhaseDuration;
        public float MovementPhaseDuration => movementPhaseDuration;
        public float AuraChargingPhaseDuration => auraChargingPhaseDuration;
        public float BufferPhaseDuration => bufferPhaseDuration;
        
        // Properties required by ITurnSystem
        public int CurrentTurn => turnStateContext != null ? turnStateContext.GetCurrentTurn() : 1;
        public TurnPhase CurrentPhase => turnStateContext != null ? turnStateContext.GetCurrentPhase() : TurnPhase.Opening;
        public int ActivePlayerId => turnStateContext != null ? turnStateContext.GetActivePlayer() : 0;

        public UnitStateManager UnitStateManager => unitStateManager;

        public int GetActivePlayer()
        {
            var activePlayer = ActivePlayerId;
            var currentPhase = CurrentPhase;
            
            // Log detailed phase and player mapping
            SmartLogger.Log($"[DTSCore.GetActivePlayer] ========== ACTIVE PLAYER CHECK ==========", LogCategory.TurnSystem, this);
            SmartLogger.Log($"[DTSCore.GetActivePlayer] Current Phase: {currentPhase}, Active Player: {activePlayer}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"[DTSCore.GetActivePlayer] Phase Type:", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is Opening: {currentPhase == TurnPhase.Opening}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is Movement: {currentPhase == TurnPhase.MovementPhase}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is AuraPhase1A: {currentPhase == TurnPhase.AuraPhase1A}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is AuraPhase1B: {currentPhase == TurnPhase.AuraPhase1B}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is AuraPhase2A: {currentPhase == TurnPhase.AuraPhase2A}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is AuraPhase2B: {currentPhase == TurnPhase.AuraPhase2B}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is Buffer Phase: {currentPhase == TurnPhase.BufferPhase}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is Resolution: {currentPhase == TurnPhase.Resolution}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is EndTurn: {currentPhase == TurnPhase.EndTurn}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"[DTSCore.GetActivePlayer] Player Mapping:", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Player 1 Active: {activePlayer == 1} (Human)", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Player 2 Active: {activePlayer == 2} (AI)", LogCategory.TurnSystem, this);
            SmartLogger.Log($"[DTSCore.GetActivePlayer] ========== END ACTIVE PLAYER CHECK ==========", LogCategory.TurnSystem, this);
            
            return activePlayer;
        }

        public TurnPhase GetCurrentPhase() => CurrentPhase;
        public int GetCurrentTurn() => CurrentTurn;

        private void Awake()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(Awake)}");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Get UnitManager instance
            unitManager = UnitManager.Instance;
            if (unitManager == null)
            {
                SmartLogger.LogError("DokkaebiTurnSystemCore could not find UnitManager instance!", LogCategory.TurnSystem, this);
            }

            // Initialize turn state context
            turnStateContext = new TurnStateContext(this);
            turnStateContext.OnPhaseChanged += HandlePhaseChanged;
            turnStateContext.OnTurnChanged += HandleTurnChanged;
            turnStateContext.OnMovementPhaseStart += () => OnMovementPhaseStart?.Invoke();
            turnStateContext.OnMovementPhaseEnd += () => OnMovementPhaseEnd?.Invoke();
            turnStateContext.OnTurnResolutionEnd += () => OnTurnResolutionEnd?.Invoke();

            if (turnStateContext != null)
            {
                SmartLogger.Log($"[DTSCore.Awake] TurnStateContext initialized. Initial Phase: {turnStateContext.GetCurrentPhase()}, Initial Turn: {turnStateContext.GetCurrentTurn()}", LogCategory.TurnSystem, this);
            }
            else
            {
                SmartLogger.LogError("[DTSCore.Awake] TurnStateContext failed to initialize!", LogCategory.TurnSystem, this);
            }

            // Register with update manager
            DokkaebiUpdateManager.Instance.RegisterUpdateObserver(this);

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(Awake)}");
        }

        private void OnDestroy()
        {
            if (turnStateContext != null)
            {
                turnStateContext.OnPhaseChanged -= HandlePhaseChanged;
                turnStateContext.OnTurnChanged -= HandleTurnChanged;
            }

            if (DokkaebiUpdateManager.Instance != null)
            {
                DokkaebiUpdateManager.Instance.UnregisterUpdateObserver(this);
            }

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(OnDestroy)}");
        }

        private void HandlePhaseChanged(TurnPhase newPhase)
        {
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] ========== PHASE CHANGE START ==========", LogCategory.TurnSystem, this);
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Turn {turnStateContext.GetCurrentTurn()}", LogCategory.TurnSystem, this);

            // Log previous state
            var previousPhase = CurrentPhase;
            var previousActivePlayer = turnStateContext.GetActivePlayer();
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Previous State:", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Previous Phase: {previousPhase}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Previous Active Player: {previousActivePlayer}", LogCategory.TurnSystem, this);

            // Log new state
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] New State:", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- New Phase: {newPhase}", LogCategory.TurnSystem, this);

            // Fire phase change event
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Firing OnPhaseChanged event...", LogCategory.TurnSystem, this);
            OnPhaseChanged?.Invoke(newPhase);
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] OnPhaseChanged event fired", LogCategory.TurnSystem, this);

            // Get and log new active player
            int activePlayer = turnStateContext.GetActivePlayer();
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Active Player Update:", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- New Active Player: {activePlayer}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is Player Turn: {activePlayer == 1}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is AI Turn: {activePlayer == 2}", LogCategory.TurnSystem, this);

            // Fire active player change event if it changed
            if (activePlayer != previousActivePlayer)
            {
                SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Active player changed from {previousActivePlayer} to {activePlayer}. Firing OnActivePlayerChanged event...", LogCategory.TurnSystem, this);
                OnActivePlayerChanged?.Invoke(activePlayer);
                SmartLogger.Log($"[DTSCore.HandlePhaseChanged] OnActivePlayerChanged event fired", LogCategory.TurnSystem, this);
            }

            // Log debug info if enabled
            if (debugLogTurns)
            {
                SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Debug Info:", LogCategory.Debug, this);
                SmartLogger.Log($"- Turn Number: {turnStateContext.GetCurrentTurn()}", LogCategory.Debug, this);
                SmartLogger.Log($"- Phase Duration: {GetPhaseDuration(newPhase)}s", LogCategory.Debug, this);
                SmartLogger.Log($"- Is Phase Locked: {turnStateContext.IsTransitionLocked}", LogCategory.Debug, this);
            }

            // Handle Movement Phase specific logic
            if (newPhase == TurnPhase.MovementPhase)
            {
                SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Entering Movement Phase Logic:", LogCategory.TurnSystem, this);
                
                var allActiveUnits = UnitManager.Instance?.GetAliveUnits();
                if (allActiveUnits != null)
                {
                    SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Processing {allActiveUnits.Count} active units:", LogCategory.TurnSystem, this);
                    foreach (var unit in allActiveUnits)
                    {
                        if (unit != null)
                        {
                            SmartLogger.Log($"- Unit {unit.UnitId} ({unit.GetUnitName()}):", LogCategory.TurnSystem, this);
                            SmartLogger.Log($"  * Team: {unit.TeamId}", LogCategory.TurnSystem, this);
                            SmartLogger.Log($"  * Position: {unit.CurrentGridPosition}", LogCategory.TurnSystem, this);
                            SmartLogger.Log($"  * Is Player Controlled: {unit.IsPlayerControlled}", LogCategory.TurnSystem, this);
                            
                            unit.RecordPositionAtTurnStart();
                            SmartLogger.Log($"  * Start Position Recorded: {unit.GetPositionAtTurnStart()}", LogCategory.TurnSystem, this);
                        }
                    }
                }
                else
                {
                    SmartLogger.LogWarning("[DTSCore.HandlePhaseChanged] No active units found or UnitManager is null", LogCategory.TurnSystem, this);
                }

                // Reset movement tracking
                unitsActedThisPhase.Clear();
                pendingMoves.Clear();
                isExecutingMoves = false;
                totalMovesMade = 0;
                SmartLogger.Log("[DTSCore.HandlePhaseChanged] Movement tracking variables reset", LogCategory.TurnSystem, this);
            }

            // Log phase-specific information
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Phase-Specific State:", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Units Acted This Phase: {unitsActedThisPhase.Count}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Pending Moves: {pendingMoves.Count}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Is Executing Moves: {isExecutingMoves}", LogCategory.TurnSystem, this);
            SmartLogger.Log($"- Total Moves Made: {totalMovesMade}", LogCategory.TurnSystem, this);

            SmartLogger.Log($"[DTSCore.HandlePhaseChanged] ========== PHASE CHANGE COMPLETE ==========", LogCategory.TurnSystem, this);
        }

        private float GetPhaseDuration(TurnPhase phase)
        {
            return phase switch
            {
                TurnPhase.Opening => openingPhaseDuration,
                TurnPhase.MovementPhase => movementPhaseDuration,
                TurnPhase.AuraPhase1A => auraChargingPhaseDuration,
                TurnPhase.AuraPhase1B => auraChargingPhaseDuration,
                TurnPhase.AuraPhase2A => auraChargingPhaseDuration,
                TurnPhase.AuraPhase2B => auraChargingPhaseDuration,
                TurnPhase.BufferPhase => bufferPhaseDuration,
                _ => 0f
            };
        }

        private void HandleTurnChanged(int newTurn)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(HandleTurnChanged)}");

            SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Processing start-of-turn logic for Turn {newTurn}", LogCategory.TurnSystem);

            if (unitManager != null)
            {
                // Reset action states for all units at the start of the new turn
                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Calling UnitManager.ResetActionStates() for the new turn.", LogCategory.TurnSystem);
                unitManager.ResetActionStates(true); // Reset player unit states
                unitManager.ResetActionStates(false); // Reset enemy unit states
                // Process start-of-turn effects for both players at the beginning of the turn cycle
                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Calling UnitManager.StartPlayerTurn()", LogCategory.TurnSystem);
                unitManager.StartPlayerTurn();
                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Calling UnitManager.StartEnemyTurn()", LogCategory.TurnSystem);
                unitManager.StartEnemyTurn();

                // Grant passive Aura gain to each active unit at the start of the turn
                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Granting unit-based passive Aura for turn {newTurn}", LogCategory.TurnSystem);
                var activeUnits = unitManager.GetAliveUnits(); // Assuming this returns List<DokkaebiUnit> or similar
                if (activeUnits != null && activeUnits.Count > 0)
                {
                    SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Found {activeUnits.Count} active units to grant Aura.", LogCategory.TurnSystem);
                    foreach (var unit in activeUnits)
                    {
                        if (unit != null)
                        {
                            var unitData = unit.GetUnitDefinitionData();
                            if (unitData != null)
                            {
                                int auraGain = unitData.passiveAuraPerTurn;
                                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Granting {auraGain} passive aura to unit {unit.GetUnitName()} (ID: {unit.UnitId}) from UnitDefinitionData ({unitData.name})", LogCategory.TurnSystem | LogCategory.Debug);
                                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] About to call ModifyUnitAura for unit {unit.GetUnitName()} (ID: {unit.UnitId}) with amount {auraGain}", LogCategory.TurnSystem);
                                unit.ModifyUnitAura(auraGain);
                            }
                            else
                            {
                                SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.HandleTurnChanged] Could not get UnitDefinitionData for unit {unit.GetUnitName()} (ID: {unit.UnitId}). Cannot grant passive aura.", LogCategory.TurnSystem);
                            }
                        }
                        else
                        {
                            SmartLogger.LogWarning("[DokkaebiTurnSystemCore.HandleTurnChanged] Encountered a null unit while granting Aura.", LogCategory.TurnSystem);
                        }
                    }
                    SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Finished granting unit-based Aura.", LogCategory.TurnSystem);
                }
                else
                {
                    SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.HandleTurnChanged] No active units found to grant Aura.", LogCategory.TurnSystem);
                }
            }
            else
            {
                SmartLogger.LogError("[DokkaebiTurnSystemCore.HandleTurnChanged] UnitManager reference is null, cannot process unit start-of-turn!", LogCategory.TurnSystem);
            }

            OnTurnChanged?.Invoke(newTurn);
            
            // Log each unit in unitsActedThisPhase
            SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Current units in unitsActedThisPhase: {unitsActedThisPhase.Count}", LogCategory.Debug, this);
            foreach (var unit in unitsActedThisPhase)
            {
                if (unit != null)
                {
                    SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] - Unit in unitsActedThisPhase: {unit.GetUnitName()} (ID: {unit.UnitId})", LogCategory.Debug, this);
                }
                else
                {
                    Debug.LogWarning("[DokkaebiTurnSystemCore.HandleTurnChanged] - Found null unit in unitsActedThisPhase");
                }
            }
            
            // Log each unit in the pendingMoves dictionary
            SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Current units in pendingMoves: {pendingMoves.Count}", LogCategory.Debug, this);
            foreach (var kvp in pendingMoves)
            {
                if (kvp.Key != null)
                {
                    SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] - Unit in pendingMoves: {kvp.Key.GetUnitName()} (ID: {kvp.Key.UnitId}) targeting {kvp.Value}", LogCategory.Debug, this);
                }
                else
                {
                    Debug.LogWarning("[DokkaebiTurnSystemCore.HandleTurnChanged] - Found null unit in pendingMoves");
                }
            }
            
            // Debug.Log("[DokkaebiTurnSystemCore] Resetting unit state for new turn");
            unitsActedThisPhase.Clear();
            pendingMoves.Clear();
            isExecutingMoves = false;
            totalMovesMade = 0;

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(HandleTurnChanged)}");
        }

        public void CustomUpdate(float deltaTime)
        {
            //SmartLogger.Log("[DokkaebiTurnSystemCore.CustomUpdate ENTRY]", LogCategory.TurnSystem, this);
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(CustomUpdate)}");
            //Debug.LogError("[DEBUG_FREEZE] F: Entered DTSCore.CustomUpdate.");
            if (turnStateContext != null)
            {
                //Debug.LogError("[DEBUG_FREEZE] G: Before TurnStateContext.Update.");
                turnStateContext.Update(deltaTime);
                //Debug.LogError("[DEBUG_FREEZE] H: After TurnStateContext.Update.");
            }
            //Debug.LogError("[DEBUG_FREEZE] I: Exited DTSCore.CustomUpdate.");
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(CustomUpdate)}");
            //SmartLogger.Log("[DokkaebiTurnSystemCore.CustomUpdate EXIT]", LogCategory.TurnSystem, this);
        }

        // Unit movement methods
        private bool CanUnitMove(DokkaebiUnit unit)
        {
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(CanUnitMove)}");
            if (turnStateContext == null || !turnStateContext.AllowsMovement())
            {
                Debug.Log($"[DokkaebiTurnSystemCore.CanUnitMove] Movement not allowed for {unit.GetUnitName()} (ID: {unit.UnitId}). turnStateContext null: {turnStateContext == null}, AllowsMovement: {turnStateContext?.AllowsMovement()}");
                return false;
            }
            
            if (isExecutingMoves)
            {
                Debug.Log($"[DokkaebiTurnSystemCore.CanUnitMove] Movement not allowed for {unit.GetUnitName()} (ID: {unit.UnitId}). isExecutingMoves: {isExecutingMoves}");
                return false;
            }
            
            if (HasUnitActedThisPhase(unit))
            {
                Debug.Log($"[DokkaebiTurnSystemCore.CanUnitMove] Movement not allowed for {unit.GetUnitName()} (ID: {unit.UnitId}). Unit has already acted this phase");
                return false;
            }
            
            bool canMove = unit.CanMove();
            Debug.Log($"[DokkaebiTurnSystemCore.CanUnitMove] Final check for {unit.GetUnitName()} (ID: {unit.UnitId}). CanMove: {canMove}");
            return canMove;
        }

        private bool CanUnitUseAura(DokkaebiUnit unit)
        {
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(CanUnitUseAura)}");
            if (turnStateContext == null)
                return false;

            return turnStateContext.AllowsAuraActivation(unit.IsPlayer());
        }

        private void QueueMove(DokkaebiUnit unit, GridPosition targetPosition)
        {
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(QueueMove)}");
            if (!CanUnitMove(unit))
            {
                Debug.LogWarning($"Unit {unit.UnitId} cannot move in the current phase");
                return;
            }

            pendingMoves[unit] = targetPosition;
            unitsActedThisPhase.Add(unit);

            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(QueueMove)}");
        }

        public void NextPhase()
        {
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(NextPhase)}");
            if (turnStateContext != null)
            {
                turnStateContext.TransitionToNextState();
            }
            else
            {
                Debug.LogError("TurnStateContext is null in DokkaebiTurnSystemCore.NextPhase");
            }

            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(NextPhase)}");
        }

        public void NextTurn()
        {
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(NextTurn)}");
            if (turnStateContext != null)
            {
                turnStateContext.IncrementTurn();
            }
            else
            {
                Debug.LogError("TurnStateContext is null in DokkaebiTurnSystemCore.NextTurn");
            }

            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(NextTurn)}");
        }

        /// <summary>
        /// Register a unit with the turn system
        /// </summary>
        public void RegisterUnit(DokkaebiUnit unit)
        {
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(RegisterUnit)}");
            if (unit != null && !registeredUnits.Contains(unit))
            {
                // Debug.Log($"[DokkaebiTurnSystemCore.RegisterUnit] Registering unit {unit.GetUnitName()} (ID: {unit.UnitId})");
                registeredUnits.Add(unit);
                // Debug.Log($"[DokkaebiTurnSystemCore.RegisterUnit] After registration, registeredUnits count: {registeredUnits.Count}");
                // SmartLogger.Log($"Unit {unit.GetUnitName()} registered with turn system", LogCategory.TurnSystem);
            }

            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(RegisterUnit)}");
        }
        
        /// <summary>
        /// Unregister a unit from the turn system
        /// </summary>
        public void UnregisterUnit(DokkaebiUnit unit)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(UnregisterUnit)}");
            if (unit != null && registeredUnits.Contains(unit))
            {
                Debug.Log($"[DokkaebiTurnSystemCore.UnregisterUnit] Unregistering unit {unit.GetUnitName()} (ID: {unit.UnitId}). Current count: {registeredUnits.Count}");
                registeredUnits.Remove(unit);
                Debug.Log($"[DokkaebiTurnSystemCore.UnregisterUnit] After unregistration, registeredUnits count: {registeredUnits.Count}");
                
                // Also remove from pending actions if present
                if (pendingMoves.ContainsKey(unit))
                {
                    pendingMoves.Remove(unit);
                }
                
                if (unitsActedThisPhase.Contains(unit))
                {
                    unitsActedThisPhase.Remove(unit);
                }
            }

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(UnregisterUnit)}");
        }
        
        /// <summary>
        /// Check if all required movement has been completed
        /// </summary>
        private void CheckMovementCompletion()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(CheckMovementCompletion)}");
            TurnPhase currentPhase = GetCurrentPhase();
            if (currentPhase != TurnPhase.MovementPhase || isExecutingMoves)
            {
                return;
            }
            
            bool allMovesComplete = false;
            bool bothPlayersComplete = false;
            
            // Check if all required moves have been completed
            if (totalMovesMade >= requiredMoves)
            {
                allMovesComplete = true;
            }
            
            // Check if both players have reached their individual move limits
            if (unitStateManager != null)
            {
                bool p1MaxReached = unitStateManager.HasReachedMaxMoves(true);
                bool p2MaxReached = unitStateManager.HasReachedMaxMoves(false);
                bothPlayersComplete = p1MaxReached && p2MaxReached;
            }
            
            // If either condition is met, trigger the phase advance
            if ((allMovesComplete || bothPlayersComplete) && !turnStateContext.IsTransitionLocked)
            {
                SmartLogger.Log("All required moves completed!", LogCategory.Movement);
                
                // Execute all pending moves and advance phase
                ExecuteAllPendingMoves();
                turnStateContext.TransitionToNextState();
            }

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(CheckMovementCompletion)}");
        }
        
        /// <summary>
        /// Queue an Aura ability usage with strict limit enforcement
        /// </summary>
        public bool QueueAura(DokkaebiUnit unit)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(QueueAura)}");
            using (new PerformanceScope("QueueAura"))
            {
                LogTurnSystemState(); // Log turn state for debugging
                
                if (unit == null)
                {
                    SmartLogger.LogError("Cannot queue Aura for null unit!", LogCategory.Ability);
                    return false;
                }
                
                // Check if we're in an Aura phase
                TurnPhase currentPhase = GetCurrentPhase();
                bool isAuraPhase = currentPhase == TurnPhase.AuraPhase1A || 
                                  currentPhase == TurnPhase.AuraPhase1B || 
                                  currentPhase == TurnPhase.AuraPhase2A || 
                                  currentPhase == TurnPhase.AuraPhase2B;
                if (!isAuraPhase)
                {
                    SmartLogger.LogWarning($"Cannot use Aura for {unit.GetUnitName()} - not in Aura phase", LogCategory.Ability);
                    return false;
                }
                
                // Check if it's the right player's turn
                bool isUnitPlayer1 = unit.IsPlayer();
                if (!turnStateContext.AllowsAuraActivation(isUnitPlayer1))
                {
                    SmartLogger.LogWarning($"Not {unit.GetUnitName()}'s turn to use Aura. Active player: {GetActivePlayer()}", LogCategory.Ability);
                    return false;
                }
                
                // Check if unit already used an ability this phase
                if (HasUnitActedThisPhase(unit))
                {
                    SmartLogger.LogWarning($"{unit.GetUnitName()} has already used an ability this phase", LogCategory.Ability);
                    return false;
                }
                
                // Check if player has reached their aura limit for this phase
                if (unitStateManager != null)
                {
                    int maxAuras = unitStateManager.GetMaxAurasPerPhase();
                    bool isPlayer1 = unit.IsPlayer();
                    int currentAuras = isPlayer1 ? unitStateManager.GetPlayer1AurasActivated() : unitStateManager.GetPlayer2AurasActivated();
                    
                    if (currentAuras >= maxAuras)
                    {
                        SmartLogger.LogWarning($"{unit.GetUnitName()} has reached the maximum aura activations for this phase", LogCategory.Ability);
                        return false;
                    }
                }
                
                // Mark unit as having acted this phase
                unitsActedThisPhase.Add(unit);
                
                // Update aura activation count
                if (unitStateManager != null)
                {
                    unitStateManager.RegisterAuraActivated(unit.IsPlayer());
                }
                
                return true;
            }
        }
        
        /// <summary>
        /// Check if we should advance to the next phase based on aura usage
        /// </summary>
        private void CheckAuraCompletion()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(CheckAuraCompletion)}");
            TurnPhase currentPhase = GetCurrentPhase();
            bool isAuraPhase = currentPhase == TurnPhase.AuraPhase1A || 
                              currentPhase == TurnPhase.AuraPhase1B || 
                              currentPhase == TurnPhase.AuraPhase2A || 
                              currentPhase == TurnPhase.AuraPhase2B;
            if (!isAuraPhase || turnStateContext.IsTransitionLocked)
            {
                return;
            }
            
            if (unitStateManager != null)
            {
                int activePlayer = GetActivePlayer();
                int maxAuras = unitStateManager.GetMaxAurasPerPhase();
                
                int currentAuras = 0;
                if (activePlayer == 1)
                {
                    currentAuras = unitStateManager.GetPlayer1AurasActivated();
                }
                else if (activePlayer == 2)
                {
                    currentAuras = unitStateManager.GetPlayer2AurasActivated();
                }
                
                // Auto-advance if max auras used
                if (currentAuras >= maxAuras)
                {
                    SmartLogger.Log($"Player {activePlayer} used all {currentAuras}/{maxAuras} auras", LogCategory.Ability);
                    turnStateContext.TransitionToNextState();
                }
                else
                {
                    SmartLogger.Log($"Player {activePlayer} used {currentAuras}/{maxAuras} auras - not advancing yet", LogCategory.Ability);
                }
            }

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(CheckAuraCompletion)}");
        }
        
        /// <summary>
        /// Check if a unit has already acted in the current phase
        /// </summary>
        public bool HasUnitActedThisPhase(DokkaebiUnit unit)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(HasUnitActedThisPhase)}");
            return unit != null && unitsActedThisPhase.Contains(unit);
        }
        
        /// <summary>
        /// Log the current turn system state
        /// </summary>
        public void LogTurnSystemState()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(LogTurnSystemState)}");
            TurnPhase phase = GetCurrentPhase();
            int turn = GetCurrentTurn();
            int activePlayer = GetActivePlayer();
            
            SmartLogger.Log($"TURN SYSTEM STATE: Turn {turn}, Phase {phase}, Active Player: {activePlayer}", LogCategory.TurnSystem);
            
            if (unitStateManager != null)
            {
                int p1Auras = unitStateManager.GetPlayer1AurasActivated();
                int p2Auras = unitStateManager.GetPlayer2AurasActivated();
                int maxAuras = unitStateManager.GetMaxAurasPerPhase();
                SmartLogger.Log($"P1 Auras: {p1Auras}/{maxAuras}, P2 Auras: {p2Auras}/{maxAuras}", LogCategory.TurnSystem);
                
                int p1Moves = unitStateManager.GetPlayer1UnitsMoved();
                int p2Moves = unitStateManager.GetPlayer2UnitsMoved();
                int p1Required = unitStateManager.GetRequiredPlayer1Moves();
                int p2Required = unitStateManager.GetRequiredPlayer2Moves();
                SmartLogger.Log($"P1 Moves: {p1Moves}/{p1Required}, P2 Moves: {p2Moves}/{p2Required}", LogCategory.TurnSystem);
            }

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(LogTurnSystemState)}");
        }
        
        /// <summary>
        /// Executes all pending moves by initiating pathfinding for each unit
        /// </summary>
        public void ExecuteAllPendingMoves()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(ExecuteAllPendingMoves)}");
            if (isExecutingMoves)
            {
                SmartLogger.Log("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Already executing moves, skipping", LogCategory.Movement);
                return;
            }

            isExecutingMoves = true;
            SmartLogger.Log("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Starting execution of pending moves", LogCategory.Movement);

            // Call ExecutePendingMove for all AI agents before processing units
            // Ensure EnemyAIManager.Instance is not null before accessing GetAllAgents
            var aiAgents = EnemyAIManager.Instance?.GetAllAgents();
            AIWorldState currentWorldState = null; // Placeholder
            if (aiAgents != null)
            {
                foreach (var agent in aiAgents)
                {
                    if (agent != null) // Added null check for agent
                    {
                        agent.ExecutePendingMove(currentWorldState);
                    }
                }
            }


            // Get units from UnitManager
            if (UnitManager.Instance == null)
            {
                SmartLogger.LogError("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] UnitManager.Instance is null!", LogCategory.Movement);
                isExecutingMoves = false;
                return;
            }

            // Initialize set to track reserved target tiles
            HashSet<GridPosition> reservedTargetTiles = new HashSet<GridPosition>();
            SmartLogger.Log("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Initialized reserved target tile set", LogCategory.Movement);

            var unitsToProcess = UnitManager.Instance.GetAliveUnits();
            SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Found {unitsToProcess.Count} alive units from UnitManager.", LogCategory.Movement);

            // Process all alive units
            foreach (var unit in unitsToProcess)
            {
                if (unit == null) continue;

                SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Checking unit {unit.GetUnitName()} (ID: {unit.UnitId}). HasPendingMovement: {unit.HasPendingMovement}", LogCategory.TurnSystem);

                if (unit.HasPendingMovement)
                {
                    var targetPosition = unit.GetPendingTargetPosition();
                    GridPosition currentPos = unit.CurrentGridPosition;
                    
                    // Only process the move if the target is different from current position
                    if (targetPosition != currentPos)
                    {
                        SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Unit {unit.GetUnitName()} has pending move to {targetPosition}", LogCategory.Movement);

                        // Check if target tile is already reserved
                        if (reservedTargetTiles.Contains(targetPosition))
                        {
                            SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Conflict: Tile {targetPosition} already reserved. Cancelling move for {unit.GetUnitName()}", LogCategory.Movement);
                            unit.ClearPendingMovement();
                            continue;
                        }

                        // Reserve the tile for this unit
                        reservedTargetTiles.Add(targetPosition);
                        SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Tile {targetPosition} reserved for {unit.GetUnitName()}", LogCategory.Movement);
                        
                        // Get the movement handler and initiate movement
                        DokkaebiMovementHandler handler = unit.GetComponent<DokkaebiMovementHandler>();
                        if (handler != null)
                        {
                            SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Initiating movement for {unit.GetUnitName()} to target {targetPosition}", LogCategory.Movement);
                            handler.RequestPath(targetPosition);
                            
                            // Clear pending state AFTER initiating movement
                            unit.ClearPendingMovement();
                        }
                        else
                        {
                            SmartLogger.LogError($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Unit {unit.GetUnitName()} has no DokkaebiMovementHandler component!", LogCategory.Movement);
                            // Remove reservation since movement failed
                            reservedTargetTiles.Remove(targetPosition);
                        }
                    }
                    else
                    {
                        SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Unit {unit.GetUnitName()} has pending movement but target position is same as current position", LogCategory.Movement);
                        unit.ClearPendingMovement();
                    }
                }
            }

            // Mark execution as complete
            isExecutingMoves = false;
            SmartLogger.Log("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Completed execution of pending moves", LogCategory.Movement);

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(ExecuteAllPendingMoves)}");
        }
        
        /// <summary>
        /// Enable or disable debug logging
        /// </summary>
        public void ToggleDebugMode(bool enabled)
        {
            debugLogTurns = enabled;
            SmartLogger.SetCategoryEnabled(LogCategory.TurnSystem, enabled);
            SmartLogger.Log($"Debug mode {(enabled ? "enabled" : "disabled")}", LogCategory.TurnSystem);
        }
        
        // Getters that use the turn state context
        public bool IsPlayerTurn() => GetActivePlayer() == 1 || GetActivePlayer() == 0;
        public bool IsExecutingMoves() => isExecutingMoves;
        public bool IsPhaseAdvancementLocked() => turnStateContext?.IsTransitionLocked ?? false;

        // ITurnSystem implementation
        public bool CanUnitMove(IDokkaebiUnit unit)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(CanUnitMove)}");
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                return CanUnitMove(dokkaebiUnit);
            }
            return false;
        }
        
        public bool CanUnitUseAura(IDokkaebiUnit unit)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(CanUnitUseAura)}");
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                return CanUnitUseAura(dokkaebiUnit);
            }
            return false;
        }
        
        public void QueueMove(IDokkaebiUnit unit, GridPosition targetPosition)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(QueueMove)}");
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                QueueMove(dokkaebiUnit, targetPosition);
            }
        }
        
        public void EndMovementPhase()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(EndMovementPhase)}");
            SmartLogger.Log($"[DokkaebiTurnSystemCore.EndMovementPhase] Called. Current phase: {GetCurrentPhase()}, IsTransitionLocked: {turnStateContext?.IsTransitionLocked ?? false}", LogCategory.TurnSystem);
            
            if (turnStateContext != null && GetCurrentPhase() == TurnPhase.MovementPhase)
            {
                SmartLogger.Log("[DokkaebiTurnSystemCore.EndMovementPhase] Attempting to transition to next state", LogCategory.TurnSystem);
                turnStateContext.TransitionToNextState();
                
                // Log the result
                SmartLogger.Log($"[DokkaebiTurnSystemCore.EndMovementPhase] After transition attempt. New phase: {GetCurrentPhase()}", LogCategory.TurnSystem);
            }
            else
            {
                SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.EndMovementPhase] Cannot end movement phase. TurnStateContext null: {turnStateContext == null}, Current phase: {GetCurrentPhase()}", LogCategory.TurnSystem);
            }

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(EndMovementPhase)}");
        }

        public void ForceTransitionTo(TurnPhase phase)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(ForceTransitionTo)}");
            if (turnStateContext != null)
            {
                turnStateContext.ForceTransitionTo(phase);
            }
        }

        public bool RequestMovement(DokkaebiUnit unit, GridPosition targetPosition)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(RequestMovement)}");
            if (!unit || !turnStateContext.AllowsMovement() || isExecutingMoves)
            {
                return false;
            }
            
            // Add to pending moves
            pendingMoves[unit] = targetPosition;
            return true;
        }
        
        public bool RequestAuraActivation(DokkaebiUnit unit, bool isPlayer1)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(RequestAuraActivation)}");
            if (!unit || !turnStateContext.AllowsAuraActivation(isPlayer1))
            {
                return false;
            }
            
            // Add to units that acted this phase
            unitsActedThisPhase.Add(unit);
            return true;
        }

        /// <summary>
        /// Sets the turn system state based on network data
        /// </summary>
        /// <param name="turnNumber">The turn number to set</param>
        /// <param name="phase">The phase to transition to</param>
        /// <param name="activePlayer">The active player (1 for player 1, 0 for player 2)</param>
        public void SetState(int turnNumber, TurnPhase phase, int activePlayer)
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(SetState)}");
            if (turnStateContext == null)
            {
                Debug.LogError("Cannot set state: TurnStateContext is null");
                return;
            }

            // Set turn number first
            turnStateContext.SetTurn(turnNumber);

            // Force transition to the target phase
            turnStateContext.ForceTransitionTo(phase);

            // Log the state change
            if (debugLogTurns)
            {
                SmartLogger.Log($"Turn system state set: Turn {turnNumber}, Phase {phase}, Active Player {activePlayer}", LogCategory.TurnSystem);
            }

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(SetState)}");
        }

        public float GetRemainingPhaseTime()
        {
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(GetRemainingPhaseTime)}");
            return turnStateContext != null ? turnStateContext.GetRemainingTime() : 0f;
        }

        /// <summary>
        /// Resets the turn system to its initial state.
        /// </summary>
        public void ResetTurnSystem()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: ENTER {nameof(ResetTurnSystem)}");
            // Clear all collections
            pendingMoves.Clear();
            unitsActedThisPhase.Clear();
            registeredUnits.Clear();

            // Reset movement tracking
            isExecutingMoves = false;
            totalMovesMade = 0;

            // Reset turn state context
            if (turnStateContext != null)
            {
                // Reset to turn 1, opening phase, player 1
                SetState(1, TurnPhase.Opening, 1);
            }

            // Reset all units via UnitManager if available
            if (UnitManager.Instance != null)
            {
                // Ensure all units' action states are reset when the turn system is reset
                SmartLogger.Log("[DokkaebiTurnSystemCore.ResetTurnSystem] Calling UnitManager.ResetActionStates() as part of system reset.", LogCategory.TurnSystem);
                UnitManager.Instance.ResetActionStates(true); // Reset player unit states
                UnitManager.Instance.ResetActionStates(false); // Reset enemy unit states
                var allUnits = UnitManager.Instance.GetAliveUnits();
                if (allUnits != null)
                {
                    foreach (var unit in allUnits)
                    {
                        if (unit != null)
                        {
                            unit.ResetActionState();
                        }
                    }
                }
            }

            if (debugLogTurns)
            {
                SmartLogger.Log("Turn system has been reset to initial state", LogCategory.TurnSystem);
            }

            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] DTSCore: EXIT {nameof(ResetTurnSystem)}");
        }
    }
}
