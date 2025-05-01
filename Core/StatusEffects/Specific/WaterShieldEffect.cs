using UnityEngine;
using Dokkaebi.Core.Data;
using Dokkaebi.Common;

namespace Dokkaebi.Core.StatusEffects.Specific
{
    /// <summary>
    /// Defines the Water Shield status effect that absorbs a fixed amount of damage.
    /// </summary>
    [CreateAssetMenu(fileName = "WaterShieldEffect", menuName = "Dokkaebi/Status Effects/Water Shield", order = 1)]
    public class WaterShieldEffect : StatusEffectData
    {
        protected virtual void OnValidate()
        {
            // Set default values for Water Shield effect
            if (string.IsNullOrEmpty(effectId))
            {
                effectId = "WaterShield";
                displayName = "Water Shield";
                description = "Absorbs the next 5 damage.";
                effectType = StatusEffectType.Shield;
                isStackable = false;
                maxStacks = 1;
                isPermanent = false;
                duration = 1;
                potency = 5;  // Used as damageAbsorptionAmount
                removeOnHit = true;
                effectColor = Color.cyan;
            }
        }
    }
} 