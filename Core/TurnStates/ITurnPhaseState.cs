using System;
using System.Collections.Generic;
using UnityEngine;
using Dokkaebi.Utilities;
using Dokkaebi.Common;
using System.Linq;
using Dokkaebi.Zones;
using Dokkaebi.Interfaces;

namespace Dokkaebi.Core.TurnStates
{
    /// <summary>
    /// Base interface for all turn phase states
    /// </summary>
    public interface ITurnPhaseState
    {
        TurnPhase PhaseType { get; }
        void Enter();
        void Update(float deltaTime);
        void Exit();
        ITurnPhaseState GetNextState();
        bool CanTransition();
        int GetActivePlayer();
        bool AllowsMovement();
        bool AllowsAuraActivation(bool isPlayerOne);
        float GetStateTimer();
        float GetPhaseTimeLimit();
    }

    /// <summary>
    /// Base class for all turn phase states
    /// </summary>
    public abstract class BaseTurnPhaseState : ITurnPhaseState
    {
        protected readonly TurnStateContext context;
        protected float stateTimer;
        protected float phaseTimeLimit;
        
        public abstract TurnPhase PhaseType { get; }
        
        protected BaseTurnPhaseState(TurnStateContext context)
        {
            this.context = context;
            // Set the phase time limit based on the phase type
            phaseTimeLimit = GetPhaseTimeLimit();
        }
        
        protected virtual float GetPhaseTimeLimit()
        {
            var turnSystem = context.GetTurnSystem();
            switch (PhaseType)
            {
                case TurnPhase.Opening:
                    return turnSystem.OpeningPhaseDuration;
                case TurnPhase.MovementPhase:
                    return turnSystem.MovementPhaseDuration;
                case TurnPhase.AuraPhase1A:
                case TurnPhase.AuraPhase1B:
                case TurnPhase.AuraPhase2A:
                case TurnPhase.AuraPhase2B:
                    return turnSystem.AuraChargingPhaseDuration;
                default:
                    return 30f; // Default fallback
            }
        }
        
        public virtual void Enter()
        {
            SmartLogger.Log($"[BaseTurnPhaseState.Enter ENTRY] Phase: {PhaseType}", LogCategory.TurnSystem, null);
            stateTimer = 0f;
            SmartLogger.Log($"Entering {PhaseType} phase", LogCategory.TurnSystem);
            SmartLogger.Log($"[BaseTurnPhaseState.Enter EXIT] Phase: {PhaseType}", LogCategory.TurnSystem, null);
        }
        
        public virtual void Update(float deltaTime)
        {
            stateTimer += deltaTime;
            // --- ADDED LOG 1 ---
            SmartLogger.Log($"[BaseTurnPhaseState.Update] Phase: {PhaseType}, stateTimer: {stateTimer:F2}, phaseTimeLimit: {phaseTimeLimit:F2}, CanTransition: {CanTransition()} (before transition check)", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
            // --- END ADDED LOG 1 ---
            if (PhaseType == TurnPhase.MovementPhase)
            {
                SmartLogger.Log($"[{PhaseType}] Update: stateTimer={stateTimer:F2}, phaseTimeLimit={phaseTimeLimit:F2}, CanTransition()={CanTransition()}", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
            }
            // Log timer state periodically (every second)
            if (Mathf.FloorToInt(stateTimer) > Mathf.FloorToInt(stateTimer - deltaTime))
            {
                if (PhaseType == TurnPhase.Opening)
                {
                    SmartLogger.Log($"[{PhaseType}] Timer Update: {stateTimer:F2}/{phaseTimeLimit:F2}, CanTransition: {CanTransition()}", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
                }
            }
            // Auto-advance when time expires and phase limit is positive
            const float epsilon = 0.1f;
            if (phaseTimeLimit > 0 && stateTimer >= phaseTimeLimit - epsilon && CanTransition())
            {
                // --- ADDED LOG 2 ---
                SmartLogger.Log($"[BaseTurnPhaseState.Update] Transition condition met. Phase: {PhaseType}, stateTimer: {stateTimer:F2}, phaseTimeLimit: {phaseTimeLimit:F2}, CanTransition: {CanTransition()} (about to call TransitionToNextState)", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
                // --- END ADDED LOG 2 ---
                context.TransitionToNextState();
                // --- ADDED LOG 3 ---
                SmartLogger.Log($"[BaseTurnPhaseState.Update] Returned from TransitionToNextState call. Phase: {PhaseType}", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
                // --- END ADDED LOG 3 ---
            }
        }
        
        public virtual void Exit()
        {
            SmartLogger.Log($"Exiting {PhaseType} phase", LogCategory.TurnSystem);
        }
        
        public abstract ITurnPhaseState GetNextState();
        
        public virtual bool CanTransition()
        {
            return !context.IsTransitionLocked;
        }
        
        public virtual int GetActivePlayer()
        {
            // For AuraPhase1A and AuraPhase2A, player 1 is active
            if (PhaseType == TurnPhase.AuraPhase1A || PhaseType == TurnPhase.AuraPhase2A)
                return 1;
            // For AuraPhase1B and AuraPhase2B, player 2 is active
            if (PhaseType == TurnPhase.AuraPhase1B || PhaseType == TurnPhase.AuraPhase2B)
                return 2;
            // For movement phase and opening, both players are active
            return 0;
        }
        
        public virtual bool AllowsMovement()
        {
            return PhaseType == TurnPhase.MovementPhase;
        }
        
        public virtual bool AllowsAuraActivation(bool isPlayerOne)
        {
            // Only allow aura activation in the appropriate phase for each player
            if (isPlayerOne)
            {
                return PhaseType == TurnPhase.AuraPhase1A || PhaseType == TurnPhase.AuraPhase2A;
            }
            else
            {
                return PhaseType == TurnPhase.AuraPhase1B || PhaseType == TurnPhase.AuraPhase2B;
            }
        }

        public float GetStateTimer()
        {
            return stateTimer;
        }

        float ITurnPhaseState.GetPhaseTimeLimit()
        {
            return phaseTimeLimit;
        }

        /// <summary>
        /// Helper method to apply zone effects to units at the start of a phase
        /// </summary>
        protected void ApplyZoneEffectsToUnitsInZone()
        {
            SmartLogger.Log($"[BaseTurnPhaseState.ApplyZoneEffectsToUnitsInZone ENTRY] Phase: {PhaseType}", LogCategory.Zone, null);
            SmartLogger.Log($"[{PhaseType}] Applying zone effects to units in zones at start of phase.", LogCategory.Zone);

            ZoneManager zoneManager = ZoneManager.Instance;
            UnitManager unitManager = UnitManager.Instance;

            if (zoneManager == null || unitManager == null)
            {
                if (zoneManager == null) SmartLogger.LogError($"[{PhaseType}] ZoneManager.Instance is null!", LogCategory.Zone);
                if (unitManager == null) SmartLogger.LogError($"[{PhaseType}] UnitManager.Instance is null!", LogCategory.Zone);
                return;
            }

            // Get active ZoneInstance objects using the new method
            var activeZones = zoneManager.GetAllActiveZoneInstances();
            var activeUnits = unitManager.GetAliveUnits();

            if (activeZones == null || activeZones.Count == 0) {
                SmartLogger.Log($"[BaseTurnPhaseState.ApplyZoneEffectsToUnitsInZone] No active zones to process, returning cleanly.", LogCategory.Zone, null);
                return;
            }

            SmartLogger.Log($"[{PhaseType}] Checking {activeUnits.Count} active units against {activeZones.Count} active zones.", LogCategory.Zone);

            int unitCount = 0;
            foreach (var unit in activeUnits)
            {
                unitCount++;
                SmartLogger.Log($"Processing unit {++unitCount}/{activeUnits.Count}: {unit?.GetUnitName() ?? "NULL"}", LogCategory.Zone, null);

                if (unit == null || !unit.IsAlive) continue;

                int zoneCount = 0;
                foreach (var zoneInstance in activeZones)
                {
                    zoneCount++;
                    SmartLogger.Log($"Checking zone {++zoneCount}/{activeZones.Count}: {zoneInstance?.DisplayName ?? "NULL"}", LogCategory.Zone, null);

                    if (zoneInstance != null && zoneInstance.IsActive)
                    {
                        SmartLogger.Log($"Before ContainsPosition check: Unit={unit?.GetUnitName() ?? "NULL"}, Zone={zoneInstance?.DisplayName ?? "NULL"}", LogCategory.Zone, null);
                        if (zoneInstance.ContainsPosition(unit.GetGridPosition()))
                        {
                            SmartLogger.Log($"Unit {unit?.GetUnitName() ?? "NULL"} is inside zone {zoneInstance?.DisplayName ?? "NULL"}", LogCategory.Zone, null);
                            SmartLogger.Log($"Before ApplyStatusEffectToUnitImmediate: Unit={unit?.GetUnitName() ?? "NULL"}, Zone={zoneInstance?.DisplayName ?? "NULL"}", LogCategory.Zone, null);
                            zoneInstance.ApplyStatusEffectToUnitImmediate(unit);
                            SmartLogger.Log($"After ApplyStatusEffectToUnitImmediate: Unit={unit?.GetUnitName() ?? "NULL"}, Zone={zoneInstance?.DisplayName ?? "NULL"}", LogCategory.Zone, null);
                        }
                        else
                        {
                            SmartLogger.Log($"Unit {unit?.GetUnitName() ?? "NULL"} is NOT inside zone {zoneInstance?.DisplayName ?? "NULL"}", LogCategory.Zone, null);
                        }
                    }
                }
            }

            SmartLogger.Log($"[{PhaseType}] Finished applying zone effects at start of phase.", LogCategory.Zone);
            SmartLogger.Log($"[BaseTurnPhaseState.ApplyZoneEffectsToUnitsInZone EXIT] Phase: {PhaseType}", LogCategory.Zone, null);
        }
    }

    /// <summary>
    /// Opening phase - initial game setup
    /// </summary>
    public class OpeningPhaseState : BaseTurnPhaseState
    {
        public override TurnPhase PhaseType => TurnPhase.Opening;
        
        public OpeningPhaseState(TurnStateContext context) : base(context) { }
        
        public override ITurnPhaseState GetNextState()
        {
            return new MovementPhaseState(context);
        }
        
        public override int GetActivePlayer()
        {
            return 0; // Both players active
        }
        
        public override bool AllowsMovement()
        {
            return true; // Special case for initial placement
        }

        public override void Enter()
        {
            base.Enter();
            SmartLogger.Log("[OpeningPhaseState] Entered opening phase", LogCategory.TurnSystem);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }

        public override void Exit()
        {
            base.Exit();
        }
    }

    /// <summary>
    /// Movement phase - both players move simultaneously
    /// </summary>
    public class MovementPhaseState : BaseTurnPhaseState
    {
        public override TurnPhase PhaseType => TurnPhase.MovementPhase;
        
        public MovementPhaseState(TurnStateContext context) : base(context) 
        {
            SmartLogger.Log($"[MovementPhaseState] Initialized with phaseTimeLimit: {phaseTimeLimit:F2}", LogCategory.Debug);
        }
        
        public override void Enter()
        {
            base.Enter();
            SmartLogger.Log("[MovementPhaseState] Entered movement phase", LogCategory.TurnSystem);
            ApplyZoneEffectsToUnitsInZone();
        }
        
        public override void Exit()
        {
            SmartLogger.Log("[MovementPhaseState] Exiting movement phase, executing pending moves", LogCategory.Debug);
            base.Exit();
            
            // Execute any remaining pending moves before transitioning
            var turnSystem = context.GetTurnSystem();
            if (turnSystem is DokkaebiTurnSystemCore core)
            {
                SmartLogger.Log("[MovementPhaseState] Executing any remaining pending moves before exiting movement phase", LogCategory.Debug);
                core.ExecuteAllPendingMoves();
            }
            else
            {
                SmartLogger.LogWarning("[MovementPhaseState] TurnSystem is not DokkaebiTurnSystemCore, skipping pending moves execution", LogCategory.TurnSystem);
            }

            // --- Execute Pending Moves for AI Agents ---
            var enemyAIManager = Dokkaebi.AI.EnemyAIManager.Instance;
            if (enemyAIManager != null)
            {
                // TODO: Replace null with actual world state retrieval if needed
                Dokkaebi.AI.Data.AIWorldState currentActualWorldState = null;
                var aiAgents = enemyAIManager.GetAllAgents();
                SmartLogger.Log($"[MovementPhaseState] Found {aiAgents.Count()} AI agents to check for pending moves.", LogCategory.Movement);
                foreach (var agent in aiAgents)
                {
                    agent.ExecutePendingMove(currentActualWorldState);
                }
                SmartLogger.Log("[MovementPhaseState] Completed execution of pending moves for AI agents.", LogCategory.Movement);
            }
            else
            {
                SmartLogger.LogWarning("[MovementPhaseState] EnemyAIManager instance not found. Cannot execute pending moves.", LogCategory.Movement);
            }
            // --- End Execute Pending Moves ---
        }
        
        public override ITurnPhaseState GetNextState()
        {
            SmartLogger.Log("[MovementPhaseState] Creating next state (BufferPhase)", LogCategory.TurnSystem);
            return new BufferPhaseState(context);
        }
        
        public override int GetActivePlayer()
        {
            return 0; // Both players active in movement phase
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }

        public override bool CanTransition()
        {
            // Log timer condition
            SmartLogger.Log($"[{PhaseType}] CanTransition() check: stateTimer={stateTimer:F2}, phaseTimeLimit={phaseTimeLimit:F2}, Timer Condition Met: {stateTimer >= phaseTimeLimit}", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
            // Check if both players have reached their max moves
            bool bothPlayersDone = false;
            var turnSystem = context.GetTurnSystem() as DokkaebiTurnSystemCore;
            if (turnSystem != null)
            {
                var unitStateManager = turnSystem.UnitStateManager;
                if (unitStateManager != null)
                {
                    bool p1Done = unitStateManager.HasReachedMaxMoves(true);
                    bool p2Done = unitStateManager.HasReachedMaxMoves(false);
                    bothPlayersDone = p1Done && p2Done;
                    SmartLogger.Log($"[{PhaseType}] CanTransition() check: Player1 Done: {p1Done}, Player2 Done: {p2Done}, Both Done: {bothPlayersDone}", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
                }
                else
                {
                    SmartLogger.LogWarning($"[{PhaseType}] CanTransition() - UnitStateManager not found on TurnSystem!", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
                }
            }
            else
            {
                SmartLogger.LogWarning($"[{PhaseType}] CanTransition() - TurnSystem is not DokkaebiTurnSystemCore!", LogCategory.TurnSystem);
            }
            // Combine timer and bothPlayersDone for the result (example logic, adapt as needed)
            bool result = (stateTimer >= phaseTimeLimit) || bothPlayersDone;
            SmartLogger.Log($"[{PhaseType}] CanTransition() returning: {result}", LogCategory.TurnSystem, context.GetTurnSystem() as MonoBehaviour);
            return result;
        }
    }

    /// <summary>
    /// First aura phase for the first player
    /// </summary>
    public class AuraPhase1AState : BaseTurnPhaseState
    {
        public override TurnPhase PhaseType => TurnPhase.AuraPhase1A;
        
        public AuraPhase1AState(TurnStateContext context) : base(context) { }
        
        public override ITurnPhaseState GetNextState()
        {
            return new AuraPhase1BState(context);
        }

        public override void Enter()
        {
            base.Enter();
            SmartLogger.Log("[AuraPhase1AState] Entered Aura phase 1A", LogCategory.TurnSystem);
            ApplyZoneEffectsToUnitsInZone();
        }

        public override void Exit()
        {
            SmartLogger.Log("[AuraPhase1AState.Exit] Exiting Phase 1A", LogCategory.TurnSystem);
            base.Exit();
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }
    }

    /// <summary>
    /// First aura phase for the second player
    /// </summary>
    public class AuraPhase1BState : BaseTurnPhaseState
    {
        public override TurnPhase PhaseType => TurnPhase.AuraPhase1B;
        
        public AuraPhase1BState(TurnStateContext context) : base(context) { }
        
        public override ITurnPhaseState GetNextState()
        {
            return new AuraPhase2AState(context);
        }

        public override void Enter()
        {
            SmartLogger.Log("[AuraPhase1BState.Enter ENTRY]", LogCategory.TurnSystem);
            base.Enter();
            ApplyZoneEffectsToUnitsInZone();
            SmartLogger.Log("[AuraPhase1BState.Enter EXIT]", LogCategory.TurnSystem);
        }

        public override void Exit()
        {
            SmartLogger.Log("[AuraPhase1BState.Exit ENTRY]", LogCategory.TurnSystem);
            SmartLogger.Log("[AuraPhase1BState.Exit] Exiting Phase 1B", LogCategory.TurnSystem);

            // Execute all queued AI ability actions for this phase
            Dokkaebi.AI.EnemyAIManager.Instance?.ExecuteQueuedActions(); // Use null-conditional for safety
            SmartLogger.Log("[AuraPhase1BState.Exit] Called EnemyAIManager.ExecuteQueuedActions.", LogCategory.TurnSystem);

            base.Exit();
            SmartLogger.Log("[AuraPhase1BState.Exit EXIT]", LogCategory.TurnSystem);
        }

        public override void Update(float deltaTime)
        {
            SmartLogger.Log("[AuraPhase1BState.Update ENTRY]", LogCategory.TurnSystem);
            base.Update(deltaTime);
            SmartLogger.Log("[AuraPhase1BState.Update EXIT]", LogCategory.TurnSystem);
        }
    }

    /// <summary>
    /// Second aura phase for the first player
    /// </summary>
    public class AuraPhase2AState : BaseTurnPhaseState
    {
        public override TurnPhase PhaseType => TurnPhase.AuraPhase2A;
        
        public AuraPhase2AState(TurnStateContext context) : base(context) { }
        
        public override ITurnPhaseState GetNextState()
        {
            return new AuraPhase2BState(context);
        }

        public override void Enter()
        {
            SmartLogger.Log("[AuraPhase2AState.Enter ENTRY]", LogCategory.TurnSystem);
            base.Enter();
            SmartLogger.Log("[AuraPhase2AState] Entered Aura phase 2A", LogCategory.TurnSystem);
            ApplyZoneEffectsToUnitsInZone();
            SmartLogger.Log("[AuraPhase2AState.Enter EXIT]", LogCategory.TurnSystem);
        }

        public override void Exit()
        {
            SmartLogger.Log("[AuraPhase2AState.Exit ENTRY]", LogCategory.TurnSystem);
            SmartLogger.Log("[AuraPhase2AState.Exit] Exiting Phase 2A", LogCategory.TurnSystem);
            base.Exit();
            SmartLogger.Log("[AuraPhase2AState.Exit EXIT]", LogCategory.TurnSystem);
        }

        public override void Update(float deltaTime)
        {
            SmartLogger.Log("[AuraPhase2AState.Update ENTRY]", LogCategory.TurnSystem);
            base.Update(deltaTime);
            SmartLogger.Log("[AuraPhase2AState.Update EXIT]", LogCategory.TurnSystem);
        }
    }

    /// <summary>
    /// Second aura phase for the second player
    /// </summary>
    public class AuraPhase2BState : BaseTurnPhaseState
    {
        public override TurnPhase PhaseType => TurnPhase.AuraPhase2B;
        
        public AuraPhase2BState(TurnStateContext context) : base(context) { }
        
        public override ITurnPhaseState GetNextState()
        {
            // After phase 2B, we go back to movement phase of the next turn
            return new MovementPhaseState(context);
        }

        public override void Enter()
        {
            SmartLogger.Log("[AuraPhase2BState.Enter ENTRY]", LogCategory.TurnSystem);
            base.Enter();
            SmartLogger.Log("[AuraPhase2BState] Entered Aura phase 2B", LogCategory.TurnSystem);
            ApplyZoneEffectsToUnitsInZone();
            SmartLogger.Log("[AuraPhase2BState.Enter EXIT]", LogCategory.TurnSystem);
        }

        public override void Exit()
        {
            SmartLogger.Log("[AuraPhase2BState.Exit ENTRY]", LogCategory.TurnSystem);
            SmartLogger.Log("[AuraPhase2BState.Exit START] Beginning turn resolution phase", LogCategory.TurnSystem);

            // Execute all queued AI ability actions for this phase
            Dokkaebi.AI.EnemyAIManager.Instance?.ExecuteQueuedActions(); // Use null-conditional for safety
            SmartLogger.Log("[AuraPhase2BState.Exit] Called EnemyAIManager.ExecuteQueuedActions.", LogCategory.TurnSystem);

            base.Exit();
            // --- CRITICAL: Execute all pending AI moves before resetting action state ---
            var turnSystem = context.GetTurnSystem();
            if (turnSystem is DokkaebiTurnSystemCore core)
            {
                SmartLogger.Log("[AuraPhase2BState.Exit] Executing all pending AI moves before turn resolution/reset.", LogCategory.TurnSystem);
                core.ExecuteAllPendingMoves();
            }
            else
            {
                SmartLogger.LogWarning("[AuraPhase2BState.Exit] TurnSystem is not DokkaebiTurnSystemCore, skipping pending AI moves execution", LogCategory.TurnSystem);
            }
            // --- END CRITICAL ---
            SmartLogger.Log("[AuraPhase2BState.Exit] Triggering turn resolution end", LogCategory.TurnSystem);
            SmartLogger.Log("[AuraPhase2BState.Exit] About to trigger TurnResolutionEnd event - this should trigger zone effects", LogCategory.TurnSystem);
            context.TriggerTurnResolutionEnd();
            SmartLogger.Log("[AuraPhase2BState.Exit] Incrementing turn counter", LogCategory.TurnSystem);
            context.IncrementTurn();
            SmartLogger.Log("[AuraPhase2BState.Exit END] Turn resolution phase complete", LogCategory.TurnSystem);
            SmartLogger.Log("[AuraPhase2BState.Exit EXIT]", LogCategory.TurnSystem);
        }

        public override void Update(float deltaTime)
        {
            SmartLogger.Log("[AuraPhase2BState.Update ENTRY]", LogCategory.TurnSystem);
            base.Update(deltaTime);
            SmartLogger.Log("[AuraPhase2BState.Update EXIT]", LogCategory.TurnSystem);
        }
    }

    /// <summary>
    /// Buffer phase - allows visual completion of movement before aura phase
    /// </summary>
    public class BufferPhaseState : BaseTurnPhaseState
    {
        public override TurnPhase PhaseType => TurnPhase.BufferPhase;

        public BufferPhaseState(TurnStateContext context) : base(context) { }

        public override ITurnPhaseState GetNextState()
        {
            return new AuraPhase1AState(context);
        }

        protected override float GetPhaseTimeLimit()
        {
            var turnSystem = context.GetTurnSystem();
            if (turnSystem is Dokkaebi.Core.DokkaebiTurnSystemCore core)
                return core.BufferPhaseDuration;
            return 2.0f; // fallback default
        }

        public override void Enter()
        {
            SmartLogger.Log("[BufferPhaseState.Enter ENTRY]", LogCategory.TurnSystem);
            base.Enter();
            SmartLogger.Log("[BufferPhaseState.Enter EXIT]", LogCategory.TurnSystem);
        }

        public override void Exit()
        {
            SmartLogger.Log("[BufferPhaseState.Exit ENTRY]", LogCategory.TurnSystem);
            base.Exit();
            SmartLogger.Log("[BufferPhaseState.Exit EXIT]", LogCategory.TurnSystem);
        }

        public override void Update(float deltaTime)
        {
            SmartLogger.Log("[BufferPhaseState.Update ENTRY]", LogCategory.TurnSystem);
            base.Update(deltaTime);
            SmartLogger.Log("[BufferPhaseState.Update EXIT]", LogCategory.TurnSystem);
        }
    }
}