using System;
using System.Collections.Generic;
using UnityEngine; // Required for ScriptableObject and Debug.Log
using Dokkaebi.Core.Data; // Assuming AbilityData, ZoneData, UnitDefinitionData are here
using Dokkaebi.Interfaces; // Assuming GridPosition, IUnit, IZoneInstance are here
using System.Linq; // Required for LINQ operations like Select and ToList
using Dokkaebi.Common; // Assuming TurnPhase is here
using Dokkaebi.Core; // Corrected: PerformanceScope is in Dokkaebi.Core
using Dokkaebi.Utilities; // SmartLogger is here

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Represents the AI's perception of the current game world state.
    /// This data structure is a snapshot used for GOAP planning.
    /// </summary>
    public class AIWorldState
    {
        // Data about all units relevant to the AI
        public List<AIUnitState> UnitStates = new List<AIUnitState>();
        // Data about the grid
        public AIGridState GridState = new AIGridState();
        // Data about zones
        public List<AIZoneState> ZoneStates = new List<AIZoneState>();
        // Turn data
        public AITurnState TurnState = new AITurnState();

        /// <summary>
        /// Creates a deep clone of the current AIWorldState.
        /// Essential for simulating action effects during planning without modifying the actual state.
        /// </summary>
        /// <returns>A deep copy of the AIWorldState.</returns>
        public AIWorldState Clone()
        {
            // --- ADDED: Performance Scope for Clone ---
            using (new PerformanceScope("AIWorldState Clone"))
            {
                return new AIWorldState
                {
                    UnitStates = this.UnitStates.Select(u => u.Clone()).ToList(),
                    ZoneStates = this.ZoneStates.Select(z => z.Clone()).ToList(),
                    GridState = this.GridState.Clone(),
                    TurnState = this.TurnState.Clone()
                };
            }
            // --- END ADDED ---
        }

        /// <summary>
        /// Checks if the current AIWorldState matches the conditions defined in a required state.
        /// Used for checking action preconditions and goal achievement.
        /// This is a simplified placeholder implementation. Real implementation
        /// would compare relevant fields based on the 'requiredState'.
        /// </summary>
        /// <param name="requiredState">A partial AIWorldState defining the conditions to match.</param>
        /// <returns>True if the current state matches the required state conditions, false otherwise.</returns>
        public bool Matches(AIWorldState requiredState)
        {
            // TODO: Implement comprehensive logic to check if this state meets the conditions defined in requiredState.
            // This will involve iterating through the requiredState's properties (UnitStates, GridState, etc.)
            // and comparing them against the current state's corresponding properties.
            // For example, if requiredState.UnitStates contains a condition like "Unit with ID X isAlive = false",
            // you would find the unit with ID X in this.UnitStates and check its IsAlive property.

            // Placeholder implementation: Always returns false until implemented.
            Debug.LogWarning("AIWorldState.Matches is a placeholder and needs full implementation.");
            return false;
        }

        // Returns the AIUnitState for a given unitId, or null if not found
        public AIUnitState GetUnitState(int unitId)
        {
            return UnitStates.FirstOrDefault(u => u.UnitId == unitId);
        }

        // Returns true if the tile is occupied by any unit (according to GridState.OccupyingUnitId)
        public bool IsTileOccupied(GridPosition pos)
        {
            if (GridState.OccupyingUnitId.TryGetValue(pos, out int occupyingUnitId))
            {
                return occupyingUnitId != -1 && occupyingUnitId != 0;
            }
            return false;
        }

        // Returns true if the tile is targeted by another AI unit's planned move (excluding the current unit)
        public bool IsTileTargetedByOtherAI(GridPosition pos, int currentUnitId, Dictionary<int, GridPosition> plannedMoves = null)
        {
            if (plannedMoves == null) return false;
            foreach (var kvp in plannedMoves)
            {
                if (kvp.Key != currentUnitId && kvp.Value == pos)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether this AIWorldState is equal to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            SmartLogger.Log("[AIWorldState.Equals] Entering Equals.", LogCategory.AI);
            SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.1 - Before type/null checks.", LogCategory.AI);
            using (new PerformanceScope("AIWorldState Equals"))
            {
                try
                {
                    if (ReferenceEquals(this, obj)) return true;
                    if (obj == null || GetType() != obj.GetType())
                    {
                        SmartLogger.Log("[AIWorldState.Equals] Not equal: other is null or wrong type.", LogCategory.AI);
                        return false;
                    }
                    var other = (AIWorldState)obj;

                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.2 - Before UnitStates null check.", LogCategory.AI);
                    if (UnitStates == null || other.UnitStates == null)
                    {
                        SmartLogger.Log("[AIWorldState.Equals] One of the UnitStates lists is null!", LogCategory.AI);
                        return false;
                    }
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.3 - Before UnitStates comparison.", LogCategory.AI);
                    if (!UnitStates.SequenceEqual(other.UnitStates))
                    {
                        SmartLogger.Log("[AIWorldState.Equals] Not equal: UnitStates differ.", LogCategory.AI);
                        return false;
                    }
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.4 - After UnitStates comparison.", LogCategory.AI);

                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.5 - Before GridState null check.", LogCategory.AI);
                    if ((GridState == null) != (other.GridState == null))
                    {
                        SmartLogger.Log("[AIWorldState.Equals] One of the GridStates is null!", LogCategory.AI);
                        return false;
                    }
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.6 - Before GridState comparison.", LogCategory.AI);
                    if (GridState != null && !GridState.Equals(other.GridState))
                    {
                        SmartLogger.Log("[AIWorldState.Equals] Not equal: GridState differs.", LogCategory.AI);
                        return false;
                    }
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.7 - After GridState comparison.", LogCategory.AI);

                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.8 - Before ZoneStates null check.", LogCategory.AI);
                    if (ZoneStates == null || other.ZoneStates == null)
                    {
                        SmartLogger.Log("[AIWorldState.Equals] One of the ZoneStates lists is null!", LogCategory.AI);
                        return false;
                    }
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.9 - Before ZoneStates comparison.", LogCategory.AI);
                    if (!ZoneStates.SequenceEqual(other.ZoneStates))
                    {
                        SmartLogger.Log("[AIWorldState.Equals] Not equal: ZoneStates differ.", LogCategory.AI);
                        return false;
                    }
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.10 - After ZoneStates comparison.", LogCategory.AI);

                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.11 - Before TurnState null check.", LogCategory.AI);
                    if ((TurnState == null) != (other.TurnState == null))
                    {
                        SmartLogger.Log("[AIWorldState.Equals] One of the TurnStates is null!", LogCategory.AI);
                        return false;
                    }
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.12 - Before TurnState comparison.", LogCategory.AI);
                    if (TurnState != null && !TurnState.Equals(other.TurnState))
                    {
                        SmartLogger.Log("[AIWorldState.Equals] Not equal: TurnState differs.", LogCategory.AI);
                        return false;
                    }
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.13 - After TurnState comparison.", LogCategory.AI);

                    // MICRO-DEBUG: After all main state comparisons, before return true
                    SmartLogger.Log("[AIWorldState.Equals] Micro-log E1.14 - All main state components compared, about to return true.", LogCategory.AI);
                    return true;
                }
                catch (Exception ex)
                {
                    SmartLogger.LogError($"[AIWorldState.Equals] --- CAUGHT EXCEPTION --- Exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.AI);
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns a hash code for this AIWorldState.
        /// </summary>
        public override int GetHashCode()
        {
            SmartLogger.Log("[AIWorldState.GetHashCode] Entering GetHashCode.", LogCategory.AI);
            SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.1 - Before unchecked block.", LogCategory.AI);
            using (new PerformanceScope("AIWorldState GetHashCode"))
            {
                unchecked
                {
                    try
                    {
                        int hash = 17;
                        SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.2 - Before UnitStates hashing.", LogCategory.AI);
                        if (UnitStates != null)
                        {
                            foreach (var unit in UnitStates)
                            {
                                if (unit != null)
                                {
                                    hash = hash * 31 + unit.GetHashCode();
                                }
                                else
                                {
                                    SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.2a - Encountered null unit in UnitStates.", LogCategory.AI);
                                }
                            }
                        }
                        else
                        {
                            SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.2b - UnitStates is null!", LogCategory.AI);
                        }
                        SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.3 - After UnitStates hashing.", LogCategory.AI);

                        SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.4 - Before GridState hashing.", LogCategory.AI);
                        if (GridState != null)
                        {
                            hash = hash * 31 + GridState.GetHashCode();
                        }
                        else
                        {
                            SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.4a - GridState is null!", LogCategory.AI);
                        }
                        SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.5 - After GridState hashing.", LogCategory.AI);

                        SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.6 - Before ZoneStates hashing.", LogCategory.AI);
                        if (ZoneStates != null)
                        {
                            foreach (var zone in ZoneStates)
                            {
                                if (zone != null)
                                {
                                    hash = hash * 31 + zone.GetHashCode();
                                }
                                else
                                {
                                    SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.6a - Encountered null zone in ZoneStates.", LogCategory.AI);
                                }
                            }
                        }
                        else
                        {
                            SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.6b - ZoneStates is null!", LogCategory.AI);
                        }
                        SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.7 - After ZoneStates hashing.", LogCategory.AI);

                        SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.8 - Before TurnState hashing.", LogCategory.AI);
                        if (TurnState != null)
                        {
                            hash = hash * 31 + TurnState.GetHashCode();
                        }
                        else
                        {
                            SmartLogger.Log("[AIWorldState.GetHashCode] Micro-log H1.8a - TurnState is null!", LogCategory.AI);
                        }
                        SmartLogger.Log($"[AIWorldState.GetHashCode] Micro-log H1.9 - Final hash: {hash}", LogCategory.AI);
                        return hash;
                    }
                    catch (Exception ex)
                    {
                        SmartLogger.LogError($"[AIWorldState.GetHashCode] --- CAUGHT EXCEPTION --- Exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.AI);
                        return 0;
                    }
                }
            }
        }

        // TODO: Add methods to easily query the state, e.g., GetUnitState(int unitId), GetZoneState(GridPosition pos)

        /// <summary>
        /// Returns a concise summary of the world state for debugging/logging purposes.
        /// </summary>
        public string GetStateSummary()
        {
            // Summarize units: id, pos, HP, alive
            var unitSummaries = UnitStates.Select(u => $"U{u.UnitId}({u.Position.x},{u.Position.z}) HP:{u.CurrentHP}/{u.MaxHP} {(u.IsAlive ? "A" : "D")}");
            string units = string.Join(", ", unitSummaries);
            // Summarize phase/active player if available
            string phase = TurnState != null ? TurnState.CurrentPhase.ToString() : "?";
            int activePlayer = TurnState != null ? TurnState.ActivePlayerId : -1;
            return $"Phase:{phase} ActivePlayer:{activePlayer} | Units: [{units}]";
        }
    }
} 