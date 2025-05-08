using System;
using System.Collections.Generic;
using Dokkaebi.Interfaces; // Assuming GridPosition is here
using System.Linq;
using Dokkaebi.Utilities; // For SmartLogger

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Represents the state of the grid from the AI's perspective.
    /// </summary>
    public class AIGridState
    {
        // Simplified grid data (e.g., walkable/occupied status per tile)
        // Using Dictionary for easy lookup by GridPosition
        public Dictionary<GridPosition, bool> IsWalkable = new Dictionary<GridPosition, bool>();
        public Dictionary<GridPosition, int> OccupyingUnitId = new Dictionary<GridPosition, int>(); // Stores unit ID occupying a tile (-1 or 0 if empty)
        // TODO: Add other relevant grid data, like tile types, cover information, etc.

        /// <summary>
        /// Creates a deep clone of the current AIGridState.
        /// </summary>
        /// <returns>A deep copy of the AIGridState.</returns>
        public AIGridState Clone()
        {
            return new AIGridState
            {
                // Clone dictionaries
                IsWalkable = new Dictionary<GridPosition, bool>(this.IsWalkable),
                OccupyingUnitId = new Dictionary<GridPosition, int>(this.OccupyingUnitId)
                // TODO: Clone other dictionaries/data structures if added
            };
        }

        /// <summary>
        /// Determines whether this AIGridState is equal to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var other = (AIGridState)obj;

            // Compare IsWalkable dictionaries
            if (IsWalkable.Count != other.IsWalkable.Count)
            {
                SmartLogger.Log($"[AIGridState.Equals] IsWalkable count mismatch: {IsWalkable.Count} vs {other.IsWalkable.Count}", LogCategory.AI);
                return false;
            }
            foreach (var kv in IsWalkable)
            {
                if (!other.IsWalkable.TryGetValue(kv.Key, out bool otherValue) || kv.Value != otherValue)
                {
                    SmartLogger.Log($"[AIGridState.Equals] IsWalkable key/value mismatch at {kv.Key}", LogCategory.AI);
                    return false;
                }
            }

            // Compare OccupyingUnitId dictionaries
            if (OccupyingUnitId.Count != other.OccupyingUnitId.Count)
            {
                SmartLogger.Log($"[AIGridState.Equals] OccupyingUnitId count mismatch: {OccupyingUnitId.Count} vs {other.OccupyingUnitId.Count}", LogCategory.AI);
                return false;
            }
            foreach (var kv in OccupyingUnitId)
            {
                if (!other.OccupyingUnitId.TryGetValue(kv.Key, out int otherValue) || kv.Value != otherValue)
                {
                    SmartLogger.Log($"[AIGridState.Equals] OccupyingUnitId key/value mismatch at {kv.Key}", LogCategory.AI);
                    return false;
                }
            }

            SmartLogger.Log("[AIGridState.Equals] States are equal.", LogCategory.AI);
            return true;
        }

        /// <summary>
        /// Returns a hash code for this AIGridState.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var kv in IsWalkable)
                {
                    hash = hash * 31 + kv.Key.GetHashCode();
                    hash = hash * 31 + kv.Value.GetHashCode();
                }
                foreach (var kv in OccupyingUnitId)
                {
                    hash = hash * 31 + kv.Key.GetHashCode();
                    hash = hash * 31 + kv.Value.GetHashCode();
                }
                SmartLogger.Log($"[AIGridState.GetHashCode] Hash: {hash}", LogCategory.AI);
                return hash;
            }
        }
    }
} 