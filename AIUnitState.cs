using System;
using System.Collections.Generic;
using Dokkaebi.Interfaces; // Assuming GridPosition is here
using Dokkaebi.Core.Data; // For AbilityData reference if needed
using System.Linq;

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Represents the state of a single unit from the AI's perspective.
    /// </summary>
    public class AIUnitState
    {
        public int UnitId;
        public GridPosition Position;
        public int CurrentHP;
        public int MaxHP;
        public int CurrentAura;
        public int MaxAura;
        public List<string> ActiveStatusEffects = new List<string>(); // Now using string IDs for status effects
        public List<string> AvailableAbilityIds = new List<string>(); // List of AbilityData IDs
        public Dictionary<string, int> AbilityCooldowns = new Dictionary<string, int>(); // Ability ID to cooldown turns
        public int TeamId;
        public bool IsAlive;
        public string UnitTypeId; // New: Reference to UnitDefinitionData ID
        public int MovementRange; // New: Movement range for reachability checks
        public bool IsPlayerControlled; // Added for AI to distinguish player vs AI units
        public string DisplayName; // Added for AI logging/debugging

        /// <summary>
        /// Creates a deep clone of the current AIUnitState.
        /// </summary>
        /// <returns>A deep copy of the AIUnitState.</returns>
        public AIUnitState Clone()
        {
            return new AIUnitState
            {
                UnitId = this.UnitId,
                Position = this.Position, // GridPosition is a struct, copies by value
                CurrentHP = this.CurrentHP,
                MaxHP = this.MaxHP,
                CurrentAura = this.CurrentAura,
                MaxAura = this.MaxAura,
                ActiveStatusEffects = new List<string>(this.ActiveStatusEffects),
                AvailableAbilityIds = new List<string>(this.AvailableAbilityIds),
                AbilityCooldowns = new Dictionary<string, int>(this.AbilityCooldowns),
                TeamId = this.TeamId,
                IsAlive = this.IsAlive,
                UnitTypeId = this.UnitTypeId,
                MovementRange = this.MovementRange, // Clone movement range
                IsPlayerControlled = this.IsPlayerControlled, // Clone IsPlayerControlled
                DisplayName = this.DisplayName // Clone DisplayName
            };
        }

        /// <summary>
        /// Determines whether this AIUnitState is equal to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var other = (AIUnitState)obj;
            return UnitId == other.UnitId &&
                   Position.Equals(other.Position) &&
                   CurrentHP == other.CurrentHP &&
                   MaxHP == other.MaxHP &&
                   CurrentAura == other.CurrentAura &&
                   MaxAura == other.MaxAura &&
                   ActiveStatusEffects.SequenceEqual(other.ActiveStatusEffects) &&
                   AvailableAbilityIds.SequenceEqual(other.AvailableAbilityIds) &&
                   AbilityCooldowns.OrderBy(kv => kv.Key).SequenceEqual(other.AbilityCooldowns.OrderBy(kv => kv.Key)) &&
                   TeamId == other.TeamId &&
                   IsAlive == other.IsAlive &&
                   UnitTypeId == other.UnitTypeId &&
                   MovementRange == other.MovementRange &&
                   IsPlayerControlled == other.IsPlayerControlled &&
                   DisplayName == other.DisplayName;
        }

        /// <summary>
        /// Returns a hash code for this AIUnitState.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + UnitId.GetHashCode();
                hash = hash * 31 + Position.GetHashCode();
                hash = hash * 31 + CurrentHP.GetHashCode();
                hash = hash * 31 + MaxHP.GetHashCode();
                hash = hash * 31 + CurrentAura.GetHashCode();
                hash = hash * 31 + MaxAura.GetHashCode();
                foreach (var s in ActiveStatusEffects)
                    hash = hash * 31 + (s?.GetHashCode() ?? 0);
                foreach (var s in AvailableAbilityIds)
                    hash = hash * 31 + (s?.GetHashCode() ?? 0);
                foreach (var kv in AbilityCooldowns.OrderBy(kv => kv.Key))
                {
                    hash = hash * 31 + (kv.Key?.GetHashCode() ?? 0);
                    hash = hash * 31 + kv.Value.GetHashCode();
                }
                hash = hash * 31 + TeamId.GetHashCode();
                hash = hash * 31 + IsAlive.GetHashCode();
                hash = hash * 31 + (UnitTypeId?.GetHashCode() ?? 0);
                hash = hash * 31 + MovementRange.GetHashCode();
                hash = hash * 31 + IsPlayerControlled.GetHashCode();
                hash = hash * 31 + (DisplayName?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
} 