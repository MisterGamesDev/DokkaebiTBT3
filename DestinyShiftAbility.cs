using UnityEngine;
using Dokkaebi.Core.Data;
using System.Collections.Generic;
using Dokkaebi.Interfaces;

namespace Dokkaebi.Abilities.Specific
{
    [CreateAssetMenu(fileName = "DestinyShiftAbility", menuName = "Dokkaebi/Abilities/Destiny Shift")]
    public class DestinyShiftAbility : AbilityData
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            abilityId = "DestinyShift";
            displayName = "Destiny Shift";
            description = "Reduce the target's ability range to 0 for 1 turn.";
            targetsEnemy = true;
            range = 3;
            auraCost = 3;
            cooldownTurns = 3;
            abilityType = AbilityType.Special;
            damageAmount = 0;
            healAmount = 0;
            createsZone = false;
            movementType = AbilityMovementType.None;
            areaOfEffect = 0;
            targetsSelf = false;
            targetsAlly = true;
            targetsGround = false;
            if (appliedEffects == null) { appliedEffects = new System.Collections.Generic.List<StatusEffectData>(); }
        }
    }
} 