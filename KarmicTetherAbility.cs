using UnityEngine;
using Dokkaebi.Core.Data;

namespace Dokkaebi.Abilities.Specific
{
    [CreateAssetMenu(fileName = "KarmicTetherAbility", menuName = "Dokkaebi/Abilities/Karmic Tether")]
    public class KarmicTetherAbility : AbilityData
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            abilityId = "KarmicTether";
        }
    }
} 