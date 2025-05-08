using System;
using Dokkaebi.Interfaces; // Assuming GridPosition is here

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Represents the state of zones from the AI's perspective.
    /// </summary>
    public class AIZoneState
    {
        public string ZoneId; // Unique identifier for the zone instance
        public GridPosition Position; // The grid position the zone occupies
        public string ZoneType; // Identifier for the type of zone (e.g., "StormSurge", "HealingZone")
        public bool IsActive; // Whether the zone is currently active
        public int RemainingDuration; // Turns or time remaining
        public int OwnerTeamId; // The team that created or controls the zone (-1 if neutral)
        // TODO: Add other relevant zone data, like effects, intensity, etc.

        /// <summary>
        /// Creates a deep clone of the current AIZoneState.
        /// </summary>
        /// <returns>A deep copy of the AIZoneState.</returns>
        public AIZoneState Clone()
        {
            return new AIZoneState
            {
                ZoneId = this.ZoneId,
                Position = this.Position, // GridPosition is a struct
                ZoneType = this.ZoneType,
                IsActive = this.IsActive,
                RemainingDuration = this.RemainingDuration,
                OwnerTeamId = this.OwnerTeamId
                // TODO: Clone other fields if added
            };
        }

        /// <summary>
        /// Determines whether this AIZoneState is equal to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var other = (AIZoneState)obj;
            return ZoneId == other.ZoneId &&
                   Position.Equals(other.Position) &&
                   ZoneType == other.ZoneType &&
                   IsActive == other.IsActive &&
                   RemainingDuration == other.RemainingDuration &&
                   OwnerTeamId == other.OwnerTeamId;
        }

        /// <summary>
        /// Returns a hash code for this AIZoneState.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (ZoneId?.GetHashCode() ?? 0);
                hash = hash * 31 + Position.GetHashCode();
                hash = hash * 31 + (ZoneType?.GetHashCode() ?? 0);
                hash = hash * 31 + IsActive.GetHashCode();
                hash = hash * 31 + RemainingDuration.GetHashCode();
                hash = hash * 31 + OwnerTeamId.GetHashCode();
                return hash;
            }
        }
    }
} 