using System;
using UnityEngine;
using Dokkaebi.Common;
using Dokkaebi.Utilities;
using Dokkaebi.Core.TurnStates;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Context class for managing turn phase states
    /// </summary>
    public class TurnStateContext
    {
        private ITurnPhaseState currentState;
        private int currentTurn = 1;
        private readonly DokkaebiTurnSystemCore turnSystem;
        
        public event Action<TurnPhase> OnPhaseChanged;
        public event Action<int> OnTurnChanged;
        public event Action OnMovementPhaseStart;
        public event Action OnMovementPhaseEnd;
        public event Action OnTurnResolutionEnd;
        
        public bool IsTransitionLocked { get; private set; }
        
        public DokkaebiTurnSystemCore GetTurnSystem() => turnSystem;
        
        public TurnStateContext(DokkaebiTurnSystemCore turnSystem)
        {
            this.turnSystem = turnSystem;
            // Initialize with opening phase
            currentState = new OpeningPhaseState(this);
            currentState.Enter();
        }
        
        public void Update(float deltaTime)
        {
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] TurnStateContext: ENTER {nameof(Update)}");
            if (currentState != null)
            {
                currentState.Update(deltaTime);
            }
            //UnityEngine.Debug.LogError($"[DEBUG_FREEZE] TurnStateContext: EXIT {nameof(Update)}");
        }
        
        public void TransitionToNextState()
        {
            // --- ADDED LOG 4 ---
            SmartLogger.Log($"[TurnSystem] [TurnStateContext.TransitionToNextState] Transitioning from: {currentState.PhaseType}", LogCategory.TurnSystem, null);
            // --- END ADDED LOG 4 ---
            ITurnPhaseState nextState = currentState.GetNextState();
            // --- ADDED LOG 5 ---
            SmartLogger.Log($"[TurnSystem] [TurnStateContext.TransitionToNextState] Calculated next state: {nextState?.PhaseType.ToString() ?? "NULL"}", LogCategory.TurnSystem, null);
            // --- END ADDED LOG 5 ---
            if (nextState != null)
            {
                TransitionTo(nextState);
            }
            else
            {
                SmartLogger.LogError($"[TurnSystem] [TurnStateContext.TransitionToNextState] Failed to find next state after {currentState.PhaseType}.", LogCategory.TurnSystem, null);
            }
        }
        
        public void TransitionTo(ITurnPhaseState newState)
        {
            SmartLogger.Log($"[TurnStateContext.TransitionTo] ========== STATE TRANSITION START ==========", LogCategory.TurnSystem, null);
            SmartLogger.Log($"[TurnStateContext.TransitionTo] Turn {currentTurn}", LogCategory.TurnSystem, null);

            // Log initial state
            SmartLogger.Log($"[TurnStateContext.TransitionTo] Initial State:", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Current Phase: {currentState?.PhaseType ?? TurnPhase.Opening}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Active Player: {currentState?.GetActivePlayer() ?? 0}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Is Transition Locked: {IsTransitionLocked}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Allows Movement: {currentState?.AllowsMovement() ?? false}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Allows P1 Aura: {currentState?.AllowsAuraActivation(true) ?? false}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Allows P2 Aura: {currentState?.AllowsAuraActivation(false) ?? false}", LogCategory.TurnSystem, null);

            // Validate transition
            if (currentState == null)
            {
                SmartLogger.LogWarning("[TurnStateContext.TransitionTo] FAILED: Current state is null", LogCategory.TurnSystem, null);
                SmartLogger.Log("[TurnStateContext.TransitionTo] ========== STATE TRANSITION ABORTED ==========", LogCategory.TurnSystem, null);
                return;
            }
            if (IsTransitionLocked)
            {
                SmartLogger.LogWarning("[TurnStateContext.TransitionTo] FAILED: Transitions are locked", LogCategory.TurnSystem, null);
                SmartLogger.Log("[TurnStateContext.TransitionTo] ========== STATE TRANSITION ABORTED ==========", LogCategory.TurnSystem, null);
                return;
            }
            if (newState == null)
            {
                SmartLogger.LogWarning("[TurnStateContext.TransitionTo] FAILED: New state is null", LogCategory.TurnSystem, null);
                SmartLogger.Log("[TurnStateContext.TransitionTo] ========== STATE TRANSITION ABORTED ==========", LogCategory.TurnSystem, null);
                return;
            }

            // Log target state
            SmartLogger.Log($"[TurnStateContext.TransitionTo] Target State:", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Target Phase: {newState.PhaseType}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Target Active Player: {newState.GetActivePlayer()}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Will Allow Movement: {newState.AllowsMovement()}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Will Allow P1 Aura: {newState.AllowsAuraActivation(true)}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Will Allow P2 Aura: {newState.AllowsAuraActivation(false)}", LogCategory.TurnSystem, null);

            // Execute transition
            TurnPhase oldPhase = currentState.PhaseType;
            int oldActivePlayer = currentState.GetActivePlayer();

            SmartLogger.Log("[TurnStateContext.TransitionTo] Exiting current state...", LogCategory.TurnSystem, null);
            currentState.Exit();
            SmartLogger.Log($"[TurnStateContext.TransitionTo] Current state ({oldPhase}) exited", LogCategory.TurnSystem, null);

            currentState = newState;
            SmartLogger.Log("[TurnStateContext.TransitionTo] Entering new state...", LogCategory.TurnSystem, null);
            currentState.Enter();
            SmartLogger.Log($"[TurnStateContext.TransitionTo] New state ({currentState.PhaseType}) entered", LogCategory.TurnSystem, null);

            // Handle phase change events
            if (oldPhase != currentState.PhaseType)
            {
                SmartLogger.Log($"[TurnStateContext.TransitionTo] Phase Change Detected:", LogCategory.TurnSystem, null);
                SmartLogger.Log($"- Old Phase: {oldPhase}", LogCategory.TurnSystem, null);
                SmartLogger.Log($"- New Phase: {currentState.PhaseType}", LogCategory.TurnSystem, null);
                SmartLogger.Log($"- Old Active Player: {oldActivePlayer}", LogCategory.TurnSystem, null);
                SmartLogger.Log($"- New Active Player: {currentState.GetActivePlayer()}", LogCategory.TurnSystem, null);

                SmartLogger.Log("[TurnStateContext.TransitionTo] Firing OnPhaseChanged event...", LogCategory.TurnSystem, null);
                OnPhaseChanged?.Invoke(currentState.PhaseType);
                SmartLogger.Log("[TurnStateContext.TransitionTo] OnPhaseChanged event fired", LogCategory.TurnSystem, null);

                // Handle movement phase specific events
                if (currentState.PhaseType == TurnPhase.MovementPhase)
                {
                    SmartLogger.Log("[TurnStateContext.TransitionTo] Firing OnMovementPhaseStart event...", LogCategory.TurnSystem, null);
                    OnMovementPhaseStart?.Invoke();
                    SmartLogger.Log("[TurnStateContext.TransitionTo] OnMovementPhaseStart event fired", LogCategory.TurnSystem, null);
                }
                else if (oldPhase == TurnPhase.MovementPhase)
                {
                    SmartLogger.Log("[TurnStateContext.TransitionTo] Firing OnMovementPhaseEnd event...", LogCategory.TurnSystem, null);
                    OnMovementPhaseEnd?.Invoke();
                    SmartLogger.Log("[TurnStateContext.TransitionTo] OnMovementPhaseEnd event fired", LogCategory.TurnSystem, null);
                }
            }
            else
            {
                SmartLogger.Log("[TurnStateContext.TransitionTo] No phase change detected", LogCategory.TurnSystem, null);
            }

            // Log final state
            SmartLogger.Log($"[TurnStateContext.TransitionTo] Final State:", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Current Phase: {currentState.PhaseType}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Active Player: {currentState.GetActivePlayer()}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Allows Movement: {currentState.AllowsMovement()}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Allows P1 Aura: {currentState.AllowsAuraActivation(true)}", LogCategory.TurnSystem, null);
            SmartLogger.Log($"- Allows P2 Aura: {currentState.AllowsAuraActivation(false)}", LogCategory.TurnSystem, null);

            SmartLogger.Log($"[TurnStateContext.TransitionTo] ========== STATE TRANSITION COMPLETE ==========", LogCategory.TurnSystem, null);
        }
        
        /// <summary>
        /// Triggers the OnTurnResolutionEnd event without incrementing the turn
        /// </summary>
        public void TriggerTurnResolutionEnd()
        {
            SmartLogger.Log($"Resolving turn {currentTurn} effects", LogCategory.TurnSystem);
            OnTurnResolutionEnd?.Invoke();
        }
        
        public void IncrementTurn()
        {
            // We no longer trigger OnTurnResolutionEnd here since it's now called explicitly in AuraPhase2BState.Exit()
            
            currentTurn++;
            OnTurnChanged?.Invoke(currentTurn);
            SmartLogger.Log($"Turn incremented to {currentTurn}", LogCategory.TurnSystem);
        }
        
        public void LockTransition()
        {
            IsTransitionLocked = true;
            SmartLogger.Log("Phase transitions locked", LogCategory.TurnSystem);
        }
        
        public void UnlockTransition()
        {
            IsTransitionLocked = false;
            SmartLogger.Log("Phase transitions unlocked", LogCategory.TurnSystem);
        }
        
        public void ForceTransitionTo(TurnPhase targetPhase)
        {
            // Temporarily unlock transitions
            bool wasLocked = IsTransitionLocked;
            IsTransitionLocked = false;
            
            // Create the appropriate state
            ITurnPhaseState targetState = null;
            switch (targetPhase)
            {
                case TurnPhase.Opening:
                    targetState = new OpeningPhaseState(this);
                    break;
                case TurnPhase.MovementPhase:
                    targetState = new MovementPhaseState(this);
                    break;
                case TurnPhase.AuraPhase1A:
                    targetState = new AuraPhase1AState(this);
                    break;
                case TurnPhase.AuraPhase1B:
                    targetState = new AuraPhase1BState(this);
                    break;
                case TurnPhase.AuraPhase2A:
                    targetState = new AuraPhase2AState(this);
                    break;
                case TurnPhase.AuraPhase2B:
                    targetState = new AuraPhase2BState(this);
                    break;
            }
            
            // Transition to the target state
            if (targetState != null)
            {
                TransitionTo(targetState);
                SmartLogger.Log($"Forced transition to {targetPhase}", LogCategory.TurnSystem);
            }
            
            // Restore lock state
            IsTransitionLocked = wasLocked;
        }
        
        // Getters
        public TurnPhase GetCurrentPhase() => currentState?.PhaseType ?? TurnPhase.Opening;
        public int GetCurrentTurn() => currentTurn;
        public int GetActivePlayer() => currentState?.GetActivePlayer() ?? 0;
        public bool AllowsMovement() => currentState?.AllowsMovement() ?? false;
        public bool AllowsAuraActivation(bool isPlayerOne) => currentState?.AllowsAuraActivation(isPlayerOne) ?? false;
        
        /// <summary>
        /// Gets the remaining time in the current phase
        /// </summary>
        public float GetRemainingTime()
        {
            if (currentState == null) return 0f;
            return Mathf.Max(0f, currentState.GetPhaseTimeLimit() - currentState.GetStateTimer());
        }
        
        // Force a specific turn number (for debugging or save/load)
        public void SetTurn(int turnNumber)
        {
            if (turnNumber < 1)
                turnNumber = 1;
                
            if (currentTurn != turnNumber)
            {
                currentTurn = turnNumber;
                OnTurnChanged?.Invoke(currentTurn);
                SmartLogger.Log($"Turn set to {currentTurn}", LogCategory.TurnSystem);
            }
        }
    }
}