using System.Collections.Generic;
using UnityEngine;
using Dokkaebi.Grid;
using Dokkaebi.Units;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;
using Dokkaebi.Common;
using Dokkaebi.Core;

namespace Dokkaebi.Core.Networking.Commands
{
    /// <summary>
    /// Command for moving a unit to a new position
    /// </summary>
    public class MoveCommand : CommandBase
    {
        public int UnitId { get; private set; }
        public Vector2Int TargetPosition { get; private set; }

        // Required for deserialization
        public MoveCommand() : base() { }

        public MoveCommand(int unitId, Vector2Int targetPosition) : base()
        {
            UnitId = unitId;
            TargetPosition = targetPosition;
        }

        public override string CommandType => "move";

        public override Dictionary<string, object> Serialize()
        {
            var data = base.Serialize();
            data["unitId"] = UnitId;
            data["targetX"] = TargetPosition.x;
            data["targetY"] = TargetPosition.y;
            return data;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            base.Deserialize(data);

            if (data.TryGetValue("unitId", out object unitIdObj))
            {
                if (unitIdObj is long unitIdLong)
                {
                    UnitId = (int)unitIdLong;
                }
                else if (unitIdObj is int unitIdInt)
                {
                    UnitId = unitIdInt;
                }
            }

            int x = 0, y = 0;
            if (data.TryGetValue("targetX", out object xObj))
            {
                if (xObj is long xLong)
                {
                    x = (int)xLong;
                }
                else if (xObj is int xInt)
                {
                    x = xInt;
                }
            }

            if (data.TryGetValue("targetY", out object yObj))
            {
                if (yObj is long yLong)
                {
                    y = (int)yLong;
                }
                else if (yObj is int yInt)
                {
                    y = yInt;
                }
            }

            TargetPosition = new Vector2Int(x, y);
        }

        /// <summary>
        /// Validates if this move command is currently executable.
        /// Checks unit ownership, target position validity, movement range, etc.
        /// </summary>
        /// <returns>True if the command is valid, false otherwise.</returns>
        public override bool Validate()
        {
            SmartLogger.Log($"[MoveCommand.Validate] ========== START VALIDATION ==========", LogCategory.Movement, null);
            SmartLogger.Log($"[MoveCommand.Validate] ENTRY for Unit {UnitId} to position {TargetPosition}", LogCategory.Movement, null);

            // Get required managers
            var unitManager = UnitManager.Instance;
            var turnSystem = DokkaebiTurnSystemCore.Instance;
            var gridManager = GridManager.Instance;

            // Check managers
            if (unitManager == null)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: UnitManager not found!", LogCategory.Movement, null);
                return false;
            }
            if (turnSystem == null)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: DokkaebiTurnSystemCore not found!", LogCategory.Movement, null);
                return false;
            }
            if (gridManager == null)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: GridManager not found!", LogCategory.Movement, null);
                return false;
            }

            // 1. Check if the unit exists and is alive
            DokkaebiUnit unit = unitManager.GetUnitById(UnitId);
            if (unit == null)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: Unit {UnitId} not found!", LogCategory.Movement, null);
                return false;
            }
            if (!unit.IsAlive)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: Unit {UnitId} is not alive!", LogCategory.Movement, null);
                return false;
            }

            // Log unit state
            SmartLogger.Log($"[MoveCommand.Validate] Unit State:", LogCategory.Movement, unit.GameObject);
            SmartLogger.Log($"- Unit ID: {UnitId}", LogCategory.Movement, unit.GameObject);
            SmartLogger.Log($"- Team ID: {unit.TeamId}", LogCategory.Movement, unit.GameObject);
            SmartLogger.Log($"- Is Player Controlled: {unit.IsPlayerControlled}", LogCategory.Movement, unit.GameObject);
            SmartLogger.Log($"- Current Position: {unit.CurrentGridPosition}", LogCategory.Movement, unit.GameObject);
            SmartLogger.Log($"- Movement Range: {unit.MovementRange}", LogCategory.Movement, unit.GameObject);

            // Log turn system state
            SmartLogger.Log($"[MoveCommand.Validate] Turn System State:", LogCategory.Movement);
            SmartLogger.Log($"- Current Phase: {turnSystem.CurrentPhase}", LogCategory.Movement);
            SmartLogger.Log($"- Active Player ID: {turnSystem.ActivePlayerId}", LogCategory.Movement);

            // 2. Check phase and ownership validation
            bool isPlayerUnit = unit.IsPlayerControlled;
            bool isAIUnit = !unit.IsPlayerControlled;
            bool isMovementPhase = turnSystem.CurrentPhase == TurnPhase.MovementPhase;
            bool isCorrectTeamTurn = unit.TeamId == turnSystem.ActivePlayerId;

            SmartLogger.Log($"[MoveCommand.Validate] Phase/Ownership Check:", LogCategory.Movement);
            SmartLogger.Log($"- Is Player Unit: {isPlayerUnit}", LogCategory.Movement);
            SmartLogger.Log($"- Is AI Unit: {isAIUnit}", LogCategory.Movement);
            SmartLogger.Log($"- Is Movement Phase: {isMovementPhase}", LogCategory.Movement);
            SmartLogger.Log($"- Is Correct Team's Turn: {isCorrectTeamTurn}", LogCategory.Movement);

            // REVISED LOGIC: Allow any unit to act in MovementPhase
            bool canActInPhase = false;
            if (isMovementPhase)
            {
                canActInPhase = true; // Any unit can queue a move in MovementPhase
            }
            else
            {
                SmartLogger.LogWarning($"[MoveCommand.Validate] Move command received in non-MovementPhase ({turnSystem.CurrentPhase}). This should not happen.", LogCategory.Movement, null);
                return false; // Move command should only be valid in MovementPhase
            }

            if (!canActInPhase)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: Move not allowed in phase {turnSystem.CurrentPhase}. Unit Team: {unit.TeamId}, Active Player: {turnSystem.ActivePlayerId}", LogCategory.Movement, null);
                return false;
            }

            // 3. Check target position validity
            var targetGridPos = GridManager.Vector2IntToGrid(TargetPosition);
            
            // Check grid bounds
            bool isInBounds = gridManager.IsValidGridPosition(targetGridPos);
            SmartLogger.Log($"[MoveCommand.Validate] Target Position Check:", LogCategory.Movement);
            SmartLogger.Log($"- Target Position: {targetGridPos}", LogCategory.Movement);
            SmartLogger.Log($"- Is In Bounds: {isInBounds}", LogCategory.Movement);
            
            if (!isInBounds)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: Target position {targetGridPos} is out of bounds", LogCategory.Movement, null);
                return false;
            }

            // Check walkability
            bool isWalkable = gridManager.IsWalkable(targetGridPos, unit);
            SmartLogger.Log($"- Is Walkable: {isWalkable}", LogCategory.Movement);
            
            if (!isWalkable)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: Target position {targetGridPos} is not walkable", LogCategory.Movement, null);
                return false;
            }

            // Check occupancy
            bool isOccupied = gridManager.IsPositionOccupied(targetGridPos);
            SmartLogger.Log($"- Is Occupied: {isOccupied}", LogCategory.Movement);
            
            if (isOccupied)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: Target position {targetGridPos} is occupied", LogCategory.Movement, null);
                return false;
            }

            // Check movement range
            int distanceToTarget = GridPosition.GetManhattanDistance(unit.CurrentGridPosition, targetGridPos);
            bool isInRange = distanceToTarget <= unit.MovementRange;
            SmartLogger.Log($"- Distance to Target: {distanceToTarget}", LogCategory.Movement);
            SmartLogger.Log($"- Is In Range: {isInRange}", LogCategory.Movement);
            
            if (!isInRange)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: Target position {targetGridPos} is out of range (Distance: {distanceToTarget}, Range: {unit.MovementRange})", LogCategory.Movement, null);
                return false;
            }

            // Check pathfinding
            var pathfindingInfo = gridManager as IPathfindingGridInfo;
            bool hasPath = pathfindingInfo != null && pathfindingInfo.IsWalkable(targetGridPos, unit);
            SmartLogger.Log($"- Has Valid Path: {hasPath}", LogCategory.Movement);
            
            if (!hasPath)
            {
                SmartLogger.LogError($"[MoveCommand.Validate] FAILED: No valid path to target position {targetGridPos}", LogCategory.Movement, null);
                return false;
            }

            // All checks passed
            SmartLogger.Log($"[MoveCommand.Validate] ========== VALIDATION PASSED ==========", LogCategory.Movement, null);
            return true;
        }

        public override void Execute()
        {
            SmartLogger.Log($"[MoveCommand.Execute] ENTRY for Unit {UnitId} to position {TargetPosition}", LogCategory.Movement, null);

            var unitManager = Object.FindObjectOfType<UnitManager>();
            if (unitManager == null)
            {
                SmartLogger.LogError($"[MoveCommand.Execute] UnitManager not found! Cannot execute move.", LogCategory.Movement, null);
                return;
            }

            DokkaebiUnit unit = unitManager.GetUnitById(UnitId);
            if (unit == null)
            {
                SmartLogger.LogError($"[MoveCommand.Execute] Unit with ID {UnitId} not found. Cannot execute move.", LogCategory.Movement, null);
                return;
            }
            SmartLogger.Log($"[MoveCommand.Execute] Unit {UnitId} found: {unit.GetUnitName()}", LogCategory.Movement, unit.GameObject);

            // Convert TargetPosition (Vector2Int) to GridPosition
            GridPosition targetGridPos = GridManager.Vector2IntToGrid(TargetPosition);
            SmartLogger.Log($"[MoveCommand.Execute] Calling SetTargetPosition for unit {UnitId} with target {targetGridPos}", LogCategory.Movement, unit.GameObject);
            unit.SetTargetPosition(targetGridPos);
            SmartLogger.Log($"[MoveCommand.Execute] Returned from SetTargetPosition for unit {UnitId}", LogCategory.Movement, unit.GameObject);

            // If there are other movement initiation steps, log them here (none in current logic)

            SmartLogger.Log($"[MoveCommand.Execute] EXIT for Unit {UnitId}", LogCategory.Movement, unit.GameObject);
        }
    }
} 