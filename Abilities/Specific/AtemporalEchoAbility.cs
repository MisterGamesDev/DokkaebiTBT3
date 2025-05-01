using UnityEngine;
using Dokkaebi.Core.Data;

namespace Dokkaebi.Abilities.Specific
{
    [CreateAssetMenu(fileName = "AtemporalEchoAbility", menuName = "Dokkaebi/Abilities/Atemporal Echo")]
    public class AtemporalEchoAbility : AbilityData
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            abilityId = "AtemporalEcho";
        }
    }
} 