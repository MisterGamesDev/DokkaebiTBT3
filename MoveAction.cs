using UnityEngine;
using System.Collections.Generic;
using Dokkaebi.Interfaces; // Required for GridPosition, ICommand, IDokkaebiUnit
using Dokkaebi.Units; // Required for DokkaebiUnit (still needed for accessing properties, but methods use interface)
using Dokkaebi.Core.Networking.Commands; // Required for MoveCommand (assuming commands are here)
using Dokkaebi.Utilities; // Required for SmartLogger

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Concrete AIAction representing a unit moving to a target grid position.
    /// </summary>
    [CreateAssetMenu(fileName = "MoveAction", menuName = "Dokkaebi/AI/Actions/Move")]
    public class MoveAction : AIAction
    {
        [Tooltip("The target grid position for this move action.")]
        // Note: For a real action, this might be determined dynamically during planning.
        // This field is more for defining the *type* of move action (e.g., move to cover, move to attack range).
        // The actual target position would likely be a parameter passed during planning or execution.
        // For this basic example, we'll assume the target position is determined elsewhere (e.g., by the agent).
        public GridPosition TargetPosition;

        // TODO: Consider adding parameters like MaxMoveDistance, PathfindingCostMultiplier, etc.

        /// <summary>
        /// Checks if the preconditions for performing the move action are met.
        /// Preconditions: Unit is alive, has enough movement points (if applicable), target position is walkable and not occupied.
        /// </summary>
        /// <param name="currentState">The current AIWorldState.</param>
        /// <param name="agentUnit">The IDokkaebiUnit performing the action.</param>
        /// <returns>True if preconditions are met, false otherwise.</returns>
        public override bool CheckPreconditions(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK START ==========", LogCategory.AI, this);
            SmartLogger.Log($"[MoveAction.CheckPreconditions] Action Details:", LogCategory.AI, this);
            SmartLogger.Log($"- Action Name: {ActionName}", LogCategory.AI, this);
            SmartLogger.Log($"- Target Position: {TargetPosition}", LogCategory.AI, this);
            SmartLogger.Log($"- Agent Unit ID: {agentUnit.UnitId}", LogCategory.AI, this);
            SmartLogger.Log($"- Agent Unit Name: {agentUnit.GetUnitName()}", LogCategory.AI, this);

            // --- PHASE/TURN CHECK ---
            var phase = currentState.TurnState.CurrentPhase;
            SmartLogger.Log($"[MoveAction.CheckPreconditions] Phase Check: Phase={phase}", LogCategory.AI, this);
            if (phase != Dokkaebi.Common.TurnPhase.MovementPhase)
            {
                SmartLogger.LogWarning($"[MoveAction.CheckPreconditions] FAIL: Not MovementPhase. Phase={phase}", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK FAILED ==========", LogCategory.AI, this);
                return false;
            }

            AIUnitState agentState = currentState.GetUnitState(agentUnit.UnitId);
            if (agentState == null)
            {
                SmartLogger.LogWarning($"[MoveAction.CheckPreconditions] FAIL: No AIUnitState for unit {agentUnit.UnitId}", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK FAILED ==========", LogCategory.AI, this);
                return false;
            }

            SmartLogger.Log($"[MoveAction.CheckPreconditions] Agent State:", LogCategory.AI, this);
            SmartLogger.Log($"- Current Position: {agentState.Position}", LogCategory.AI, this);
            SmartLogger.Log($"- Is Alive: {agentState.IsAlive}", LogCategory.AI, this);
            SmartLogger.Log($"- Movement Range: {agentState.MovementRange}", LogCategory.AI, this);

            if (!agentState.IsAlive)
            {
                SmartLogger.LogWarning($"[MoveAction.CheckPreconditions] FAIL: Unit {agentUnit.UnitId} is not alive.", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK FAILED ==========", LogCategory.AI, this);
                return false;
            }

            int distance = Dokkaebi.Interfaces.GridPosition.GetManhattanDistance(agentState.Position, TargetPosition);
            SmartLogger.Log($"[MoveAction.CheckPreconditions] Range Check:", LogCategory.AI, this);
            SmartLogger.Log($"- From Position: {agentState.Position}", LogCategory.AI, this);
            SmartLogger.Log($"- To Position: {TargetPosition}", LogCategory.AI, this);
            SmartLogger.Log($"- Distance: {distance}", LogCategory.AI, this);
            SmartLogger.Log($"- Movement Range: {agentState.MovementRange}", LogCategory.AI, this);

            if (distance > agentState.MovementRange)
            {
                SmartLogger.LogWarning($"[MoveAction.CheckPreconditions] FAIL: Target {TargetPosition} is out of movement range. Distance: {distance}, Max Range: {agentState.MovementRange}", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK FAILED ==========", LogCategory.AI, this);
                return false;
            }

            SmartLogger.Log($"[MoveAction.CheckPreconditions] Grid State Check:", LogCategory.AI, this);
            bool isWalkable = false;
            if (!currentState.GridState.IsWalkable.TryGetValue(TargetPosition, out isWalkable))
            {
                SmartLogger.LogWarning($"[MoveAction.CheckPreconditions] FAIL: Target position {TargetPosition} not found in IsWalkable dictionary.", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK FAILED ==========", LogCategory.AI, this);
                return false;
            }
            SmartLogger.Log($"- Is Walkable: {isWalkable}", LogCategory.AI, this);

            if (!isWalkable)
            {
                SmartLogger.LogWarning($"[MoveAction.CheckPreconditions] FAIL: Target {TargetPosition} is not walkable.", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK FAILED ==========", LogCategory.AI, this);
                return false;
            }

            bool isOccupied = currentState.IsTileOccupied(TargetPosition);
            SmartLogger.Log($"- Is Occupied: {isOccupied}", LogCategory.AI, this);
            if (isOccupied)
            {
                SmartLogger.LogWarning($"[MoveAction.CheckPreconditions] FAIL: Target {TargetPosition} is occupied.", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK FAILED ==========", LogCategory.AI, this);
                return false;
            }

            // Pathfinding check
            SmartLogger.Log($"[MoveAction.CheckPreconditions] Pathfinding Check:", LogCategory.AI, this);
            SmartLogger.Log($"- Start Position: {agentState.Position}", LogCategory.AI, this);
            SmartLogger.Log($"- Target Position: {TargetPosition}", LogCategory.AI, this);
            SmartLogger.Log($"- Max Steps: {agentState.MovementRange}", LogCategory.AI, this);

            var visited = new HashSet<GridPosition>();
            var queue = new Queue<(GridPosition pos, int steps)>();
            queue.Enqueue((agentState.Position, 0));
            visited.Add(agentState.Position);
            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };
            bool pathFound = false;
            int stepsToTarget = -1;

            while (queue.Count > 0)
            {
                var (current, steps) = queue.Dequeue();
                if (current == TargetPosition)
                {
                    pathFound = true;
                    stepsToTarget = steps;
                    break;
                }
                if (steps >= agentState.MovementRange)
                    continue;
                for (int i = 0; i < 4; i++)
                {
                    var neighbor = new GridPosition(current.x + dx[i], current.z + dz[i]);
                    if (visited.Contains(neighbor))
                        continue;
                    bool neighborWalkable = false;
                    if (!currentState.GridState.IsWalkable.TryGetValue(neighbor, out neighborWalkable) || !neighborWalkable)
                        continue;
                    if (currentState.IsTileOccupied(neighbor) && neighbor != TargetPosition)
                        continue;
                    visited.Add(neighbor);
                    queue.Enqueue((neighbor, steps + 1));
                }
            }

            if (pathFound)
            {
                SmartLogger.Log($"[MoveAction.CheckPreconditions] Path found to target in {stepsToTarget} steps.", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] All preconditions passed successfully!", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK PASSED ==========", LogCategory.AI, this);
                return true;
            }
            else
            {
                SmartLogger.LogWarning($"[MoveAction.CheckPreconditions] FAIL: No valid path found to {TargetPosition} within movement range {agentState.MovementRange}.", LogCategory.AI, this);
                SmartLogger.Log($"[MoveAction.CheckPreconditions] ========== PRECONDITION CHECK FAILED ==========", LogCategory.AI, this);
                return false;
            }
        }

        /// <summary>
        /// Applies the effects of the move action to a simulated world state.
        /// Effect: Updates the unit's position in the simulated state.
        /// </summary>
        /// <param name="currentState">The AIWorldState to apply effects to (should be a clone).</param>
        /// <param name="agentUnit">The IDokkaebiUnit performing the action.</param>
        /// <returns>The modified AIWorldState after applying effects.</returns>
        public override AIWorldState ApplyEffects(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            // Ensure we are modifying a clone! The planner should pass a clone.
            // Find the agent's unit state in the cloned world state and update its position.
            AIUnitState agentState = currentState.UnitStates.Find(u => u.UnitId == agentUnit.UnitId);
            if (agentState != null)
            {
                // Simulate the unit moving from its old position to the new target position
                // Find the agent's *original* position in the current state before cloning
                // Note: This requires finding the unit in the *original* currentState before cloning,
                // or storing the original position in the AIUnitState.
                // For simplicity in this simulation, we'll assume the unit was at the position
                // stored in agentState *before* this action's effects were applied.
                // A more robust simulation might require passing the previous state or position.

                // Let's assume agentState.Position holds the position *before* this action's effects.
                GridPosition oldPosition = agentState.Position; // This might be incorrect depending on planner's ApplyEffects usage
                // A more correct approach for GOAP: ApplyEffects receives the state *before* the action.
                // The state inside the Node in the planner's search holds the state *after* the action.
                // So, when ApplyEffects is called, currentState is the state *before* this action.
                // The agentState.Position in this currentState is the position *before* the move.

                // Find the agent state in the *original* currentState to get the position before this action
                AIUnitState originalAgentState = currentState.UnitStates.Find(u => u.UnitId == agentUnit.UnitId);
                if (originalAgentState != null)
                {
                    GridPosition positionBeforeMove = originalAgentState.Position;
                    if (currentState.GridState.OccupyingUnitId.ContainsKey(positionBeforeMove))
                    {
                        currentState.GridState.OccupyingUnitId[positionBeforeMove] = -1; // Mark old tile as unoccupied
                        SmartLogger.Log($"[MoveAction - {ActionName}] Applied simulated effect: Marked old tile {positionBeforeMove} as unoccupied in grid state.", LogCategory.AI, this);
                    }
                }
                else
                {
                    SmartLogger.LogWarning($"[MoveAction - {ActionName}] Original agent state not found to clear old position in grid state simulation.", LogCategory.AI, this);
                }

                agentState.Position = TargetPosition; // Update agent's position in the simulated state

                // Mark the new target tile as occupied by this unit in the simulated grid state
                currentState.GridState.OccupyingUnitId[TargetPosition] = agentUnit.UnitId;
                SmartLogger.Log($"[MoveAction - {ActionName}] Applied simulated effect: Marked new tile {TargetPosition} as occupied by unit {agentUnit.UnitId} in grid state.", LogCategory.AI, this);

                SmartLogger.Log($"[MoveAction] Applying effects: Unit {agentUnit.UnitId} ({agentUnit.GetUnitName()}) moved to {TargetPosition} in simulated state.", LogCategory.AI, this);
            }
            else
            {
                SmartLogger.LogWarning($"[MoveAction] Could not find unit {agentUnit.UnitId} in simulated world state to apply move effects.", LogCategory.AI, this);
            }

            return currentState; // Return the modified clone
        }

        /// <summary>
        /// Executes the move action in the actual game world by generating and submitting a MoveCommand.
        /// </summary>
        /// <param name="agentUnit">The IDokkaebiUnit performing the action.</param>
        /// <param name="currentState">The current AIWorldState (can be used to determine command parameters).</param>
        public override void Execute(IDokkaebiUnit agentUnit, AIWorldState currentState)
        {
            var dokkaebiUnit = agentUnit as Dokkaebi.Units.DokkaebiUnit;
            var unitPosLog = dokkaebiUnit != null ? dokkaebiUnit.GetGridPosition().ToString() : "Unknown";
            SmartLogger.Log($"[AI] [MoveAction.Execute] Called for unit {agentUnit?.UnitId} at position {unitPosLog}.", LogCategory.AI, this);
            // 1. Log the entry of the Execute method for the MoveAction, including the unit's ID and the target position from the action data.
            SmartLogger.Log($"[MoveAction] Execute ENTRY for unit {agentUnit.UnitId} to position {TargetPosition}", LogCategory.AI, agentUnit.GameObject);
            // TODO: Determine the actual target position based on the action's goal or internal logic.
            // For this simple example, we'll use the placeholder TargetPosition field.
            GridPosition finalTargetPosition = TargetPosition; // Placeholder

            // 2. Log immediately before the call to agentUnit.SetTargetPosition(TargetPosition).
            SmartLogger.Log($"[MoveAction] Calling SetTargetPosition for unit {agentUnit.UnitId} with target {TargetPosition}", LogCategory.AI, agentUnit.GameObject);
            agentUnit.SetTargetPosition(TargetPosition);
            // 3. Log immediately after the call to agentUnit.SetTargetPosition(TargetPosition). In this log, check and report the value of agentUnit.HasPendingMovement.
            SmartLogger.Log($"[MoveAction] Returned from SetTargetPosition. Unit {agentUnit.UnitId} HasPendingMovement: {agentUnit.HasPendingMovement}", LogCategory.AI, agentUnit.GameObject);

            // Generate the MoveCommand
            // Assuming MoveCommand constructor takes unit ID and target position as Vector2Int.
            // Assuming agentUnit.UnitId is available on IDokkaebiUnit
            MoveCommand moveCommand = new MoveCommand(agentUnit.UnitId, TargetPosition.ToVector2Int());

            // 4. Add a log before submitting the command to EnemyAIManager.
            SmartLogger.Log($"[AI] [MoveAction.Execute] Submitting MoveCommand to EnemyAIManager.", LogCategory.AI, this);
            EnemyAIManager.Instance.SubmitCommand(moveCommand);
            SmartLogger.Log($"[AI] [MoveAction.Execute] MoveCommand submitted.", LogCategory.AI, this);

            // 5. Add a log at the very end of the Execute method.
            SmartLogger.Log($"[MoveAction] Execute EXIT for unit {agentUnit.UnitId}", LogCategory.AI, agentUnit.GameObject);
            // TODO: Mark the unit as having acted for this phase/turn if applicable.
            // This might involve interacting with a UnitStateManager or the TurnSystemCore.
        }

        public bool ParameterizeAction(AIWorldState currentState, AIGoal goal, AIUnitState agentState)
        {
            SmartLogger.Log($"[MoveAction.ParameterizeAction] Parameterizing Move Action for unit {agentState.UnitId} towards goal {goal?.GoalName ?? "NULL"}", LogCategory.AI, this);

            // Reset target parameter
            TargetPosition = GridPosition.invalid;

            // If the goal is a ReachPositionGoal, set the target position from the goal
            if (goal is ReachPositionGoal reachPositionGoal)
            {
                if (reachPositionGoal.DesiredPosition != GridPosition.invalid)
                {
                    TargetPosition = reachPositionGoal.DesiredPosition;
                    SmartLogger.Log($"[MoveAction.ParameterizeAction] TargetPosition set from ReachPositionGoal: {TargetPosition}", LogCategory.AI, this);
                    return true; // Parameterization successful
                }
            }
            // TODO: Add logic to determine TargetPosition based on other goal types or AI logic

            SmartLogger.LogWarning($"[MoveAction.ParameterizeAction] Failed to determine a valid TargetPosition for move.", LogCategory.AI, this);
            return false; // Parameterization failed
        }
    }
} 