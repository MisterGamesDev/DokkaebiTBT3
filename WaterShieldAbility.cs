using UnityEngine;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;  // Added for AbilityType

namespace Dokkaebi.Abilities.Specific
{
    /// <summary>
    /// Defines the Water Shield ability that creates a damage-absorbing shield on an ally or self.
    /// </summary>
    [CreateAssetMenu(fileName = "WaterShieldAbility", menuName = "Dokkaebi/Abilities/Water Shield", order = 1)]
    public class WaterShieldAbility : AbilityData
    {
        // No custom logic needed here for now, inherits all fields from AbilityData
        
        protected virtual void OnValidate()  // Changed to protected virtual
        {
            // Set default values for Water Shield ability
            if (string.IsNullOrEmpty(abilityId))
            {
                abilityId = "WaterShield";
                displayName = "Water Shield";
                description = "Creates a shield on an ally or self that absorbs 5 damage.";
                abilityType = AbilityType.Secondary;  // Removed Common. prefix
                auraCost = 2;
                cooldownTurns = 3;
                range = 1;
                targetsSelf = true;
                targetsAlly = true;
                targetsEnemy = false;
                targetsGround = false;
            }
        }
    }
} 