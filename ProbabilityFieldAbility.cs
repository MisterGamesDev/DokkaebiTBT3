using UnityEngine;
using Dokkaebi.Core.Data;

namespace Dokkaebi.Abilities.Specific
{
    [CreateAssetMenu(fileName = "ProbabilityFieldAbility", menuName = "Dokkaebi/Abilities/Probability Field")]
    public class ProbabilityFieldAbility : AbilityData
    {
        [Header("Probability Field Settings")]
        [Tooltip("The chance (0-1) that the crit buff status effect is applied to units inside the zone each turn.")]
        [SerializeField]
        private float applicationChance = 0.25f;

        [Tooltip("The StatusEffectData asset for the critical chance/damage buff applied by this zone.")]
        [SerializeField]
        private StatusEffectData critBuffEffectData;

        // Public getters for the new fields
        public float ApplicationChance => applicationChance;
        public StatusEffectData CritBuffEffectData => critBuffEffectData;

        protected override void OnValidate()
        {
            base.OnValidate();
            abilityId = "ProbabilityField";
        }
    }
} 